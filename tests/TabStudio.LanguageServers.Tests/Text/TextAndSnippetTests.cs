using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Text;

namespace TabStudio.LanguageServers.Tests.Text;

public sealed class TextSnapshotTests
{
    [Theory]
    [InlineData("a\nbc\nd", 5, 2, 0)]
    [InlineData("a\r\nbc\r\nd", 7, 2, 0)]
    [InlineData("a\rbc\rd", 5, 2, 0)]
    [InlineData("a\nbc", 3, 1, 1)]
    [InlineData("", 0, 0, 0)]
    public void Offsets_and_positions_convert_both_ways(string text, int offset, int line, int character)
    {
        var snapshot = new TextSnapshot(text, 1);

        Assert.Equal(new TextPosition(line, character), snapshot.GetPosition(offset));
        Assert.Equal(offset, snapshot.GetOffset(new TextPosition(line, character)));
    }

    [Fact]
    public void A_position_past_the_end_of_its_line_stays_in_the_line()
    {
        var snapshot = new TextSnapshot("ab\r\ncd", 1);

        Assert.Equal(2, snapshot.GetOffset(new TextPosition(0, 40)));
        Assert.Equal(6, snapshot.GetOffset(new TextPosition(9, 0)));
        Assert.Equal("cd", snapshot.GetLineText(1));
    }

    [Fact]
    public void A_change_makes_the_next_version()
    {
        var snapshot = new TextSnapshot("print(x)", 3).WithChange(6, 1, "value");

        Assert.Equal("print(value)", snapshot.Text);
        Assert.Equal(4, snapshot.Version);
    }

    [Fact]
    public void Paths_and_uris_round_trip_with_encoded_characters()
    {
        var path = Path.Combine(Path.GetTempPath(), "a folder", "x#1.py");

        var uri = DocumentUri.FromPath(path);

        Assert.StartsWith("file:///", uri, StringComparison.Ordinal);
        Assert.Contains("a%20folder", uri, StringComparison.Ordinal);
        Assert.Contains("x%231.py", uri, StringComparison.Ordinal);
        Assert.True(DocumentUri.SamePath(path, DocumentUri.ToPath(uri)));
        Assert.Null(DocumentUri.ToPath("untitled:Untitled-1"));
    }
}

public sealed class SnippetParserTests
{
    [Fact]
    public void Tab_stops_are_removed_and_the_caret_goes_to_the_first()
    {
        var expansion = SnippetParser.Expand("print($1)$0");

        Assert.Equal("print()", expansion.Text);
        Assert.Equal(new SnippetTabStop(1, 6, 0), expansion.FirstStop);
        Assert.Equal(new[] { 1, 0 }, expansion.TabStops.Select(s => s.Index));
    }

    [Fact]
    public void Placeholders_keep_their_text_and_select_it()
    {
        var expansion = SnippetParser.Expand("for ${1:item} in ${2:items}:\n    ${0:pass}");

        Assert.Equal("for item in items:\n    pass", expansion.Text);
        Assert.Equal(new SnippetTabStop(1, 4, 4), expansion.TabStops[0]);
        Assert.Equal(new SnippetTabStop(2, 12, 5), expansion.TabStops[1]);
        Assert.Equal(new SnippetTabStop(0, 23, 4), expansion.TabStops[2]);
    }

    [Fact]
    public void Nested_placeholders_and_mirrors()
    {
        var expansion = SnippetParser.Expand("${1:name${2:Suffix}} = $1");

        Assert.Equal("nameSuffix = nameSuffix", expansion.Text);
        Assert.Equal(new SnippetTabStop(1, 0, 10), expansion.TabStops[0]);
        Assert.Equal(new SnippetTabStop(2, 4, 6), expansion.TabStops[1]);
    }

    [Fact]
    public void A_choice_inserts_its_first_option()
    {
        Assert.Equal("let x", SnippetParser.Expand("${1|let,const,var|} x").Text);
    }

    [Fact]
    public void Variables_use_their_value_or_default()
    {
        var expansion = SnippetParser.Expand("// $TM_FILENAME by ${AUTHOR:someone} ${UNKNOWN}", name => name == "TM_FILENAME" ? "main.py" : null);

        Assert.Equal("// main.py by someone ", expansion.Text);
    }

    [Fact]
    public void Escapes_and_text_that_is_not_snippet_syntax_stay_as_written()
    {
        Assert.Equal("cost: $5 and ${ and }", SnippetParser.Expand("cost: \\$5 and ${ and \\}").Text);
        Assert.Equal("price $", SnippetParser.Expand("price $").Text);
    }
}
