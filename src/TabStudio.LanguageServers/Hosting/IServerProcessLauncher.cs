namespace TabStudio.LanguageServers.Hosting;

/// <summary>What to start: a program, its arguments, the folder it runs in and its environment.</summary>
/// <param name="FileName">The program: a full path, as <see cref="IServerProcessLauncher.Resolve"/> returns it.</param>
/// <param name="Arguments">The arguments, each passed as one argument.</param>
/// <param name="WorkingDirectory">The folder it starts in.</param>
/// <param name="Environment">Variables to set on top of TabStudio's own environment; a <see langword="null"/> value removes one.</param>
public sealed record ServerStartInfo(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment);

/// <summary>
/// Starts the programs that language servers and debug adapters are, and finds where they are installed. This is
/// the only place TabStudio's language support runs a program, so each platform decides how: the desktop with
/// <see cref="ProcessServerLauncher"/>, Android with the Toolchain Store's process runtime.
/// </summary>
public interface IServerProcessLauncher
{
    /// <summary>Finds the program a command names.</summary>
    /// <param name="command">A program name, such as <c>pyright-langserver</c>, or a full path.</param>
    /// <returns>The program's full path, or <see langword="null"/> when it is not installed.</returns>
    string? Resolve(string command);

    /// <summary>Starts a program with its standard streams redirected.</summary>
    /// <param name="startInfo">What to start.</param>
    /// <returns>The running program.</returns>
    /// <exception cref="ServerStartException">The program could not be started.</exception>
    IServerProcess Start(ServerStartInfo startInfo);
}

/// <summary>A program started by an <see cref="IServerProcessLauncher"/>, with its standard streams.</summary>
public interface IServerProcess : IDisposable
{
    /// <summary>The program's standard input, written by TabStudio.</summary>
    Stream StandardInput { get; }

    /// <summary>The program's standard output, read by TabStudio.</summary>
    Stream StandardOutput { get; }

    /// <summary>The program's standard error, read by TabStudio for the log.</summary>
    Stream StandardError { get; }

    /// <summary>Completes with the exit code when the program has exited.</summary>
    Task<int> Exited { get; }

    /// <summary>Ends the program at once, without waiting for it to finish.</summary>
    void Kill();
}

/// <summary>A language server or debug adapter could not be started.</summary>
public sealed class ServerStartException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ServerStartException()
        : base("The program could not be started.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, for the user.</param>
    public ServerStartException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, for the user.</param>
    /// <param name="innerException">The cause.</param>
    public ServerStartException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
