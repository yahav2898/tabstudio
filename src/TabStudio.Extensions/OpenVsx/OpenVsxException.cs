namespace TabStudio.Extensions.OpenVsx;

/// <summary>Why a request to an Open VSX registry failed.</summary>
public enum OpenVsxFailure
{
    /// <summary>The registry could not be reached: no connection, a timeout, or a server that is down.</summary>
    Offline,

    /// <summary>The registry has no such extension or version.</summary>
    NotFound,

    /// <summary>The registry refused the request.</summary>
    Rejected,

    /// <summary>The registry answered with something that is not what its API describes.</summary>
    InvalidResponse,

    /// <summary>A downloaded package did not match the hash or signature the registry published for it.</summary>
    IntegrityFailed,
}

/// <summary>A request to an Open VSX registry failed.</summary>
public sealed class OpenVsxException : Exception
{
    /// <summary>Creates the exception.</summary>
    public OpenVsxException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, in words for the user.</param>
    public OpenVsxException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, in words for the user.</param>
    /// <param name="innerException">The cause.</param>
    public OpenVsxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a kind of failure.</summary>
    /// <param name="failure">Why the request failed.</param>
    /// <param name="message">What went wrong, in words for the user.</param>
    /// <param name="innerException">The cause, if any.</param>
    public OpenVsxException(OpenVsxFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>Why the request failed.</summary>
    public OpenVsxFailure Failure { get; } = OpenVsxFailure.InvalidResponse;
}
