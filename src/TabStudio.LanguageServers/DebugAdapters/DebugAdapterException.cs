namespace TabStudio.LanguageServers.DebugAdapters;

/// <summary>A debug adapter answered a request with an error, or the connection to it closed.</summary>
public sealed class DebugAdapterException : Exception
{
    /// <summary>Creates the exception.</summary>
    public DebugAdapterException()
        : base("The debug adapter could not do that.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The adapter's message, for the user.</param>
    public DebugAdapterException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, for the user.</param>
    /// <param name="innerException">The cause.</param>
    public DebugAdapterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a request the adapter refused, or one it never answered.</summary>
    /// <param name="command">The request.</param>
    /// <param name="message">The adapter's message, for the user.</param>
    /// <param name="isConnectionClosed">Whether the connection closed before the answer.</param>
    public DebugAdapterException(string command, string message, bool isConnectionClosed)
        : base(message)
    {
        Command = command;
        IsConnectionClosed = isConnectionClosed;
    }

    /// <summary>The request that failed, if known.</summary>
    public string? Command { get; }

    /// <summary>Whether the connection closed before the answer arrived.</summary>
    public bool IsConnectionClosed { get; }
}
