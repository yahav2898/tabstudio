using System.Text.Json;

namespace TabStudio.LanguageServers.Features;

/// <summary>An item of a completion list.</summary>
/// <remarks>
/// Servers often leave out the documentation and the edits that add imports until the item is resolved; the
/// document's <c>ResolveCompletionAsync</c> fills them in. The item keeps the JSON the server sent, which goes back
/// to the server when it is resolved.
/// </remarks>
public sealed class LspCompletionItem
{
    internal LspCompletionItem(JsonElement raw, int snapshotVersion)
    {
        Raw = raw;
        SnapshotVersion = snapshotVersion;
    }

    /// <summary>The text shown in the list.</summary>
    public required string Label { get; init; }

    /// <summary>Text shown right after the label, such as a function's parameters.</summary>
    public string? LabelDetail { get; init; }

    /// <summary>Text shown at the right of the row, such as the module a name comes from.</summary>
    public string? LabelDescription { get; init; }

    /// <summary>More about the item, such as its type or signature.</summary>
    public string? Detail { get; init; }

    /// <summary>What the item is, for its icon.</summary>
    public LspCompletionItemKind Kind { get; init; }

    /// <summary>Its documentation, if any.</summary>
    public MarkupText? Documentation { get; init; }

    /// <summary>The text matched against what the user types.</summary>
    public required string FilterText { get; init; }

    /// <summary>The text the list is sorted by.</summary>
    public required string SortText { get; init; }

    /// <summary>Whether the item should be selected when the list opens.</summary>
    public bool IsPreselected { get; init; }

    /// <summary>Whether the item is deprecated, shown struck through.</summary>
    public bool IsDeprecated { get; init; }

    /// <summary>The characters that commit the item when typed, such as <c>(</c> and <c>.</c>.</summary>
    public IReadOnlyList<char> CommitCharacters { get; init; } = [];

    /// <summary>What committing the item inserts.</summary>
    public required LspCompletionEdit Edit { get; init; }

    /// <summary>Edits elsewhere in the document, such as an import the item needs.</summary>
    public IReadOnlyList<LspTextEdit> AdditionalEdits { get; init; } = [];

    /// <summary>Whether the server asks to run a command after committing the item.</summary>
    public bool HasCommand { get; init; }

    /// <summary>The JSON the server sent, for <c>completionItem/resolve</c>.</summary>
    internal JsonElement Raw { get; }

    /// <summary>The document version the list was computed for.</summary>
    internal int SnapshotVersion { get; }
}
