namespace TabStudio.Link;

/// <summary>The connection closed before the other device answered.</summary>
public sealed class LinkConnectionLostException : IOException
{
    /// <summary>Creates the exception.</summary>
    public LinkConnectionLostException()
        : base("The connection to the other device was lost.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What happened.</param>
    public LinkConnectionLostException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What happened.</param>
    /// <param name="innerException">Why the connection closed.</param>
    public LinkConnectionLostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
