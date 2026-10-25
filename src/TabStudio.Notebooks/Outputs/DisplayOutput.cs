namespace TabStudio.Notebooks.Outputs;

/// <summary>
/// A value shown below a cell: the cell's result, or something it passed to <c>display(...)</c>.
/// This is a Jupyter <c>execute_result</c> or <c>display_data</c> output.
/// </summary>
/// <param name="Data">The value's representations, such as <c>text/html</c> and <c>text/plain</c>.</param>
public sealed record DisplayOutput(MimeBundle Data) : NotebookOutput
{
    /// <summary>Display metadata, such as image sizes.</summary>
    public NotebookMetadata Metadata { get; init; } = NotebookMetadata.Empty;

    /// <summary>
    /// The value as a table, when it is a collection or an object, so a native view can show it without HTML.
    /// Only outputs produced by the kernel have it; outputs read from a file do not.
    /// </summary>
    public TableOutput? Table { get; init; }

    /// <summary>
    /// Whether this is the value of the cell's last expression (a Jupyter <c>execute_result</c>) rather than
    /// something the cell displayed.
    /// </summary>
    public bool IsResult { get; init; }

    /// <summary>For a result, the execution count of the run that produced it.</summary>
    public int? ExecutionCount { get; init; }
}
