using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Services.Search;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels.Search;

/// <summary>A row of the search's results: a result, or the row that shows the rest of a group.</summary>
public abstract class SearchRow
{
    /// <summary>The heading of the group this row starts, such as "Commands", or <see langword="null"/> inside a group.</summary>
    public string? Group { get; init; }

    /// <summary>Whether the row starts a group.</summary>
    public bool StartsGroup => Group is not null;
}

/// <summary>A result: what it is, its name with the matched letters, where it is, and its key gesture.</summary>
public sealed partial class SearchResultRow : SearchRow
{
    /// <summary>Creates the row.</summary>
    /// <param name="ranked">The ranked item.</param>
    /// <param name="isBestMatch">Whether it is the best match of all, shown first and larger.</param>
    public SearchResultRow(RankedItem ranked, bool isBestMatch = false)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        Ranked = ranked;
        IsBestMatch = isBestMatch;
        Keys = ranked.Item.Gesture is { } gesture ? SplitGesture(gesture) : [];
    }

    /// <summary>The ranked item.</summary>
    public RankedItem Ranked { get; }

    /// <summary>The item.</summary>
    public SearchItem Item => Ranked.Item;

    /// <summary>Its name.</summary>
    public string Title => Item.Title;

    /// <summary>Where it is or what it does.</summary>
    public string? Detail => Item.Detail;

    /// <summary>Whether there is a detail line.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Item.Detail);

    /// <summary>A symbol's glyph, or <see cref="SymbolGlyph.Other"/> for other results.</summary>
    public SymbolGlyph Glyph => Item.Symbol ?? SymbolGlyph.Other;

    /// <summary>Whether the result is a symbol, drawn with its IntelliSense glyph.</summary>
    public bool HasGlyph => Item.Symbol is not null;

    /// <summary>Whether the result has a note, such as "Current".</summary>
    public bool HasNote => !string.IsNullOrEmpty(Item.Note);

    /// <summary>Whether the result cannot be opened now, drawn dimmed.</summary>
    public bool IsDisabled => !Item.IsEnabled;

    /// <summary>The matched letters of the title.</summary>
    public IReadOnlyList<int> Highlights => Ranked.Highlights;

    /// <summary>Whether it is the best match of all.</summary>
    public bool IsBestMatch { get; }

    /// <summary>The key gesture as key caps, such as "Ctrl", "Shift", "B"; a chord's parts are separated by ",".</summary>
    public IReadOnlyList<string> Keys { get; }

    /// <summary>Whether it has a key gesture.</summary>
    public bool HasKeys => Keys.Count > 0;

    /// <summary>What kind of result it is, in words, shown on the best match.</summary>
    public string KindName => Item.Kind switch
    {
        SearchKind.File => "File",
        SearchKind.Command => "Command",
        SearchKind.Setting => "Setting",
        SearchKind.Symbol => "Symbol",
        SearchKind.Template => "New project",
        _ => "Recent",
    };

    /// <summary>Splits a gesture such as "Ctrl+Shift+B" or "Ctrl+R, A" into key caps.</summary>
    /// <param name="gesture">The gesture.</param>
    public static IReadOnlyList<string> SplitGesture(string gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        var keys = new List<string>();
        var chords = gesture.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < chords.Length; i++)
        {
            if (i > 0)
            {
                keys.Add(",");
            }

            // "Ctrl++" is Ctrl and the plus key.
            keys.AddRange(KeyParts().Matches(chords[i]).Select(match => match.Value));
        }

        return keys;
    }

    [GeneratedRegex(@"[^+]+|\+(?=$)")]
    private static partial Regex KeyParts();
}

/// <summary>The row that shows the rest of a group's results, by switching to its scope.</summary>
/// <param name="kind">The group's kind.</param>
/// <param name="count">How many results it has in all.</param>
public sealed class SearchMoreRow(SearchKind kind, int count) : SearchRow
{
    /// <summary>The group's kind.</summary>
    public SearchKind Kind { get; } = kind;

    /// <summary>How many results the group has.</summary>
    public int Count { get; } = count;

    /// <summary>The row's text, such as "Show all 12 files".</summary>
    public string Text => $"Show all {Count} {UnifiedSearchViewModel.PluralName(Kind)}";
}

/// <summary>One of the scope chips above the results: All, Files, Commands, Settings, Symbols, Templates, Recent.</summary>
/// <param name="kind">What the chip shows, or <see langword="null"/> for everything.</param>
/// <param name="label">Its label.</param>
public sealed partial class SearchScopeChip(SearchKind? kind, string label) : ObservableObject
{
    /// <summary>What the chip shows, or <see langword="null"/> for everything.</summary>
    public SearchKind? Kind { get; } = kind;

    /// <summary>Its label.</summary>
    public string Label { get; } = label;

    /// <summary>Whether it is the chosen scope.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>How many results it has for the query, or <see langword="null"/> when not counted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    public partial int? Count { get; set; }

    /// <summary>The count as the chip shows it.</summary>
    public string? CountText => Count is { } count && count > 0 ? count.ToString(CultureInfo.CurrentCulture) : null;
}
