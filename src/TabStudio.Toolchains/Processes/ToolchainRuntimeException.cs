namespace TabStudio.Toolchains.Processes;

/// <summary>A program could not be started, with a message the user can read.</summary>
public sealed class ToolchainRuntimeException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ToolchainRuntimeException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, in a sentence the user can read.</param>
    public ToolchainRuntimeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception behind it.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public ToolchainRuntimeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with a message and the system's error number.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="errorNumber">The <c>errno</c> or Windows error code.</param>
    /// <param name="innerException">The cause, if any.</param>
    public ToolchainRuntimeException(string message, int errorNumber, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorNumber = errorNumber;
    }

    /// <summary>The system's error number, or 0 when there is none.</summary>
    public int ErrorNumber { get; }
}
