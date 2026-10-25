namespace TabStudio.Link;

/// <summary>The other device sent something the protocol does not allow, such as a frame that is too large.</summary>
public sealed class LinkProtocolException : IOException
{
    /// <summary>Creates the exception.</summary>
    public LinkProtocolException()
        : base("The other device broke the TabStudio Link protocol.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What was wrong.</param>
    public LinkProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What was wrong.</param>
    /// <param name="innerException">The cause.</param>
    public LinkProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
