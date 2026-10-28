using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TabStudio.Compilation;

/// <summary>
/// Colors C# and Visual Basic from their syntax alone: keywords, literals, comments and preprocessor directives.
/// </summary>
/// <remarks>
/// Parsing takes a few milliseconds even for a large file, so the editor uses this to color a file the
/// moment it opens. The semantic classification from <see cref="RoslynWorkspaceHost.GetClassificationsAsync"/>,
/// which also knows which identifiers are types, methods or locals, replaces it as soon as it is ready.
/// The classification names are Roslyn's, so both passes share one color table. Use
/// <see cref="ClassifyFile"/> when the language follows from the file name.
/// </remarks>
public static partial class SyntaxClassifier
{
    private static readonly CSharpParseOptions ParseOptions = CSharpCompiler.CreateParseOptions();

    /// <summary>
    /// Classifies a whole source text by the language of its file: Visual Basic for a <c>.vb</c> file, C# for
    /// anything else.
    /// </summary>
    /// <param name="filePath">The file's path or name; only its extension is read.</param>
    /// <param name="text">The source text.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Ranges in document order. Plain identifiers, operators and punctuation are not reported.</returns>
    public static IReadOnlyList<ClassifiedRange> ClassifyFile(string filePath, string text, CancellationToken cancellationToken = default) =>
        Classify(text, LanguageOf(filePath), cancellationToken);

    /// <summary>Classifies a whole source text in a given language.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="language">
    /// <see cref="LanguageNames.CSharp"/> or <see cref="LanguageNames.VisualBasic"/>; any other value classifies as C#.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Ranges in document order. Plain identifiers, operators and punctuation are not reported.</returns>
    public static IReadOnlyList<ClassifiedRange> Classify(string text, string language, CancellationToken cancellationToken = default) =>
        string.Equals(language, LanguageNames.VisualBasic, StringComparison.Ordinal)
            ? ClassifyVisualBasic(text, cancellationToken)
            : Classify(text, cancellationToken);

    /// <summary>The Roslyn language of a source file: <see cref="LanguageNames.VisualBasic"/> for <c>.vb</c>, else <see cref="LanguageNames.CSharp"/>.</summary>
    /// <param name="filePath">The file's path or name.</param>
    public static string LanguageOf(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return VisualBasic.VisualBasicCompiler.IsSource(filePath) ? LanguageNames.VisualBasic : LanguageNames.CSharp;
    }

    /// <summary>Classifies a whole C# source text.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Ranges in document order. Plain identifiers, operators and punctuation are not reported.</returns>
    public static IReadOnlyList<ClassifiedRange> Classify(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tree = CSharpSyntaxTree.ParseText(SourceText.From(text), ParseOptions, cancellationToken: cancellationToken);
        var root = tree.GetRoot(cancellationToken);
        var ranges = new List<ClassifiedRange>();

        foreach (var token in root.DescendantTokens(descendIntoTrivia: false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddTrivia(token.LeadingTrivia, ranges);
            if (ClassifyToken(token) is { } name && token.Span.Length > 0)
            {
                ranges.Add(new ClassifiedRange(token.SpanStart, token.Span.Length, name));
            }

            AddTrivia(token.TrailingTrivia, ranges);
        }

        return ranges;
    }

    /// <summary>The classification of one token, or <see langword="null"/> for tokens shown in the default color.</summary>
    internal static string? ClassifyToken(SyntaxToken token)
    {
        var kind = token.Kind();
        if (SyntaxFacts.IsKeywordKind(kind))
        {
            return IsControlKeyword(kind) ? ClassificationTypeNames.ControlKeyword : ClassificationTypeNames.Keyword;
        }

        return kind switch
        {
            SyntaxKind.NumericLiteralToken => ClassificationTypeNames.NumericLiteral,
            SyntaxKind.CharacterLiteralToken => ClassificationTypeNames.StringLiteral,
            SyntaxKind.StringLiteralToken or SyntaxKind.Utf8StringLiteralToken =>
                token.Text.StartsWith('@') ? ClassificationTypeNames.VerbatimStringLiteral : ClassificationTypeNames.StringLiteral,
            SyntaxKind.SingleLineRawStringLiteralToken
                or SyntaxKind.MultiLineRawStringLiteralToken
                or SyntaxKind.Utf8SingleLineRawStringLiteralToken
                or SyntaxKind.Utf8MultiLineRawStringLiteralToken
                or SyntaxKind.InterpolatedStringStartToken
                or SyntaxKind.InterpolatedVerbatimStringStartToken
                or SyntaxKind.InterpolatedSingleLineRawStringStartToken
                or SyntaxKind.InterpolatedMultiLineRawStringStartToken
                or SyntaxKind.InterpolatedStringTextToken
                or SyntaxKind.InterpolatedStringEndToken
                or SyntaxKind.InterpolatedRawStringEndToken => ClassificationTypeNames.StringLiteral,
            SyntaxKind.IdentifierToken when IsImplicitTypeKeyword(token) => ClassificationTypeNames.Keyword,
            _ => null,
        };
    }

    private static void AddTrivia(SyntaxTriviaList trivia, List<ClassifiedRange> ranges)
    {
        foreach (var item in trivia)
        {
            var name = item.Kind() switch
            {
                SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia => ClassificationTypeNames.Comment,
                SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia => ClassificationTypeNames.Comment,
                SyntaxKind.DisabledTextTrivia => ClassificationTypeNames.ExcludedCode,
                _ when item.IsDirective => ClassificationTypeNames.PreprocessorKeyword,
                _ => null,
            };

            if (name is not null && item.Span.Length > 0)
            {
                ranges.Add(new ClassifiedRange(item.SpanStart, item.Span.Length, name));
            }
        }
    }

    private static bool IsControlKeyword(SyntaxKind kind) => kind is
        SyntaxKind.IfKeyword or SyntaxKind.ElseKeyword or SyntaxKind.WhileKeyword or SyntaxKind.ForKeyword
        or SyntaxKind.ForEachKeyword or SyntaxKind.DoKeyword or SyntaxKind.SwitchKeyword or SyntaxKind.CaseKeyword
        or SyntaxKind.BreakKeyword or SyntaxKind.ContinueKeyword or SyntaxKind.ReturnKeyword or SyntaxKind.GotoKeyword
        or SyntaxKind.ThrowKeyword or SyntaxKind.TryKeyword or SyntaxKind.CatchKeyword or SyntaxKind.FinallyKeyword
        or SyntaxKind.YieldKeyword or SyntaxKind.AwaitKeyword;

    /// <summary>
    /// <c>var</c> is an ordinary identifier to the parser. Color it as a keyword where it declares a variable.
    /// </summary>
    private static bool IsImplicitTypeKeyword(SyntaxToken token) =>
        token.ValueText == "var"
        && token.Parent is IdentifierNameSyntax
        {
            Parent: VariableDeclarationSyntax or ForEachStatementSyntax or DeclarationExpressionSyntax,
        };
}
