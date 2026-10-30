using System.Text.RegularExpressions;
using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Installation;

/// <summary>
/// Installs toolchains into TabStudio's Linux environment on Android: Alpine packages through
/// <see cref="AlpinePackageManager"/>, then the toolchain's setup steps (archives such as a debug adapter, and
/// commands such as <c>npm install --global</c>).
/// </summary>
/// <remarks>
/// <para>
/// An install checks the device first: the Linux runtime, the architecture, and free space for the downloads and
/// what they unpack to, so a full storage is reported before anything is downloaded. The first install sets up
/// the environment itself. Downloads continue where an interrupted attempt stopped.
/// </para>
/// <para>
/// Removing a toolchain removes its packages, unless another installed toolchain lists them too, and
/// <c>apk</c> removes the dependencies nothing else needs; then the files its setup steps installed.
/// </para>
/// </remarks>
public sealed partial class LinuxToolchainInstaller : IToolchainInstaller
{
    private readonly AlpineEnvironment _environment;
    private readonly AlpinePackageManager _packages;
    private readonly Downloader _downloader;
    private readonly string _recordsFile;

    /// <summary>Creates the installer.</summary>
    /// <param name="environment">The Linux environment.</param>
    /// <param name="packages">Installs Alpine packages into it.</param>
    /// <param name="downloader">Downloads setup archives.</param>
    public LinuxToolchainInstaller(AlpineEnvironment environment, AlpinePackageManager packages, Downloader downloader)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(downloader);
        _environment = environment;
        _packages = packages;
        _downloader = downloader;
        _recordsFile = Path.Combine(environment.Root, "toolchains.json");
    }

    /// <inheritdoc />
    public IToolchainRuntime Runtime => _environment.Runtime;

    /// <inheritdoc />
    public bool CanInstall => Problem is null;

    /// <inheritdoc />
    public string? Problem => _environment.Problem;

    /// <summary>The Linux environment.</summary>
    public AlpineEnvironment Environment => _environment;

    /// <inheritdoc />
    public bool IsRuntimeReady => Problem is null && _environment.IsSetUp;

    /// <inheritdoc />
    public async Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        var firstSetup = !_environment.IsSetUp;
        await _environment.SetUpAsync(progress, cancellationToken).ConfigureAwait(false);
        var records = InstalledToolchains.Load(_recordsFile);
        if (firstSetup || records.BasePackages.Count == 0)
        {
            // What the environment has before any toolchain: the base, which no toolchain's size counts.
            records.BasePackages.Clear();
            records.BasePackages.AddRange(_packages.Installed.Packages.Select(p => p.Name));
            records.Save(_recordsFile);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains, bool checkForUpdates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        if (Problem is { } problem)
        {
            return [.. toolchains.Select(t => Estimate(t, ToolchainState.Unavailable) with { Note = problem })];
        }

        if (!_environment.IsSetUp)
        {
            var setup = _environment.Distribution.Rootfs.GetValueOrDefault(_environment.Architecture ?? string.Empty)?.Size ?? 0;
            return [.. toolchains.Select(t => CompilerProblem(t) is { } compilerProblem
                ? Estimate(t, ToolchainState.Unavailable) with { Note = compilerProblem }
                : Estimate(t, ToolchainState.NotInstalled) with
            {
                DownloadSize = t.EstimatedDownloadSize + setup,
                Note = $"Sets up {_environment.Distribution.DisplayName} the first time.",
            })];
        }

        var records = InstalledToolchains.Load(_recordsFile);
        var installed = _packages.Installed;
        ApkIndex? index = _packages.CachedIndex;
        if (checkForUpdates)
        {
            try
            {
                index = await _packages.LoadIndexAsync(refresh: true, cancellationToken).ConfigureAwait(false);
            }
            catch (ToolchainInstallException)
            {
                // Offline: the last index, if any, still gives sizes; updates are checked next time.
            }
        }

        var result = new List<ToolchainStatus>();
        foreach (var toolchain in toolchains)
        {
            result.Add(CompilerProblem(toolchain) is { } compilerProblem
                ? Estimate(toolchain, ToolchainState.Unavailable) with { Note = compilerProblem }
                : ReadStatus(ForDevice(toolchain), records, installed, index));
        }

        return result;
    }

    /// <inheritdoc />
    public Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        InstallAsync(toolchain, progress, refreshMetadata: true, cancellationToken);

    /// <inheritdoc />
    public async Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, bool refreshMetadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        toolchain = ForDevice(toolchain);
        if (Microchip.MicrochipInstallation.IsCompiler(toolchain.Id))
        {
            Microchip.MicrochipInstallation.CheckConsent(_environment, toolchain.Id);
        }
        progress?.Report(new InstallProgress(InstallStage.Preparing, $"Preparing to install {toolchain.Name}", 0));
        await PrepareRuntimeAsync(new InlineProgress<InstallProgress>(p => progress?.Report(p with { Fraction = p.Fraction * 0.1 })), cancellationToken).ConfigureAwait(false);

        progress?.Report(new InstallProgress(InstallStage.Resolving, $"Reading the {_environment.Distribution.Name} package index", 0.1));
        var plan = await PlanAsync(() => _packages.PlanInstallAsync(toolchain.Packages, refreshMetadata, cancellationToken), toolchain).ConfigureAwait(false);
        var archives = toolchain.Setup.Where(s => s.Archive is not null).ToList();
        var archiveBytes = archives.Sum(s => s.Archive!.Size);
        _environment.EnsureSpace(plan.DownloadSize + plan.InstalledSize + (archiveBytes * 3), toolchain.Name);
        if (Microchip.MicrochipInstallation.IsCompiler(toolchain.Id))
        {
            _environment.EnsureSpace(toolchain.EstimatedDownloadSize + toolchain.EstimatedInstalledSize + plan.DownloadSize + plan.InstalledSize, toolchain.Name);
        }

        await DownloadAsync(toolchain, plan, archives, progress, cancellationToken).ConfigureAwait(false);
        await InstallPackagesAsync(toolchain, upgrade: false, progress, cancellationToken).ConfigureAwait(false);
        var setup = await RunSetupAsync(toolchain, toolchain.Setup, progress, cancellationToken).ConfigureAwait(false);

        var records = InstalledToolchains.Load(_recordsFile);
        records.Toolchains[toolchain.Id] = new InstalledToolchain
        {
            Version = VersionOf(toolchain, _packages.Installed),
            Setup = setup,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        records.Save(_recordsFile);
        CleanDownloads(archives);
        progress?.Report(new InstallProgress(InstallStage.Completed, $"{toolchain.Name} is installed", 1));
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        toolchain = ForDevice(toolchain);
        progress?.Report(new InstallProgress(InstallStage.Resolving, $"Checking for a newer {toolchain.Name}", 0.05));
        await _environment.SetUpAsync(null, cancellationToken).ConfigureAwait(false);
        var records = InstalledToolchains.Load(_recordsFile);
        var record = records.Toolchains.GetValueOrDefault(toolchain.Id) ?? new InstalledToolchain();
        var plan = await PlanAsync(() => _packages.PlanUpgradeAsync(toolchain.Packages, refresh: true, cancellationToken), toolchain).ConfigureAwait(false);
        var stale = toolchain.Setup.Where(s => record.Setup.GetValueOrDefault(s.Name) != s.Fingerprint).ToList();
        if (stale.Count > 0)
        {
            stale = toolchain.Setup.Where(s => stale.Contains(s) || s.RunAfterSetupChanges).ToList();
        }
        var archives = stale.Where(s => s.Archive is not null).ToList();
        _environment.EnsureSpace(plan.DownloadSize + plan.InstalledSize + (archives.Sum(s => s.Archive!.Size) * 3), toolchain.Name);

        await DownloadAsync(toolchain, plan, archives, progress, cancellationToken).ConfigureAwait(false);
        if (plan.Packages.Count > 0)
        {
            await InstallPackagesAsync(toolchain, upgrade: true, progress, cancellationToken).ConfigureAwait(false);
        }

        var setup = new Dictionary<string, string>(record.Setup, StringComparer.Ordinal);
        foreach (var (name, fingerprint) in await RunSetupAsync(toolchain, stale, progress, cancellationToken).ConfigureAwait(false))
        {
            setup[name] = fingerprint;
        }

        records = InstalledToolchains.Load(_recordsFile);
        records.Toolchains[toolchain.Id] = record with { Version = VersionOf(toolchain, _packages.Installed), Setup = setup, UpdatedAt = DateTimeOffset.UtcNow };
        records.Save(_recordsFile);
        CleanDownloads(archives);
        progress?.Report(new InstallProgress(InstallStage.Completed, $"{toolchain.Name} is up to date", 1));
    }

    /// <inheritdoc />
    public async Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        ArgumentNullException.ThrowIfNull(installed);
        toolchain = ForDevice(toolchain);
        progress?.Report(new InstallProgress(InstallStage.Removing, $"Removing {toolchain.Name}", 0.1));
        var kept = installed.Where(t => t.Id != toolchain.Id).SelectMany(t => t.Packages).ToHashSet(StringComparer.Ordinal);
        var database = _packages.Installed;
        var packages = toolchain.Packages.Where(p => !kept.Contains(p) && database.Find(p) is not null).ToList();
        await _packages.RemoveAsync(packages, line => progress?.Report(new InstallProgress(InstallStage.Removing, line.Trim(), 0.5)), cancellationToken).ConfigureAwait(false);

        progress?.Report(new InstallProgress(InstallStage.Removing, $"Removing {toolchain.Name}'s tools", 0.8));
        var keptPaths = installed.Where(t => t.Id != toolchain.Id).SelectMany(PathsOf).ToHashSet(StringComparer.Ordinal);
        foreach (var path in PathsOf(toolchain).Where(p => !keptPaths.Contains(p)))
        {
            DeleteGuestPath(path);
        }

        var records = InstalledToolchains.Load(_recordsFile);
        records.Toolchains.Remove(toolchain.Id);
        records.Save(_recordsFile);
        progress?.Report(new InstallProgress(InstallStage.Completed, $"{toolchain.Name} was removed", 1));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Toolchains count what their packages and the packages those need take, as <c>apk</c> records it, and the
    /// files their setup steps installed; the Linux environment itself is the rest of its root file system, so
    /// the parts add up to what TabStudio's Linux takes on the device.
    /// </remarks>
    public DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        var downloads = DiskSpace.Size(_environment.Downloads);
        if (!_environment.IsSetUp)
        {
            return new DiskUsage(0, 0, downloads, _environment.FreeSpace ?? 0);
        }

        var records = InstalledToolchains.Load(_recordsFile);
        var installed = toolchains.Where(t => records.Toolchains.ContainsKey(t.Id)).ToList();
        var basePackages = records.BasePackages.ToHashSet(StringComparer.Ordinal);
        var packages = ApkResolver.Closure(_packages.Installed.Packages, installed.SelectMany(t => t.Packages))
            .Where(p => !basePackages.Contains(p.Name))
            .Sum(p => p.InstalledSize);
        var setup = installed.SelectMany(PathsOf).Distinct(StringComparer.Ordinal).Sum(p => DiskSpace.Size(GuestToDevice(p)));
        var waiting = _packages.DownloadsSize;
        var linux = Math.Max(0, DiskSpace.Size(_environment.Rootfs) - packages - setup - waiting);
        return new DiskUsage(packages + setup, linux, downloads + waiting, _environment.FreeSpace ?? 0);
    }

    private ToolchainStatus ReadStatus(ToolchainDefinition toolchain, InstalledToolchains records, ApkInstalledDatabase installed, ApkIndex? index)
    {
        if (records.Toolchains.TryGetValue(toolchain.Id, out var record) && installed.HasAll(toolchain.Packages))
        {
            var basePackages = records.BasePackages.ToHashSet(StringComparer.Ordinal);
            var size = ApkResolver.Closure(installed.Packages, toolchain.Packages).Where(p => !basePackages.Contains(p.Name)).Sum(p => p.InstalledSize)
                + PathsOf(toolchain).Sum(p => DiskSpace.Size(GuestToDevice(p)));
            var status = new ToolchainStatus
            {
                Toolchain = toolchain,
                State = ToolchainState.Installed,
                Version = VersionOf(toolchain, installed) is { } version ? ApkVersion.Display(version) : null,
                InstalledSize = size,
                SizesAreExact = true,
            };

            var upgrade = index is null ? null : ApkResolver.PlanUpgrade(index, installed.Packages, toolchain.Packages);
            var staleSetup = toolchain.Setup.Where(s => record.Setup.GetValueOrDefault(s.Name) != s.Fingerprint).ToList();
            if (upgrade is { Packages.Count: > 0 } || staleSetup.Count > 0)
            {
                var newest = toolchain.PinnedVersion ?? (toolchain.VersionPackage is { } name ? index?.Find(name)?.Version : null);
                status = status with
                {
                    State = ToolchainState.UpdateAvailable,
                    AvailableVersion = newest is null ? null : ApkVersion.Display(newest),
                    DownloadSize = (upgrade?.DownloadSize ?? 0) + staleSetup.Sum(s => s.Archive?.Size ?? 0),
                    InstallSize = upgrade?.InstalledSize ?? 0,
                };
            }

            return status;
        }

        if (index is null)
        {
            return Estimate(toolchain, ToolchainState.NotInstalled);
        }

        var plan = ApkResolver.PlanInstall(index, installed.Packages, toolchain.Packages);
        var archives = toolchain.Setup.Sum(s => s.Archive?.Size ?? 0);
        return new ToolchainStatus
        {
            Toolchain = toolchain,
            State = ToolchainState.NotInstalled,
            AvailableVersion = toolchain.PinnedVersion ?? (toolchain.VersionPackage is { } package && index.Find(package) is { } found ? ApkVersion.Display(found.Version) : null),
            DownloadSize = plan.DownloadSize + archives + (Microchip.MicrochipInstallation.IsCompiler(toolchain.Id) ? toolchain.EstimatedDownloadSize : 0),
            InstallSize = plan.InstalledSize + (archives * 3) + (Microchip.MicrochipInstallation.IsCompiler(toolchain.Id) ? toolchain.EstimatedInstalledSize : 0),
            SizesAreExact = !Microchip.MicrochipInstallation.IsCompiler(toolchain.Id),
            Note = plan.Missing.Count > 0 ? $"{_environment.Distribution.DisplayName} has no {string.Join(", ", plan.Missing)}." : null,
        };
    }

    private static ToolchainStatus Estimate(ToolchainDefinition toolchain, ToolchainState state) => new()
    {
        Toolchain = toolchain,
        State = state,
        DownloadSize = toolchain.EstimatedDownloadSize,
        InstallSize = toolchain.EstimatedInstalledSize,
    };

    /// <summary>Plans, refusing a plan with packages the distribution lacks.</summary>
    private async Task<ApkPlan> PlanAsync(Func<Task<ApkPlan>> plan, ToolchainDefinition toolchain)
    {
        var result = await plan().ConfigureAwait(false);
        if (result.Missing.Count > 0)
        {
            throw new ToolchainInstallException(
                InstallFailure.PackageNotFound,
                $"{_environment.Distribution.DisplayName} has no {string.Join(", ", result.Missing)}, which {toolchain.Name} needs.");
        }

        return result;
    }

    private async Task DownloadAsync(ToolchainDefinition toolchain, ApkPlan plan, List<SetupStepDefinition> archives, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        var total = plan.DownloadSize + archives.Sum(s => s.Archive!.Size);
        void Report(long done, string what) =>
            progress?.Report(new InstallProgress(InstallStage.Downloading, $"Downloading {what}", 0.12 + (0.58 * Ratio(done, total)), done, total));

        try
        {
            try
            {
                await _packages.DownloadAsync(plan, (done, package) => Report(done, $"{package.Name} {ApkVersion.Display(package.Version)}"), cancellationToken).ConfigureAwait(false);
            }
            catch (DownloadException ex) when (ex.Failure == DownloadFailure.NotFound)
            {
                // The mirror moved on since the index was read: read it again and download what it lists now.
                plan = await _packages.PlanInstallAsync(toolchain.Packages, cancellationToken).ConfigureAwait(false);
                await _packages.DownloadAsync(plan, (done, package) => Report(done, $"{package.Name} {ApkVersion.Display(package.Version)}"), cancellationToken).ConfigureAwait(false);
            }

            var offset = plan.DownloadSize;
            foreach (var step in archives)
            {
                var archive = step.Archive!;
                var start = offset;
                await _downloader.DownloadAsync(
                    new DownloadRequest { Address = archive.Url, Destination = ArchivePath(archive), Name = step.Name, Size = archive.Size, Sha256 = archive.Sha256 },
                    new InlineProgress<DownloadProgress>(p => Report(start + p.Received, step.Name)),
                    cancellationToken).ConfigureAwait(false);
                offset += archive.Size;
            }
        }
        catch (DownloadException ex)
        {
            throw Failures.From(ex);
        }
    }

    private async Task InstallPackagesAsync(ToolchainDefinition toolchain, bool upgrade, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new InstallProgress(InstallStage.Installing, $"Installing {toolchain.Name}", 0.7));
        await _packages.InstallAsync(
            toolchain.Packages,
            upgrade,
            line =>
            {
                // "(3/29) Installing libffi (3.5.2-r1)"
                if (StepPattern().Match(line) is { Success: true } step)
                {
                    var n = int.Parse(step.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
                    var of = int.Parse(step.Groups["of"].Value, System.Globalization.CultureInfo.InvariantCulture);
                    progress?.Report(new InstallProgress(InstallStage.Installing, step.Groups["what"].Value.Trim(), 0.7 + (0.2 * Ratio(n, of)), StepsDone: n, StepsTotal: of));
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, string>> RunSetupAsync(ToolchainDefinition toolchain, IReadOnlyList<SetupStepDefinition> steps, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (Microchip.MicrochipInstallation.IsCompiler(toolchain.Id))
        {
            Microchip.MicrochipInstallation.CheckConsent(_environment, toolchain.Id);
            Microchip.MicrochipInstallation.PrepareTranslator(_environment);
        }
        var done = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var fraction = 0.9 + (0.09 * i / Math.Max(1, steps.Count));
            progress?.Report(new InstallProgress(InstallStage.Configuring, $"Setting up {step.Name}", fraction));
            if (step.Archive is { } archive)
            {
                // Unpacked beside the installed copy, which is replaced only once the new one is complete: an
                // update that was cancelled or failed while unpacking left the toolchain without its debugger while
                // the Store still called it ready.
                var destination = GuestToDevice(archive.Destination).TrimEnd(Path.DirectorySeparatorChar);
                var unpacked = destination + ".tabstudio-new";
                if (Directory.Exists(unpacked))
                {
                    Directory.Delete(unpacked, recursive: true);
                }

                try
                {
                    await ArchiveExtractor.ExtractAsync(ArchivePath(archive), unpacked, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or OperationCanceledException)
                {
                    TryDeleteDirectory(unpacked);
                    if (ex is InvalidDataException)
                    {
                        File.Delete(ArchivePath(archive));
                        throw new ToolchainInstallException(InstallFailure.IntegrityFailed, ex.Message, innerException: ex);
                    }

                    throw;
                }

                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, recursive: true);
                }

                Directory.Move(unpacked, destination);
            }

            if (step.File is { } file)
            {
                WriteGuestFile(file);
            }

            if (step.Command.Count > 0)
            {
                var result = await CommandRunner.RunAsync(
                    Runtime,
                    new ProcessStartSpec
                    {
                        Program = step.Command[0],
                        Arguments = [.. step.Command.Skip(1)],
                        Environment = toolchain.Run?.Environment.ToDictionary(e => e.Key, e => (string?)e.Value) ?? new Dictionary<string, string?>(),
                    },
                    line => progress?.Report(new InstallProgress(InstallStage.Configuring, $"Setting up {step.Name}: {line.Trim()}", fraction)),
                    cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    var offline = result.Output.Contains("ENOTFOUND", StringComparison.Ordinal) || result.Output.Contains("EAI_AGAIN", StringComparison.Ordinal)
                        || result.Output.Contains("Network is unreachable", StringComparison.Ordinal);
                    throw new ToolchainInstallException(
                        offline ? InstallFailure.NoNetwork : InstallFailure.CommandFailed,
                        offline
                            ? $"Setting up {step.Name} needs the internet, and this tablet seems to be offline. Connect and try again."
                            : $"Setting up {step.Name} failed ({result.Exit.Description}): {result.Tail(3)}",
                        result.Output);
                }
            }

            done[step.Name] = step.Fingerprint;
        }

        return done;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // What is left is replaced by the next attempt.
        }
    }

    /// <summary>The paths a toolchain's setup steps put files at, in the environment.</summary>
    private ToolchainDefinition ForDevice(ToolchainDefinition toolchain) => toolchain with
    {
        Setup = toolchain.Setup.Where(s => s.Architectures.Count == 0 || s.Architectures.Contains(_environment.Architecture, StringComparer.Ordinal)).ToList(),
    };

    private static IEnumerable<string> PathsOf(ToolchainDefinition toolchain) =>
        toolchain.Setup.SelectMany(s => (s.Archive, s.File) switch
        {
            ({ } archive, _) => [archive.Destination, .. s.Removes],
            (_, { } file) => [file.Path, .. s.Removes],
            _ => s.Removes,
        });

    /// <summary>Writes a setup step's file into the environment, as a program when it is one.</summary>
    private void WriteGuestFile(FileDefinition file)
    {
        var path = GuestToDevice(file.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (new FileInfo(path).LinkTarget is not null)
        {
            File.Delete(path);
        }

        File.WriteAllText(path, file.Content.ReplaceLineEndings("\n"));
        if (file.Executable && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
        }
    }

    private string GuestToDevice(string guestPath) => Path.Combine(_environment.Rootfs, guestPath.TrimStart('/'));

    private void DeleteGuestPath(string guestPath)
    {
        var path = GuestToDevice(guestPath);
        var info = new FileInfo(path);
        if (info.LinkTarget is not null || info.Exists)
        {
            info.Delete();
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private string ArchivePath(ArchiveDefinition archive) => Path.Combine(_environment.Downloads, "archives", Path.GetFileName(archive.Url.AbsolutePath));

    private void CleanDownloads(IEnumerable<SetupStepDefinition> archives)
    {
        foreach (var step in archives)
        {
            File.Delete(ArchivePath(step.Archive!));
        }

        _packages.CleanLocalRepository();
    }

    private static string? VersionOf(ToolchainDefinition toolchain, ApkInstalledDatabase installed) =>
        toolchain.PinnedVersion ?? (toolchain.VersionPackage is { } name ? installed.Find(name)?.Version : null);

    private string? CompilerProblem(ToolchainDefinition toolchain) => !Microchip.MicrochipInstallation.IsCompiler(toolchain.Id) ? null
        : _environment.Architecture is not ("aarch64" or "x86_64") ? "XC compilers need 64-bit Android. Use the Windows Bridge on this device."
        : _environment.Architecture == "aarch64" && !File.Exists(Path.Combine(_environment.NativeLibraryDirectory ?? string.Empty, "libbox64.so"))
            ? "This APK has no box64 translator. Install an ARM64 APK with PIC support, or use the Windows Bridge." : null;

    private static double Ratio(long done, long total) => total <= 0 ? 1 : Math.Clamp((double)done / total, 0, 1);

    [GeneratedRegex(@"^\(\s*(?<n>\d+)/(?<of>\d+)\)\s+(?<what>.+)$")]
    private static partial Regex StepPattern();
}
