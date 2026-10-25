using TabStudio.App.Notebooks.Markdown;

namespace TabStudio.App.Tests.Notebooks;

public sealed class MarkdownParserTests
{
    [Fact]
    public void Reads_headings_paragraphs_and_rules()
    {
        var blocks = MarkdownParser.Parse("# Sales\n\nNumbers for\nOctober.\n\n---\n\nTotals\n------\n### Small #");

        Assert.Collection(
            blocks,
            b => Assert.Equal((1, "Sales"), Heading(b)),
            b => Assert.Equal("Numbers for October.", Text(((MarkdownParagraph)b).Spans)),
            b => Assert.IsType<MarkdownRule>(b),
            b => Assert.Equal((2, "Totals"), Heading(b)),
            b => Assert.Equal((3, "Small"), Heading(b)));
    }

    [Fact]
    public void Reads_emphasis_code_strikethrough_and_links()
    {
        var spans = MarkdownInlineParser.Parse("Some **bold**, *italic*, `code`, ~~old~~ and [docs](https://learn.microsoft.com \"title\").");

        Assert.Contains(spans, s => s.Text == "bold" && s.Style == MarkdownStyles.Bold);
        Assert.Contains(spans, s => s.Text == "italic" && s.Style == MarkdownStyles.Italic);
        Assert.Contains(spans, s => s.Text == "code" && s.Style == MarkdownStyles.Code);
        Assert.Contains(spans, s => s.Text == "old" && s.Style == MarkdownStyles.Strikethrough);
        Assert.Contains(spans, s => s.Text == "docs" && s.Destination == "https://learn.microsoft.com");
        Assert.Equal("Some bold, italic, code, old and docs.", MarkdownInlineParser.ToPlainText(spans));
    }

    [Fact]
    public void Leaves_snake_case_math_and_unmatched_markers_alone()
    {
        var spans = MarkdownInlineParser.Parse("snake_case_name, 2 * 3 * 4 and a lone **star");

        Assert.Equal("snake_case_name, 2 * 3 * 4 and a lone **star", MarkdownInlineParser.ToPlainText(spans));
        Assert.All(spans, s => Assert.Equal(MarkdownStyles.None, s.Style));
    }

    [Fact]
    public void Reads_escapes_entities_html_breaks_and_bare_links()
    {
        var spans = MarkdownInlineParser.Parse("\\*not italic\\* &amp; more<br>See https://github.com/yahav2898/tabstudio.");

        Assert.Equal("*not italic* & more\nSee https://github.com/yahav2898/tabstudio.", MarkdownInlineParser.ToPlainText(spans));
        Assert.Contains(spans, s => s.Destination == "https://github.com/yahav2898/tabstudio");
    }

    [Fact]
    public void Reads_nested_bulleted_numbered_and_task_lists()
    {
        var blocks = MarkdownParser.Parse("- one\n  - nested\n- two\n  continued\n\n1. first\n2. second\n\n- [x] done\n- [ ] todo");

        var bullets = Assert.IsType<MarkdownList>(blocks[0]);
        Assert.Equal(new[] { 0, 1, 0 }, bullets.Items.Select(i => i.Depth));
        Assert.Equal("two continued", Text(bullets.Items[2].Spans));
        var numbered = Assert.IsType<MarkdownList>(blocks[1]);
        Assert.Equal(new[] { "1.", "2." }, numbered.Items.Select(i => i.Marker));
        var tasks = Assert.IsType<MarkdownList>(blocks[2]);
        Assert.Equal(new[] { "☑", "☐" }, tasks.Items.Select(i => i.Marker));
        Assert.Equal("todo", Text(tasks.Items[1].Spans));
    }

    [Fact]
    public void Reads_fenced_and_indented_code_and_quotes()
    {
        var blocks = MarkdownParser.Parse("```csharp\nvar x = 1;\n\nx\n```\n\n    indented\n\n> quoted **text**");

        var fenced = Assert.IsType<MarkdownCodeBlock>(blocks[0]);
        Assert.Equal("csharp", fenced.Language);
        Assert.Equal("var x = 1;\n\nx", fenced.Code);
        Assert.Equal("indented", Assert.IsType<MarkdownCodeBlock>(blocks[1]).Code);
        var quote = Assert.IsType<MarkdownQuote>(blocks[2]);
        Assert.Equal("quoted text", Text(Assert.IsType<MarkdownParagraph>(Assert.Single(quote.Blocks)).Spans));
    }

    [Fact]
    public void Reads_pipe_tables()
    {
        var blocks = MarkdownParser.Parse("| Name | Total |\n|:-----|------:|\n| Ada | 42 |\n| Alan \\| Turing | 7 |");

        var table = Assert.IsType<MarkdownTable>(Assert.Single(blocks));
        Assert.Equal(new[] { "Name", "Total" }, table.Header.Select(Text));
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Alan | Turing", Text(table.Rows[1][0]));
        Assert.Equal("7", Text(table.Rows[1][1]));
    }

    private static (int Level, string Text) Heading(MarkdownBlock block) =>
        block is MarkdownHeading heading ? (heading.Level, Text(heading.Spans)) : (0, block.GetType().Name);

    private static string Text(IReadOnlyList<MarkdownSpan> spans) => MarkdownInlineParser.ToPlainText(spans);
}
