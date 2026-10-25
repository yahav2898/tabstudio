namespace TabStudio.Packages.Feeds;

/// <summary>
/// Thrown by a feed when its source cannot be read. The restorer turns it into a diagnostic.
/// </summary>
internal sealed class PackageSourceException : Exception
{
    /// <summary>Creates an exception with a generic message.</summary>
    public PackageSourceException()
        : base("The package source could not be read.")
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">What went wrong.</param>
    public PackageSourceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The underlying error.</param>
    public PackageSourceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception for a specific source.</summary>
    /// <param name="source">The source that failed.</param>
    /// <param name="message">What went wrong, as a full sentence.</param>
    /// <param name="isUnreachable">Whether the source could not be contacted at all, rather than answering with an error.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public PackageSourceException(PackageSource source, string message, bool isUnreachable, Exception? innerException = null)
        : base(message, innerException)
    {
        PackageSource = source;
        IsUnreachable = isUnreachable;
    }

    /// <summary>The source that failed, when known.</summary>
    public PackageSource? PackageSource { get; }

    /// <summary>Whether the source could not be contacted at all: no network, a DNS failure or a timeout.</summary>
    public bool IsUnreachable { get; }
}
