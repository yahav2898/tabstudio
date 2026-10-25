namespace TabStudio.Link;

/// <summary>The other device answered a request with an error.</summary>
public sealed class LinkRemoteException : Exception
{
    /// <summary>Creates the exception.</summary>
    public LinkRemoteException()
        : this(LinkErrorCodes.Failed, "The other device could not answer.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public LinkRemoteException(string message)
        : this(LinkErrorCodes.Failed, message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public LinkRemoteException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = LinkErrorCodes.Failed;
    }

    /// <summary>Creates the exception with an error code, as handlers throw it to answer with that error.</summary>
    /// <param name="code">One of <see cref="LinkErrorCodes"/>.</param>
    /// <param name="message">What went wrong, in words for the user.</param>
    public LinkRemoteException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        Code = code;
    }

    /// <summary>The error code, one of <see cref="LinkErrorCodes"/>.</summary>
    public string Code { get; }
}
