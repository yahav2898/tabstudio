namespace TabStudio.Core.Workspaces;

/// <summary>
/// Thrown when a solution or project file cannot be read.
/// </summary>
public sealed class ProjectLoadException : Exception
{
    /// <summary>Creates an exception with a generic message.</summary>
    public ProjectLoadException()
        : base("The file could not be loaded.")
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">What went wrong.</param>
    public ProjectLoadException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The underlying error.</param>
    public ProjectLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception for a specific file.</summary>
    /// <param name="filePath">The file that could not be loaded.</param>
    /// <param name="reason">Why it could not be loaded.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public ProjectLoadException(string filePath, string reason, Exception? innerException)
        : base($"Could not load '{Path.GetFileName(filePath)}': {reason}", innerException)
    {
        FilePath = filePath;
    }

    /// <summary>The file that could not be loaded, when known.</summary>
    public string? FilePath { get; }
}
