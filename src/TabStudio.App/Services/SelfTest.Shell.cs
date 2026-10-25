using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Search;
using TabStudio.App.Views;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's look at the shell of docs/design/stage-2.md on the device: the activity bar marks the side bar's
/// tool window, the search finds a command of the menus by its name, and the toolbar and the status bar fit the screen,
/// with Run in the toolbar and nothing past the screen's edge.
/// </summary>
public static partial class SelfTest
{
    /// <summary>How long the search may take to answer on the device.</summary>
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Checks the shell of the IDE shown in <paramref name="root"/>.</summary>
    /// <returns>What went wrong, or <see langword="null"/> with what was seen.</returns>
    private static async Task<(string? Problem, string Seen)> CheckShellAsync(Visual root, WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        if (root.GetVisualDescendants().OfType<WorkspaceView>().FirstOrDefault(view => view.IsEffectivelyVisible) is not { } shell)
        {
            return ("the IDE's shell is not shown.", string.Empty);
        }

        var activity = shell.GetVisualDescendants().OfType<ActivityBar>().FirstOrDefault(bar => bar.IsEffectivelyVisible);
        var shown = workspace.Activity.Items.Count(item => item.IsAvailable);
        if (activity is null || shown < 5 || workspace.Activity.SelectedItem?.Id != workspace.SelectedSideBarPanel.ToString())
        {
            return ($"the activity bar is missing or out of step ({shown} tool windows, {workspace.Activity.SelectedItem?.Id ?? "none"} marked).", string.Empty);
        }

        var width = shell.Bounds.Width;
        foreach (var bar in shell.GetVisualDescendants().OfType<AdaptiveBar>().Where(bar => bar.Name is "Toolbar" or "StatusBar"))
        {
            var origin = bar.TranslatePoint(default, shell);
            if (origin is not { } point || point.X + bar.Bounds.Width > width + 0.5)
            {
                return ($"the {bar.Name} reaches past the screen's {width:0} pixels.", string.Empty);
            }
        }

        if (shell.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "RunButton") is not { IsEffectivelyVisible: true })
        {
            return ("the Run button is not in the toolbar.", string.Empty);
        }

        // One search for everything: the menus' commands, by name. The search is not opened, as its query box would
        // bring up the on-screen keyboard over the IDE the design step looks at next.
        var search = workspace.Search;
        search.Query = "build solution";
        var found = await WaitForAsync(
            () => search.Completion.IsCompleted && search.Rows.OfType<SearchResultRow>().FirstOrDefault()?.Title == "Build Solution",
            SearchTimeout,
            cancellationToken);
        var rows = search.Rows.Count;
        search.Query = string.Empty;
        if (!found)
        {
            return ($"the search did not find Build Solution (it shows {rows} rows).", string.Empty);
        }

        return (null, string.Create(CultureInfo.InvariantCulture, $"the activity bar with {shown} tool windows, the search found Build Solution, the bars fit {width:0} pixels"));
    }
}
