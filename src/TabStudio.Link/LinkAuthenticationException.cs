namespace TabStudio.Link;

/// <summary>A host refused the connection, or proved not to be the expected host.</summary>
public sealed class LinkAuthenticationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public LinkAuthenticationException()
        : this(LinkRefusal.NotPaired, "The other device did not accept this one.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What happened.</param>
    public LinkAuthenticationException(string message)
        : this(LinkRefusal.NotPaired, message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What happened.</param>
    /// <param name="innerException">The cause.</param>
    public LinkAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Refusal = LinkRefusal.WrongHost;
    }

    /// <summary>Creates the exception for a refusal.</summary>
    /// <param name="refusal">Why the connection was refused.</param>
    /// <param name="message">What happened, in words for the user.</param>
    public LinkAuthenticationException(LinkRefusal refusal, string message)
        : base(message)
    {
        Refusal = refusal;
    }

    /// <summary>Why the connection was refused.</summary>
    public LinkRefusal Refusal { get; }
}
