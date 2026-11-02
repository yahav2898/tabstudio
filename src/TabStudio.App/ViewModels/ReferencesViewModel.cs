using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>A row of the references panel: either a file heading or one reference.</summary>
public abstract class ReferenceRow
{
}

/// <summary>The heading of the references in one file.</summary>
/// <param name="fileName">The file name.</param>
/// <param name="folder">The folder, relative to the workspace.</param>
/// <param name="count">How many references the file has.</param>
/// <param name="noun">What is counted, singular and plural: references, or matches for Find in Files.</param>
public sealed class ReferenceFileRow(string fileName, string folder, int count, (string One, string Many)? noun = null) : ReferenceRow
{
    /// <summary>The file name.</summary>
    public string FileName { get; } = fileName;

    /// <summary>The folder, relative to the workspace.</summary>
    public string Folder { get; } = folder;

    /// <summary>"1 reference" or "3 references".</summary>
    public string CountText { get; } = count == 1 ? $"1 {(noun ?? References).One}" : $"{count} {(noun ?? References).Many}";

    /// <summary>What Find All References counts.</summary>
    internal static readonly (string One, string Many) References = ("reference", "references");
}

/// <summary>One reference: its line, with the reference in bold.</summary>
/// <param name="item">The reference.</param>
/// <param name="text">The line, trimmed, with the reference in bold.</param>
public sealed class ReferenceLineRow(ReferenceItem item, IReadOnlyList<StyledRun> text) : ReferenceRow
{
    /// <summary>The reference.</summary>
    public ReferenceItem Item { get; } = item;

    /// <summary>The line, with the reference in bold.</summary>
    public IReadOnlyList<StyledRun> Text { get; } = text;

    /// <summary>The 1-based line number, as text.</summary>
    public string Line { get; } = item.Line.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Whether this is where the symbol is declared.</summary>
    public bool IsDefinition => Item.IsDefinition;
}

/// <summary>
/// The Find All References panel (Shift+F12): every reference to a symbol, grouped by file, with the line of each
/// and the reference in bold. Tapping a reference opens it. Go to Implementation also lists its results here when
/// there is more than one.
/// </summary>
public sealed partial class ReferencesViewModel : ObservableObject
{
    private readonly Func<string, int, int, Task> _navigate;

    /// <summary>Creates the panel.</summary>
    /// <param name="navigate">Opens a file at a 1-based line and column.</param>
    public ReferencesViewModel(Func<string, int, int, Task> navigate)
    {
        ArgumentNullException.ThrowIfNull(navigate);
        _navigate = navigate;
    }

    /// <summary>What was searched for, such as <c>'Greet' references</c>.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "Find All References";

    /// <summary>A summary under the title, such as <c>3 references in 2 files</c>.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "Press Shift+F12 on a symbol to list everywhere it is used.";

    /// <summary>Whether a search is running.</summary>
    [ObservableProperty]
    public partial bool IsSearching { get; private set; }

    /// <summary>The file headings and references, in file and position order.</summary>
    public RangeObservableCollection<ReferenceRow> Rows { get; } = [];

    /// <summary>The references of the last search.</summary>
    public IReadOnlyList<ReferenceItem> Items { get; private set; } = [];

    /// <summary>Clears the list and shows that a search is running.</summary>
    /// <param name="title">What is searched for.</param>
    public void BeginSearch(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        Title = title;
        Summary = "Searching…";
        IsSearching = true;
        Items = [];
        Rows.ReplaceAll([]);
    }

    /// <summary>Shows the results of a search.</summary>
    /// <param name="title">What was searched for.</param>
    /// <param name="items">The results, sorted by file and position.</param>
    /// <param name="workspaceRoot">The workspace folder, to show folders relative to it.</param>
    /// <param name="noun">What the results are, singular and plural; references when <see langword="null"/>.</param>
    /// <param name="emptyText">What the summary says when there are none.</param>
    public void Show(string title, IReadOnlyList<ReferenceItem> items, string workspaceRoot, (string One, string Many)? noun = null, string? emptyText = null)
    {
        var (one, many) = noun ?? ReferenceFileRow.References;
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(workspaceRoot);

        Title = title;
        IsSearching = false;
        Items = items;
        var rows = new List<ReferenceRow>();
        var files = items.GroupBy(i => i.FilePath, StringComparer.Ordinal).ToList();
        foreach (var file in files)
        {
            var folder = Path.GetDirectoryName(file.Key) is { } directory ? Path.GetRelativePath(workspaceRoot, directory) : string.Empty;
            rows.Add(new ReferenceFileRow(Path.GetFileName(file.Key), folder == "." ? string.Empty : folder, file.Count(), (one, many)));
            rows.AddRange(file.Select(item => new ReferenceLineRow(item, Highlight(item))));
        }

        Rows.ReplaceAll(rows);
        Summary = items.Count switch
        {
            0 => emptyText ?? $"No {many} found.",
            1 => $"1 {one}",
            _ => files.Count == 1 ? $"{items.Count} {many}" : $"{items.Count} {many} in {files.Count} files",
        };
    }

    /// <summary>Opens a reference.</summary>
    /// <param name="row">The row that was tapped.</param>
    [RelayCommand]
    private async Task NavigateAsync(ReferenceRow? row)
    {
        if (row is ReferenceLineRow line)
        {
            await _navigate(line.Item.FilePath, line.Item.Line, line.Item.Column);
        }
    }

    /// <summary>The line without its indentation, split around the reference so it can be bold.</summary>
    private static IReadOnlyList<StyledRun> Highlight(ReferenceItem item)
    {
        var text = item.LineText.TrimEnd();
        var indent = text.Length - text.TrimStart().Length;
        var start = Math.Clamp(item.Column - 1, indent, text.Length);
        var end = Math.Clamp(start + item.Length, start, text.Length);
        return
        [
            new StyledRun(text[indent..start]),
            new StyledRun(text[start..end], IsBold: true),
            new StyledRun(text[end..]),
        ];
    }
}
