using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Execution;

/// <summary>Raised when the kernel starts running a cell.</summary>
/// <param name="cellId">The cell's id.</param>
/// <param name="executionCount">The execution count the run will have.</param>
public sealed class CellExecutionStartedEventArgs(string cellId, int executionCount) : EventArgs
{
    /// <summary>The <see cref="NotebookCell.Id"/> of the cell that started.</summary>
    public string CellId { get; } = cellId;

    /// <summary>The execution count the run will have.</summary>
    public int ExecutionCount { get; } = executionCount;
}

/// <summary>Raised for each output while a cell runs, so the view can show output as it appears.</summary>
/// <param name="cellId">The cell's id.</param>
/// <param name="output">The new output.</param>
public sealed class CellOutputEventArgs(string cellId, NotebookOutput output) : EventArgs
{
    /// <summary>The <see cref="NotebookCell.Id"/> of the cell that produced the output.</summary>
    public string CellId { get; } = cellId;

    /// <summary>
    /// The new output. Console text arrives a line at a time; the final <see cref="CellExecutionResult.Outputs"/>
    /// joins consecutive text of the same stream into one output.
    /// </summary>
    public NotebookOutput Output { get; } = output;
}

/// <summary>Raised when a call to run a cell finishes, however it ended.</summary>
/// <param name="result">The outcome.</param>
public sealed class CellExecutionCompletedEventArgs(CellExecutionResult result) : EventArgs
{
    /// <summary>The outcome of the run.</summary>
    public CellExecutionResult Result { get; } = result;
}
