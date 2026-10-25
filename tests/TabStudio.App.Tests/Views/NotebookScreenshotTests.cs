using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Notebooks;
using TabStudio.App.Views;
using TabStudio.App.Views.Notebooks;
using TabStudio.Compilation;
using TabStudio.Notebooks;
using TabStudio.Notebooks.Formats;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>Renders the notebook editor with Skia and saves screenshots of it, like <see cref="ScreenshotTests"/>.</summary>
public sealed class NotebookScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public NotebookScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Notebook_with_markdown_code_a_table_and_errors()
    {
        var window = Show(1280, 1000);
        var workspace = await OpenSampleAsync();
        workspace.IsBottomPanelVisible = false;
        var path = Path.Combine(workspace.StartupProject!.DirectoryPath, "Orders.ipynb");
        await NotebookSerializer.SaveAsync(
            NotebookDocument.Create(
                NotebookCell.Markdown("""
                    # Exploring orders

                    A **C# notebook** runs one cell at a time, and *variables carry over* from cell to cell.
                    See the [Polyglot Notebooks docs](https://github.com/dotnet/interactive).

                    - Run a cell with `Ctrl+Enter`, or `Shift+Enter` to move on
                    - Run everything with **Run All**
                    """),
                NotebookCell.Code("""
                    record Order(string Customer, string Item, decimal Total);

                    var orders = new List<Order>
                    {
                        new("Ada", "Tablet", 499m),
                        new("Alan", "Keyboard", 89.90m),
                        new("Grace", "Stylus", 39.50m),
                    };
                    Console.WriteLine($"{orders.Count} orders loaded");
                    """),
                NotebookCell.Code("orders.Where(o => o.Total > 20).OrderByDescending(o => o.Total)"),
                NotebookCell.Code("// No order is that large, so First throws.\norders.First(o => o.Total > 1000m)"),
                NotebookCell.Code("var average = orders.Average(o => o.Totl);\naverage")),
            path);
        await workspace.ReloadWorkspaceAsync();
        var document = await workspace.OpenDocumentAsync(path);
        workspace.Explorer.Reveal(path);
        var notebook = document!.Notebook!;

        await notebook.RunAllAsync();
        await notebook.RunCellAsync(notebook.Cells[4]);
        notebook.SelectedCell = notebook.Cells[2];
        await UiTestHelpers.WaitUntilAsync(() => notebook.Cells.All(IsColored), because: "syntax colors");

        UiTestHelpers.SaveScreenshot(window, "07-notebook");

        var view = window.GetVisualDescendants().OfType<NotebookView>().Single(v => v.IsEffectivelyVisible);
        view.FindControl<ScrollViewer>("Scroller")!.ScrollToEnd();
        notebook.OpenFind();
        notebook.FindText = "Totl";
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "08-notebook-compile-error");

        Assert.Equal(5, view.GetVisualDescendants().OfType<NotebookCellView>().Count());
        Assert.IsType<TableOutputViewModel>(Assert.Single(notebook.Cells[2].Outputs));
        Assert.IsType<ErrorOutputViewModel>(Assert.Single(notebook.Cells[3].Outputs));
        Assert.Contains(notebook.Cells[4].DiagnosticItems, d => d.Code == "CS1061");
        Assert.Equal("1 of 1", notebook.FindStatus);
        Assert.Same(notebook.Cells[4], notebook.SelectedCell);
        Assert.NotNull(view.EditorFor(notebook.Cells[1]));
    }

    [AvaloniaFact]
    public async Task The_sample_tour_notebook_on_a_portrait_tablet()
    {
        var window = Show(800, 1280);
        var workspace = await OpenSampleAsync();
        workspace.IsBottomPanelVisible = false;
        var document = await workspace.OpenDocumentAsync(Path.Combine(workspace.StartupProject!.DirectoryPath, "Tour.ipynb"));
        var notebook = document!.Notebook!;

        await notebook.RunAllAsync();
        await UiTestHelpers.WaitUntilAsync(() => notebook.Cells.All(IsColored), because: "syntax colors");

        UiTestHelpers.SaveScreenshot(window, "10-notebook-portrait");

        Assert.False(workspace.IsSideBarVisible);
        Assert.All(notebook.Cells.Where(c => c.IsCode), c => Assert.Equal(CellRunState.Succeeded, c.RunState));
    }

    [AvaloniaFact]
    public void The_start_screen_offers_a_notebook_template()
    {
        var window = Show(1280, 800);
        _main.Start.SelectCommand.Execute(StartAction.NewProject);
        _main.Start.SelectedTemplate = _main.Start.Templates.Single(t => t.Id == ProjectTemplates.NotebookId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        UiTestHelpers.SaveScreenshot(window, "09-new-notebook");

        Assert.True(_main.Start.CreateProjectCommand.CanExecute(null));
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    /// <summary>Whether a cell shows its syntax colors: C# cells with anything to color have been classified.</summary>
    private static bool IsColored(NotebookCellViewModel cell) =>
        !cell.IsCSharp || cell.Classifications.Count > 0 || SyntaxClassifier.Classify(cell.Source.Text).Count == 0;

    private MainWindow Show(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<WorkspaceViewModel> OpenSampleAsync()
    {
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        return _main.Workspace!;
    }
}
