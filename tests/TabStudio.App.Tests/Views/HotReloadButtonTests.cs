using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TabStudio.App.Controls;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using TabStudio.Compilation.HotReload;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// The shell's bars: the toolbar's Hot Reload button shows only where the runtime can apply edits, and the status
/// bar's messages trim at whole words, as the design asks ("…Merge Confli…" was cut mid-word).
/// </summary>
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

            foreach (var name in new[] { "StatusMessageText", "OperationsText" })
            {
                var text = window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == name);
                Assert.Equal(Avalonia.Media.TextTrimming.WordEllipsis, text.TextTrimming);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_folder_with_nothing_to_run_has_no_Run_on_item_and_a_solution_names_its_host()
    {
        // The toolbar said "Run on Nothing to run" for a folder of notes.
        using var temp = new TempDirectory();
        temp.Write("notes/todo.txt", "Buy milk\n");
        using var folder = new WorkspaceFixture();
        await folder.Workspace.LoadAsync(temp.Resolve("notes"));
        Assert.Equal("Nothing to run", folder.Workspace.RunHostText);
        Assert.False(folder.Workspace.HasRunHost);

        using var sample = new WorkspaceFixture();
        await sample.OpenSampleAsync();
        Assert.True(sample.Workspace.HasRunHost);
    }
}
