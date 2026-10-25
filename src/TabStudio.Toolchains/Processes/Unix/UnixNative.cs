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

    [LibraryImport(LibC)]
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
