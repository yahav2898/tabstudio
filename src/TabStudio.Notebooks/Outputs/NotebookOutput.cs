namespace TabStudio.Notebooks.Outputs;

/// <summary>
/// Something a code cell produced when it ran: console text (<see cref="StreamOutput"/>), a displayed value
/// (<see cref="DisplayOutput"/>) or an error (<see cref="ErrorOutput"/>).
/// </summary>
/// <remarks>The three kinds match the output types of Jupyter's nbformat 4.</remarks>
public abstract record NotebookOutput
{
    /// <summary>Other properties the output had in its file, kept so saving does not lose them.</summary>
    public NotebookMetadata AdditionalProperties { get; init; } = NotebookMetadata.Empty;
}
