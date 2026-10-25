using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// Text in a fixed height is centered in it, never stretched to it (see <see cref="StretchedText"/>): on the Start
/// screen, and in the IDE with each tool window open in turn.
/// </summary>
public sealed class CenteredTextTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public CenteredTextTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public void The_start_screen_centers_its_text()
    {
        var window = Show();

        StretchedText.AssertNone(window);
    }

    [AvaloniaFact]
    public async Task The_IDE_centers_its_text_in_every_tool_window()
    {
        var window = Show();
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;
        Dispatcher.UIThread.RunJobs();

        var found = new List<string>(StretchedText.In(window));
        foreach (var panel in new[] { SideBarPanel.GitChanges, SideBarPanel.TestExplorer, SideBarPanel.Agent, SideBarPanel.Actions, SideBarPanel.PullRequests, SideBarPanel.SolutionExplorer })
        {
            workspace.SelectSideBarPanelCommand.Execute(panel);
            Dispatcher.UIThread.RunJobs();
            found.AddRange(StretchedText.In(window).Select(text => $"{panel}: {text}"));
        }

        foreach (var panel in new[] { BottomPanel.Output, BottomPanel.Compatibility, BottomPanel.References, BottomPanel.ErrorList })
        {
            workspace.SelectPanelCommand.Execute(panel);
            Dispatcher.UIThread.RunJobs();
            found.AddRange(StretchedText.In(window).Select(text => $"{panel}: {text}"));
        }

        StretchedText.AssertNone(found);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private MainWindow Show()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
