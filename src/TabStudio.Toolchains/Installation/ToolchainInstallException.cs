namespace TabStudio.Toolchains.Installation;

/// <summary>Why installing, updating or removing a toolchain failed.</summary>
public enum InstallFailure
{
    /// <summary>The device is offline or the mirror cannot be reached.</summary>
    NoNetwork,

    /// <summary>There is not enough free space.</summary>
    NotEnoughSpace,

    /// <summary>A download did not match its signature or hash.</summary>
    IntegrityFailed,

    /// <summary>The distribution has no package the toolchain needs.</summary>
    PackageNotFound,

    /// <summary>This build or this device cannot run the Linux environment.</summary>
    RuntimeUnavailable,

    /// <summary>The package manager or a setup step failed.</summary>
    CommandFailed,

    /// <summary>The server answered with an error, or the connection kept breaking.</summary>
    ServerError,
}

/// <summary>A toolchain operation failed, with a message the user can read and act on.</summary>
public sealed class ToolchainInstallException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ToolchainInstallException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public ToolchainInstallException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public ToolchainInstallException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a failure.</summary>
    /// <param name="failure">Why it failed.</param>
    /// <param name="message">What went wrong and what to do, in sentences the user can read.</param>
    /// <param name="details">Output of the command that failed, if any, for the Output panel.</param>
    /// <param name="innerException">The cause, if any.</param>
    public ToolchainInstallException(InstallFailure failure, string message, string? details = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        Details = details;
    }

    /// <summary>Why it failed.</summary>
    public InstallFailure Failure { get; } = InstallFailure.CommandFailed;

    /// <summary>Output of the command that failed, if any.</summary>
    public string? Details { get; }
}
