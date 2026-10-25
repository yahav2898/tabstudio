namespace TabStudio.Toolchains.Processes.Unix;

/// <summary>
/// The values of the C library constants TabStudio uses, which differ between Linux (glibc and Android's bionic
/// agree on all of them) and macOS.
/// </summary>
internal static class UnixConstants
{
    private static readonly bool IsMac = OperatingSystem.IsMacOS();

    /// <summary><c>O_RDWR</c>.</summary>
    public const int OpenReadWrite = 2;

    /// <summary><c>O_NOCTTY</c>: opening a terminal does not make it the controlling terminal.</summary>
    public static int OpenNoControllingTerminal => IsMac ? 0x20000 : 0x100;

    /// <summary><c>O_CLOEXEC</c>, or 0 where <c>posix_openpt</c> and <c>pipe2</c> do not take it (macOS).</summary>
    public static int OpenCloseOnExec => IsMac ? 0 : 0x80000;

    /// <summary><c>F_SETFD</c>.</summary>
    public const int SetDescriptorFlags = 2;

    /// <summary><c>FD_CLOEXEC</c>.</summary>
    public const int CloseOnExecFlag = 1;

    /// <summary><c>POSIX_SPAWN_SETPGROUP</c>.</summary>
    public const short SpawnSetProcessGroup = 0x02;

    /// <summary><c>POSIX_SPAWN_SETSIGDEF</c>.</summary>
    public const short SpawnSetSignalDefaults = 0x04;

    /// <summary><c>POSIX_SPAWN_SETSIGMASK</c>.</summary>
    public const short SpawnSetSignalMask = 0x08;

    /// <summary><c>POSIX_SPAWN_SETSID</c>: the child starts a new session, so it can have a controlling terminal.</summary>
    public static short SpawnSetSession => IsMac ? (short)0x400 : (short)0x80;

    /// <summary><c>TIOCSWINSZ</c>.</summary>
    public static nuint SetWindowSize => IsMac ? 0x80087467 : 0x5414;

    /// <summary><c>SIGKILL</c>.</summary>
    public const int SignalKill = 9;

    /// <summary><c>SIGSTOP</c>, whose action cannot be changed.</summary>
    public static int SignalStop => IsMac ? 17 : 19;

    /// <summary><c>SIGINT</c>.</summary>
    public const int SignalInterrupt = 2;

    /// <summary><c>SIGHUP</c>.</summary>
    public const int SignalHangUp = 1;

    /// <summary><c>EINTR</c>.</summary>
    public const int ErrorInterrupted = 4;

    /// <summary><c>EIO</c>: what reading a pseudo terminal's master returns once no process has the terminal open.</summary>
    public const int ErrorInputOutput = 5;

    /// <summary><c>ECHILD</c>.</summary>
    public const int ErrorNoChild = 10;

    /// <summary><c>ENOENT</c>.</summary>
    public const int ErrorNoEntry = 2;

    /// <summary><c>EACCES</c>.</summary>
    public const int ErrorAccess = 13;

    /// <summary><c>ENOEXEC</c>.</summary>
    public const int ErrorNotExecutable = 8;
}
