using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Linux;

/// <summary>
/// TabStudio's Linux environment on Android: an Alpine Linux root file system in the app's storage, set up the
/// first time a toolchain is installed, and the <see cref="ProotRuntime"/> that runs its programs.
/// </summary>
/// <remarks>
/// <para>
/// Setting up downloads the catalog's minimal root file system for the device's architecture (about 4 MB),
/// checks it against the hash the catalog pins, unpacks it beside the final folder and moves it into place only
/// when it is complete, so an interrupted setup never leaves a broken environment. Then it writes what Android
/// does not provide: DNS servers, <c>/etc/hosts</c>, the package repositories of the pinned release, a shell
/// profile, and stand-ins for the <c>/proc</c> files Android hides.
/// </para>
/// <para>Layout, under the folder it is given: <c>rootfs/</c>, <c>downloads/</c> and <c>tmp/</c> (PRoot's own).</para>
/// </remarks>
public sealed class AlpineEnvironment
{
    /// <summary>The file in the root file system that marks a finished setup.</summary>
    public const string ReadyMarker = ".tabstudio/ready";

    private readonly LinuxDistribution _distribution;
    private readonly ToolchainPlatform _platform;
    private readonly Downloader _downloader;
    private readonly Lazy<ProotRuntime> _runtime;
    private readonly List<string> _sharedFolders;

    /// <summary>Creates the environment over a folder.</summary>
    /// <param name="root">The folder that holds everything: the root file system, downloads and temporary files.</param>
    /// <param name="distribution">The pinned distribution, from the catalog.</param>
    /// <param name="platform">The device: the native library folder and the DNS servers.</param>
    /// <param name="downloader">Downloads the root file system.</param>
    /// <param name="sharedFolders">Folders programs see at their own paths, such as TabStudio's projects folder.</param>
    public AlpineEnvironment(string root, LinuxDistribution distribution, ToolchainPlatform platform, Downloader downloader, IEnumerable<string> sharedFolders)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(distribution);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(sharedFolders);
        Root = root;
        _distribution = distribution;
        _platform = platform;
        _downloader = downloader;
        _sharedFolders = [.. sharedFolders, .. platform.SharedFolders];
        _runtime = new(CreateRuntime);
    }

    /// <summary>The folder everything is kept in.</summary>
    public string Root { get; }

    /// <summary>The root file system.</summary>
    public string Rootfs => Path.Combine(Root, "rootfs");

    /// <summary>Where downloads are kept until they are complete and used.</summary>
    public string Downloads => Path.Combine(Root, "downloads");

    /// <summary>The distribution, as the catalog pins it.</summary>
    public LinuxDistribution Distribution => _distribution;

    /// <summary>Alpine's name for this device's architecture.</summary>
    public string? Architecture => _platform.Architecture;

    /// <summary>Whether the environment is set up.</summary>
    public bool IsSetUp => File.Exists(Path.Combine(Rootfs, ReadyMarker));

    /// <summary>Why the environment cannot be used on this device, or <see langword="null"/> when it can.</summary>
    public string? Problem
    {
        get
        {
            if (_platform.ProotPath is not { } proot || !File.Exists(proot) || !File.Exists(_platform.LoaderPath))
            {
                return "This build of TabStudio was made without its Linux runtime (PRoot), so it cannot install toolchains.";
            }

            if (Architecture is not { } architecture || !_distribution.Rootfs.ContainsKey(architecture))
            {
                return $"{_distribution.Name} has no build for this tablet's processor ({RuntimeInformation.ProcessArchitecture}).";
            }

            return null;
        }
    }

    /// <summary>The runtime that runs the environment's programs.</summary>
    public ProotRuntime Runtime => _runtime.Value;

    /// <summary>
    /// Downloads, verifies and unpacks the root file system, and configures it. Does nothing when the environment
    /// is already set up, except refreshing its configuration.
    /// </summary>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Stops the setup; a later setup continues the download.</param>
    /// <exception cref="ToolchainInstallException">The setup failed.</exception>
    public async Task SetUpAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (Problem is { } problem)
        {
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, problem);
        }

        if (IsSetUp)
        {
            Configure();
            return;
        }

        var architecture = Architecture!;
        var image = _distribution.Rootfs[architecture];
        EnsureSpace(image.Size * 4, $"{_distribution.DisplayName}");

        var archive = Path.Combine(Downloads, image.File);
        progress?.Report(new InstallProgress(InstallStage.SettingUpLinux, $"Downloading {_distribution.DisplayName}", 0, 0, image.Size));
        try
        {
            await _downloader.DownloadAsync(
                new DownloadRequest { Address = _distribution.RootfsAddress(architecture, image), Destination = archive, Name = _distribution.DisplayName, Size = image.Size, Sha256 = image.Sha256 },
                new InlineProgress<DownloadProgress>(p => progress?.Report(new InstallProgress(InstallStage.SettingUpLinux, $"Downloading {_distribution.DisplayName}", Ratio(p.Received, p.Total ?? image.Size) * 0.9, p.Received, p.Total ?? image.Size))),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DownloadException ex)
        {
            throw Installation.Failures.From(ex);
        }

        progress?.Report(new InstallProgress(InstallStage.SettingUpLinux, $"Unpacking {_distribution.DisplayName}", 0.92));
        var partial = Rootfs + ".partial";
        if (Directory.Exists(partial))
        {
            Directory.Delete(partial, recursive: true);
        }

        try
        {
            await ArchiveExtractor.ExtractTarGzAsync(archive, partial, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            File.Delete(archive);
            throw new ToolchainInstallException(InstallFailure.IntegrityFailed, ex.Message, innerException: ex);
        }

        if (Directory.Exists(Rootfs))
        {
            Directory.Delete(Rootfs, recursive: true);
        }

        Directory.Move(partial, Rootfs);
        Configure();
        var marker = Path.Combine(Rootfs, ReadyMarker);
        await File.WriteAllTextAsync(marker, $"{_distribution.Name} {_distribution.Version} {architecture}\n", cancellationToken).ConfigureAwait(false);
        File.Delete(archive);
        progress?.Report(new InstallProgress(InstallStage.SettingUpLinux, $"{_distribution.DisplayName} is set up", 1));
    }

    /// <summary>
    /// Writes the files Android does not provide. Runs after setup and whenever the IDE starts the environment,
    /// so DNS follows the network the device is on.
    /// </summary>
    public void Configure()
    {
        var etc = Path.Combine(Rootfs, "etc");
        Directory.CreateDirectory(etc);

        var dns = _platform.DnsServers().Where(s => s.Length > 0).Distinct().ToList();
        if (dns.Count == 0)
        {
            dns = ["1.1.1.1", "8.8.8.8"];
        }

        WriteIfChanged(Path.Combine(etc, "resolv.conf"), string.Concat(dns.Select(s => $"nameserver {s}\n")));
        WriteIfChanged(Path.Combine(etc, "hosts"), "127.0.0.1 localhost\n::1 localhost ip6-localhost ip6-loopback\n");
        WriteIfChanged(
            Path.Combine(etc, "apk", "repositories"),
            string.Concat(_distribution.Repositories.Select(r => $"{_distribution.Mirror.AbsoluteUri.TrimEnd('/')}/{_distribution.Branch}/{r}\n")));
        WriteIfChanged(Path.Combine(etc, "profile.d", "tabstudio.sh"), ShellProfile);

        foreach (var folder in new[] { "root", "tmp", ".tabstudio" })
        {
            Directory.CreateDirectory(Path.Combine(Rootfs, folder));
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path.Combine(Rootfs, "tmp"), (UnixFileMode)0b1_111_111_111);
        }

        WriteProcStandIns();
        Directory.CreateDirectory(Path.Combine(Root, "tmp"));
    }

    /// <summary>The free space on the storage the environment is on, when the system says.</summary>
    public long? FreeSpace => _platform.FreeSpace(Root);

    /// <summary>Throws when the storage has less than some bytes free.</summary>
    /// <param name="bytes">The bytes needed.</param>
    /// <param name="what">What needs them, for the message.</param>
    /// <exception cref="ToolchainInstallException">There is not enough space.</exception>
    public void EnsureSpace(long bytes, string what)
    {
        const long Margin = 50_000_000;
        if (_platform.FreeSpace(Root) is { } free && free < bytes + Margin)
        {
            throw new ToolchainInstallException(
                InstallFailure.NotEnoughSpace,
                $"{what} needs {DiskSpace.Format(bytes)} and this tablet has {DiskSpace.Format(free)} free. Free up some space, or remove toolchains you no longer use, and try again.");
        }
    }

    private const string ShellProfile = """
        # Written by TabStudio: the Terminal's prompt and settings for programs run from it.
        export PS1='\[\033[1;32m\]\w\[\033[0m\] \$ '
        export PIP_ROOT_USER_ACTION=ignore
        export PIP_DISABLE_PIP_VERSION_CHECK=1
        export npm_config_update_notifier=false
        export npm_config_fund=false
        alias ll='ls -la'

        """;

    private ProotRuntime CreateRuntime() => new(new ProotRuntimeOptions
    {
        ProotPath = _platform.ProotPath ?? string.Empty,
        LoaderPath = _platform.LoaderPath ?? string.Empty,
        Rootfs = Rootfs,
        TemporaryDirectory = Path.Combine(Root, "tmp"),
        DisplayName = _distribution.DisplayName,
        SharedFolders = _sharedFolders,
    });

    /// <summary>Writes stand-ins for the parts of <c>/proc</c> Android hides from apps, with the device's processor count.</summary>
    private void WriteProcStandIns()
    {
        var folder = Path.Combine(Rootfs, ProotRuntime.ProcStandInFolder);
        Directory.CreateDirectory(folder);
        var cpus = Environment.ProcessorCount;
        var stat = new StringBuilder("cpu  1000 0 1000 100000 100 0 0 0 0 0\n");
        for (var i = 0; i < cpus; i++)
        {
            stat.Append(CultureInfo.InvariantCulture, $"cpu{i} {1000 / cpus} 0 {1000 / cpus} {100000 / cpus} 0 0 0 0 0 0\n");
        }

        stat.Append("intr 0\nctxt 0\nbtime 0\nprocesses 1\nprocs_running 1\nprocs_blocked 0\nsoftirq 0 0 0 0 0 0 0 0 0 0 0\n");
        var uptime = (Environment.TickCount64 / 1000.0).ToString("0.00", CultureInfo.InvariantCulture);
        WriteIfChanged(Path.Combine(folder, "stat"), stat.ToString());
        WriteIfChanged(Path.Combine(folder, "loadavg"), "0.00 0.00 0.00 1/100 1\n");
        File.WriteAllText(Path.Combine(folder, "uptime"), $"{uptime} {uptime}\n");
        WriteIfChanged(Path.Combine(folder, "version"), $"Linux version {Environment.OSVersion.Version} (TabStudio) #1 SMP PREEMPT\n");
        WriteIfChanged(Path.Combine(folder, "vmstat"), "nr_free_pages 0\nnr_inactive_anon 0\nnr_active_anon 0\n");
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static double Ratio(long done, long total) => total <= 0 ? 0 : Math.Clamp((double)done / total, 0, 1);
}

/// <summary>An <see cref="IProgress{T}"/> that reports on the reporting thread, without posting to a context.</summary>
/// <typeparam name="T">What is reported.</typeparam>
internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
