namespace TabStudio.Extensions.Manifests;

/// <summary>An extension package or its manifest cannot be used: it is not a .vsix, is damaged, or is missing required parts.</summary>
public sealed class ExtensionPackageException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ExtensionPackageException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What is wrong with the package, in words for the user.</param>
    public ExtensionPackageException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What is wrong with the package, in words for the user.</param>
    /// <param name="innerException">The cause.</param>
    public ExtensionPackageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
