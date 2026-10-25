namespace TabStudio.WindowsCompatibility;

/// <summary>Windows Compatibility could not do something; the message says why, in words for the user.</summary>
public sealed class WindowsCompatibilityException : Exception
{
    /// <summary>Creates the exception.</summary>
    public WindowsCompatibilityException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public WindowsCompatibilityException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public WindowsCompatibilityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
