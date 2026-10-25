namespace TabStudio.Link;

/// <summary>No address of the host answered.</summary>
public sealed class LinkUnreachableException : IOException
{
    /// <summary>Creates the exception.</summary>
    public LinkUnreachableException()
        : base("The host could not be reached.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Which host could not be reached, and where it was looked for.</param>
    public LinkUnreachableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">Which host could not be reached, and where it was looked for.</param>
    /// <param name="innerException">Why the last address failed.</param>
    public LinkUnreachableException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
