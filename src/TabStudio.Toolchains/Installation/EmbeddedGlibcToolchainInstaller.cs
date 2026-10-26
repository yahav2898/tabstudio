using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Installation;

/// <summary>Installs native glibc embedded tools in one shared Ubuntu side environment, using the existing PRoot and verified downloader.</summary>
public sealed class EmbeddedGlibcToolchainInstaller : IToolchainInstaller, IDisposable
{
    private const string BaseRevision = "embedded-ubuntu24-1";
    private static readonly Lazy<string> SupportFingerprint = new(() => Hash(string.Join('\n',
        UsbBridgeInstallation.SupportFiles.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "\n" + p.Value))));
    private readonly IToolchainInstaller _primary;
    private readonly ToolchainPlatform _platform;
    private readonly Downloader _downloader;
    private readonly IToolchainRuntime _guest;
    private readonly string _root;
    private readonly ConcurrentDictionary<string, ToolchainDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _operation = new(1, 1);
    private string Downloads => Path.Combine(_root, "downloads");
    private string ImageMarker => Path.Combine(Rootfs, ".tabstudio-embedded-image");
    private string BaseMarker => Path.Combine(Rootfs, ".tabstudio-embedded-base");

    /// <summary>Wraps the primary installer, preserving Alpine and Flutter commands while adding embedded glibc tools.</summary>
    /// <param name="primary">The existing installer chain.</param>
    /// <param name="root">The app-owned shared embedded environment directory.</param>
    /// <param name="platform">The Android runtime and native architecture.</param>
    /// <param name="downloader">The existing SHA-256 checking downloader.</param>
    /// <param name="sharedFolders">Workspace folders shared at their original paths.</param>
    /// <param name="guestRuntime">An optional runtime substitute for command-plan tests.</param>
    public EmbeddedGlibcToolchainInstaller(IToolchainInstaller primary, string root, ToolchainPlatform platform,
        Downloader downloader, IEnumerable<string> sharedFolders, IToolchainRuntime? guestRuntime = null)
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
            ProotPath = platform.ProotPath ?? "",
            LoaderPath = platform.LoaderPath ?? "",
            TemporaryDirectory = Path.Combine(_root, "tmp"),
            DisplayName = "Embedded SDKs · Ubuntu 24.04 (glibc)",
            Shell = "/bin/bash",
            SharedFolders = [.. sharedFolders, .. platform.SharedFolders, Downloads],
        });
        Runtime = new EmbeddedGlibcRuntime(primary.Runtime, _guest, IsToolchainReady, () => IsBaseReady);
    }

    /// <summary>The extracted glibc filesystem in app storage.</summary>
    public string Rootfs => Path.Combine(_root, "rootfs");

    /// <inheritdoc />
    public IToolchainRuntime Runtime { get; }

    /// <inheritdoc />
    public bool CanInstall => _primary.CanInstall;

    /// <inheritdoc />
    public string? Problem => _primary.Problem;

    /// <inheritdoc />
    public bool IsRuntimeReady => _primary.IsRuntimeReady;

    /// <summary>SDKs whose downloaded native host tools require glibc rather than Alpine's musl.</summary>
    /// <param name="id">The catalog identifier.</param>
    public static bool Handles(string id) => id is "platformio" or "arduino" or "probe-rs" or "embedded-esptool" or "embedded-picotool";

    /// <inheritdoc />
    public Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        _primary.PrepareRuntimeAsync(progress, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains,
        bool checkForUpdates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        foreach (var definition in toolchains.Where(t => Handles(t.Id)))
        {
            _definitions[definition.Id] = definition;
        }
        var primary = await _primary.ReadStatusAsync([.. toolchains.Where(t => !Handles(t.Id))], checkForUpdates, cancellationToken).ConfigureAwait(false);
        var supported = FlutterToolchainInstaller.RootImage(_platform.Architecture) is not null;
        return [.. toolchains.Select(toolchain => !Handles(toolchain.Id) ? primary.First(s => s.Toolchain.Id == toolchain.Id)
            : new ToolchainStatus
            {
                Toolchain = toolchain,
                State = !supported || Problem is not null ? ToolchainState.Unavailable
                    : IsToolchainReady(toolchain.Id) ? ToolchainState.Installed : ToolchainState.NotInstalled,
                Version = IsToolchainReady(toolchain.Id) ? toolchain.PinnedVersion : null,
                DownloadSize = toolchain.EstimatedDownloadSize,
                InstallSize = toolchain.EstimatedInstalledSize,
                Note = !supported ? "The embedded glibc environment requires ARM64 or x64. No x86 translation is selected implicitly."
                    : Problem ?? "Native tools run in the shared Ubuntu glibc environment. USB shims are built for the same ABI. PlatformIO telemetry is disabled.",
            })];
    }

    /// <inheritdoc />
    public Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, bool refreshMetadata, CancellationToken cancellationToken) =>
        Handles(toolchain.Id) ? InstallAsync(toolchain, progress, cancellationToken)
            : _primary.InstallAsync(toolchain, progress, refreshMetadata, cancellationToken);

    /// <inheritdoc />
    public async Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        if (!Handles(toolchain.Id))
        {
            await _primary.InstallAsync(toolchain, progress, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (FlutterToolchainInstaller.RootImage(_platform.Architecture) is not { } image)
        {
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, "Embedded glibc SDKs require ARM64 or x64.");
        }
        if (Problem is { } problem)
        {
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, problem);
        }
        Directory.CreateDirectory(_root);
        _definitions[toolchain.Id] = toolchain;
        if (IsToolchainReady(toolchain.Id))
        {
            return;
        }
        if (_platform.FreeSpace(_root) is { } free && free < 2_000_000_000)
        {
            throw new ToolchainInstallException(InstallFailure.NotEnoughSpace, "Embedded SDKs need at least 2 GB free for Ubuntu, native build tools and board packages.");
        }
        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _definitions[toolchain.Id] = toolchain;
            if (IsToolchainReady(toolchain.Id))
            {
                return;
            }
            await _primary.PrepareRuntimeAsync(progress, cancellationToken).ConfigureAwait(false);
            await PrepareImageAsync(image.Address, image.Sha256, progress, cancellationToken).ConfigureAwait(false);
            ConfigureNetwork();
            if (!IsBaseReady)
            {
                File.Delete(BaseMarker);
                await CommandAsync(new ProcessStartSpec { Program = "apt-get", Arguments = ["update"] }, progress, cancellationToken).ConfigureAwait(false);
                await CommandAsync(new ProcessStartSpec
                {
                    Program = "apt-get", Arguments = ["install", "-y", "--no-install-recommends", .. EmbeddedSdkBootstrap.BasePackages],
                }, progress, cancellationToken).ConfigureAwait(false);
                await UsbBridgeInstallation.WriteSourcesAsync(Rootfs, cancellationToken).ConfigureAwait(false);
                foreach (var command in UsbBridgeInstallation.BuildCommands)
                {
                    await CommandAsync(command, progress, cancellationToken).ConfigureAwait(false);
                }
                await CommandAsync(UsbBridgeProcess.CreateVirtualSelfTest(), progress, cancellationToken,
                    requiredOutput: "EMBEDDED_BRIDGE_PASSED").ConfigureAwait(false);
                await AtomicWriteAsync(BaseMarker, BaseFingerprint, cancellationToken).ConfigureAwait(false);
            }
            if (File.Exists(ToolchainMarker(toolchain.Id)))
            {
                File.Delete(ToolchainMarker(toolchain.Id));
            }
            if (toolchain.Id == "probe-rs")
            {
                await InstallProbeAsync(toolchain, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                foreach (var step in toolchain.Setup.Where(s => s.Architectures.Count == 0
                    || s.Architectures.Contains(_platform.Architecture ?? "", StringComparer.Ordinal)))
                {
                    await SetupAsync(step, progress, cancellationToken).ConfigureAwait(false);
                }
            }
            await CommandAsync(EmbeddedSdkBootstrap.ValidationCommand(toolchain.Id), progress, cancellationToken,
                requiredOutput: toolchain.PinnedVersion).ConfigureAwait(false);
            await CommandAsync(new ProcessStartSpec { Program = "apt-get", Arguments = ["clean"] }, progress, cancellationToken).ConfigureAwait(false);
            await AtomicWriteAsync(ToolchainMarker(toolchain.Id), ToolchainFingerprint(toolchain), cancellationToken).ConfigureAwait(false);
            progress?.Report(new(InstallStage.Completed, toolchain.Name + " is installed in the embedded glibc environment", 1));
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

    /// <inheritdoc />
    public Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        Handles(toolchain.Id) ? InstallAsync(toolchain, progress, cancellationToken) : _primary.UpdateAsync(toolchain, progress, cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed,
        IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (!Handles(toolchain.Id))
        {
            await _primary.RemoveAsync(toolchain, installed, progress, cancellationToken).ConfigureAwait(false);
            return;
        }
        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(ToolchainMarker(toolchain.Id)))
            {
                File.Delete(ToolchainMarker(toolchain.Id));
            }
            var paths = toolchain.Id == "probe-rs"
                ? new[] { "/opt/tabstudio/probe-rs", "/usr/local/bin/probe-rs", "/usr/local/bin/cargo-flash", "/usr/local/bin/cargo-embed" }
                : toolchain.Setup.SelectMany(step => (step.Archive is { } archive ? new[] { archive.Destination } : Array.Empty<string>())
                    .Concat(step.File is { } file ? [file.Path] : []).Concat(step.Removes)).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var path in paths)
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
            // The shared base and board caches stay available to other SDKs and later reinstalls.
        }
        finally
        {
            _operation.Release();
        }
    }

    /// <inheritdoc />
    public DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains)
    {
        var primary = _primary.MeasureDiskUsage([.. toolchains.Where(t => !Handles(t.Id))]);
        return primary with { Toolchains = primary.Toolchains + DiskSpace.Size(Rootfs), Downloads = primary.Downloads + DiskSpace.Size(Downloads) };
    }

    /// <inheritdoc />
    public void Dispose() => _operation.Dispose();

    /// <summary>Whether a complete SDK and this build's ABI-specific USB bridge are installed.</summary>
    /// <param name="id">The catalog identifier.</param>
    public bool IsToolchainReady(string id)
    {
        var definition = _definitions.GetValueOrDefault(id) ?? ToolchainCatalog.Default.Find(id);
        if (definition is null || !Handles(id) || !IsBaseReady)
        {
            return false;
        }
        var command = "/usr/local/bin/" + EmbeddedSdkBootstrap.ValidationCommand(id).Program;
        return Matches(ToolchainMarker(id), ToolchainFingerprint(definition)) && File.Exists(GuestPath(command));
    }

    private bool IsBaseReady => Matches(BaseMarker, BaseFingerprint)
        && File.Exists(GuestPath(UsbBridgeInstallation.ToolsDirectory + "/libusb-fd.so"))
        && File.Exists(GuestPath(UsbBridgeInstallation.ToolsDirectory + "/serial-posix.so"))
        && File.Exists(GuestPath(UsbBridgeInstallation.ToolsDirectory + "/usbfs-fd.so"));

    private string BaseFingerprint => Hash(BaseRevision + "\n" + FlutterToolchainInstaller.RootImage(_platform.Architecture)?.Sha256
        + "\n" + string.Join('\n', EmbeddedSdkBootstrap.BasePackages)
        + "\n" + SupportFingerprint.Value);

    private string ToolchainFingerprint(ToolchainDefinition definition) => Hash(BaseFingerprint + "\n" + JsonSerializer.Serialize(definition));

    private string ToolchainMarker(string id) => Path.Combine(Rootfs, "var/lib/tabstudio/embedded", id + ".ready");

    private async Task PrepareImageAsync(Uri address, string sha256, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (Matches(ImageMarker, sha256) && File.Exists(GuestPath("/usr/bin/apt-get")))
        {
            return;
        }
        Directory.CreateDirectory(Downloads);
        var archive = Path.Combine(Downloads, "ubuntu-base.tar.gz");
        progress?.Report(new(InstallStage.SettingUpLinux, "Downloading the verified shared embedded Ubuntu environment", null));
        await _downloader.DownloadAsync(new DownloadRequest { Address = address, Destination = archive, Sha256 = sha256 },
            new InlineProgress<DownloadProgress>(p => progress?.Report(new(InstallStage.Downloading, "Ubuntu base", null, p.Received, p.Total ?? 0))), cancellationToken).ConfigureAwait(false);
        var partial = Rootfs + ".partial";
        if (Directory.Exists(partial))
        {
            Directory.Delete(partial, recursive: true);
        }
        await ArchiveExtractor.ExtractTarGzAsync(archive, partial, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(partial, ".tabstudio-embedded-image"), sha256, cancellationToken).ConfigureAwait(false);
        if (Directory.Exists(Rootfs))
        {
            Directory.Delete(Rootfs, recursive: true);
        }
        Directory.Move(partial, Rootfs);
    }

    private void ConfigureNetwork()
    {
        Directory.CreateDirectory(GuestPath("/tmp"));
        Directory.CreateDirectory(GuestPath("/root"));
        var dns = _platform.DnsServers();
        var resolv = GuestPath("/etc/resolv.conf");
        File.Delete(resolv);
        File.WriteAllText(resolv, string.Concat((dns.Count > 0 ? dns : ["1.1.1.1", "8.8.8.8"]).Select(server => "nameserver " + server + "\n")));
        File.WriteAllText(GuestPath("/etc/hosts"), "127.0.0.1 localhost\n::1 localhost\n");
        Directory.CreateDirectory(GuestPath("/etc/apt/apt.conf.d"));
        File.WriteAllText(GuestPath("/etc/apt/apt.conf.d/99tabstudio"), "Acquire::Retries \"2\";\n");
    }

    private async Task SetupAsync(SetupStepDefinition step, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new(InstallStage.Configuring, "Setting up " + step.Name, null));
        if (step.Archive is { } archive)
        {
            Directory.CreateDirectory(Downloads);
            var xz = archive.Url.AbsolutePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase);
            var name = archive.Sha256 + (archive.Url.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : xz ? ".tar.xz" : ".tar.gz");
            var download = Path.Combine(Downloads, name);
            await _downloader.DownloadAsync(new DownloadRequest
            {
                Address = archive.Url, Destination = download, Size = archive.Size > 0 ? archive.Size : null, Sha256 = archive.Sha256,
            }, null, cancellationToken).ConfigureAwait(false);
            var destination = GuestPath(archive.Destination);
            var partial = destination + ".partial";
            if (Directory.Exists(partial))
            {
                Directory.Delete(partial, recursive: true);
            }
            if (xz)
            {
                Directory.CreateDirectory(partial);
                _guest.ShareFolder(Downloads);
                const string extraction = "import sys,tarfile; t=tarfile.open(sys.argv[1],'r:xz'); t.extractall(sys.argv[2],filter='data'); t.close()";
                await CommandAsync(new ProcessStartSpec
                {
                    Program = "python3", Arguments = ["-c", extraction, download, archive.Destination + ".partial"],
                }, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ArchiveExtractor.ExtractAsync(download, partial, cancellationToken).ConfigureAwait(false);
            }
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(partial, destination);
        }
        if (step.File is { } file)
        {
            var target = GuestPath(file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (new FileInfo(target).LinkTarget is not null)
            {
                File.Delete(target);
            }
            await File.WriteAllTextAsync(target, file.Content.ReplaceLineEndings("\n"), cancellationToken).ConfigureAwait(false);
            if (file.Executable && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        if (step.Command.Count > 0)
        {
            await CommandAsync(new ProcessStartSpec { Program = step.Command[0], Arguments = [.. step.Command.Skip(1)] }, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CommandAsync(ProcessStartSpec spec, IProgress<InstallProgress>? progress, CancellationToken cancellationToken,
        string? requiredOutput = null)
    {
        var environment = new Dictionary<string, string?>(spec.Environment);
        foreach (var entry in EmbeddedSdkBootstrap.Environment("embedded"))
        {
            environment[entry.Key] = entry.Value;
        }
        var command = spec with { Environment = environment, WorkingDirectory = spec.WorkingDirectory ?? "/" };
        progress?.Report(new(InstallStage.Configuring, command.CommandLine, null));
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromMinutes(20));
        var result = await CommandRunner.RunAsync(_guest, command,
            line => progress?.Report(new(InstallStage.Configuring, line.TrimEnd(), null)), bounded.Token).ConfigureAwait(false);
        if (!result.Succeeded || requiredOutput is not null && !result.Output.Contains(requiredOutput, StringComparison.Ordinal))
        {
            throw new ToolchainInstallException(InstallFailure.CommandFailed, command.Program + " failed: " + result.Tail(8), result.Output);
        }
    }

    private async Task InstallProbeAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        var archiveDefinition = toolchain.Setup.Where(step => step.Architectures.Count == 0
                || step.Architectures.Contains(_platform.Architecture ?? "", StringComparer.Ordinal))
            .Select(step => step.Archive).OfType<ArchiveDefinition>().Single();
        Directory.CreateDirectory(Downloads);
        var archive = Path.Combine(Downloads, archiveDefinition.Sha256 + ".tar.xz");
        await _downloader.DownloadAsync(new DownloadRequest
        {
            Address = archiveDefinition.Url, Destination = archive,
            Sha256 = archiveDefinition.Sha256, Size = archiveDefinition.Size > 0 ? archiveDefinition.Size : null,
        }, null, cancellationToken).ConfigureAwait(false);
        _guest.ShareFolder(Downloads);
        await CommandAsync(new ProcessStartSpec
        {
            Program = "python3", Arguments = ["-c", ProbeExtractionScript, archive, archiveDefinition.Destination],
        }, progress, cancellationToken, "PROBE_RS_EXTRACTED").ConfigureAwait(false);
        foreach (var name in new[] { "probe-rs", "cargo-embed", "cargo-flash" })
        {
            if (File.Exists(GuestPath(archiveDefinition.Destination + "/" + name)))
            {
                await SetupAsync(new SetupStepDefinition
                {
                    Name = name + " launcher",
                    File = new FileDefinition { Path = "/usr/local/bin/" + name, Executable = true,
                        Content = "#!/bin/sh\nexec " + archiveDefinition.Destination + "/" + name + " \"$@\"\n" },
                }, progress, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private const string ProbeExtractionScript = """
        import os, pathlib, shutil, sys, tarfile
        source, destination = sys.argv[1:]
        wanted = {'probe-rs', 'cargo-embed', 'cargo-flash'}
        os.makedirs(destination, exist_ok=True)
        with tarfile.open(source, 'r:xz') as archive:
            members = {}
            for member in archive.getmembers():
                name = pathlib.PurePosixPath(member.name).name
                if name not in wanted: continue
                if not member.isfile() or member.size > 150*1024*1024 or name in members:
                    raise ValueError('Unexpected or duplicate probe-rs executable')
                members[name] = member
            if 'probe-rs' not in members: raise ValueError('probe-rs executable missing')
            for name, member in members.items():
                target = os.path.join(destination, name)
                if os.path.lexists(target): os.unlink(target)
                with archive.extractfile(member) as src, open(target, 'xb') as dst:
                    shutil.copyfileobj(src, dst)
                os.chmod(target, 0o755)
        print('PROBE_RS_EXTRACTED')
        """;

    private string GuestPath(string path)
    {
        if (!path.StartsWith('/') || path.Split('/').Contains("..", StringComparer.Ordinal))
        {
            throw new InvalidDataException("Embedded setup paths must be absolute guest paths without parent traversal.");
        }
        return Path.Combine(Rootfs, path.TrimStart('/'));
    }

    private static bool Matches(string path, string expected)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length < 4096 && File.ReadAllText(path).Trim() == expected;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static async Task AtomicWriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var partial = path + ".partial";
        await File.WriteAllTextAsync(partial, content, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(partial, path, overwrite: true);
    }
}
