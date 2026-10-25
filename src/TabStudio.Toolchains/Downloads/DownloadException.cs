namespace TabStudio.Toolchains.Downloads;

/// <summary>Why a download failed.</summary>
public enum DownloadFailure
{
    /// <summary>The device is offline, or the server cannot be reached.</summary>
    NoNetwork,

    /// <summary>The server has no such file.</summary>
    NotFound,

    /// <summary>The server answered with an error.</summary>
    ServerError,

    /// <summary>The file arrived, but its size or hash is not the one expected.</summary>
    IntegrityFailed,

    /// <summary>The device's storage is full.</summary>
    NoSpace,

    /// <summary>The connection broke and retrying did not help.</summary>
    Interrupted,
}

/// <summary>A download failed, with a message the user can read.</summary>
public sealed class DownloadException : Exception
{
    /// <summary>Creates the exception.</summary>
    public DownloadException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public DownloadException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public DownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a failure.</summary>
    /// <param name="failure">Why the download failed.</param>
    /// <param name="message">What went wrong, in a sentence the user can read.</param>
    /// <param name="innerException">The cause, if any.</param>
    public DownloadException(DownloadFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>Why the download failed.</summary>
    public DownloadFailure Failure { get; } = DownloadFailure.Interrupted;
}
