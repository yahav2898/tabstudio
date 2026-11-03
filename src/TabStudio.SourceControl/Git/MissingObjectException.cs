namespace TabStudio.SourceControl.Git;

/// <summary>A Git object that should be in the object database is not.</summary>
public sealed class MissingObjectException : Exception
{
    /// <summary>Creates the exception.</summary>
    public MissingObjectException()
    {
    }

    /// <summary>Creates the exception for a missing object.</summary>
    /// <param name="id">The object's name.</param>
    public MissingObjectException(string id)
        : base($"The Git object {id} is missing.")
    {
        Id = id;
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public MissingObjectException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The missing object's name, when known.</summary>
    public string? Id { get; }
}
