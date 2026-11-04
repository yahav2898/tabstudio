using System.Runtime.InteropServices;

namespace TabStudio.Containers.Store;

/// <summary>The few C library file calls .NET has no API for: hard links, FIFOs and a link's own times.</summary>
internal static partial class UnixFiles
{
    private const int AtFdCwd = -100;
    private const int AtSymlinkNoFollow = 0x100;

    [LibraryImport("libc", EntryPoint = "link", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LinkNative(string existing, string created);

    [LibraryImport("libc", EntryPoint = "mkfifo", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int MakeFifoNative(string path, uint mode);

    [LibraryImport("libc", EntryPoint = "utimensat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int SetTimesNative(int directory, string path, Timespec[] times, int flags);

    // .NET's own native library: its FileStatus has the same layout on every Unix and architecture, Mode second.
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_LStat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LStatNative(string path, [Out] byte[] status);

    private const int FileTypeMask = 0xF000;
    private const int RegularFile = 0x8000;
    private const int DirectoryFile = 0x4000;
    private const int LinkFile = 0xA000;

    [LibraryImport("libc", EntryPoint = "setxattr", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int SetAttributeNative(string path, string name, byte[] value, nuint size, int flags);

    [LibraryImport("libc", EntryPoint = "getxattr", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial nint GetAttributeNative(string path, string name, [Out] byte[] value, nuint size);

    /// <summary>The extended attribute that keeps the mode an image gave a file TabStudio made read-only to share it.</summary>
    private const string SharedModeAttribute = "user.tabstudio.mode";

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int KillNative(int pid, int signal);

    /// <summary>Sends a signal to a process; false when it is gone or not ours.</summary>
    public static bool TrySignal(int pid, int signal) => !OperatingSystem.IsWindows() && pid > 0 && KillNative(pid, signal) == 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    /// <summary>Whether this device lets the app make hard links; Android does not, so layers copy instead.</summary>
    public static bool TryHardLink(string existing, string created) => OperatingSystem.IsWindows() is false && LinkNative(existing, created) == 0;

    /// <summary>Makes a FIFO; false where the platform refuses.</summary>
    public static bool TryMakeFifo(string path, UnixFileMode mode) => OperatingSystem.IsWindows() is false && MakeFifoNative(path, (uint)mode) == 0;

    /// <summary>
    /// Whether a path is a FIFO, socket or device rather than a file, folder or link. .NET reports a FIFO as an ordinary
    /// existing file, and opening one to copy it waits for a writer that never comes.
    /// </summary>
    public static bool IsSpecial(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        var status = new byte[256];
        try
        {
            if (LStatNative(path, status) != 0)
            {
                return false;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }

        var type = BitConverter.ToInt32(status, 4) & FileTypeMask;
        return type is not (RegularFile or DirectoryFile or LinkFile);
    }

    /// <summary>
    /// Records a file's own mode with it, as an extended attribute, before it is made read-only to be shared; false
    /// where the file system keeps no such attributes.
    /// </summary>
    public static bool TrySetSharedMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return SetAttributeNative(path, SharedModeAttribute, BitConverter.GetBytes((int)mode), 4, 0) == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>The mode <see cref="TrySetSharedMode"/> recorded for a file, or <see langword="null"/>.</summary>
    public static UnixFileMode? SharedMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var value = new byte[4];
        try
        {
            return GetAttributeNative(path, SharedModeAttribute, value, 4) == 4 ? (UnixFileMode)BitConverter.ToInt32(value) : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Sets a path's modification time without following a link.</summary>
    public static void TrySetModified(string path, DateTimeOffset time)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var seconds = time.ToUnixTimeSeconds();
        var stamp = new Timespec { Seconds = seconds, Nanoseconds = (time.UtcTicks % TimeSpan.TicksPerSecond) * 100 };
        _ = SetTimesNative(AtFdCwd, path, [stamp, stamp], AtSymlinkNoFollow);
    }
}
