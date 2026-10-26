using System.ComponentModel;
using System.Runtime.InteropServices;
using TabStudio.Toolchains.Processes.Unix;

namespace TabStudio.Toolchains.Processes;

/// <summary>Creates a private Unix FIFO for a debuggee's input, separate from its adapter's protocol stream.</summary>
public static class UnixNamedPipe
{
    /// <summary>Creates a FIFO readable and writable only by the app's user.</summary>
    /// <param name="path">An absolute path in an existing private directory.</param>
    /// <exception cref="IOException">The FIFO could not be created.</exception>
    /// <exception cref="PlatformNotSupportedException">The host is Windows.</exception>
    public static void Create(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unix FIFOs are not available on Windows.");
        }

        UnixNative.EnsureLoaded();
        // Bionic uses mknodat, which Android allows. Alpine's legacy mknod syscall is blocked on x86_64.
        if (UnixNative.mkfifo(path, 0x180) != 0)
        {
            throw new IOException("Could not create the debugger input pipe.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }
}
