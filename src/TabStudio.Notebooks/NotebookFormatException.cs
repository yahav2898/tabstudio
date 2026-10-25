namespace TabStudio.Notebooks;

/// <summary>
/// Thrown when a notebook file cannot be read because it is not a valid notebook.
/// </summary>
public sealed class NotebookFormatException : Exception
{
    /// <summary>Creates an exception with a generic message.</summary>
    public NotebookFormatException()
        : base("The notebook could not be read.")
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">What is wrong with the notebook.</param>
    public NotebookFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    /// <param name="message">What is wrong with the notebook.</param>
    /// <param name="innerException">The underlying error, such as a JSON syntax error.</param>
    public NotebookFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
