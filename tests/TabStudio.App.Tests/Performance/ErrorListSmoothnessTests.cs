using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// The Error List of a large solution, shown: 3,000 warnings and errors in 150 files. While a person types, live
/// analysis replaces one file's diagnostics after every pause, which changes a row or two; the list must follow without
/// rebuilding every row. Writes <c>smoothness-error-list.md</c>.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class ErrorListSmoothnessTests
{
    private const int Files = 150;
    private const int PerFile = 20;
    private const string Root = "/work/Big";

    [AvaloniaFact]
    public void One_files_new_diagnostics_update_a_long_Error_List_without_rebuilding_it()
    {
        if (Smoothness.IsLightPass)
        {
            return;
        }

        var errors = new ErrorListViewModel();
        var window = new Window { Width = 1280, Height = 420, Content = new ErrorListView { DataContext = errors } };
        window.Show();
        var all = Enumerable.Range(0, Files).SelectMany(f => FileDiagnostics(f, extra: false)).ToList();

        var load = Smoothness.Measure(window, () => errors.SetProjectDiagnostics(Root, all));
        Assert.Equal(Files * PerFile, errors.Items.Count);

        // Typing in one file: its analysis adds an error, then the next one takes it away again.
        var notifications = 0;
        void Count(object? sender, NotifyCollectionChangedEventArgs e) => notifications++;
        errors.Items.CollectionChanged += Count;
        var typing = new List<UiCost>();
        var file = FilePath(Files / 2);
        for (var i = 0; i < 30; i++)
        {
            var extra = i % 2 == 0;
            typing.Add(Smoothness.Measure(window, () => errors.SetFileDiagnostics(file, FileDiagnostics(Files / 2, extra))));
        }

        errors.Items.CollectionChanged -= Count;
        var perUpdate = notifications / 30.0;
        Assert.Equal(Files * PerFile, errors.Items.Count);

        // Before 0.21.7, the same update cleared the list and added every row back, one notification each: done here
        // to the same list, with the rows the update makes, for the comparison.
        var rows = errors.Items.ToList();
        var before = new List<UiCost>();
        for (var i = 0; i < 10; i++)
        {
            before.Add(Smoothness.Measure(window, () =>
            {
                errors.Items.Clear();
                foreach (var row in rows)
                {
                    errors.Items.Add(row);
                }
            }));
        }

        var filters = new List<UiCost>();
        for (var i = 0; i < 4; i++)
        {
            filters.Add(Smoothness.Measure(window, () => errors.ShowWarnings = !errors.ShowWarnings));
        }

        var search = new List<UiCost>();
        foreach (var text in new[] { "T", "TS", "TS0", "TS01", "TS0", "TS", "T", string.Empty })
        {
            search.Add(Smoothness.Measure(window, () => errors.SearchText = text));
        }

        var report = new StringBuilder("# Error List with 3,000 rows\n\n");
        var culture = CultureInfo.InvariantCulture;
        report.AppendLine(culture, $"- {Files} files, {PerFile} diagnostics each, shown in a 1280 × 420 window.");
        report.AppendLine(culture, $"- Every cost is the UI thread's, from the change to the end of the frame that shows it.\n");
        report.AppendLine("| Action | Cost |");
        report.AppendLine("| --- | --- |");
        report.AppendLine(culture, $"| The solution's diagnostics arrive | {load} |");
        report.AppendLine(culture, $"| One file's analysis adds or removes an error (30 times) | {Smoothness.Spread(typing)}; {perUpdate:0.#} list notifications each |");
        report.AppendLine(culture, $"| The same update as before 0.21.7: the list cleared and every row added back (10 times) | {Smoothness.Spread(before)}; {rows.Count + 1} list notifications each |");
        report.AppendLine(culture, $"| Warnings filter on and off | {Smoothness.Spread(filters)} |");
        report.AppendLine(culture, $"| Typing a search, letter by letter, and erasing it | {Smoothness.Spread(search)} |");
        Smoothness.Write("error-list", report.ToString());

        // Before 0.21.7 every such update cleared the list and added its 3,000 rows back one notification at a time.
        Assert.True(perUpdate <= 2, $"one file's new analysis told the list about {perUpdate:0.#} changes, not the one row that changed");
        window.Close();
    }

    private static string FilePath(int file) => string.Create(CultureInfo.InvariantCulture, $"{Root}/Folder{file % 10}/File{file}.cs");

    private static List<BuildDiagnostic> FileDiagnostics(int file, bool extra)
    {
        var path = FilePath(file);
        var diagnostics = Enumerable.Range(0, PerFile)
            .Select(i => new BuildDiagnostic(
                string.Create(CultureInfo.InvariantCulture, $"TS{i % 40:000}"),
                i % 7 == 0 ? DiagnosticSeverityLevel.Error : DiagnosticSeverityLevel.Warning,
                string.Create(CultureInfo.InvariantCulture, $"Diagnostic {i} of file {file}: something to look at in this line of code"),
                new DiagnosticLocation(path, i * 100, 5, i * 3 + 1, 5),
                "Big"))
            .ToList();
        if (extra)
        {
            diagnostics.Add(new BuildDiagnostic("CS1002", DiagnosticSeverityLevel.Error, "; expected", new DiagnosticLocation(path, 450, 1, 16, 9), "Big"));
        }

        return diagnostics;
    }
}
