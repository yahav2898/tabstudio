using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Processes.Unix;

namespace TabStudio.Toolchains.Runtimes;

/// <summary>Where the PRoot runtime finds its programs and its root file system.</summary>
public sealed record ProotRuntimeOptions
{
    /// <summary>PRoot itself: <c>libproot.so</c> in the app's native library folder.</summary>
    public required string ProotPath { get; init; }

    /// <summary>PRoot's loader: <c>libproot-loader.so</c> next to it.</summary>
    public required string LoaderPath { get; init; }

    /// <summary>The root file system, an extracted Alpine Linux.</summary>
    public required string Rootfs { get; init; }

    /// <summary>A folder PRoot may write temporary files to, outside the root file system.</summary>
    public required string TemporaryDirectory { get; init; }

    /// <summary>The environment's name for the UI, such as <c>Alpine Linux 3.24</c>.</summary>
    public string DisplayName { get; init; } = "Alpine Linux";

    /// <summary>Folders of the device visible to programs at the same path, such as TabStudio's projects folder.</summary>
    public IReadOnlyList<string> SharedFolders { get; init; } = [];
}

/// <summary>
/// Runs programs in TabStudio's Linux environment on Android: an Alpine Linux root file system, entered through
/// PRoot.
/// </summary>
/// <remarks>
/// <para>
/// Android only lets an app execute files from its native library folder, so PRoot and its loader ship there as
/// <c>libproot.so</c> and <c>libproot-loader.so</c>. PRoot starts every program of the environment through the
/// loader, which maps the program into memory itself, so nothing from the app's writable storage is ever
/// executed by the system, and every child and grandchild program works the same way. PRoot also translates
/// paths, so programs see the Alpine root file system as <c>/</c>.
/// </para>
/// <para>
/// Programs run as a simulated root user (<c>-0</c>), which <c>apk</c> needs; hard links, which Android does not
/// allow apps to create, become symbolic links (<c>--link2symlink</c>); and System V IPC is emulated
/// (<c>--sysvipc</c>). The device's <c>/dev</c>, <c>/proc</c> and <c>/sys</c> are visible, and TabStudio's
/// projects folder and each open workspace appear at their own paths, so paths in programs' output are the
/// files the IDE opens. Android hides some of <c>/proc</c> from apps (<c>stat</c>, <c>loadavg</c>,
/// <c>uptime</c>, <c>version</c>, <c>vmstat</c>); programs such as <c>top</c> and Node's <c>os.cpus()</c> read
/// stand-ins the environment's setup writes instead.
/// </para>
/// <para>
/// Every program is a descendant of TabStudio's own process and is killed with its whole tree when it is
/// stopped (<c>--kill-on-exit</c> also ends anything a program left behind), which is what Android 12 and later
/// expect of apps' child processes.
/// </para>
/// </remarks>
public sealed class ProotRuntime : IToolchainRuntime
{
    /// <summary>The environment's search path for programs.</summary>
    public const string SearchPath = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin";

    /// <summary>Where the environment's setup writes the stand-ins for the parts of <c>/proc</c> Android hides.</summary>
    public const string ProcStandInFolder = ".tabstudio/proc";

    /// <summary>The <c>/proc</c> files Android hides from apps, which get stand-ins.</summary>
    public static readonly IReadOnlyList<string> HiddenProcFiles = ["stat", "loadavg", "uptime", "version", "vmstat"];

    private readonly ProotRuntimeOptions _options;
    private readonly Lock _gate = new();
    private readonly List<string> _sharedFolders;
    private readonly Lazy<IReadOnlyList<string>> _procStandIns;

    /// <summary>Creates the runtime.</summary>
    /// <param name="options">Where PRoot and the root file system are.</param>
    public ProotRuntime(ProotRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _sharedFolders = [.. options.SharedFolders.Select(Normalize)];
        _procStandIns = new(FindProcStandIns);
    }

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName;

    /// <inheritdoc />
    public RuntimeKind Kind => RuntimeKind.Linux;

    /// <summary>The root file system.</summary>
    public string Rootfs => _options.Rootfs;

    /// <inheritdoc />
    public ToolchainProcess Start(ProcessStartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!File.Exists(_options.ProotPath))
        {
            throw new ToolchainRuntimeException("This build of TabStudio does not include its Linux runtime (PRoot), so it cannot run toolchains.");
        }

        if (!HasCommand(spec.Program))
        {
            throw new ToolchainRuntimeException($"'{spec.Program}' is not installed in TabStudio's Linux environment. Install its toolchain from the Toolchain Store.");
        }

        var arguments = BuildArguments(spec);
        var environment = BuildEnvironment(spec);
        return new ToolchainProcess(UnixSpawner.Start(_options.ProotPath, arguments, environment, workingDirectory: null, spec.Terminal), spec);
    }

    /// <inheritdoc />
    public bool HasCommand(string command) => Resolve(command) is not null;

    /// <inheritdoc />
    public string? Resolve(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.StartsWith('/'))
        {
            return Exists(ToRootfs(command)) || (IsShared(command) && Exists(command)) ? command : null;
        }

        return SearchPath.Split(':')
            .Where(folder => Exists(Path.Combine(_options.Rootfs, folder.TrimStart('/'), command)))
            .Select(folder => folder + "/" + command)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => new()
    {
        Program = "/bin/ash",
        Arguments = ["-l"],
        WorkingDirectory = workingDirectory,
        Terminal = size,
    };

    /// <inheritdoc />
    public void ShareFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var normalized = Normalize(folder);
        lock (_gate)
        {
            if (!IsShared(normalized) && !normalized.StartsWith(Normalize(_options.Rootfs) + "/", StringComparison.Ordinal))
            {
                _sharedFolders.Add(normalized);
            }
        }
    }

    /// <inheritdoc />
    public string? ToDevicePath(string programPath, string? workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        var guest = programPath.StartsWith('/') ? programPath : Path.Combine(workingDirectory ?? "/root", programPath);
        guest = Path.GetFullPath(guest);
        var device = IsShared(guest) ? guest : ToRootfs(guest);
        return File.Exists(device) ? device : null;
    }

    /// <summary>The argument vector that starts a program through PRoot, PRoot's own path first.</summary>
    internal List<string> BuildArguments(ProcessStartSpec spec)
    {
        var arguments = new List<string>
        {
            _options.ProotPath,
            "--kill-on-exit",
            "--link2symlink",
            "--sysvipc",
            "-L",
            "-r", _options.Rootfs,
            "-b", "/dev",
            "-b", "/proc",
            "-b", "/sys",
            "-b", "/dev/urandom:/dev/random",
            "-b", "/proc/self/fd:/dev/fd",
            "-b", "/proc/self/fd/0:/dev/stdin",
            "-b", "/proc/self/fd/1:/dev/stdout",
            "-b", "/proc/self/fd/2:/dev/stderr",
            // Android has no /dev/shm; POSIX shared memory and semaphores (Python's multiprocessing) need one.
            "-b", $"{Path.Combine(_options.Rootfs, "tmp")}:/dev/shm",
        };

        if (spec.UserId is { } uid)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(uid);
            arguments.AddRange(["-i", $"{uid}:{uid}"]);
        }
        else
        {
            arguments.Add("-0");
        }

        foreach (var file in _procStandIns.Value)
        {
            arguments.Add("-b");
            arguments.Add($"{Path.Combine(_options.Rootfs, ProcStandInFolder, file)}:/proc/{file}");
        }

        if (spec.WorkingDirectory is { } folder && Directory.Exists(folder) && !folder.StartsWith(Normalize(_options.Rootfs) + "/", StringComparison.Ordinal))
        {
            ShareFolder(folder);
        }

        lock (_gate)
        {
            foreach (var shared in _sharedFolders.Where(Directory.Exists))
            {
                arguments.Add("-b");
                arguments.Add(shared);
            }
        }

        foreach (var mount in spec.Mounts)
        {
            if (!Path.IsPathFullyQualified(mount.Source) || !mount.Target.StartsWith('/')
                || mount.Source.Contains(':') || mount.Target.Contains(':') || mount.Target.Split('/').Contains(".."))
            {
                throw new ArgumentException("Process mounts need absolute paths without ':' or '..'.", nameof(spec));
            }

            arguments.AddRange(["-b", $"{mount.Source}:{mount.Target}"]);
        }

        arguments.Add("-w");
        arguments.Add(GuestWorkingDirectory(spec.WorkingDirectory));
        arguments.Add(spec.Program);
        arguments.AddRange(spec.Arguments);
        return arguments;
    }

    /// <summary>The environment programs start with: the environment's own, then the spec's variables.</summary>
    internal List<string> BuildEnvironment(ProcessStartSpec spec)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = SearchPath,
            ["HOME"] = "/root",
            ["USER"] = "root",
            ["LOGNAME"] = "root",
            ["SHELL"] = "/bin/ash",
            ["LANG"] = "C.UTF-8",
            ["TMPDIR"] = "/tmp",
            ["TERM"] = spec.Terminal is null ? "dumb" : "xterm-256color",
            ["PROOT_TMP_DIR"] = _options.TemporaryDirectory,
            ["PROOT_LOADER"] = _options.LoaderPath,
        };
        if (spec.Terminal is not null)
        {
            environment["COLORTERM"] = "truecolor";
        }

        foreach (var (name, value) in spec.Environment)
        {
            if (value is null)
            {
                environment.Remove(name);
            }
            else
            {
                environment[name] = name == "PATH" ? value.Replace("$PATH", SearchPath, StringComparison.Ordinal) : value;
            }
        }

        Directory.CreateDirectory(_options.TemporaryDirectory);
        return [.. environment.Select(e => $"{e.Key}={e.Value}")];
    }

    private string GuestWorkingDirectory(string? folder) =>
        folder is null ? "/root"
        : folder.StartsWith(Normalize(_options.Rootfs) + "/", StringComparison.Ordinal) ? "/" + Path.GetRelativePath(_options.Rootfs, folder).Replace('\\', '/')
        : folder;

    private bool IsShared(string path)
    {
        lock (_gate)
        {
            return _sharedFolders.Any(shared => path == shared || path.StartsWith(shared + "/", StringComparison.Ordinal));
        }
    }

    private string ToRootfs(string guestPath) => Path.Combine(_options.Rootfs, guestPath.TrimStart('/'));

    /// <summary>The stand-ins to bind: for the hidden <c>/proc</c> files the environment has one for.</summary>
    private List<string> FindProcStandIns() =>
        HiddenProcFiles
            .Where(file => File.Exists(Path.Combine(_options.Rootfs, ProcStandInFolder, file)) && !IsReadable($"/proc/{file}"))
            .ToList();

    private static bool IsReadable(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Whether a file or a link exists; a link may be absolute, pointing into the environment.</summary>
    private static bool Exists(string path) => File.Exists(path) || new FileInfo(path).LinkTarget is not null;

    private static string Normalize(string folder) => Path.GetFullPath(folder).TrimEnd('/');
}
