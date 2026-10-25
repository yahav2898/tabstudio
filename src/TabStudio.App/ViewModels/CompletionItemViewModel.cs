using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Editing;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>One row of the completion list: its icon, its text with the typed letters in bold, and a note.</summary>
public sealed partial class CompletionItemViewModel : ObservableObject
{
    /// <summary>Creates the row for an item.</summary>
    /// <param name="entry">The item from the language service.</param>
    /// <param name="order">Its position in the sorted list, which breaks ties between equally good matches.</param>
    public CompletionItemViewModel(CompletionEntry entry, int order)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
        Order = order;
        Runs = [new StyledRun(DisplayText)];
    }

    /// <summary>The item, for committing it and for its description.</summary>
    public CompletionEntry Entry { get; }

    /// <summary>The position of the item in the sorted list.</summary>
    public int Order { get; }

    /// <summary>The icon.</summary>
    public SymbolGlyph Kind => Entry.Kind;

    /// <summary>The text shown in the list, such as <c>List&lt;&gt;</c>.</summary>
    public string DisplayText => Entry.DisplayTextPrefix + Entry.DisplayText + Entry.DisplayTextSuffix;

    /// <summary>A note at the right of the row, such as the namespace of a type that is not imported yet.</summary>
    public string InlineDescription => Entry.InlineDescription;

    /// <summary>How the item matches the typed text, or <see langword="null"/> when it does not.</summary>
    public CompletionMatch? Match { get; private set; }

    /// <summary>The text of the row, with the matched letters in bold.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StyledRun> Runs { get; private set; }

    /// <summary>Matches the item against what the user typed and updates the bold letters.</summary>
    /// <param name="typed">The text from the start of the word to the caret.</param>
    /// <returns>Whether the item matches and should be listed.</returns>
    public bool Filter(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        Match = CompletionMatcher.Match(typed, Entry.FilterText);
        if (Match is null)
        {
            return false;
        }

        // The bold letters are positions in the filter text; they apply to the display text when both are the same.
        var highlight = string.Equals(Entry.FilterText, Entry.DisplayText, StringComparison.Ordinal)
            ? Match
            : CompletionMatcher.Match(typed, Entry.DisplayText);
        var prefix = Entry.DisplayTextPrefix.Length;
        var bold = highlight?.MatchedIndices.Select(i => i + prefix).ToArray() ?? [];
        Runs = StyledRun.WithBold(DisplayText, bold);
        return true;
    }
}
