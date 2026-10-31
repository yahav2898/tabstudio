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

    /// <summary>The interactive shell of this distribution.</summary>
    public string Shell { get; init; } = "/bin/ash";

    /// <summary>The distribution's command search path.</summary>
    public string SearchPath { get; init; } = ProotRuntime.SearchPath;

    /// <summary>Folders of the device visible to programs at the same path, such as TabStudio's projects folder.</summary>
    public IReadOnlyList<string> SharedFolders { get; init; } = [];

    /// <summary>
    /// PRoot options added before the root file system, such as a container's <c>--net-map</c> rules or
    /// <c>-q</c> for an emulator.
    /// </summary>
    public IReadOnlyList<string> ExtraArguments { get; init; } = [];

    /// <summary>
    /// Whether a working directory that exists on the device is shared into the environment at its own path. A
    /// container's working directory is a path in its own file system, so containers turn this off.
    /// </summary>
    public bool ShareWorkingDirectory { get; init; } = true;

    /// <summary>
    /// The root file system that holds the stand-ins for the parts of <c>/proc</c> Android hides
    /// (<see cref="ProotRuntime.ProcStandInFolder"/>); <see langword="null"/> for <see cref="Rootfs"/> itself.
    /// </summary>
    public string? ProcStandInRoot { get; init; }

    /// <summary>
    /// Whether the device's <c>/sys</c> is visible. A container keeps it, as Docker does, read through PRoot.
    /// </summary>
    public bool BindSys { get; init; } = true;
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
public sealed partial class ProotRuntime : IToolchainRuntime
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
        return new ToolchainProcess(UnixSpawner.Start(_options.ProotPath, arguments, environment, workingDirectory: null, spec.Terminal, spec.InheritedDescriptors), spec);
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

        return _options.SearchPath.Split(':')
            .Where(folder => Exists(Path.Combine(_options.Rootfs, folder.TrimStart('/'), command)))
            .Select(folder => folder + "/" + command)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => new()
    {
        Program = _options.Shell,
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
        var javaShim = JavaMappingShim(spec);
        if (javaShim is not null)
        {
            ShareFolder(Path.GetDirectoryName(javaShim)!);
        }

        var arguments = new List<string>
        {
            _options.ProotPath,
            "--kill-on-exit",
            "--link2symlink",
            "--sysvipc",
            "-L",
        };
        arguments.AddRange(_options.ExtraArguments);
        arguments.AddRange(
        [
            "-r", _options.Rootfs,
            "-b", "/dev",
            "-b", "/proc",
            "-b", "/dev/urandom:/dev/random",
            "-b", "/proc/self/fd:/dev/fd",
            "-b", "/proc/self/fd/0:/dev/stdin",
            "-b", "/proc/self/fd/1:/dev/stdout",
            "-b", "/proc/self/fd/2:/dev/stderr",
            // Android has no /dev/shm; POSIX shared memory and semaphores (Python's multiprocessing) need one.
            "-b", $"{Path.Combine(_options.Rootfs, "tmp")}:/dev/shm",
        ]);
        if (_options.BindSys)
        {
            arguments.AddRange(["-b", "/sys"]);
        }

        if (spec.UserId is { } uid)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(uid);
            var gid = spec.GroupId ?? uid;
            ArgumentOutOfRangeException.ThrowIfNegative(gid);
            arguments.AddRange(["-i", $"{uid}:{gid}"]);
        }
        else
        {
            arguments.Add("-0");
        }

        foreach (var file in _procStandIns.Value)
        {
            arguments.Add("-b");
            arguments.Add($"{Path.Combine(_options.ProcStandInRoot ?? _options.Rootfs, ProcStandInFolder, file)}:/proc/{file}");
        }

        if (_options.ShareWorkingDirectory && spec.WorkingDirectory is { } folder && Directory.Exists(folder) && !folder.StartsWith(Normalize(_options.Rootfs) + "/", StringComparison.Ordinal))
        {
            ShareFolder(folder);
        }

        List<string> shared;
        lock (_gate)
        {
            shared = [.. _sharedFolders];
        }

        foreach (var path in shared)
        {
            if (!Directory.Exists(path))
            {
                // A session's folder that is gone, such as a debug adapter's socket folder.
                RemoveMountPoint(path);
                continue;
            }

            EnsureMountPoint(path);
            arguments.Add("-b");
            arguments.Add(path);
        }

        foreach (var mount in spec.Mounts)
        {
            if (!Path.IsPathFullyQualified(mount.Source) || !mount.Target.StartsWith('/')
                || mount.Source.Contains(':') || mount.Target.Contains(':') || mount.Target.Split('/').Contains(".."))
            {
                throw new ArgumentException("Process mounts need absolute paths without ':' or '..'.", nameof(spec));
            }

            // A service's data folder gets a lasting mount point too, so no other PRoot's exit removes it.
            if (Directory.Exists(mount.Source))
            {
                EnsureMountPoint(mount.Target);
            }

            arguments.AddRange(["-b", $"{mount.Source}:{mount.Target}"]);
        }

        arguments.Add("-w");
        arguments.Add(GuestWorkingDirectory(spec.WorkingDirectory));
        var guestPreload = spec.Environment.TryGetValue("LD_PRELOAD", out var explicitPreload) ? explicitPreload : javaShim;
        if (guestPreload is not null)
        {
            // Set this after entering Alpine: Android's bionic linker cannot load a musl preload into PRoot.
            arguments.Add("/usr/bin/env");
            arguments.Add("LD_PRELOAD=" + guestPreload);
        }

        arguments.Add(spec.Program);
        arguments.AddRange(spec.Arguments);
        return arguments;
    }

    /// <summary>The environment programs start with: the environment's own, then the spec's variables.</summary>
    internal List<string> BuildEnvironment(ProcessStartSpec spec)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = _options.SearchPath,
            ["HOME"] = "/root",
            ["USER"] = "root",
            ["LOGNAME"] = "root",
            ["SHELL"] = _options.Shell,
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
            // A guest's libusb/serial/JVM shim must never be loaded into the Android Bionic PRoot executable.
            // BuildArguments adds it through the guest's env program, after the Linux loader takes over.
            if (name == "LD_PRELOAD") continue;
            if (value is null)
            {
                environment.Remove(name);
            }
            else
            {
                environment[name] = name == "PATH" ? value.Replace("$PATH", _options.SearchPath, StringComparison.Ordinal) : value;
            }
        }

        Directory.CreateDirectory(_options.TemporaryDirectory);
        return [.. environment.Select(e => $"{e.Key}={e.Value}")];
    }

    /// <summary>
    /// Creates a shared folder's own path inside the root file system, so that PRoot binds over a directory that
    /// already exists.
    /// </summary>
    /// <remarks>
    /// When a binding's guest path is missing, PRoot creates each missing component in the root file system as a
    /// placeholder and removes the empty placeholders when that PRoot process exits. Every program shares one root
    /// file system, so the first PRoot to bind <c>/data/…/Projects</c> owned <c>rootfs/data</c> and its children,
    /// and its exit (a language server or debugger stopping with a closed workspace) deleted them under programs
    /// still running: their working directory vanished, and <c>rustc</c> started by a running <c>cargo build</c>
    /// failed with "Current directory is invalid". A path that exists before PRoot starts is never a placeholder,
    /// so no PRoot removes it.
    /// </remarks>
    private void EnsureMountPoint(string shared)
    {
        if (!Directory.Exists(_options.Rootfs))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(ToRootfs(shared));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // PRoot then glues the path itself, as it did before.
        }
    }

    /// <summary>Removes the empty mount point of a shared folder that no longer exists on the device.</summary>
    private void RemoveMountPoint(string shared)
    {
        var mountPoint = ToRootfs(shared);
        try
        {
            if (Directory.Exists(mountPoint) && !Directory.EnumerateFileSystemEntries(mountPoint).Any())
            {
                Directory.Delete(mountPoint);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not empty, or already removed: an empty folder is all that is left either way.
        }
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
            .Where(file => File.Exists(Path.Combine(_options.ProcStandInRoot ?? _options.Rootfs, ProcStandInFolder, file)) && !IsReadable($"/proc/{file}"))
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
