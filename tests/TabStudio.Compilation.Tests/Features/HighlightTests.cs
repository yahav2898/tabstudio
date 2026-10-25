using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class HighlightTests
{
    [Fact]
    public async Task Highlights_mark_the_definition_and_every_use_in_the_file()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            var total = 0;
            total += 2;
            Console.WriteLine(to$$tal);
            """);

        var highlights = await workspace.Features.Highlights.GetDocumentHighlightsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        var text = workspace.TextOf("App/Program.cs");
        Assert.Equal(3, highlights.Count);
        Assert.All(highlights, h => Assert.Equal("total", text.Substring(h.Start, h.Length)));
        Assert.Equal(HighlightKind.Definition, highlights[0].Kind);
        Assert.Equal(4, highlights[0].Start);
        Assert.All(highlights.Skip(1), h => Assert.Equal(HighlightKind.Reference, h.Kind));
    }

    [Fact]
    public async Task Highlights_are_empty_away_from_a_symbol()
    {
        using var workspace = FeatureWorkspace.WithProgram("var total = 0;\n$$\n");

        var highlights = await workspace.Features.Highlights.GetDocumentHighlightsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.Empty(highlights);
    }
}
