using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// A folder of 5,000 files in Solution Explorer, shown: one folder of 3,000 files and 40 of 50. Expanding and
/// collapsing the big folder and typing a search must each take a moment, not a pause. Writes
/// <c>smoothness-solution-explorer.md</c>.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class SolutionExplorerSmoothnessTests
{
    [AvaloniaFact]
    public async Task A_folder_of_thousands_of_files_expands_and_searches_without_a_pause()
    {
        if (Smoothness.IsLightPass)
        {
            return;
        }

        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "Notes");
        var big = Path.Combine(root, "big");
        Directory.CreateDirectory(big);
        for (var i = 0; i < 3000; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(big, string.Create(CultureInfo.InvariantCulture, $"note{i:0000}.txt")), "A note.\n");
        }

        for (var folder = 0; folder < 40; folder++)
        {
            var path = Path.Combine(root, string.Create(CultureInfo.InvariantCulture, $"topic{folder:00}"));
            Directory.CreateDirectory(path);
            for (var i = 0; i < 50; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(path, string.Create(CultureInfo.InvariantCulture, $"page{i:00}.md")), "# A page\n");
            }
        }

        using var fixture = new WorkspaceFixture();
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        Smoothness.Settle(window);
        using var stalls = new UiStallMonitor(Dispatcher.UIThread, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(4)) { Phase = "opening the folder" };
        var clock = Stopwatch.StartNew();
        await fixture.Workspace.LoadAsync(root);
        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.State != TabStudio.App.ViewModels.IdeState.Busy, TimeSpan.FromMinutes(2), "the folder");
        Smoothness.Settle(window);
        var opened = clock.Elapsed;
        stalls.Dispose();

        var explorer = fixture.Workspace.Explorer;
        var row = explorer.Rows.First(r => r.Node.Name == "big");
        var resets = 0;
        explorer.Rows.CollectionChanged += (_, e) => resets += e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ? 1 : 0;
        var expanding = new List<UiCost>();
        var collapsing = new List<UiCost>();
        for (var i = 0; i < 4; i++)
        {
            expanding.Add(Smoothness.Measure(window, () => explorer.Toggle(row)));
            Assert.True(explorer.Rows.Count > 3000);
            collapsing.Add(Smoothness.Measure(window, () => explorer.Toggle(row)));
        }

        // Before 0.21.11 each toggle reset the list and made every row again.
        Assert.Equal(0, resets);
        explorer.Toggle(row);
        var first = explorer.Rows[explorer.Rows.IndexOf(row) + 1];
        explorer.Toggle(row);
        explorer.Toggle(row);
        Assert.Same(first, explorer.Rows[explorer.Rows.IndexOf(row) + 1]);
        explorer.Toggle(row);

        var search = new List<UiCost>();
        foreach (var text in new[] { "n", "no", "not", "note", "note1", "note12", "note1", "note", "not", "no", "n", string.Empty })
        {
            search.Add(Smoothness.Measure(window, () => explorer.SearchText = text));
        }

        var report = new StringBuilder("# Solution Explorer with 5,000 files\n\n");
        var culture = CultureInfo.InvariantCulture;
        report.AppendLine(culture, $"- One folder of 3,000 files and 40 folders of 50, opened as a folder in the IDE's window at 1280 × 800.");
        report.AppendLine(culture, $"- Opened in {opened.TotalSeconds:0.00} s; the UI thread meanwhile: {stalls.Summary()}.\n");
        report.AppendLine("| Action | Cost |");
        report.AppendLine("| --- | --- |");
        report.AppendLine(culture, $"| Expanding the folder of 3,000 files | {Smoothness.Spread(expanding)} |");
        report.AppendLine(culture, $"| Collapsing it | {Smoothness.Spread(collapsing)} |");
        report.AppendLine(culture, $"| Typing a search, letter by letter, and erasing it | {Smoothness.Spread(search)} |");
        report.AppendLine().Append(stalls.Table(8));
        Smoothness.Write("solution-explorer", report.ToString());
        window.Close();
    }
}
