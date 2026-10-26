using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Installation;

/// <summary>Installs Flutter and its bundled Dart SDK in an isolated glibc PRoot environment.</summary>
public sealed class FlutterToolchainInstaller : IToolchainInstaller
{
    /// <summary>The tested Flutter release. Its own engine pin selects the matching Linux ARM64/x64 Dart SDK.</summary>
    public const string Version = "3.38.0";
    /// <summary>The immutable commit behind the release tag; checked before running Flutter's bootstrap.</summary>
    public const string Revision = "a0e9b9dbf78c8a5ef39b45a7efd40ed2de19c1a7";
    /// <summary>The engine revision defining this release's Dart SDK.</summary>
    public const string EngineRevision = "cb467e31a54207dc987aca5bb0941c4d2e3fd9ed";
    private readonly IToolchainInstaller _primary;
    private readonly ToolchainPlatform _platform;
    private readonly Downloader _downloader;
    private readonly ProotRuntime _flutter;
    private readonly string _root;
    private string Rootfs => Path.Combine(_root, "rootfs");
    private string Marker => Path.Combine(Rootfs, ".tabstudio-flutter-ready");
    private bool Ready => File.Exists(Marker) && File.ReadAllText(Marker).Trim() == Revision;

    /// <summary>Wraps the Alpine installer, adding a Flutter-only side environment.</summary>
    public FlutterToolchainInstaller(IToolchainInstaller primary, string root, ToolchainPlatform platform, Downloader downloader, IEnumerable<string> sharedFolders)
    {
        _primary = primary;
        _root = root;
        _platform = platform;
        _downloader = downloader;
        _flutter = new ProotRuntime(new ProotRuntimeOptions
        {
            Rootfs = Rootfs, ProotPath = platform.ProotPath ?? string.Empty, LoaderPath = platform.LoaderPath ?? string.Empty,
            TemporaryDirectory = Path.Combine(root, "tmp"), DisplayName = "Flutter · Ubuntu 24.04 (glibc)",
            Shell = "/bin/bash", SearchPath = "/opt/flutter/bin:" + ProotRuntime.SearchPath,
            SharedFolders = [.. sharedFolders, .. platform.SharedFolders],
        });
        Runtime = new FlutterRuntime(primary.Runtime, _flutter, () => Ready);
    }

    /// <inheritdoc />
    public IToolchainRuntime Runtime { get; }
    /// <inheritdoc />
    public bool CanInstall => _primary.CanInstall;
    /// <inheritdoc />
    public string? Problem => _primary.Problem;
    /// <inheritdoc />
    public bool IsRuntimeReady => _primary.IsRuntimeReady;
    /// <inheritdoc />
    public Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => _primary.PrepareRuntimeAsync(progress, cancellationToken);

    /// <summary>The upstream rootfs and verified SHA-256 for a supported architecture.</summary>
    public static (Uri Address, string Sha256)? RootImage(string? architecture) => architecture switch
    {
        "aarch64" => (new("https://cdimage.ubuntu.com/ubuntu-base/releases/24.04.3/release/ubuntu-base-24.04.3-base-arm64.tar.gz"), "7b2dced6dd56ad5e4a813fa25c8de307b655fdabc6ea9213175a92c48dabb048"),
        "x86_64" => (new("https://cdimage.ubuntu.com/ubuntu-base/releases/24.04.3/release/ubuntu-base-24.04.3-base-amd64.tar.gz"), "6bc2cde3930ad088b3bb46fa45279e96d25bc3810f209850ecbe4722711874f9"),
        _ => null,
    };

    /// <summary>The official, hash-pinned Dart SDK matching Flutter's engine, for a supported Linux host.</summary>
    public static (Uri Address, string Sha256)? DartImage(string? architecture) => architecture switch
    {
        "aarch64" => (new($"https://storage.googleapis.com/flutter_infra_release/flutter/{EngineRevision}/dart-sdk-linux-arm64.zip"), "41b209d2dfb4dde1a33214dc5ac25521272cab43bd40d20467c113c82b6894dc"),
        "x86_64" => (new($"https://storage.googleapis.com/flutter_infra_release/flutter/{EngineRevision}/dart-sdk-linux-x64.zip"), "6dc4f5c13611de54eced525280c78d25999b41b28a9d45922500b80f8dcda2f0"),
        _ => null,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains, bool checkForUpdates, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(Rootfs, "etc"))) Configure();
        var statuses = await _primary.ReadStatusAsync([.. toolchains.Where(t => t.Id != "flutter")], checkForUpdates, cancellationToken).ConfigureAwait(false);
        return [.. toolchains.Select(t => t.Id == "flutter" ? new ToolchainStatus
        {
            Toolchain = t, State = RootImage(_platform.Architecture) is null ? ToolchainState.Unavailable : Ready ? ToolchainState.Installed : ToolchainState.NotInstalled,
            Version = Ready ? Version : null, DownloadSize = t.EstimatedDownloadSize, InstallSize = t.EstimatedInstalledSize,
            Note = RootImage(_platform.Architecture) is null ? "Flutter requires an ARM64 or x64 processor." : "Flutter and Dart run in their own Ubuntu glibc environment.",
        } : statuses.First(s => s.Toolchain.Id == t.Id))];
    }

    /// <inheritdoc />
    public Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, bool refreshMetadata, CancellationToken cancellationToken) =>
        toolchain.Id == "flutter"
            ? InstallAsync(toolchain, progress, cancellationToken)
            : _primary.InstallAsync(toolchain, progress, refreshMetadata, cancellationToken);

    /// <inheritdoc />
    public async Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (toolchain.Id != "flutter") { await _primary.InstallAsync(toolchain, progress, cancellationToken).ConfigureAwait(false); return; }
        if (RootImage(_platform.Architecture) is not { } image)
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, "Flutter requires ARM64 or x64.");
        if (Problem is { } problem) throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, problem);
        Directory.CreateDirectory(_root);
        if (_platform.FreeSpace(_root) is { } free && free < 4_000_000_000)
            throw new ToolchainInstallException(InstallFailure.NotEnoughSpace, "Installing Flutter needs at least 4 GB free for the SDK, its caches and the glibc environment.");
        if (Ready) return;
        try
        {
            if (!File.Exists(Path.Combine(Rootfs, "usr/bin/apt-get")))
            {
                progress?.Report(new(InstallStage.SettingUpLinux, "Downloading Flutter's Ubuntu glibc environment", 0.05));
                var archive = Path.Combine(_root, "ubuntu-base.tar.gz");
                await _downloader.DownloadAsync(new DownloadRequest { Address = image.Address, Destination = archive, Sha256 = image.Sha256 },
                    new InlineProgress<DownloadProgress>(p => progress?.Report(new(InstallStage.Downloading, "Ubuntu base", null, p.Received, p.Total ?? 0))), cancellationToken).ConfigureAwait(false);
                var partial = Rootfs + ".partial";
                if (Directory.Exists(partial)) Directory.Delete(partial, true);
                await ArchiveExtractor.ExtractTarGzAsync(archive, partial, cancellationToken).ConfigureAwait(false);
                if (Directory.Exists(Rootfs)) Directory.Delete(Rootfs, true);
                Directory.Move(partial, Rootfs);
                File.Delete(archive);
            }
            Configure();
            await CommandAsync(["apt-get", "update"], progress, cancellationToken).ConfigureAwait(false);
            await CommandAsync(["apt-get", "install", "-y", "--no-install-recommends", "ca-certificates", "curl", "git", "unzip", "xz-utils", "zip", "libglu1-mesa", "libstdc++6"], progress, cancellationToken).ConfigureAwait(false);
            if (!Directory.Exists(Path.Combine(Rootfs, "opt/flutter/.git")))
                await CommandAsync(["git", "clone", "--depth", "1", "--single-branch", "--branch", Version, "https://github.com/flutter/flutter.git", "/opt/flutter"], progress, cancellationToken).ConfigureAwait(false);
            var revision = await CommandRunner.RunAsync(_flutter, new ProcessStartSpec { Program = "git", Arguments = ["-C", "/opt/flutter", "rev-parse", "HEAD"] }, null, cancellationToken).ConfigureAwait(false);
            if (!revision.Succeeded || revision.Output.Trim() != Revision)
                throw new ToolchainInstallException(InstallFailure.IntegrityFailed, "The Flutter release tag does not match its pinned commit. Remove Flutter and install again.");
            var cache = Path.Combine(Rootfs, "opt/flutter/bin/cache");
            var dartStamp = Path.Combine(cache, "engine-dart-sdk.stamp");
            if (!File.Exists(dartStamp) || File.ReadAllText(dartStamp).Trim() != EngineRevision)
            {
                var dart = DartImage(_platform.Architecture)!.Value;
                var archive = Path.Combine(_root, "dart-sdk.zip");
                progress?.Report(new(InstallStage.Downloading, "Downloading Flutter's Dart SDK", null));
                await _downloader.DownloadAsync(new DownloadRequest { Address = dart.Address, Destination = archive, Sha256 = dart.Sha256 }, null, cancellationToken).ConfigureAwait(false);
                await ArchiveExtractor.ExtractAsync(archive, cache, cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(dartStamp, EngineRevision, cancellationToken).ConfigureAwait(false);
                File.Delete(archive);
            }
            await CommandAsync(["flutter", "--disable-analytics"], progress, cancellationToken).ConfigureAwait(false);
            await CommandAsync(["dart", "--disable-analytics"], progress, cancellationToken).ConfigureAwait(false);
            await CommandAsync(["flutter", "config", "--enable-web"], progress, cancellationToken).ConfigureAwait(false);
            await CommandAsync(["flutter", "precache", "--web"], progress, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Marker, Revision, cancellationToken).ConfigureAwait(false);
            await CommandAsync(["apt-get", "clean"], progress, cancellationToken).ConfigureAwait(false);
            progress?.Report(new(InstallStage.Completed, "Flutter and Dart are installed", 1));
        }
        catch (DownloadException ex) { throw Failures.From(ex); }
        catch (ToolchainRuntimeException ex) { throw new ToolchainInstallException(InstallFailure.CommandFailed, ex.Message, innerException: ex); }
    }

    /// <inheritdoc />
    public Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        toolchain.Id == "flutter" ? InstallAsync(toolchain, progress, cancellationToken) : _primary.UpdateAsync(toolchain, progress, cancellationToken);
    /// <inheritdoc />
    public Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (toolchain.Id != "flutter") return _primary.RemoveAsync(toolchain, installed, progress, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains)
    {
        var usage = _primary.MeasureDiskUsage([.. toolchains.Where(t => t.Id != "flutter")]);
        return usage with { Toolchains = usage.Toolchains + DiskSpace.Size(_root) };
    }
    private void Configure()
    {
        Directory.CreateDirectory(Path.Combine(Rootfs, "tmp"));
        Directory.CreateDirectory(Path.Combine(Rootfs, "root"));
        var dns = _platform.DnsServers();
        var resolv = Path.Combine(Rootfs, "etc/resolv.conf");
        File.Delete(resolv); // Ubuntu's archive may contain a link into /run/systemd/resolve.
        File.WriteAllText(resolv, string.Concat((dns.Count > 0 ? dns : ["1.1.1.1", "8.8.8.8"]).Select(d => $"nameserver {d}\n")));
        File.WriteAllText(Path.Combine(Rootfs, "etc/hosts"), "127.0.0.1 localhost\n::1 localhost\n");
    }
    private async Task CommandAsync(string[] command, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new(InstallStage.Configuring, string.Join(' ', command), null));
        var result = await CommandRunner.RunAsync(_flutter, new ProcessStartSpec { Program = command[0], Arguments = command[1..],
            Environment = new Dictionary<string, string?> { ["DEBIAN_FRONTEND"] = "noninteractive", ["CI"] = "true" } },
            line => progress?.Report(new(InstallStage.Configuring, line.Trim(), null)), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) throw new ToolchainInstallException(InstallFailure.CommandFailed, $"{command[0]} failed: {result.Tail(8)}", result.Output);
    }
}
