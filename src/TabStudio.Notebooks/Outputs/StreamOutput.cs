namespace TabStudio.Notebooks.Outputs;

/// <summary>
/// Text a cell wrote to the console: a Jupyter <c>stream</c> output.
/// </summary>
/// <param name="Name">The stream: <see cref="StandardOutput"/> or <see cref="StandardError"/>.</param>
/// <param name="Text">The text, which may span several lines.</param>
public sealed record StreamOutput(string Name, string Text) : NotebookOutput
{
    /// <summary>The name of standard output, where <see cref="Console.Out"/> writes.</summary>
    public const string StandardOutput = "stdout";

    /// <summary>The name of standard error, where <see cref="Console.Error"/> writes.</summary>
    public const string StandardError = "stderr";

    /// <summary>Whether the text was written to standard error.</summary>
    public bool IsError => Name == StandardError;
}
