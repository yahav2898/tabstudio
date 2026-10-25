namespace TabStudio.Notebooks.Execution;

/// <summary>
/// The outcome of running several cells of a notebook, such as with Run All.
/// </summary>
/// <param name="Document">The notebook with each cell that ran showing its new outputs and execution count.</param>
/// <param name="Results">One result per code cell the kernel was asked to run, in order. Running stops after a cell that fails or is interrupted.</param>
public sealed record NotebookRunResult(NotebookDocument Document, IReadOnlyList<CellExecutionResult> Results)
{
    /// <summary>Whether every C# cell ran to the end. Cells in other languages are skipped and do not count.</summary>
    public bool Succeeded => Results.All(r => r.Status is CellExecutionStatus.Succeeded or CellExecutionStatus.Skipped or CellExecutionStatus.Unsupported);
}
