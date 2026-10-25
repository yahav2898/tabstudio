using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Runtimes;

/// <summary>What kind of environment a runtime runs programs in.</summary>
public enum RuntimeKind
{
    /// <summary>The computer TabStudio runs on, with the toolchains installed on it: the desktop build.</summary>
    Host,

    /// <summary>TabStudio's own Linux environment, Alpine Linux through PRoot: Android.</summary>
    Linux,
}

/// <summary>
/// Where toolchain programs run: TabStudio's Linux environment on Android, or the computer itself on the
/// desktop. Every program the IDE starts for a toolchain (a run, a package install, a language server, the
/// Terminal's shell) goes through one.
/// </summary>
public interface IToolchainRuntime
{
    /// <summary>The environment's name for the UI, such as <c>Alpine Linux 3.24</c> or <c>This computer</c>.</summary>
    string DisplayName { get; }

    /// <summary>What kind of environment it is.</summary>
    RuntimeKind Kind { get; }

    /// <summary>Starts a program.</summary>
    /// <param name="spec">What to run.</param>
    /// <returns>The running program.</returns>
    /// <exception cref="ToolchainRuntimeException">It could not be started; the message says why.</exception>
    ToolchainProcess Start(ProcessStartSpec spec);

    /// <summary>Whether a command is installed, as <see cref="ProcessStartSpec.Program"/> would find it.</summary>
    /// <param name="command">A command name, such as <c>python3</c>.</param>
    bool HasCommand(string command);

    /// <summary>What the Terminal runs: the environment's interactive shell, in a folder.</summary>
    /// <param name="workingDirectory">The folder, usually the workspace's.</param>
    /// <param name="size">The terminal's size.</param>
    ProcessStartSpec Shell(string workingDirectory, TerminalSize size);

    /// <summary>
    /// Makes a folder visible to programs at the same path, such as a workspace kept outside TabStudio's own
    /// storage. Folders under TabStudio's storage are always visible.
    /// </summary>
    /// <param name="folder">The folder.</param>
    void ShareFolder(string folder);

    /// <summary>
    /// Turns a path a program printed, such as a file in a stack trace, into the path of that file on the device,
    /// or <see langword="null"/> when it is not a file the IDE can open.
    /// </summary>
    /// <param name="programPath">The path as the program printed it.</param>
    /// <param name="workingDirectory">The folder the program ran in, for relative paths.</param>
    string? ToDevicePath(string programPath, string? workingDirectory);
}
