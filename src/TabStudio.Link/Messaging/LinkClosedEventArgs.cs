namespace TabStudio.Link.Messaging;

/// <summary>Why a <see cref="LinkConnection"/> closed.</summary>
public sealed class LinkClosedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="error">Why the connection failed, or <see langword="null"/> when it was closed on purpose.</param>
    public LinkClosedEventArgs(Exception? error)
    {
        Error = error;
    }

    /// <summary>Why the connection failed, or <see langword="null"/> when either side closed it on purpose.</summary>
    public Exception? Error { get; }
}
