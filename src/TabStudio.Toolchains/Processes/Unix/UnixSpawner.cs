using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace TabStudio.Toolchains.Processes.Unix;

/// <summary>
/// Starts processes on Linux, Android and macOS with <c>posix_spawn</c>, with their standard streams on pipes or
/// on a new pseudo terminal.
/// </summary>
/// <remarks>
/// <para>
/// Every process starts with default signal handlers and no blocked signals, whatever the .NET runtime set for its
/// own process (it ignores <c>SIGPIPE</c>, for example). A process on pipes starts its own process group; a
/// process in a terminal starts its own session and takes the terminal as its controlling terminal, so the
/// terminal's Ctrl+C and window size reach it, as in any terminal emulator.
/// </para>
/// <para>
/// The child process inherits no descriptor of TabStudio's but its three standard streams: .NET opens files with
/// <c>O_CLOEXEC</c>, and so does this class.
/// </para>
/// </remarks>
internal static class UnixSpawner
{
    private static readonly Lazy<bool> ChangeDirectorySupported = new(() =>
    {
        UnixNative.EnsureLoaded();
        unsafe
        {
            return UnixNative.FindAddChdir() is not null;
        }
    });

    /// <summary>
    /// Whether processes can start in a folder of their own: <c>posix_spawn_file_actions_addchdir_np</c> exists on
    /// glibc 2.29 and later, macOS 10.15 and later and Android 14 and later.
    /// </summary>
    public static bool CanChangeDirectory => ChangeDirectorySupported.Value;

    /// <summary>Starts a process.</summary>
    /// <param name="path">The absolute path of the program to execute.</param>
    /// <param name="arguments">The full argument vector, starting with the program's name.</param>
    /// <param name="environment">The complete environment, as <c>NAME=value</c> strings.</param>
    /// <param name="workingDirectory">The folder to start in, or <see langword="null"/> to keep TabStudio's.</param>
    /// <param name="terminal">The size of a new pseudo terminal, or <see langword="null"/> for pipes.</param>
    /// <exception cref="ToolchainRuntimeException">The program could not be started.</exception>
    public static IProcessHandle Start(string path, IReadOnlyList<string> arguments, IReadOnlyList<string> environment, string? workingDirectory, TerminalSize? terminal)
    {
        UnixNative.EnsureLoaded();
        return terminal is { } size
            ? StartInTerminal(path, arguments, environment, workingDirectory, size)
            : StartWithPipes(path, arguments, environment, workingDirectory);
    }

    private static UnixProcessHandle StartWithPipes(string path, IReadOnlyList<string> arguments, IReadOnlyList<string> environment, string? workingDirectory)
    {
        var input = CreatePipe();
        var output = CreatePipe();
        var error = CreatePipe();
        int pid;
        try
        {
            pid = Spawn(path, arguments, environment, workingDirectory, UnixConstants.SpawnSetProcessGroup, actions =>
            {
                Check(UnixNative.posix_spawn_file_actions_adddup2(actions, input.Read, 0), "posix_spawn_file_actions_adddup2");
                Check(UnixNative.posix_spawn_file_actions_adddup2(actions, output.Write, 1), "posix_spawn_file_actions_adddup2");
                Check(UnixNative.posix_spawn_file_actions_adddup2(actions, error.Write, 2), "posix_spawn_file_actions_adddup2");
            });
        }
        catch
        {
            CloseAll(input, output, error);
            throw;
        }

        // The child has its own copies; keep only the parent's ends.
        _ = UnixNative.close(input.Read);
        _ = UnixNative.close(output.Write);
        _ = UnixNative.close(error.Write);
        return new UnixProcessHandle(
            pid,
            terminalFd: -1,
            new FileDescriptorStream(input.Write, canRead: false),
            new FileDescriptorStream(output.Read, canRead: true),
            new FileDescriptorStream(error.Read, canRead: true));
    }

    private static UnixProcessHandle StartInTerminal(string path, IReadOnlyList<string> arguments, IReadOnlyList<string> environment, string? workingDirectory, TerminalSize size)
    {
        var master = UnixNative.posix_openpt(UnixConstants.OpenReadWrite | UnixConstants.OpenNoControllingTerminal | UnixConstants.OpenCloseOnExec);
        if (master < 0)
        {
            throw Failure("Could not create a pseudo terminal", Marshal.GetLastPInvokeError());
        }

        try
        {
            SetCloseOnExec(master);
            if (UnixNative.grantpt(master) != 0 || UnixNative.unlockpt(master) != 0)
            {
                throw Failure("Could not set up the pseudo terminal", Marshal.GetLastPInvokeError());
            }

            Span<byte> name = stackalloc byte[128];
            if (UnixNative.ptsname_r(master, name, (nuint)name.Length) != 0)
            {
                throw Failure("Could not name the pseudo terminal", Marshal.GetLastPInvokeError());
            }

            var slave = Encoding.UTF8.GetString(name[..name.IndexOf((byte)0)]);
            SetWindowSize(master, size);

            var slavePath = Marshal.StringToCoTaskMemUTF8(slave);
            int pid;
            try
            {
                // After setsid, opening the terminal without O_NOCTTY makes it the controlling terminal.
                pid = Spawn(path, arguments, environment, workingDirectory, UnixConstants.SpawnSetSession, actions =>
                {
                    Check(UnixNative.posix_spawn_file_actions_addopen(actions, 0, slavePath, UnixConstants.OpenReadWrite, 0), "posix_spawn_file_actions_addopen");
                    Check(UnixNative.posix_spawn_file_actions_adddup2(actions, 0, 1), "posix_spawn_file_actions_adddup2");
                    Check(UnixNative.posix_spawn_file_actions_adddup2(actions, 0, 2), "posix_spawn_file_actions_adddup2");
                });
            }
            finally
            {
                Marshal.FreeCoTaskMem(slavePath);
            }

            // Reads and writes go through separate descriptors, so a blocked read never holds up a write.
            var writer = Duplicate(master);
            return new UnixProcessHandle(
                pid,
                master,
                new FileDescriptorStream(writer, canRead: false),
                new FileDescriptorStream(master, canRead: true),
                error: null);
        }
        catch
        {
            _ = UnixNative.close(master);
            throw;
        }
    }

    /// <summary>Changes a pseudo terminal's size; the kernel then sends <c>SIGWINCH</c> to its foreground processes.</summary>
    public static void SetWindowSize(int master, TerminalSize size)
    {
        var winsize = new UnixNative.WindowSize { Rows = (ushort)size.Rows, Columns = (ushort)size.Columns };
        _ = UnixNative.ioctl_winsize(master, UnixConstants.SetWindowSize, ref winsize);
    }

    private static unsafe int Spawn(
        string path,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> environment,
        string? workingDirectory,
        short flags,
        Action<nint> addFileActions)
    {
        // posix_spawn_file_actions_t and posix_spawnattr_t are opaque: a pointer on bionic, structures of up to a
        // few hundred bytes on glibc and macOS. 1 KB holds any of them; sigset_t is at most 128 bytes.
        var actions = Marshal.AllocHGlobal(1024);
        var attributes = Marshal.AllocHGlobal(1024);
        var signals = Marshal.AllocHGlobal(256);
        var strings = new List<nint>();
        try
        {
            Check(UnixNative.posix_spawn_file_actions_init(actions), "posix_spawn_file_actions_init");
            Check(UnixNative.posix_spawnattr_init(attributes), "posix_spawnattr_init");
            try
            {
                addFileActions(actions);
                if (workingDirectory is not null)
                {
                    AddChangeDirectory(actions, workingDirectory, strings);
                }

                Check(UnixNative.posix_spawnattr_setflags(attributes, (short)(flags | UnixConstants.SpawnSetSignalDefaults | UnixConstants.SpawnSetSignalMask)), "posix_spawnattr_setflags");
                if ((flags & UnixConstants.SpawnSetProcessGroup) != 0)
                {
                    Check(UnixNative.posix_spawnattr_setpgroup(attributes, 0), "posix_spawnattr_setpgroup");
                }

                // Every standard signal back to its default action, except SIGKILL and SIGSTOP, whose action cannot
                // change: bionic's posix_spawn fails the child (exit 127) when one of those is in the set.
                _ = UnixNative.sigemptyset(signals);
                for (var signal = 1; signal < 32; signal++)
                {
                    if (signal != UnixConstants.SignalKill && signal != UnixConstants.SignalStop)
                    {
                        _ = UnixNative.sigaddset(signals, signal);
                    }
                }

                Check(UnixNative.posix_spawnattr_setsigdefault(attributes, signals), "posix_spawnattr_setsigdefault");
                _ = UnixNative.sigemptyset(signals);
                Check(UnixNative.posix_spawnattr_setsigmask(attributes, signals), "posix_spawnattr_setsigmask");

                var argv = stackalloc nint[arguments.Count + 1];
                for (var i = 0; i < arguments.Count; i++)
                {
                    argv[i] = Utf8(arguments[i], strings);
                }

                argv[arguments.Count] = 0;
                var envp = stackalloc nint[environment.Count + 1];
                for (var i = 0; i < environment.Count; i++)
                {
                    envp[i] = Utf8(environment[i], strings);
                }

                envp[environment.Count] = 0;
                var program = Utf8(path, strings);
                var result = UnixNative.posix_spawn(out var pid, program, actions, attributes, argv, envp);
                if (result != 0)
                {
                    throw Failure($"Could not start {path}", result);
                }

                UnixNative.TraceProcess("spawn", pid);
                return pid;
            }
            finally
            {
                _ = UnixNative.posix_spawn_file_actions_destroy(actions);
                _ = UnixNative.posix_spawnattr_destroy(attributes);
            }
        }
        finally
        {
            foreach (var pointer in strings)
            {
                Marshal.FreeCoTaskMem(pointer);
            }

            Marshal.FreeHGlobal(actions);
            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(signals);
        }
    }

    private static unsafe void AddChangeDirectory(nint actions, string workingDirectory, List<nint> strings)
    {
        var addChdir = UnixNative.FindAddChdir();
        if (addChdir is null)
        {
            throw new ToolchainRuntimeException(
                "This version of the system cannot start a program in another folder. Start it through a shell that changes folder first.");
        }

        Check(addChdir(actions, Utf8(workingDirectory, strings)), "posix_spawn_file_actions_addchdir_np");
    }

    private static nint Utf8(string value, List<nint> strings)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(value);
        strings.Add(pointer);
        return pointer;
    }

    private static (int Read, int Write) CreatePipe()
    {
        Span<int> fds = stackalloc int[2];
        var result = UnixConstants.OpenCloseOnExec != 0
            ? UnixNative.pipe2(fds, UnixConstants.OpenCloseOnExec)
            : UnixNative.pipe(fds);
        if (result != 0)
        {
            throw Failure("Could not create a pipe", Marshal.GetLastPInvokeError());
        }

        SetCloseOnExec(fds[0]);
        SetCloseOnExec(fds[1]);
        return (fds[0], fds[1]);
    }

    private static int Duplicate(int fd)
    {
        // F_DUPFD_CLOEXEC is 1030 on Linux and 67 on macOS.
        var duplicate = UnixNative.fcntl_int(fd, OperatingSystem.IsMacOS() ? 67 : 1030, 3);
        if (duplicate < 0)
        {
            throw Failure("Could not duplicate the pseudo terminal", Marshal.GetLastPInvokeError());
        }

        return duplicate;
    }

    private static void SetCloseOnExec(int fd) =>
        _ = UnixNative.fcntl_int(fd, UnixConstants.SetDescriptorFlags, UnixConstants.CloseOnExecFlag);

    private static void CloseAll(params (int Read, int Write)[] pipes)
    {
        foreach (var (read, write) in pipes)
        {
            _ = UnixNative.close(read);
            _ = UnixNative.close(write);
        }
    }

    private static void Check(int result, string function)
    {
        if (result != 0)
        {
            throw Failure($"{function} failed", result);
        }
    }

    private static ToolchainRuntimeException Failure(string message, int error)
    {
        var reason = error switch
        {
            UnixConstants.ErrorNoEntry => "the program does not exist",
            UnixConstants.ErrorAccess => "the system does not allow executing it",
            UnixConstants.ErrorNotExecutable => "it is not a program for this device",
            _ => new Win32Exception(error).Message,
        };
        return new ToolchainRuntimeException($"{message}: {reason} (errno {error}).", error);
    }
}
