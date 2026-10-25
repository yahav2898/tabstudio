using Microsoft.CodeAnalysis.Classification;
using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class QuickInfoTests
{
    private const string Source = """
        Console.WriteLine(Greeter.Gr$$eet("tablet"));

        static class Greeter
        {
            /// <summary>Says hello to <paramref name="name"/>.</summary>
            public static string Greet(string name) => $"Hello, {name}";
        }
        """;

    [Fact]
    public async Task Quick_info_shows_the_signature_and_documentation_of_a_method()
    {
        using var workspace = FeatureWorkspace.WithProgram(Source);

        var info = await workspace.Features.QuickInfo.GetQuickInfoAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.NotNull(info);
        var description = Assert.Single(info.Sections, s => s.Kind == QuickInfoSectionKind.Description);
        Assert.Contains("string Greeter.Greet(string name)", description.Text, StringComparison.Ordinal);
        Assert.Contains(description.Runs, r => r.Text == "Greet" && r.Classification == ClassificationTypeNames.MethodName);
        Assert.Contains(description.Runs, r => r.Text == "string" && r.Kind == TaggedTextKind.Keyword);

        var documentation = Assert.Single(info.Sections, s => s.Kind == QuickInfoSectionKind.Documentation);
        Assert.Equal("Says hello to name.", documentation.Text.Trim());

        var text = workspace.TextOf("App/Program.cs");
        Assert.Equal("Greet", text.Substring(info.SpanStart, info.SpanLength));
        Assert.Equal(SymbolGlyph.Method, info.Kind);
    }

    [Fact]
    public async Task Quick_info_describes_framework_types()
    {
        using var workspace = FeatureWorkspace.WithProgram("Con$$sole.WriteLine();");

        var info = await workspace.Features.QuickInfo.GetQuickInfoAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.NotNull(info);
        Assert.Contains("class System.Console", info.Sections[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quick_info_is_empty_where_there_is_no_symbol()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.WriteLine();\n$$\n");

        var info = await workspace.Features.QuickInfo.GetQuickInfoAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.Null(info);
    }
}
