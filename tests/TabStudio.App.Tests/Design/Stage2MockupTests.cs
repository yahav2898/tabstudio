using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.Theming;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Search;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// The spec's mockups of stage 2 (docs/design/stage-2.md): the real IDE with the sample open, its shell rebuilt from the
/// new components (see <see cref="Stage2Shell"/>), in landscape and portrait, dark and light.
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class Stage2MockupTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public Stage2MockupTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Landscape_with_the_activity_bar_adaptive_bars_and_folded_Properties()
    {
        var window = await ShowSampleAsync(1280, 800);
        var shell = Stage2Shell.Apply(window, _main.Workspace!, new AppPaths(_temp.Path));
        Dispatcher.UIThread.RunJobs();

        Shoot(window, "mockup-01-landscape");

        Assert.True(shell.ActivityBar.Bounds.Height > 0);
        Assert.Empty(shell.ActivityBar.Overflowed);
    }

    [AvaloniaFact]
    public async Task The_search_over_the_IDE_finds_files_and_the_menus_commands()
    {
        var window = await ShowSampleAsync(1280, 800);
        var shell = Stage2Shell.Apply(window, _main.Workspace!, new AppPaths(_temp.Path));
        shell.Search.OpenCommand.Execute(null);
        shell.Search.Query = "gree";
        await shell.Search.Completion;

        Shoot(window, "mockup-02-search");

        Assert.Contains(shell.Search.Rows.OfType<SearchResultRow>(), row => row.Title == "Greeter.cs");
        shell.Search.Query = "build sol";
        await shell.Search.Completion;
        var build = Assert.IsType<SearchResultRow>(shell.Search.Rows[0]);
        Assert.Equal(("Build Solution", "Ctrl+Shift+B"), (build.Title, build.Item.Gesture));
        UiTestHelpers.SaveScreenshot(window, "mockup-03-search-commands-dark");
    }

    [AvaloniaFact]
    public async Task Portrait_keeps_the_activity_bar_and_opens_the_side_bar_as_a_drawer()
    {
        var window = await ShowSampleAsync(800, 1280);
        var shell = Stage2Shell.Apply(window, _main.Workspace!, new AppPaths(_temp.Path));
        Dispatcher.UIThread.RunJobs();

        Shoot(window, "mockup-04-portrait");

        shell.OpenDrawer();
        Dispatcher.UIThread.RunJobs();
        Shoot(window, "mockup-05-portrait-drawer");

        Assert.True(shell.ActivityBar.Bounds.Height > 0);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private static void Shoot(Window window, string name)
    {
        UiTestHelpers.SaveScreenshot(window, name + "-dark");
        using (ThemeScope.Light())
        {
            UiTestHelpers.SaveScreenshot(window, name + "-light");
        }

        Dispatcher.UIThread.RunJobs();
        StretchedText.AssertNone(window);
    }

    private async Task<Window> ShowSampleAsync(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var document = _main.Workspace!.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");
        return window;
    }
}
