namespace TabStudio.Packages.Installation;

/// <summary>
/// Thrown when a <c>.nupkg</c> or its manifest is malformed, or when the package would write outside its folder.
/// </summary>
internal sealed class InvalidPackageException : Exception
{
    /// <summary>Creates an exception with a generic message.</summary>
    public InvalidPackageException()
        : base("The package is not valid.")
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">What is wrong with the package, as a full sentence.</param>
    public InvalidPackageException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    /// <param name="message">What is wrong with the package, as a full sentence.</param>
    /// <param name="innerException">The underlying error.</param>
    public InvalidPackageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
