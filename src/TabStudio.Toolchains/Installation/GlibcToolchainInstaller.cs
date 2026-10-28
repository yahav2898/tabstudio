using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Installation;

/// <summary>
/// Installs the glibc part of toolchains (<see cref="ToolchainDefinition.Glibc"/>), such as Swift and JetBrains'
/// Kotlin language server, in one shared Ubuntu 24.04 environment beside Alpine, run with the same PRoot. The Alpine
/// part of a toolchain, if it has one, is installed by the installer this one wraps.
/// </summary>
/// <remarks>
/// <para>
/// The Ubuntu base image is the one Flutter's environment uses, pinned by its SHA-256 per architecture; Ubuntu's
/// packages come from its signed archive through <c>apt-get</c>, and every other download is pinned by its hash in
/// the catalog. A toolchain is ready when its Alpine part is and its marker in the environment matches the catalog
/// definition it was installed from, so a changed definition installs again.
/// </para>
/// <para>
/// Programs that belong to the environment (<see cref="GlibcEnvironmentDefinition.Runs"/>) run in it through
/// <see cref="GlibcRuntime"/>; everything else stays in Alpine. Workspace folders are shared at their own paths in
/// both, so paths in compiler output and language server messages are the files the IDE opens.
/// </para>
/// </remarks>
public sealed class GlibcToolchainInstaller : IToolchainInstaller, IDisposable
{
    private const string Revision = "glibc-languages-ubuntu24-1";
    private readonly IToolchainInstaller _primary;
    private readonly ToolchainPlatform _platform;
    private readonly Downloader _downloader;
    private readonly IToolchainRuntime _guest;
    private readonly string _root;
    private readonly SemaphoreSlim _operation = new(1, 1);
    private readonly Dictionary<string, ToolchainDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Wraps an installer, adding the shared Ubuntu environment for glibc tools.</summary>
    /// <param name="primary">The installer of everything else: Alpine, Flutter, embedded SDKs.</param>
    /// <param name="root">The app-owned folder of the environment.</param>
    /// <param name="platform">The device.</param>
    /// <param name="downloader">The SHA-256 checking downloader.</param>
    /// <param name="sharedFolders">Workspace folders, shared at their own paths.</param>
    /// <param name="guestRuntime">A runtime standing in for the environment, for tests.</param>
    public GlibcToolchainInstaller(IToolchainInstaller primary, string root, ToolchainPlatform platform, Downloader downloader,
        IEnumerable<string> sharedFolders, IToolchainRuntime? guestRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(sharedFolders);
        _primary = primary;
        _root = Path.GetFullPath(root);
        _platform = platform;
        _downloader = downloader;
        _guest = guestRuntime ?? new ProotRuntime(new ProotRuntimeOptions
        {
            Rootfs = Rootfs,
            ProotPath = platform.ProotPath ?? string.Empty,
            LoaderPath = platform.LoaderPath ?? string.Empty,
            TemporaryDirectory = Path.Combine(_root, "tmp"),
            DisplayName = "Swift and Kotlin tools · Ubuntu 24.04 (glibc)",
            Shell = "/bin/bash",
            SearchPath = "/opt/tabstudio/swift/usr/bin:" + ProotRuntime.SearchPath,
            SharedFolders = [.. sharedFolders, .. platform.SharedFolders],
        });
        Runtime = new GlibcRuntime(primary.Runtime, _guest, RunsInEnvironment);
    }

    /// <summary>The environment's root file system in app storage.</summary>
    public string Rootfs => Path.Combine(_root, "rootfs");

    private string Downloads => Path.Combine(_root, "downloads");

    private string ImageMarker => Path.Combine(Rootfs, ".tabstudio-glibc-image");

    /// <inheritdoc />
    public IToolchainRuntime Runtime { get; }

    /// <inheritdoc />
    public bool CanInstall => _primary.CanInstall;

    /// <inheritdoc />
    public string? Problem => _primary.Problem;

    /// <inheritdoc />
    public bool IsRuntimeReady => _primary.IsRuntimeReady;

    /// <summary>Whether a toolchain has a glibc part this installer installs.</summary>
    /// <param name="toolchain">The toolchain.</param>
    public static bool Handles(ToolchainDefinition toolchain)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        return toolchain.Glibc is not null;
    }

    /// <inheritdoc />
    public Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        _primary.PrepareRuntimeAsync(progress, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains, bool checkForUpdates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        Remember(toolchains);
        var statuses = await _primary.ReadStatusAsync(toolchains, checkForUpdates, cancellationToken).ConfigureAwait(false);
        var supported = FlutterToolchainInstaller.RootImage(_platform.Architecture) is not null;
        return [.. statuses.Select(status =>
        {
            if (!Handles(status.Toolchain))
            {
                return status;
            }

            if (!supported)
            {
                return status with { State = ToolchainState.Unavailable, Note = $"{status.Toolchain.Name} needs an ARM64 or x64 processor." };
            }

            return status.IsReady && !IsGlibcReady(status.Toolchain)
                ? status with { State = ToolchainState.NotInstalled, Version = null, Note = $"{status.Toolchain.Name}'s tools for glibc are not installed yet." }
                : status;
        })];
    }

    /// <inheritdoc />
    public Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        InstallAsync(toolchain, progress, refreshMetadata: true, cancellationToken);

    /// <inheritdoc />
    public async Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, bool refreshMetadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        await _primary.InstallAsync(toolchain, progress, refreshMetadata, cancellationToken).ConfigureAwait(false);
        if (Handles(toolchain))
        {
            await InstallGlibcAsync(toolchain, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        await _primary.UpdateAsync(toolchain, progress, cancellationToken).ConfigureAwait(false);
        if (Handles(toolchain))
        {
            await InstallGlibcAsync(toolchain, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        ArgumentNullException.ThrowIfNull(installed);
        await _primary.RemoveAsync(toolchain, installed, progress, cancellationToken).ConfigureAwait(false);
        if (toolchain.Glibc is not { } glibc)
        {
            return;
        }

        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            File.Delete(Marker(toolchain.Id));
            foreach (var path in glibc.Setup.SelectMany(PathsOf).Distinct(StringComparer.Ordinal))
            {
                var target = GuestPath(path);
                if (File.Exists(target) || new FileInfo(target).LinkTarget is not null)
                {
                    File.Delete(target);
                }
                else if (Directory.Exists(target))
                {
                    Directory.Delete(target, recursive: true);
                }
            }

            // The Ubuntu packages stay: other toolchains in the environment may use them, and they are reused on reinstall.
        }
        finally
        {
            _operation.Release();
        }
    }

    /// <inheritdoc />
    public DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains)
    {
        var usage = _primary.MeasureDiskUsage(toolchains);
        return usage with { Toolchains = usage.Toolchains + DiskSpace.Size(Rootfs), Downloads = usage.Downloads + DiskSpace.Size(Downloads) };
    }

    /// <inheritdoc />
    public void Dispose() => _operation.Dispose();

    /// <summary>Whether a toolchain's glibc part is installed from its current definition.</summary>
    /// <param name="toolchain">The toolchain.</param>
    public bool IsGlibcReady(ToolchainDefinition toolchain)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        return toolchain.Glibc is null || (File.Exists(Marker(toolchain.Id)) && File.ReadAllText(Marker(toolchain.Id)).Trim() == Fingerprint(toolchain));
    }

    /// <summary>Whether a program runs in the environment: a ready toolchain's glibc part names it.</summary>
    private bool RunsInEnvironment(string program)
    {
        List<ToolchainDefinition> definitions;
        lock (_gate)
        {
            definitions = [.. _definitions.Values];
        }

        if (definitions.Count == 0)
        {
            definitions = [.. ToolchainCatalog.Default.Toolchains.Where(Handles)];
        }

        return definitions.Any(t => t.Glibc is { } glibc && glibc.Runs(program) && IsGlibcReady(t));
    }

    private void Remember(IEnumerable<ToolchainDefinition> toolchains)
    {
        lock (_gate)
        {
            foreach (var toolchain in toolchains.Where(Handles))
            {
                _definitions[toolchain.Id] = toolchain;
            }
        }
    }

    private async Task InstallGlibcAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        var glibc = toolchain.Glibc!;
        Remember([toolchain]);
        if (FlutterToolchainInstaller.RootImage(_platform.Architecture) is not { } image)
        {
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, $"{toolchain.Name} needs an ARM64 or x64 processor.");
        }

        if (Problem is { } problem)
        {
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, problem);
        }

        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsGlibcReady(toolchain))
            {
                return;
            }

            Directory.CreateDirectory(_root);
            var required = Math.Max(1_500_000_000, toolchain.EstimatedInstalledSize + toolchain.EstimatedDownloadSize);
            if (_platform.FreeSpace(_root) is { } free && free < required)
            {
                throw new ToolchainInstallException(InstallFailure.NotEnoughSpace,
                    $"Installing {toolchain.Name} needs at least {DiskSpace.Format(required)} free for its tools and their Ubuntu environment.");
            }

            await PrepareImageAsync(image.Address, image.Sha256, progress, cancellationToken).ConfigureAwait(false);
            Configure();
            File.Delete(Marker(toolchain.Id));
            if (glibc.Packages.Count > 0)
            {
                await CommandAsync(["apt-get", "update"], progress, cancellationToken).ConfigureAwait(false);
                await CommandAsync(["apt-get", "install", "-y", "--no-install-recommends", .. glibc.Packages], progress, cancellationToken).ConfigureAwait(false);
            }

            var steps = glibc.Setup.Where(s => s.Architectures.Count == 0 || s.Architectures.Contains(_platform.Architecture ?? string.Empty, StringComparer.Ordinal)).ToList();
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                progress?.Report(new(InstallStage.Configuring, "Setting up " + step.Name, 0.5 + (0.45 * i / Math.Max(1, steps.Count))));
                await SetupAsync(step, progress, cancellationToken).ConfigureAwait(false);
            }

            if (glibc.Packages.Count > 0)
            {
                await CommandAsync(["apt-get", "clean"], progress, cancellationToken).ConfigureAwait(false);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Marker(toolchain.Id))!);
            await File.WriteAllTextAsync(Marker(toolchain.Id), Fingerprint(toolchain), cancellationToken).ConfigureAwait(false);
            progress?.Report(new(InstallStage.Completed, $"{toolchain.Name} is installed", 1));
        }
        catch (DownloadException error)
        {
            throw Failures.From(error);
        }
        catch (ToolchainRuntimeException error)
        {
            throw new ToolchainInstallException(InstallFailure.CommandFailed, error.Message, innerException: error);
        }
        finally
        {
            _operation.Release();
        }
    }

    private async Task PrepareImageAsync(Uri address, string sha256, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (File.Exists(ImageMarker) && File.ReadAllText(ImageMarker).Trim() == sha256 && File.Exists(GuestPath("/usr/bin/apt-get")))
        {
            return;
        }

        Directory.CreateDirectory(Downloads);
        var archive = Path.Combine(Downloads, "ubuntu-base.tar.gz");
        progress?.Report(new(InstallStage.SettingUpLinux, "Downloading the verified Ubuntu environment for glibc tools", null));
        await _downloader.DownloadAsync(new DownloadRequest { Address = address, Destination = archive, Sha256 = sha256 },
            new InlineProgress<DownloadProgress>(p => progress?.Report(new(InstallStage.Downloading, "Ubuntu base", null, p.Received, p.Total ?? 0))),
            cancellationToken).ConfigureAwait(false);
        var partial = Rootfs + ".partial";
        if (Directory.Exists(partial))
        {
            Directory.Delete(partial, recursive: true);
        }

        await ArchiveExtractor.ExtractTarGzAsync(archive, partial, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(partial, ".tabstudio-glibc-image"), sha256, cancellationToken).ConfigureAwait(false);
        if (Directory.Exists(Rootfs))
        {
            Directory.Delete(Rootfs, recursive: true);
        }

        Directory.Move(partial, Rootfs);
        File.Delete(archive);
    }

    private void Configure()
    {
        Directory.CreateDirectory(GuestPath("/tmp"));
        Directory.CreateDirectory(GuestPath("/root"));
        var dns = _platform.DnsServers();
        var resolv = GuestPath("/etc/resolv.conf");
        File.Delete(resolv); // Ubuntu's image may link it into /run/systemd/resolve.
        File.WriteAllText(resolv, string.Concat((dns.Count > 0 ? dns : ["1.1.1.1", "8.8.8.8"]).Select(server => $"nameserver {server}\n")));
        File.WriteAllText(GuestPath("/etc/hosts"), "127.0.0.1 localhost\n::1 localhost\n");
        Directory.CreateDirectory(GuestPath("/etc/apt/apt.conf.d"));
        File.WriteAllText(GuestPath("/etc/apt/apt.conf.d/99tabstudio"), "Acquire::Retries \"2\";\n");
    }

    private async Task SetupAsync(SetupStepDefinition step, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (step.Archive is { } archive)
        {
            Directory.CreateDirectory(Downloads);
            var path = archive.Url.AbsolutePath;
            var extension = path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                ? ".tar.gz"
                : Path.GetExtension(path);
            var download = Path.Combine(Downloads, archive.Sha256 + extension);
            await _downloader.DownloadAsync(new DownloadRequest
            {
                Address = archive.Url, Destination = download, Size = archive.Size > 0 ? archive.Size : null, Sha256 = archive.Sha256,
            }, new InlineProgress<DownloadProgress>(p => progress?.Report(new(InstallStage.Downloading, step.Name, null, p.Received, p.Total ?? archive.Size))),
                cancellationToken).ConfigureAwait(false);
            var destination = GuestPath(archive.Destination);
            var partial = destination + ".partial";
            if (Directory.Exists(partial))
            {
                Directory.Delete(partial, recursive: true);
            }

            progress?.Report(new(InstallStage.Installing, "Unpacking " + step.Name, null));
            try
            {
                await ArchiveExtractor.ExtractAsync(download, partial, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException error)
            {
                File.Delete(download);
                throw new ToolchainInstallException(InstallFailure.IntegrityFailed, error.Message, innerException: error);
            }

            var unpacked = Strip(partial, archive.StripComponents);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(unpacked, destination);
            if (Directory.Exists(partial))
            {
                Directory.Delete(partial, recursive: true);
            }

            File.Delete(download);
        }

        if (step.File is { } file)
        {
            var target = GuestPath(file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, file.Content, cancellationToken).ConfigureAwait(false);
            if (file.Executable && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(target, (UnixFileMode)0x1ED); // 0755
            }
        }

        if (step.Command.Count > 0)
        {
            await CommandAsync([.. step.Command], progress, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The folder inside an unpacked archive after dropping its leading folders, which must be the only entries there.</summary>
    private static string Strip(string folder, int components)
    {
        for (var i = 0; i < components; i++)
        {
            var entries = Directory.GetFileSystemEntries(folder);
            if (entries.Length != 1 || !Directory.Exists(entries[0]))
            {
                throw new ToolchainInstallException(InstallFailure.IntegrityFailed,
                    $"The archive does not have the single top folder its catalog entry expects ({Path.GetFileName(folder)}).");
            }

            folder = entries[0];
        }

        return folder;
    }

    private async Task CommandAsync(string[] command, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new(InstallStage.Configuring, string.Join(' ', command), null));
        var result = await CommandRunner.RunAsync(_guest, new ProcessStartSpec
        {
            Program = command[0],
            Arguments = command[1..],
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal) { ["DEBIAN_FRONTEND"] = "noninteractive" },
        }, line => progress?.Report(new(InstallStage.Configuring, line.Trim(), null)), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ToolchainInstallException(InstallFailure.CommandFailed, $"{command[0]} failed: {result.Tail(8)}", result.Output);
        }
    }

    private static IEnumerable<string> PathsOf(SetupStepDefinition step) =>
        (step.Archive is { } archive ? [archive.Destination] : Array.Empty<string>())
            .Concat(step.File is { } file ? [file.Path] : [])
            .Concat(step.Removes);

    private string Marker(string id) => Path.Combine(Rootfs, "var", "lib", "tabstudio", "glibc", id + ".ready");

    private string GuestPath(string path) => Path.Combine(Rootfs, path.TrimStart('/'));

    private static string Fingerprint(ToolchainDefinition toolchain) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Revision + "\n" + JsonSerializer.Serialize(toolchain.Glibc))));
}
