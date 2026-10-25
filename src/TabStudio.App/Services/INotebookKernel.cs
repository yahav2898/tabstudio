using TabStudio.Notebooks;
using TabStudio.Notebooks.Execution;

namespace TabStudio.App.Services;

/// <summary>
/// Runs a notebook's code cells, with state carried from one cell to the next. This is the boundary between the
/// notebook editor and the code that runs cells, so a remote kernel (such as one on a PC over TabStudio Link) can
/// take the place of the one inside the app.
/// </summary>
public interface INotebookKernel : IDisposable
{
    /// <summary>
    /// Raised for each output while a cell runs, so it appears as it is produced. Raised on a background thread.
    /// </summary>
    event EventHandler<CellOutputEventArgs>? OutputReceived;

    /// <summary>Runs one cell after any cell already running.</summary>
    /// <param name="cell">The cell, with its current source.</param>
    /// <param name="cancellationToken">Interrupts the cell.</param>
    /// <returns>The outcome. A cell that fails does not throw; its errors are in the result.</returns>
    Task<CellExecutionResult> ExecuteAsync(NotebookCell cell, CancellationToken cancellationToken);

    /// <summary>Interrupts the running cell.</summary>
    void Interrupt();

    /// <summary>Interrupts any running cell and forgets every variable, type and <c>using</c> of earlier cells.</summary>
    /// <param name="cancellationToken">Stops waiting for the running cell to stop.</param>
    Task RestartAsync(CancellationToken cancellationToken);
}
