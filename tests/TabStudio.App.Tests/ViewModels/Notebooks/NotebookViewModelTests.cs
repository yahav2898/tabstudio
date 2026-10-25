using Avalonia.Headless.XUnit;
using TabStudio.App.ViewModels.Notebooks;
using TabStudio.Notebooks;

namespace TabStudio.App.Tests.ViewModels.Notebooks;

/// <summary>The notebook editor's behavior, over a fake kernel that answers instantly.</summary>
public sealed class NotebookViewModelTests
{
    [AvaloniaFact]
    public async Task Running_a_cell_shows_its_output_and_execution_count()
    {
        var (notebook, host) = Create(NotebookCell.Code("hello"));
        var cell = notebook.Cells[0];

        await notebook.RunCellAsync(cell);

        Assert.Equal("[1]", cell.ExecutionLabel);
        Assert.Equal(CellRunState.Succeeded, cell.RunState);
        var output = Assert.IsType<StreamOutputViewModel>(Assert.Single(cell.Outputs));
        Assert.Equal("hello", output.DisplayText);
        Assert.False(notebook.IsBusy);
        Assert.Equal(2, host.BusyChanges);
        Assert.True(notebook.IsDirty, "Outputs are saved in .ipynb files, so running changes the file.");
    }

    [AvaloniaFact]
    public async Task Run_all_stops_at_the_first_cell_that_fails()
    {
        var (notebook, host) = Create(
            NotebookCell.Markdown("# Title"),
            NotebookCell.Code("first"),
            NotebookCell.Code("throw"),
            NotebookCell.Code("never"));

        await notebook.RunAllAsync();

        Assert.Equal(new[] { "first", "throw" }, host.Kernel.Ran);
        Assert.Equal(CellRunState.Failed, notebook.Cells[2].RunState);
        Assert.Equal(CellRunState.Idle, notebook.Cells[3].RunState);
        var error = Assert.IsType<ErrorOutputViewModel>(Assert.Single(notebook.Cells[2].Outputs));
        Assert.Equal("InvalidOperationException: Boom", error.Title);
        Assert.Contains("Submission#1", error.Details, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Compile_errors_are_listed_under_the_cell_instead_of_as_an_error_output()
    {
        var (notebook, _) = Create(NotebookCell.Code("fail"));
        var cell = notebook.Cells[0];

        await notebook.RunCellAsync(cell);

        var item = Assert.Single(cell.DiagnosticItems);
        Assert.Equal("CS0103", item.Code);
        Assert.Equal("Ln 1, Col 1", item.LocationText);
        Assert.Single(cell.Diagnostics);
        Assert.Empty(cell.Outputs);
        Assert.Equal(CellRunState.Failed, cell.RunState);
    }

    [AvaloniaFact]
    public async Task Cells_wait_their_turn_and_output_appears_while_a_cell_runs()
    {
        var (notebook, host) = Create(NotebookCell.Code("wait"), NotebookCell.Code("after"));
        var waiting = notebook.Cells[0];
        var next = notebook.Cells[1];

        var first = notebook.RunCellAsync(waiting);
        var second = notebook.RunCellAsync(next);
        await UiTestHelpers.WaitUntilAsync(() => host.Kernel.IsWaiting && waiting.Outputs.Count > 0, because: "the first cell's live output");

        Assert.True(notebook.IsBusy);
        Assert.Equal("C# (busy)", notebook.KernelStatusText);
        Assert.True(waiting.IsRunning);
        Assert.True(next.IsQueued);
        Assert.Equal("started", Assert.IsType<StreamOutputViewModel>(waiting.Outputs[0]).DisplayText);

        host.Kernel.Release();
        await Task.WhenAll(first, second);

        Assert.Equal(new[] { "wait", "after" }, host.Kernel.Ran);
        Assert.Equal("finished", Assert.IsType<StreamOutputViewModel>(Assert.Single(waiting.Outputs)).DisplayText);
        Assert.Equal(CellRunState.Succeeded, next.RunState);
        Assert.Equal("C# (idle)", notebook.KernelStatusText);
    }

    [AvaloniaFact]
    public async Task Interrupt_stops_the_running_cell_and_empties_the_queue()
    {
        var (notebook, host) = Create(NotebookCell.Code("wait"), NotebookCell.Code("after"));

        var run = notebook.RunAllAsync();
        await UiTestHelpers.WaitUntilAsync(() => host.Kernel.IsWaiting, because: "the first cell to start");
        Assert.True(notebook.InterruptCommand.CanExecute(null));
        notebook.InterruptCommand.Execute(null);
        await run;

        Assert.Equal(new[] { "wait" }, host.Kernel.Ran);
        Assert.Equal(CellRunState.Interrupted, notebook.Cells[0].RunState);
        Assert.Equal(CellRunState.Idle, notebook.Cells[1].RunState);
        Assert.False(notebook.IsBusy);
    }

    [AvaloniaFact]
    public async Task Restart_keeps_outputs_and_counts_from_one_again()
    {
        var (notebook, host) = Create(NotebookCell.Code("one"), NotebookCell.Code("two"));
        await notebook.RunAllAsync();

        await notebook.RestartCommand.ExecuteAsync(null);
        await notebook.RunCellAsync(notebook.Cells[1]);

        Assert.Equal(1, host.Kernel.Restarts);
        Assert.Equal("[1]", notebook.Cells[0].ExecutionLabel);
        Assert.Equal("[1]", notebook.Cells[1].ExecutionLabel);
        Assert.Single(notebook.Cells[0].Outputs);
    }

    [AvaloniaFact]
    public async Task Cells_do_not_run_while_the_IDE_says_they_cannot()
    {
        var (notebook, host) = Create(NotebookCell.Code("hello"));
        host.CannotRunCellsReason = "Stop the running program first.";

        await notebook.RunCellAsync(notebook.Cells[0]);

        Assert.Empty(host.Kernel.Ran);
        Assert.Equal("Stop the running program first.", notebook.StatusMessage);
        Assert.Equal(CellRunState.Idle, notebook.Cells[0].RunState);
    }

    [AvaloniaFact]
    public void Typing_makes_the_notebook_dirty_and_undoing_makes_it_clean_again()
    {
        var (notebook, _) = Create(NotebookCell.Code("var x = 1;"));
        var cell = notebook.Cells[0];

        cell.Source.Insert(0, "// note\n");
        Assert.True(notebook.IsDirty);
        Assert.True(notebook.CanUndo);

        notebook.Undo();

        Assert.False(notebook.IsDirty);
        Assert.Equal("var x = 1;", cell.Source.Text);
        Assert.True(notebook.CanRedo);
    }

    [AvaloniaFact]
    public void A_deleted_cell_comes_back_with_undo()
    {
        var (notebook, _) = Create(NotebookCell.Code("a"), NotebookCell.Code("b"), NotebookCell.Code("c"));
        var middle = notebook.Cells[1];

        middle.DeleteCommand.Execute(null);
        Assert.Equal(new[] { "a", "c" }, notebook.Cells.Select(c => c.Source.Text));
        Assert.Equal("c", notebook.SelectedCell?.Source.Text);

        notebook.Undo();

        Assert.Equal(new[] { "a", "b", "c" }, notebook.Cells.Select(c => c.Source.Text));
        Assert.Same(middle, notebook.SelectedCell);
    }

    [AvaloniaFact]
    public void Cells_move_convert_and_insert()
    {
        var (notebook, _) = Create(NotebookCell.Code("a"), NotebookCell.Code("b"));
        var first = notebook.Cells[0];

        first.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "b", "a" }, notebook.Cells.Select(c => c.Source.Text));

        first.ConvertToMarkdownCommand.Execute(null);
        Assert.True(first.IsMarkdown);
        Assert.True(first.IsRendered);
        Assert.Equal("Markdown", first.LanguageName);

        first.InsertCodeBelowCommand.Execute(null);
        Assert.Equal(3, notebook.Cells.Count);
        Assert.True(notebook.Cells[2].IsCode);
        Assert.Same(notebook.Cells[2], notebook.SelectedCell);
        Assert.True(notebook.Cells[2].HasPendingFocus);

        notebook.Undo();
        notebook.Undo();
        notebook.Undo();
        Assert.Equal(new[] { "a", "b" }, notebook.Cells.Select(c => c.Source.Text));
        Assert.True(notebook.Cells[0].IsCode);
    }

    [AvaloniaFact]
    public async Task Shift_Enter_on_the_last_cell_runs_it_and_adds_a_code_cell()
    {
        var (notebook, _) = Create(NotebookCell.Code("only"));

        await notebook.RunCellAndSelectNextAsync(notebook.Cells[0]);

        Assert.Equal(2, notebook.Cells.Count);
        Assert.Same(notebook.Cells[1], notebook.SelectedCell);
        Assert.Equal(CellRunState.Succeeded, notebook.Cells[0].RunState);
    }

    [AvaloniaFact]
    public async Task Running_the_cells_of_a_dib_does_not_change_the_file()
    {
        var (notebook, _) = Create(NotebookFormat.Polyglot, NotebookCell.Code("hello"));

        await notebook.RunAllAsync();

        Assert.False(notebook.IsDirty, ".dib files keep no outputs.");
    }

    [AvaloniaFact]
    public void Markdown_cells_render_until_edited()
    {
        var (notebook, _) = Create(NotebookCell.Code("x"), NotebookCell.Markdown("# Title\n\nSome **bold** text."));
        var markdown = notebook.Cells[1];
        Assert.True(markdown.IsRendered);
        Assert.Equal(2, markdown.MarkdownBlocks.Count);

        markdown.EditCommand.Execute(null);
        Assert.True(markdown.IsEditing);
        Assert.True(markdown.ShowsEditor);
        markdown.Source.Insert(markdown.Source.TextLength, "\n\n- item");

        notebook.SelectedCell = notebook.Cells[0];

        Assert.True(markdown.IsRendered);
        Assert.Equal(3, markdown.MarkdownBlocks.Count);
    }

    [AvaloniaFact]
    public void Find_moves_through_the_matches_in_every_cell()
    {
        var (notebook, _) = Create(NotebookCell.Code("var total = 1;"), NotebookCell.Markdown("The *total* so far"), NotebookCell.Code("total + Total"));

        notebook.OpenFind();
        notebook.FindText = "total";

        Assert.True(notebook.IsFindOpen);
        Assert.Equal("1 of 4", notebook.FindStatus);
        Assert.Same(notebook.Cells[0], notebook.SelectedCell);
        Assert.Equal((4, 5), notebook.Cells[0].TakePendingSelection());

        notebook.FindNextCommand.Execute(null);
        Assert.Same(notebook.Cells[1], notebook.SelectedCell);
        Assert.True(notebook.Cells[1].IsEditing, "A match in Markdown shows the Markdown text.");

        notebook.FindPreviousCommand.Execute(null);
        notebook.FindPreviousCommand.Execute(null);
        Assert.Equal("4 of 4", notebook.FindStatus);
        Assert.Equal((8, 5), notebook.Cells[2].TakePendingSelection());

        notebook.FindText = "nothing";
        Assert.Equal("No results", notebook.FindStatus);
    }

    [AvaloniaFact]
    public void Unsaved_work_round_trips_through_the_session_text()
    {
        var (notebook, host) = Create(NotebookCell.Code("a"));
        notebook.Cells[0].Source.Insert(1, " + 1");
        var text = notebook.Serialize();

        var (restored, _) = Create(NotebookCell.Code("a"));
        restored.RestoreUnsavedText(text);

        Assert.Equal("a + 1", Assert.Single(restored.Cells).Source.Text);
        Assert.True(restored.IsDirty);
        Assert.Empty(host.Kernel.Ran);
    }

    private static (NotebookViewModel Notebook, FakeNotebookHost Host) Create(params NotebookCell[] cells) =>
        Create(NotebookFormat.Jupyter, cells);

    private static (NotebookViewModel Notebook, FakeNotebookHost Host) Create(NotebookFormat format, params NotebookCell[] cells)
    {
        var host = new FakeNotebookHost();
        return (new NotebookViewModel(NotebookDocument.Create(cells), format, host), host);
    }
}
