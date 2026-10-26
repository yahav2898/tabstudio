namespace TabStudio.Uno.Hosting;

/// <summary>TabStudio's Uno Platform host cannot run, with the reason.</summary>
public sealed class UnoHostException : Exception
{
    /// <summary>Creates the exception.</summary>
    public UnoHostException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, for the Output panel.</param>
    public UnoHostException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, for the Output panel.</param>
    /// <param name="innerException">The cause.</param>
    public UnoHostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
