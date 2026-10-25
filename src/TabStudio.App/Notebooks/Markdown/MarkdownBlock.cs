namespace TabStudio.App.Notebooks.Markdown;

/// <summary>How a run of Markdown text is styled. Styles combine, as in <c>***bold italic***</c>.</summary>
[Flags]
public enum MarkdownStyles
{
    /// <summary>Plain text.</summary>
    None = 0,

    /// <summary><c>**bold**</c>.</summary>
    Bold = 1,

    /// <summary><c>*italic*</c>.</summary>
    Italic = 2,

    /// <summary><c>`code`</c>.</summary>
    Code = 4,

    /// <summary><c>~~struck through~~</c>.</summary>
    Strikethrough = 8,
}

/// <summary>A run of text in one style. A run inside a link carries the link's destination.</summary>
/// <param name="Text">The text, with Markdown syntax removed. A <c>\n</c> is a hard line break.</param>
/// <param name="Style">How the text is styled.</param>
/// <param name="Destination">Where the link goes, or <see langword="null"/> when the run is not a link.</param>
public sealed record MarkdownSpan(string Text, MarkdownStyles Style = MarkdownStyles.None, string? Destination = null)
{
    /// <summary>Whether the run is part of a link.</summary>
    public bool IsLink => Destination is not null;
}

/// <summary>A block of a Markdown document: a heading, paragraph, list, code block, quote, rule or table.</summary>
public abstract record MarkdownBlock;

/// <summary>A heading, <c># Title</c> to <c>###### Title</c>.</summary>
/// <param name="Level">From 1 to 6.</param>
/// <param name="Spans">The heading text.</param>
public sealed record MarkdownHeading(int Level, IReadOnlyList<MarkdownSpan> Spans) : MarkdownBlock;

/// <summary>A paragraph of text.</summary>
/// <param name="Spans">The text.</param>
public sealed record MarkdownParagraph(IReadOnlyList<MarkdownSpan> Spans) : MarkdownBlock;

/// <summary>One item of a bulleted, numbered or task list.</summary>
/// <param name="Marker">What is shown before the item: a bullet, a number such as <c>2.</c>, or a check box.</param>
/// <param name="Depth">How deeply the item is nested; 0 for the outermost list.</param>
/// <param name="Spans">The item's text.</param>
public sealed record MarkdownListItem(string Marker, int Depth, IReadOnlyList<MarkdownSpan> Spans);

/// <summary>A list, with nested items flattened in order and marked by <see cref="MarkdownListItem.Depth"/>.</summary>
/// <param name="Items">The items.</param>
public sealed record MarkdownList(IReadOnlyList<MarkdownListItem> Items) : MarkdownBlock;

/// <summary>A fenced or indented code block.</summary>
/// <param name="Language">The language after the opening fence, such as <c>csharp</c>, or <see langword="null"/>.</param>
/// <param name="Code">The code, without the fences.</param>
public sealed record MarkdownCodeBlock(string? Language, string Code) : MarkdownBlock;

/// <summary>A block quote, <c>&gt; quoted</c>.</summary>
/// <param name="Blocks">The quoted blocks.</param>
public sealed record MarkdownQuote(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

/// <summary>A horizontal rule, <c>---</c>.</summary>
public sealed record MarkdownRule : MarkdownBlock;

/// <summary>A table, as GitHub writes them with pipes.</summary>
/// <param name="Header">The header cells.</param>
/// <param name="Rows">The body rows, each with its cells.</param>
public sealed record MarkdownTable(
    IReadOnlyList<IReadOnlyList<MarkdownSpan>> Header,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<MarkdownSpan>>> Rows) : MarkdownBlock;
