namespace TabStudio.LanguageServers.Hosting;

/// <summary>How a <see cref="LanguageServerHost"/> introduces itself to servers and looks after them.</summary>
public sealed record LanguageServerHostOptions
{
    /// <summary>The client name sent in <c>initialize</c>.</summary>
    public string ClientName { get; init; } = "TabStudio";

    /// <summary>The client version sent in <c>initialize</c>, if any.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>
    /// Reads the text of a file that is not open in a document, such as the target of Go to Definition, to turn
    /// the server's positions into offsets. Reads the file from disk by default.
    /// </summary>
    public Func<string, string?> ReadText { get; init; } = ReadFromDisk;

    /// <summary>How many times a server that stops unexpectedly is started again within <see cref="RestartWindow"/>.</summary>
    public int MaxRestarts { get; init; } = 3;

    /// <summary>The period <see cref="MaxRestarts"/> counts in.</summary>
    public TimeSpan RestartWindow { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>How long a server may take to answer <c>initialize</c>.</summary>
    public TimeSpan InitializeTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>How long a server may take to shut down before it is ended.</summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Whether every message is written to the log, as VS Code's <c>"trace.server": "messages"</c>.</summary>
    public bool TraceMessages { get; init; }

    private static string? ReadFromDisk(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
