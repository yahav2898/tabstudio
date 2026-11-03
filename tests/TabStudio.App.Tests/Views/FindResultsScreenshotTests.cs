using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>Screenshots of the Find Results panel: Replace in Files with its options and matches, beside References.</summary>
public sealed class FindResultsScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public FindResultsScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Replace_in_files_with_its_options_and_matches()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;

        workspace.ReplaceInFilesCommand.Execute(null);
        var find = workspace.FindResults;
        find.FindText = "Greeter";
        find.ReplaceText = "Welcomer";
        find.MatchCase = true;
        find.MatchWholeWord = true;
        await find.FindAllCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => find.Results.Rows.Count > 0, because: "the matches");

        UiTestHelpers.SaveScreenshot(window, "10-find-results");

        Assert.Equal(BottomPanel.FindResults, workspace.SelectedPanel);
        var view = window.GetVisualDescendants().OfType<FindResultsView>().Single();
        Assert.True(view.IsEffectivelyVisible);
        Assert.Contains(view.GetVisualDescendants().OfType<ReferencesView>(), r => r.IsEffectivelyVisible);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }
}
