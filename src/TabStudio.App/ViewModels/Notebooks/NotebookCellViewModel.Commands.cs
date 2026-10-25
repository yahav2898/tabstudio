using CommunityToolkit.Mvvm.Input;
using TabStudio.Notebooks;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>The commands of a cell's gutter and toolbar. Each acts on the notebook that owns the cell.</summary>
public sealed partial class NotebookCellViewModel
{
    /// <summary>Whether the empty-Markdown hint is shown in place of rendered Markdown.</summary>
    public bool ShowsMarkdownPlaceholder => IsRendered && IsEmptyMarkdown;

    [RelayCommand]
    private void Run() => _ = Notebook.RunCellAsync(this);

    /// <summary>The gutter button: runs the cell, or interrupts it while it runs.</summary>
    [RelayCommand]
    private void RunOrStop()
    {
        if (IsWorking)
        {
            Notebook.InterruptCommand.Execute(null);
        }
        else
        {
            _ = Notebook.RunCellAsync(this);
        }
    }

    [RelayCommand]
    private void RunCellsAbove() => _ = Notebook.RunCellsAboveAsync(this);

    [RelayCommand]
    private void RunCellAndBelow() => _ = Notebook.RunCellAndBelowAsync(this);

    [RelayCommand]
    private void Delete() => Notebook.DeleteCell(this);

    [RelayCommand]
    private void MoveUp() => Notebook.MoveCell(this, -1);

    [RelayCommand]
    private void MoveDown() => Notebook.MoveCell(this, 1);

    [RelayCommand]
    private void InsertCodeAbove() => Notebook.InsertCellAbove(this, NotebookCellKind.Code);

    [RelayCommand]
    private void InsertCodeBelow() => Notebook.InsertCellBelow(this, NotebookCellKind.Code);

    [RelayCommand]
    private void InsertMarkdownBelow() => Notebook.InsertCellBelow(this, NotebookCellKind.Markdown);

    [RelayCommand]
    private void ConvertToMarkdown() => Notebook.ChangeCellKind(this, NotebookCellKind.Markdown);

    [RelayCommand]
    private void ConvertToCode() => Notebook.ChangeCellKind(this, NotebookCellKind.Code);

    [RelayCommand]
    private void ClearCellOutputs() => Notebook.ClearOutputs(this);

    [RelayCommand]
    private void Edit() => Notebook.EditCell(this);

    [RelayCommand]
    private void FinishEditing() => IsEditing = false;
}
