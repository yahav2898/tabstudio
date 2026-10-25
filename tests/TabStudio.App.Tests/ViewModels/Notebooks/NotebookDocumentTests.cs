using Avalonia.Headless.XUnit;
using TabStudio.App.Notebooks;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Notebooks;
using TabStudio.Notebooks;
using TabStudio.Notebooks.Formats;

namespace TabStudio.App.Tests.ViewModels.Notebooks;

/// <summary>Notebooks opened in the IDE, run by the real C# kernel.</summary>
public sealed class NotebookDocumentTests
{
    [AvaloniaFact]
    public async Task A_notebook_opens_in_the_notebook_editor_and_runs_with_state_across_cells()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var path = await WriteNotebookAsync(
            fixture,
            "Tour.ipynb",
            NotebookCell.Markdown("# Tour"),
            NotebookCell.Code("var answer = 40;"),
            NotebookCell.Code("answer + 2"),
            NotebookCell.Code("""Console.WriteLine($"The answer is {answer + 2}");"""),
            NotebookCell.Code("""new[] { new { Name = "Ada", Age = 36 }, new { Name = "Alan", Age = 41 } }"""));

        var document = await fixture.Workspace.OpenDocumentAsync(path);
        var notebook = document!.Notebook!;
        Assert.Same(document, fixture.Workspace.ActiveDocument);
        Assert.Equal("Tour.ipynb", document.Title);
        Assert.Equal(5, notebook.Cells.Count);
        Assert.True(notebook.Cells[0].IsRendered);

        await notebook.RunAllAsync();

        foreach (var cell in notebook.Cells.Where(c => c.IsCode))
        {
            Assert.True(cell.RunState == CellRunState.Succeeded, Describe(cell));
        }

        Assert.Equal("42", Assert.IsType<TextOutputViewModel>(Assert.Single(notebook.Cells[2].Outputs)).Text);
        Assert.Equal("The answer is 42", Assert.IsType<StreamOutputViewModel>(Assert.Single(notebook.Cells[3].Outputs)).DisplayText);
        var table = Assert.IsType<TableOutputViewModel>(Assert.Single(notebook.Cells[4].Outputs));
        Assert.Equal(new[] { "Name", "Age" }, table.Table.Columns);
        Assert.Equal(new[] { "Alan", "41" }, table.Table.Rows[1]);
        Assert.Equal("2 rows", table.RowCountText);
        Assert.Equal("[4]", notebook.Cells[4].ExecutionLabel);
        Assert.Equal("Tour.ipynb*", document.Title);
    }

    [AvaloniaFact]
    public async Task Errors_show_under_the_cell_that_failed()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var path = await WriteNotebookAsync(
            fixture,
            "Errors.ipynb",
            NotebookCell.Code("var total = 1;\nConsole.WriteLine(totl);"),
            NotebookCell.Code("""throw new InvalidOperationException("Out of tea");"""));
        var notebook = (await fixture.Workspace.OpenDocumentAsync(path))!.Notebook!;
        var typo = notebook.Cells[0];
        var throws = notebook.Cells[1];

        await notebook.RunCellAsync(typo);
        await notebook.RunCellAsync(throws);

        Assert.Equal(CellRunState.Failed, typo.RunState);
        var listed = Assert.Single(typo.DiagnosticItems, d => d.Code == "CS0103");
        Assert.Equal("Ln 2, Col 19", listed.LocationText);
        var squiggle = Assert.Single(typo.Diagnostics, d => d.Id == "CS0103");
        Assert.Equal("totl", typo.Source.Text.Substring(squiggle.Location!.Start, squiggle.Location.Length));
        Assert.DoesNotContain(typo.Outputs, o => o is ErrorOutputViewModel);

        Assert.Equal(CellRunState.Failed, throws.RunState);
        var error = Assert.IsType<ErrorOutputViewModel>(Assert.Single(throws.Outputs));
        Assert.Equal("InvalidOperationException: Out of tea", error.Title);
        Assert.DoesNotContain("TabStudio.Notebooks", error.Details, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Saving_writes_the_notebook_back_and_leaves_unchanged_cells_as_they_were()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var path = await WriteNotebookAsync(fixture, "Notes.ipynb", NotebookCell.Markdown("# Notes"), NotebookCell.Code("var a = 1;"), NotebookCell.Code("a"));
        var original = await File.ReadAllTextAsync(path);
        var document = (await fixture.Workspace.OpenDocumentAsync(path))!;
        var notebook = document.Notebook!;

        await fixture.Workspace.SaveCommand.ExecuteAsync(null);
        Assert.Equal(original, await File.ReadAllTextAsync(path));

        notebook.Cells[2].Source.Replace(0, 1, "a * 10");
        Assert.True(document.IsDirty);
        await fixture.Workspace.SaveCommand.ExecuteAsync(null);

        Assert.False(document.IsDirty);
        Assert.Equal("Notes.ipynb", document.Title);
        var saved = await NotebookSerializer.LoadAsync(path);
        Assert.Equal(new[] { "# Notes", "var a = 1;", "a * 10" }, saved.Cells.Select(c => c.Source));
        Assert.Equal(notebook.Cells.Select(c => c.Id), saved.Cells.Select(c => c.Id));
    }

    [AvaloniaFact]
    public async Task Dib_notebooks_are_saved_as_dib()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var path = await WriteNotebookAsync(fixture, "Script.dib", NotebookCell.Code("1 + 1"));
        var document = (await fixture.Workspace.OpenDocumentAsync(path))!;

        await document.Notebook!.RunAllAsync();
        Assert.False(document.IsDirty, ".dib files keep no outputs.");
        document.Notebook.AddMarkdownCellCommand.Execute(null);
        document.Notebook.Cells[1].Source.Text = "Some *notes*";
        await fixture.Workspace.SaveAllCommand.ExecuteAsync(null);

        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("#!markdown", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cells\"", text, StringComparison.Ordinal);
        var saved = await NotebookSerializer.LoadAsync(path);
        Assert.Equal(new[] { "1 + 1", "Some *notes*" }, saved.Cells.Select(c => c.Source));
    }

    [AvaloniaFact]
    public async Task The_session_restores_an_open_notebook_with_its_unsaved_cells()
    {
        using var first = new WorkspaceFixture();
        var solution = await first.OpenSampleAsync();
        var path = await WriteNotebookAsync(first, "Session.ipynb", NotebookCell.Code("1 + 1"));
        var document = (await first.Workspace.OpenDocumentAsync(path))!;
        document.Notebook!.Cells[0].Source.Insert(0, "// unsaved\n");
        first.Workspace.SaveSessionNow();

        using var second = new WorkspaceViewModel(first.Compiler, first.DeviceStatus, first.Profile, new AvaloniaUiDispatcher(), first.Sessions, first.Paths);
        await second.LoadAsync(solution, first.Sessions.Load());

        var restored = second.Documents.Single(d => d.FilePath == path);
        Assert.True(restored.IsNotebook);
        Assert.True(restored.IsDirty);
        Assert.StartsWith("// unsaved", restored.Notebook!.Cells[0].Source.Text, StringComparison.Ordinal);
        Assert.Same(restored, second.ActiveDocument);
    }

    [AvaloniaFact]
    public async Task File_New_Notebook_creates_Untitled_ipynb_in_the_startup_project_and_opens_it()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();

        await fixture.Workspace.NewNotebookCommand.ExecuteAsync(null);
        await fixture.Workspace.NewNotebookCommand.ExecuteAsync(null);

        var document = fixture.Workspace.ActiveDocument!;
        Assert.Equal("Untitled-2.ipynb", document.FileName);
        Assert.True(document.IsNotebook);
        Assert.False(document.IsDirty);
        Assert.Equal(fixture.Workspace.StartupProject!.DirectoryPath, Path.GetDirectoryName(document.FilePath));
        Assert.True(Assert.Single(document.Notebook!.Cells).IsCode);
        Assert.Contains(fixture.Workspace.Explorer.Rows, r => r.Name == "Untitled-2.ipynb" && r.Badge == "NB");
        Assert.Contains(fixture.Workspace.Documents, d => d.FileName == "Untitled.ipynb");
    }

    [AvaloniaFact]
    public async Task Opening_a_notebook_file_opens_its_folder_with_the_notebook()
    {
        using var fixture = new WorkspaceFixture();
        var path = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.NotebookId, "Scratch");

        await fixture.Workspace.LoadAsync(path);

        Assert.Equal("Scratch", fixture.Workspace.Title);
        Assert.Equal(path, fixture.Workspace.ActiveDocument?.FilePath);
        var notebook = fixture.Workspace.ActiveDocument!.Notebook!;
        Assert.Equal(2, notebook.Cells.Count);
        Assert.StartsWith("# Scratch", notebook.Cells[0].Source.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_notebook_that_cannot_be_read_opens_as_text()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var path = Path.Combine(fixture.Workspace.StartupProject!.DirectoryPath, "Broken.ipynb");
        await File.WriteAllTextAsync(path, "{ \"cells\": [ not json");

        var document = (await fixture.Workspace.OpenDocumentAsync(path))!;

        Assert.False(document.IsNotebook);
        Assert.StartsWith("{ \"cells\"", document.Document.Text, StringComparison.Ordinal);
        Assert.Contains("opened as text", fixture.Workspace.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task F5_waits_while_cells_run()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var path = await WriteNotebookAsync(fixture, "Slow.ipynb", NotebookCell.Code("Console.WriteLine(\"waiting\");\nawait Task.Delay(60_000, CancellationToken);"));
        var notebook = (await fixture.Workspace.OpenDocumentAsync(path))!.Notebook!;
        var cell = notebook.Cells[0];
        Assert.True(fixture.Workspace.RunCommand.CanExecute(null));

        var run = notebook.RunCellAsync(cell);
        await UiTestHelpers.WaitUntilAsync(() => cell.Outputs.Count > 0, because: "the cell to start");

        Assert.True(fixture.Workspace.IsNotebookRunning);
        Assert.False(fixture.Workspace.RunCommand.CanExecute(null));
        notebook.InterruptCommand.Execute(null);
        await run;

        Assert.Equal(CellRunState.Interrupted, cell.RunState);
        Assert.False(fixture.Workspace.IsNotebookRunning);
        Assert.True(fixture.Workspace.RunCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Closing_a_notebook_saves_it_and_stops_its_kernel()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var path = await WriteNotebookAsync(fixture, "Close.ipynb", NotebookCell.Code("var x = 1;"));
        var document = (await fixture.Workspace.OpenDocumentAsync(path))!;
        await document.Notebook!.RunAllAsync();

        await fixture.Workspace.CloseDocumentCommand.ExecuteAsync(document);

        Assert.DoesNotContain(document, fixture.Workspace.Documents);
        var saved = await NotebookSerializer.LoadAsync(path);
        Assert.Equal(1, saved.Cells[0].ExecutionCount);
    }

    private static async Task<string> WriteNotebookAsync(WorkspaceFixture fixture, string name, params NotebookCell[] cells)
    {
        var path = Path.Combine(fixture.Workspace.StartupProject!.DirectoryPath, name);
        await NotebookSerializer.SaveAsync(NotebookDocument.Create(cells), path);
        return path;
    }

    private static string Describe(NotebookCellViewModel cell) =>
        $"{cell.Source.Text}: {cell.RunState} " + string.Join(" | ", cell.Outputs.Select(o => o switch
        {
            ErrorOutputViewModel error => error.Title + " " + error.Details,
            StreamOutputViewModel stream => stream.Text,
            TextOutputViewModel text => text.Text,
            _ => o.GetType().Name,
        }).Concat(cell.DiagnosticItems.Select(d => d.Code + " " + d.Message)));
}
