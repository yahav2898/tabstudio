using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>How far the text an item replaces reaches when it is committed.</summary>
internal enum XamlWordKind
{
    /// <summary>A XAML name: letters, digits, <c>_</c>, <c>.</c>, <c>:</c> and <c>-</c>.</summary>
    Name,

    /// <summary>An attribute's whole value, up to its closing quote.</summary>
    Value,

    /// <summary>A part of a markup extension, up to the next <c>,</c>, <c>=</c>, <c>}</c>, space or quote.</summary>
    ExtensionPart,
}

/// <summary>An item of a XAML completion list, with what committing it writes.</summary>
/// <param name="DisplayText">What the list shows, and what the typed text is matched against.</param>
/// <param name="Glyph">Its icon.</param>
/// <param name="InsertText">What committing it writes over the word.</param>
internal sealed record XamlCompletionItem(string DisplayText, SymbolGlyph Glyph, string InsertText)
{
    /// <summary>The note beside it in the list, such as a property's type.</summary>
    public string? Description { get; init; }

    /// <summary>The type or member it stands for, whose documentation its tooltip shows.</summary>
    public ISymbol? Symbol { get; init; }

    /// <summary>How many characters before the end of <see cref="InsertText"/> the caret goes, such as 1 inside <c>Name=""</c>.</summary>
    public int CaretFromEnd { get; init; }

    /// <summary>What the typed text is matched against, when not its text.</summary>
    public string? FilterText { get; init; }

    /// <summary>What it sorts by, when not its text.</summary>
    public string? SortText { get; init; }

    /// <summary>Whether the list opens with it selected.</summary>
    public bool IsPreselected { get; init; }

    /// <summary>How far the text it replaces reaches.</summary>
    public XamlWordKind Word { get; init; }

    /// <summary>Whether committing it writes <c>=""</c> after it, unless an <c>=</c> follows already.</summary>
    public bool IsAttributeName { get; init; }
}
