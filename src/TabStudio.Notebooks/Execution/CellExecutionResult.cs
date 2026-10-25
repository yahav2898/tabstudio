using TabStudio.Core.Diagnostics;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// The outcome of running one cell: its outputs, diagnostics, execution count and timing.
/// </summary>
public sealed record CellExecutionResult
{
    /// <summary>The <see cref="NotebookCell.Id"/> of the cell that ran.</summary>
    public required string CellId { get; init; }

    /// <summary>How the run ended.</summary>
    public required CellExecutionStatus Status { get; init; }

    /// <summary>
    /// The kernel's execution counter for this run, shown as <c>[3]</c> next to the cell. <see langword="null"/>
    /// when the cell did not get to the kernel, for example because it was interrupted while waiting its turn.
    /// </summary>
    public int? ExecutionCount { get; init; }

    /// <summary>
    /// What the cell produced, in order: console text, displayed values, the value of its last expression, and
    /// an <see cref="ErrorOutput"/> when it failed to compile or threw.
    /// </summary>
    public IReadOnlyList<NotebookOutput> Outputs { get; init; } = [];

    /// <summary>
    /// Compiler errors and warnings, plus the kernel's own <c>TS</c> diagnostics. Positions are relative to the
    /// cell's text, and <see cref="DiagnosticLocation.FilePath"/> is the cell's id.
    /// </summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>How long compiling the cell took.</summary>
    public TimeSpan CompilationDuration { get; init; }

    /// <summary>How long the whole run took, compiling included.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The exception the cell threw, when it failed.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Whether the cell compiled and ran to the end.</summary>
    public bool Succeeded => Status == CellExecutionStatus.Succeeded;

    /// <summary>
    /// Returns the cell with this run's outputs and execution count. A cell that never got to run, because it
    /// was skipped or interrupted before it started, keeps the outputs it had.
    /// </summary>
    /// <param name="cell">The cell that ran.</param>
    public NotebookCell ApplyTo(NotebookCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var didNotRun = Status == CellExecutionStatus.Skipped ||
            (Status == CellExecutionStatus.Interrupted && ExecutionCount is null);
        return didNotRun ? cell : cell with { Outputs = Outputs, ExecutionCount = ExecutionCount };
    }
}
