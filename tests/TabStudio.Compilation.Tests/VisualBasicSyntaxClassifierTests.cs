using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;

namespace TabStudio.Compilation.Tests;

public sealed class VisualBasicSyntaxClassifierTests
{
    private const string Source = """
        ' A greeting
        REM An old-style comment
        #If DEBUG Then
        Imports System.Text
        #End If

        Module Program
            ''' <summary>Runs.</summary>
            Sub Main()
                Dim name = "tablet"
                Dim initial = "t"c
                Dim page = <page title="home"><%= name %></page>
                For i = 0 To 3
                    Console.WriteLine($"Hello, {name} {i * 2.5}")
                Next
                If name Is Nothing Then Exit Sub
            End Sub
        End Module
        """;

    private static string Text(ClassifiedRange range) => Source.Substring(range.Start, range.Length);

    private static string? ClassOf(string token, string before = "")
    {
        var start = Source.IndexOf(before + token, StringComparison.Ordinal) + before.Length;
        return SyntaxClassifier.ClassifyVisualBasic(Source).FirstOrDefault(r => r.Start == start && r.Length == token.Length).Classification;
    }

    [Fact]
    public void Classifies_comments_and_directives()
    {
        Assert.Equal(ClassificationTypeNames.Comment, ClassOf("' A greeting"));
        Assert.Equal(ClassificationTypeNames.Comment, ClassOf("REM An old-style comment"));
        Assert.Equal(ClassificationTypeNames.Comment, ClassOf("''' <summary>Runs.</summary>"));
        Assert.Equal(ClassificationTypeNames.PreprocessorKeyword, ClassOf("#If DEBUG Then"));
    }

    [Fact]
    public void Classifies_keywords_and_control_keywords()
    {
        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("Module"));
        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("Sub"));
        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("Dim"));
        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("Nothing"));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("For"));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("Next"));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("If", before: "    "));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("Exit"));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("Sub", before: "Exit "));
        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("Sub", before: "End "));
    }

    [Fact]
    public void Classifies_literals_including_interpolated_strings()
    {
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("\"tablet\""));
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("\"t\"c"));
        Assert.Equal(ClassificationTypeNames.NumericLiteral, ClassOf("2.5"));
        Assert.Equal(ClassificationTypeNames.NumericLiteral, ClassOf("3"));
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("$\""));
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("Hello, "));
    }

    [Fact]
    public void Colors_xml_literals_as_strings_but_not_their_embedded_expressions()
    {
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("page", before: "<"));
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("home"));
        Assert.Null(ClassOf("name", before: "<%= "));
    }

    [Fact]
    public void Leaves_identifiers_and_punctuation_for_the_semantic_pass()
    {
        var ranges = SyntaxClassifier.ClassifyVisualBasic(Source);

        Assert.DoesNotContain(ranges, r => Text(r) is "Console" or "WriteLine" or "Program" or "(" or "{");
    }

    [Fact]
    public void Ranges_are_in_document_order_and_do_not_overlap()
    {
        var ranges = SyntaxClassifier.ClassifyVisualBasic(Source);

        for (var i = 1; i < ranges.Count; i++)
        {
            Assert.True(ranges[i].Start >= ranges[i - 1].Start + ranges[i - 1].Length);
        }
    }

    [Theory]
    [InlineData("/work/App/Program.vb", LanguageNames.VisualBasic)]
    [InlineData("/work/App/PROGRAM.VB", LanguageNames.VisualBasic)]
    [InlineData("/work/App/Program.cs", LanguageNames.CSharp)]
    [InlineData("Script.csx", LanguageNames.CSharp)]
    public void Picks_the_language_from_the_file_name(string path, string language) =>
        Assert.Equal(language, SyntaxClassifier.LanguageOf(path));

    [Fact]
    public void Classify_file_uses_the_language_of_the_file()
    {
        const string Code = "Dim x = 1 ' one";

        var basic = SyntaxClassifier.ClassifyFile("Program.vb", Code);
        var csharp = SyntaxClassifier.ClassifyFile("Program.cs", Code);

        Assert.Contains(basic, r => r.Classification == ClassificationTypeNames.Keyword && Code.Substring(r.Start, r.Length) == "Dim");
        Assert.Contains(basic, r => r.Classification == ClassificationTypeNames.Comment && Code.Substring(r.Start, r.Length) == "' one");
        Assert.DoesNotContain(csharp, r => r.Classification == ClassificationTypeNames.Comment);
    }
}
