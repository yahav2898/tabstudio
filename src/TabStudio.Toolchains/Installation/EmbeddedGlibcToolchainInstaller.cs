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
    public static bool Handles(string id) => id is "platformio" or "arduino" or "probe-rs" or "embedded-esptool" or "embedded-picotool" or EmbeddedNordicInstallation.Id
        || EmbeddedSdkBootstrap.Recipes.Get(id) is not null;

    /// <inheritdoc />
    public Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        _primary.PrepareRuntimeAsync(progress, cancellationToken);

    /// <summary>Prepares the native-first PlatformIO fallback translator in the same glibc environment.</summary>
    /// <param name="line">Preparation output.</param>
    /// <param name="cancellationToken">Stops preparation before a build starts.</param>
    public async Task PrepareBox64Async(Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);
        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsToolchainReady("platformio"))
            {
                throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, "Install PlatformIO in the Toolchain Store before preparing its optional x86 package translator.");
            }
            if (_platform.Architecture != "x86_64") // Native x64 packages need no translator or private runtime.
                await EmbeddedBox64Installation.PrepareAsync(Rootfs, _platform, _downloader, _guest, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operation.Release();
        }
    }

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
                Note = !supported ? "The embedded glibc environment requires ARM64 or x64. PlatformIO prefers native host packages and can translate unavailable ARM64 tools through the pinned Box64 runtime."
                    : Problem ?? "Native tools run in the shared Ubuntu glibc environment. USB shims match its ABI. PlatformIO prefers native packages, checks executable ELF architecture and disables telemetry.",
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
        var requiredSpace = Math.Max(2_000_000_000, toolchain.EstimatedInstalledSize + toolchain.EstimatedDownloadSize);
        if (_platform.FreeSpace(_root) is { } free && free < requiredSpace)
        {
            throw new ToolchainInstallException(InstallFailure.NotEnoughSpace, "Installing " + toolchain.Name + " needs at least "
                + DiskSpace.Format(requiredSpace) + " free for Ubuntu, native tools, source and board packages.");
        }
        // Reuse each dependency's ordinary signed/hash-checked installation and readiness marker.
        // Install before taking this SDK's semaphore: all packages share one guest and operation gate.
        if (EmbeddedSdkBootstrap.Recipes.Get(toolchain.Id) is { } dependencyRecipe)
        {
            foreach (var dependencyId in dependencyRecipe.RequiredToolchains)
            {
                var dependency = _definitions.GetValueOrDefault(dependencyId) ?? ToolchainCatalog.Default.Find(dependencyId)
                    ?? throw new InvalidOperationException("The SDK's required Store package is missing: " + dependencyId);
                if (dependencyId == toolchain.Id || EmbeddedSdkBootstrap.Recipes.Get(dependencyId) is not null)
                {
                    throw new InvalidOperationException("SDK dependencies must be independent frontend packages, not recursive SDKs.");
                }
                await InstallAsync(dependency, progress, cancellationToken).ConfigureAwait(false);
            }
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
                foreach (var limiter in EmbeddedSdkBootstrap.BuildLimiters)
                {
                    await SetupAsync(new SetupStepDefinition { Name = "Bounded native build " + limiter.Path, File = limiter }, progress, cancellationToken).ConfigureAwait(false);
                }
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
            if (EmbeddedSdkBootstrap.Recipes.Get(toolchain.Id) is { } recipe)
            {
                await InstallSdkAsync(recipe, progress, cancellationToken).ConfigureAwait(false);
            }
            else if (toolchain.Id == "probe-rs")
            {
                await InstallProbeAsync(toolchain, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (toolchain.Id == EmbeddedNordicInstallation.Id)
                {
                    void Line(string line) => progress?.Report(new(InstallStage.Configuring, line.TrimEnd(), null));
                    if (_platform.Architecture == "aarch64")
                    {
                        await EmbeddedBox64Installation.PrepareAsync(Rootfs, _platform, _downloader, _guest, Line, cancellationToken).ConfigureAwait(false);
                    }
                    await EmbeddedBox64Installation.PrepareSerialShimAsync(_platform.Architecture!, _guest, Line, cancellationToken).ConfigureAwait(false);
                }
                foreach (var step in toolchain.Setup.Where(s => s.Architectures.Count == 0
                    || s.Architectures.Contains(_platform.Architecture ?? "", StringComparer.Ordinal)))
                {
                    await SetupAsync(step, progress, cancellationToken).ConfigureAwait(false);
                }
            }
            await CommandAsync(EmbeddedSdkBootstrap.ValidationCommand(toolchain.Id), progress, cancellationToken,
                requiredOutput: EmbeddedSdkBootstrap.Recipes.Get(toolchain.Id) is null ? toolchain.PinnedVersion : null).ConfigureAwait(false);
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
            var recipe = EmbeddedSdkBootstrap.Recipes.Get(toolchain.Id);
            var paths = recipe is not null
                ? new[] { recipe.SourceDirectory, recipe.SourceDirectory + ".previous", recipe.SourceDirectory + ".partial" }
                    .Concat(recipe.Files.Select(file => file.Path)).Concat(recipe.Removes)
                    .Concat(recipe.Setup.SelectMany(step => (step.Archive is { } archive ? new[] { archive.Destination, archive.Destination + ".partial" } : Array.Empty<string>())
                        .Concat(step.File is { } file ? [file.Path] : []).Concat(step.Removes)))
                    .Distinct(StringComparer.Ordinal).ToArray()
                : toolchain.Id == "probe-rs"
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
        var recipe = EmbeddedSdkBootstrap.Recipes.Get(id);
        var command = id == "pico-sdk" ? "/usr/bin/cmake" : "/usr/local/bin/" + EmbeddedSdkBootstrap.ValidationCommand(id).Program;
        return Matches(ToolchainMarker(id), ToolchainFingerprint(definition)) && File.Exists(GuestPath(command))
            && (id != EmbeddedNordicInstallation.Id || HasArtifact(EmbeddedNordicInstallation.X64SerialShim))
            && (id != "embedded-picotool" || EmbeddedFlashToolInstallation.PicotoolArtifacts.All(HasArtifact))
            && (id != "probe-rs" || HasArtifact(ProbeRsSafeLaunch.ScriptPath))
            && (recipe is null || Matches(GuestPath(recipe.SourceDirectory + "/.tabstudio-sdk-source"), SourceFingerprint(recipe))
                && recipe.RequiredArtifacts.All(HasArtifact) && recipe.RequiredToolchains.All(IsToolchainReady));
    }

    private bool IsBaseReady => Matches(BaseMarker, BaseFingerprint)
        && File.Exists(GuestPath(UsbBridgeInstallation.ToolsDirectory + "/libusb-fd.so"))
        && File.Exists(GuestPath(UsbBridgeInstallation.ToolsDirectory + "/serial-posix.so"))
        && File.Exists(GuestPath(UsbBridgeInstallation.ToolsDirectory + "/usbfs-fd.so"))
        && EmbeddedSdkBootstrap.BuildLimiters.All(file => File.Exists(GuestPath(file.Path)));

    private string BaseFingerprint => Hash(BaseRevision + "\n" + FlutterToolchainInstaller.RootImage(_platform.Architecture)?.Sha256
        + "\n" + string.Join('\n', EmbeddedSdkBootstrap.BasePackages)
        + "\n" + JsonSerializer.Serialize(EmbeddedSdkBootstrap.BuildLimiters)
        + "\n" + SupportFingerprint.Value + "\n" + string.Join('\n', UsbBridgeInstallation.BuildCommands.Select(command => command.CommandLine)));

    private string ToolchainFingerprint(ToolchainDefinition definition) => Hash(BaseFingerprint + "\n" + JsonSerializer.Serialize(definition)
        + "\n" + JsonSerializer.Serialize(EmbeddedSdkBootstrap.Recipes.Get(definition.Id))
        + (definition.Id == "probe-rs" ? "\n" + ProbeRsSafeLaunch.Source : string.Empty));

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

    private async Task<CommandResult> CommandAsync(ProcessStartSpec spec, IProgress<InstallProgress>? progress, CancellationToken cancellationToken,
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
        return result;
    }

    private async Task InstallSdkAsync(EmbeddedSdkRecipe recipe, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        await CommandAsync(new ProcessStartSpec { Program = "apt-get", Arguments = ["update"] }, progress, cancellationToken).ConfigureAwait(false);
        await CommandAsync(new ProcessStartSpec
        {
            Program = "apt-get", Arguments = ["install", "-y", "--no-install-recommends", .. recipe.Packages],
        }, progress, cancellationToken).ConfigureAwait(false);
        if (recipe.CheckoutSource)
        {
            await CheckoutSdkAsync(recipe, progress, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Directory.CreateDirectory(GuestPath(recipe.SourceDirectory));
        }
        foreach (var file in recipe.Files)
        {
            await SetupAsync(new SetupStepDefinition { Name = recipe.Id + " support " + file.Path, File = file }, progress, cancellationToken).ConfigureAwait(false);
        }
        foreach (var step in recipe.Setup.Where(step => step.Architectures.Count == 0
            || step.Architectures.Contains(_platform.Architecture ?? "", StringComparer.Ordinal)))
        {
            await SetupAsync(step, progress, cancellationToken).ConfigureAwait(false);
        }
        foreach (var command in recipe.Commands)
        {
            var environment = new Dictionary<string, string?>(EmbeddedSdkBootstrap.Environment(recipe.Id));
            foreach (var entry in command.Environment)
            {
                environment[entry.Key] = entry.Value;
            }
            EmbeddedSdkBootstrap.ApplyBuildLimits(recipe.Id, environment);
            await CommandAsync(command with { Environment = environment }, progress, cancellationToken).ConfigureAwait(false);
        }
        if (recipe.RequiredArtifacts.FirstOrDefault(path => !HasArtifact(path)) is { } missing)
        {
            throw new ToolchainInstallException(InstallFailure.CommandFailed, "SDK validation did not produce the required firmware: " + missing);
        }
        if (!recipe.CheckoutSource)
        {
            await AtomicWriteAsync(GuestPath(recipe.SourceDirectory + "/.tabstudio-sdk-source"), SourceFingerprint(recipe), cancellationToken).ConfigureAwait(false);
        }
    }

    private bool HasArtifact(string path)
    {
        var artifact = new FileInfo(GuestPath(path));
        return artifact.Exists && artifact.Length > 0;
    }

    private async Task CheckoutSdkAsync(EmbeddedSdkRecipe recipe, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (recipe.Revision.Length != 40 || !recipe.Revision.All(char.IsAsciiHexDigit))
        {
            throw new ToolchainInstallException(InstallFailure.IntegrityFailed, "SDK source must be pinned to an exact 40-digit Git commit.");
        }
        var destination = GuestPath(recipe.SourceDirectory);
        if (Matches(Path.Combine(destination, ".tabstudio-sdk-source"), SourceFingerprint(recipe)))
        {
            return;
        }
        var stage = recipe.SourceDirectory + ".partial";
        Directory.CreateDirectory(GuestPath(stage));
        async Task<CommandResult> GitAsync(params string[] args) => await CommandAsync(new ProcessStartSpec
        {
            Program = "git", Arguments = ["-C", stage, .. args], WorkingDirectory = "/",
        }, progress, cancellationToken).ConfigureAwait(false);
        if (!Directory.Exists(GuestPath(stage + "/.git")))
        {
            await GitAsync("init", "-q").ConfigureAwait(false);
        }
        // An authoritative origin is also needed for SDK submodules whose URLs are relative to their parent.
        await GitAsync("config", "remote.origin.url", recipe.Repository.AbsoluteUri).ConfigureAwait(false);
        await GitAsync("config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*").ConfigureAwait(false);
        await GitAsync("fetch", "--depth", "1", "--no-tags", "origin", recipe.Revision).ConfigureAwait(false);
        await GitAsync("checkout", "--force", "--detach", "FETCH_HEAD").ConfigureAwait(false);
        var head = await GitAsync("rev-parse", "HEAD").ConfigureAwait(false);
        if (!head.Output.Split('\n').Any(line => line.Trim() == recipe.Revision))
        {
            throw new ToolchainInstallException(InstallFailure.IntegrityFailed, "The SDK checkout did not match its pinned commit.");
        }
        if (recipe.Submodules)
        {
            await GitAsync("-c", "protocol.file.allow=never", "submodule", "sync", "--recursive").ConfigureAwait(false);
            await GitAsync("-c", "protocol.file.allow=never", "submodule", "update", "--init", "--recursive", "--depth", "1").ConfigureAwait(false);
        }
        await AtomicWriteAsync(GuestPath(stage + "/.tabstudio-sdk-source"), SourceFingerprint(recipe), cancellationToken).ConfigureAwait(false);
        var previous = destination + ".previous";
        if (Directory.Exists(previous))
        {
            Directory.Delete(previous, recursive: true);
        }
        if (Directory.Exists(destination))
        {
            Directory.Move(destination, previous);
        }
        Directory.Move(GuestPath(stage), destination);
    }

    private static string SourceFingerprint(EmbeddedSdkRecipe recipe) => recipe.Revision + "\n" + recipe.Repository.AbsoluteUri;

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
        await SetupAsync(new SetupStepDefinition
        {
            Name = "Managed probe-rs preset isolation",
            File = new FileDefinition { Path = ProbeRsSafeLaunch.ScriptPath, Content = ProbeRsSafeLaunch.Source },
        }, progress, cancellationToken).ConfigureAwait(false);
        foreach (var name in new[] { "probe-rs", "cargo-embed", "cargo-flash" })
        {
            if (File.Exists(GuestPath(archiveDefinition.Destination + "/" + name)))
            {
                await SetupAsync(new SetupStepDefinition
                {
                    Name = name + " launcher",
                    File = new FileDefinition { Path = "/usr/local/bin/" + name, Executable = true,
                        Content = name == "probe-rs" ? ProbeRsSafeLaunch.Launcher(archiveDefinition.Destination + "/" + name)
                            : "#!/bin/sh\nexec " + archiveDefinition.Destination + "/" + name + " \"$@\"\n" },
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
