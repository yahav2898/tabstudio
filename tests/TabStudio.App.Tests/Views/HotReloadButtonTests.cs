using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TabStudio.App.Controls;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using TabStudio.Compilation.HotReload;

namespace TabStudio.App.Tests.Views;

/// <summary>The toolbar's Hot Reload button shows only where the runtime can apply edits.</summary>
public sealed class HotReloadButtonTests
{
    [AvaloniaFact]
    public async Task Hot_Reload_is_on_the_toolbar_only_where_edits_can_be_applied_and_names_its_shortcut()
    {
        // On Android the runtime cannot apply edits, and the button sat disabled in the toolbar of every project,
        // Java and Python ones too, with a tooltip about "Android 0.15".
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var button = window.GetLogicalDescendants().OfType<AdaptiveBarItem>().Single(item => item.Name == "HotReloadButton");

            Assert.Equal(HotReloadSession.IsSupported, button.IsVisible);
            Assert.Equal("Hot Reload (Alt+F10)", ToolTip.GetTip(button));
        }
        finally
        {
            window.Close();
        }
    }
}
