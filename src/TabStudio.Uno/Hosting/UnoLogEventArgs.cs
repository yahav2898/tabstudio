namespace TabStudio.Uno.Hosting;

/// <summary>How serious a message from Uno Platform is.</summary>
public enum UnoLogLevel
{
    /// <summary>Worth knowing.</summary>
    Information,

    /// <summary>Something did not work as the app meant.</summary>
    Warning,

    /// <summary>Something failed.</summary>
    Error,
}

/// <summary>A message from Uno Platform, for the Output panel.</summary>
/// <param name="level">How serious it is.</param>
/// <param name="message">What happened.</param>
/// <param name="exception">The exception behind it, if any.</param>
public sealed class UnoLogEventArgs(UnoLogLevel level, string message, Exception? exception) : EventArgs
{
    /// <summary>How serious it is.</summary>
    public UnoLogLevel Level { get; } = level;

    /// <summary>What happened.</summary>
    public string Message { get; } = message;

    /// <summary>The exception behind it, if any.</summary>
    public Exception? Exception { get; } = exception;
}
