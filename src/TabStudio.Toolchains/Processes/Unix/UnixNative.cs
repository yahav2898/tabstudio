using System.Reflection;
using System.Runtime.InteropServices;

namespace TabStudio.Toolchains.Processes.Unix;

/// <summary>
/// The C library functions that start processes, create pseudo terminals and wait for and signal processes on
/// Linux, Android and macOS.
/// </summary>
/// <remarks>
/// <para>
/// Processes are started with <c>posix_spawn</c>, which runs no managed code in the child: forking a .NET process
/// and running managed code before <c>exec</c> is not safe. The constants differ between Linux (glibc and Android's
/// bionic agree) and macOS; <see cref="UnixConstants"/> holds both.
/// </para>
/// <para>
/// The library is named <c>libc</c> here and resolved per platform: <c>libc.so</c> on Android, <c>libc.so.6</c> on
/// glibc Linux and <c>libSystem</c> on macOS.
/// </para>
/// </remarks>
internal static partial class UnixNative
{
    private const string LibC = "libc";

    static UnixNative() => NativeLibrary.SetDllImportResolver(typeof(UnixNative).Assembly, Resolve);

    /// <summary>Makes sure the resolver is registered before the first call.</summary>
    internal static void EnsureLoaded()
    {
        // The static constructor does the work.
    }

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int posix_openpt(int flags);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int grantpt(int fd);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int unlockpt(int fd);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int ptsname_r(int fd, Span<byte> buffer, nuint length);

    [LibraryImport(LibC, EntryPoint = "ioctl", SetLastError = true)]
    internal static partial int ioctl_winsize(int fd, nuint request, ref WindowSize size);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int pipe(Span<int> fds);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int pipe2(Span<int> fds, int flags);

    [LibraryImport(LibC, SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int mkfifo(string path, uint mode);

    [LibraryImport(LibC, EntryPoint = "fcntl", SetLastError = true)]
    internal static partial int fcntl_int(int fd, int command, int argument);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int close(int fd);

    [LibraryImport(LibC, SetLastError = true)]
    internal static unsafe partial nint read(int fd, byte* buffer, nuint count);

    [LibraryImport(LibC, SetLastError = true)]
    internal static unsafe partial nint write(int fd, byte* buffer, nuint count);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int waitpid(int pid, out int status, int options);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int kill(int pid, int signal);

    [LibraryImport(LibC)]
    internal static partial int posix_spawn_file_actions_init(nint actions);

    [LibraryImport(LibC)]
    internal static partial int posix_spawn_file_actions_destroy(nint actions);

    [LibraryImport(LibC)]
    internal static partial int posix_spawn_file_actions_addopen(nint actions, int fd, nint path, int flags, uint mode);

    [LibraryImport(LibC)]
    internal static partial int posix_spawn_file_actions_adddup2(nint actions, int fd, int newFd);

    [LibraryImport(LibC)]
    internal static partial int posix_spawnattr_init(nint attributes);

    [LibraryImport(LibC)]
    internal static partial int posix_spawnattr_destroy(nint attributes);

    [LibraryImport(LibC)]
    internal static partial int posix_spawnattr_setflags(nint attributes, short flags);

    [LibraryImport(LibC)]
    internal static partial int posix_spawnattr_setpgroup(nint attributes, int processGroup);

    [LibraryImport(LibC)]
    internal static partial int posix_spawnattr_setsigdefault(nint attributes, nint signals);

    [LibraryImport(LibC)]
    internal static partial int posix_spawnattr_setsigmask(nint attributes, nint signals);

    [LibraryImport(LibC)]
    internal static partial int sigemptyset(nint set);

    [LibraryImport(LibC)]
    internal static partial int sigaddset(nint set, int signal);

    /// <summary>Starts a program; returns 0, or the error number.</summary>
    /// <remarks>
    /// <para>
    /// The call stays in the runtime's cooperative mode (<see cref="SuppressGCTransitionAttribute"/>): a collection
    /// that starts meanwhile waits until it returns, and never signals the thread while it forks. On Android, bionic's
    /// <c>fork</c> sets the calling thread's id to -1 for as long as the kernel copies the process (milliseconds for
    /// an app of TabStudio's size), so <c>pthread_kill</c> of that thread fails with <c>ESRCH</c>. Mono's collector
    /// sends that signal to suspend a thread that is in native code, takes <c>ESRCH</c> for a thread that has
    /// exited, leaves the thread out of the collection and marks it as running managed code; when the call returns,
    /// the thread finds itself running where it should be leaving native code and Mono aborts the app: "Cannot
    /// transition thread ... from RUNNING with DONE_BLOCKING".
    /// </para>
    /// <para>
    /// Staying cooperative is safe here: bionic's <c>posix_spawn</c> only forks and returns (the child does the rest),
    /// calls no managed code and takes no lock a suspended thread could hold, as fork handlers only wait for
    /// threads still running native code. glibc and macOS start the child without copying the process.
    /// </para>
    /// </remarks>
    [LibraryImport(LibC)]
    [SuppressGCTransition]
    internal static unsafe partial int posix_spawn(out int pid, nint path, nint actions, nint attributes, nint* argv, nint* envp);

    /// <summary>The size of a terminal, as <c>TIOCSWINSZ</c> takes it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowSize
    {
        public ushort Rows;
        public ushort Columns;
        public ushort PixelWidth;
        public ushort PixelHeight;
    }

    /// <summary>
    /// Finds <c>posix_spawn_file_actions_addchdir_np</c>, which glibc 2.29, macOS 10.15 and Android 14 have.
    /// </summary>
    internal static unsafe delegate* unmanaged<nint, nint, int> FindAddChdir()
    {
        var library = NativeLibrary.Load(LibC, typeof(UnixNative).Assembly, null);
        return NativeLibrary.TryGetExport(library, "posix_spawn_file_actions_addchdir_np", out var address)
            ? (delegate* unmanaged<nint, nint, int>)address
            : null;
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != LibC)
        {
            return 0;
        }

        string[] candidates = OperatingSystem.IsAndroid() ? ["libc.so"]
            : OperatingSystem.IsMacOS() ? ["/usr/lib/libSystem.dylib", "libSystem.dylib"]
            : ["libc.so.6", "libc.so", "libc.musl-x86_64.so.1", "libc.musl-aarch64.so.1"];
        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        return 0;
    }
}
