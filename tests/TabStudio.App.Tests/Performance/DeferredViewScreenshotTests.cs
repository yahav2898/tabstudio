using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// The views that are built only when first shown, shown and audited in both themes' runs: a view that is not in the
/// window is not in its screenshot, so each is opened here, as a person opens it.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class DeferredViewScreenshotTests
{
    [AvaloniaFact]
    public async Task The_Watch_and_Serial_Monitor_panels_are_built_and_shown_when_chosen()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        try
        {
            fixture.Workspace.SelectPanelCommand.Execute(BottomPanel.Watch);
            UiTestHelpers.SaveScreenshot(window, "deferred-01-watch");
            Assert.True(window.GetVisualDescendants().OfType<WatchView>().Single().IsEffectivelyVisible);

            fixture.Workspace.SelectPanelCommand.Execute(BottomPanel.SerialMonitor);
            UiTestHelpers.SaveScreenshot(window, "deferred-02-serial-monitor");
            Assert.True(window.GetVisualDescendants().OfType<TabStudio.App.Views.Toolchains.SerialMonitorView>().Single().IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Live_join_dialog_is_built_and_shown_when_it_opens()
    {
        using var temp = new TempDirectory();
        await using var services = AppServices.Create(new AppPaths(Path.Combine(temp.Path, "app")));
        var main = services.GetRequiredService<MainViewModel>();
        var window = new Window { Width = 1280, Height = 800, Content = new MainView { DataContext = main } };
        window.Show();
        try
        {
            Assert.NotNull(main.LiveJoin);
            main.LiveJoin!.Open();
            UiTestHelpers.SaveScreenshot(window, "deferred-03-live-join");
            Assert.True(window.GetVisualDescendants().OfType<TabStudio.App.Views.Live.LiveJoinView>().Single().IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }
}
