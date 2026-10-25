using TabStudio.LanguageServers.Features;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>Where a language server is in its life.</summary>
public enum LanguageServerState
{
    /// <summary>Starting: the program runs and answers <c>initialize</c>.</summary>
    Starting,

    /// <summary>Running and ready for requests.</summary>
    Running,

    /// <summary>It stopped unexpectedly and is being started again.</summary>
    Restarting,

    /// <summary>It could not be started, or stopped too often to start again.</summary>
    Failed,

    /// <summary>Shut down, as when the workspace closed.</summary>
    Stopped,
}

/// <summary>How important a log line is.</summary>
public enum LanguageServerLogLevel
{
    /// <summary>Something failed.</summary>
    Error,

    /// <summary>Something may be wrong.</summary>
    Warning,

    /// <summary>Something worth knowing, such as a server starting.</summary>
    Info,

    /// <summary>The server's own log, and its standard error.</summary>
    Log,

    /// <summary>The message trace, when it is turned on.</summary>
    Trace,
}

/// <summary>The kind of a message a server shows to the user.</summary>
public enum LanguageServerMessageType
{
    /// <summary>An error.</summary>
    Error,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>Information.</summary>
    Info,

    /// <summary>A log message.</summary>
    Log,
}

/// <summary>One running language server, for the status bar.</summary>
/// <param name="Id">The definition's identifier, such as <c>pyright</c>.</param>
/// <param name="Name">Its name, such as <c>Pyright</c>.</param>
/// <param name="RootDirectory">The folder it serves.</param>
/// <param name="State">Where it is in its life.</param>
/// <param name="Message">Why it failed, or what it is busy with, if anything.</param>
/// <param name="Version">The version the server reported, if any.</param>
public sealed record LanguageServerStatus(string Id, string Name, string RootDirectory, LanguageServerState State, string? Message, string? Version);

/// <summary>A line for the language servers' log.</summary>
/// <param name="serverName">The server it is about, such as <c>Pyright</c>, or <see langword="null"/> for the host itself.</param>
/// <param name="level">How important it is.</param>
/// <param name="message">The text.</param>
public sealed class LanguageServerLogEventArgs(string? serverName, LanguageServerLogLevel level, string message) : EventArgs
{
    /// <summary>The server it is about, or <see langword="null"/> for the host itself.</summary>
    public string? ServerName { get; } = serverName;

    /// <summary>How important it is.</summary>
    public LanguageServerLogLevel Level { get; } = level;

    /// <summary>The text.</summary>
    public string Message { get; } = message;
}

/// <summary>New diagnostics for a file, which replace the server's previous ones for it.</summary>
/// <param name="filePath">Full path of the file.</param>
/// <param name="diagnostics">Its diagnostics; empty when the problems are gone.</param>
/// <param name="serverName">The server that found them.</param>
/// <param name="rootDirectory">The folder that server serves.</param>
public sealed class LanguageServerDiagnosticsEventArgs(string filePath, IReadOnlyList<LspDiagnostic> diagnostics, string serverName, string rootDirectory) : EventArgs
{
    /// <summary>Full path of the file.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>Its diagnostics; empty when the problems are gone.</summary>
    public IReadOnlyList<LspDiagnostic> Diagnostics { get; } = diagnostics;

    /// <summary>The server that found them.</summary>
    public string ServerName { get; } = serverName;

    /// <summary>The folder that server serves.</summary>
    public string RootDirectory { get; } = rootDirectory;
}

/// <summary>A message a server shows to the user with <c>window/showMessage</c>.</summary>
/// <param name="serverName">The server.</param>
/// <param name="type">Its kind.</param>
/// <param name="message">The text.</param>
public sealed class LanguageServerMessageEventArgs(string serverName, LanguageServerMessageType type, string message) : EventArgs
{
    /// <summary>The server.</summary>
    public string ServerName { get; } = serverName;

    /// <summary>Its kind.</summary>
    public LanguageServerMessageType Type { get; } = type;

    /// <summary>The text.</summary>
    public string Message { get; } = message;
}

/// <summary>Work a server reports progress on, such as indexing.</summary>
/// <param name="serverName">The server.</param>
/// <param name="title">What it is doing, such as <c>Indexing</c>, if it says.</param>
/// <param name="message">More about it, such as the file it is on, if any.</param>
/// <param name="percentage">How far along it is, 0 to 100, if it knows.</param>
/// <param name="isDone">Whether the work has finished.</param>
public sealed class LanguageServerProgressEventArgs(string serverName, string? title, string? message, int? percentage, bool isDone) : EventArgs
{
    /// <summary>The server.</summary>
    public string ServerName { get; } = serverName;

    /// <summary>What it is doing, if it says.</summary>
    public string? Title { get; } = title;

    /// <summary>More about it, if any.</summary>
    public string? Message { get; } = message;

    /// <summary>How far along it is, 0 to 100, if it knows.</summary>
    public int? Percentage { get; } = percentage;

    /// <summary>Whether the work has finished.</summary>
    public bool IsDone { get; } = isDone;
}

/// <summary>A question a server asks with <c>window/showMessageRequest</c>, answered with one of its actions.</summary>
/// <param name="ServerName">The server.</param>
/// <param name="Type">Its kind.</param>
/// <param name="Message">The question.</param>
/// <param name="Actions">The answers to choose from; none means the message only needs to be seen.</param>
public sealed record LanguageServerMessageRequest(string ServerName, LanguageServerMessageType Type, string Message, IReadOnlyList<string> Actions);
