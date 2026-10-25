using TabStudio.App.Services;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>What a notebook needs from the IDE around it: a kernel, the UI thread, and what else is running.</summary>
public interface INotebookHost
{
    /// <summary>Gets work back onto the UI thread.</summary>
    IUiDispatcher Ui { get; }

    /// <summary>
    /// Why cells cannot run right now, in a sentence for the user, or <see langword="null"/> when they can. A
    /// program started with F5 owns the process's console, which cells write to as well.
    /// </summary>
    string? CannotRunCellsReason { get; }

    /// <summary>Creates the kernel that runs a notebook's cells. Called when the notebook first runs a cell.</summary>
    INotebookKernel CreateKernel();

    /// <summary>Called when a notebook starts or stops running cells.</summary>
    /// <param name="notebook">The notebook.</param>
    void OnNotebookBusyChanged(NotebookViewModel notebook);
}
