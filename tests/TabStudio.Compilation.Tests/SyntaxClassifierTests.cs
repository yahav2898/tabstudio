using Microsoft.CodeAnalysis.Classification;

namespace TabStudio.Compilation.Tests;

public sealed class SyntaxClassifierTests
{
    private const string Source = """
        // A greeting
        #nullable enable
        var name = "tablet";
        for (var i = 0; i < 3; i++)
        {
            Console.WriteLine($"Hello, {name} {i * 2.5}");
        }
        return 0;
        """;

    private static string Text(ClassifiedRange range) => Source.Substring(range.Start, range.Length);

    private static string? ClassOf(string token)
    {
        var start = Source.IndexOf(token, StringComparison.Ordinal);
        return SyntaxClassifier.Classify(Source).FirstOrDefault(r => r.Start == start && r.Length == token.Length).Classification;
    }

    [Fact]
    public void Classifies_comments_and_directives()
    {
        Assert.Equal(ClassificationTypeNames.Comment, ClassOf("// A greeting"));
        Assert.Equal(ClassificationTypeNames.PreprocessorKeyword, ClassOf("#nullable enable"));
    }

    [Fact]
    public void Classifies_keywords_and_control_keywords()
    {
        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("var"));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("for"));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("return"));
    }

    [Fact]
    public void Classifies_literals_including_interpolated_strings()
    {
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("\"tablet\""));
        Assert.Equal(ClassificationTypeNames.NumericLiteral, ClassOf("2.5"));
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("$\""));
    }

    [Fact]
    public void Leaves_identifiers_and_punctuation_for_the_semantic_pass()
    {
        var ranges = SyntaxClassifier.Classify(Source);

        Assert.DoesNotContain(ranges, r => Text(r) is "Console" or "WriteLine" or "name" or "(" or ";");
    }

    [Fact]
    public void Ranges_are_in_document_order_and_do_not_overlap()
    {
        var ranges = SyntaxClassifier.Classify(Source);

        for (var i = 1; i < ranges.Count; i++)
        {
            Assert.True(ranges[i].Start >= ranges[i - 1].Start + ranges[i - 1].Length);
        }
    }
}
