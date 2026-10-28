using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.Compilation;

// The Visual Basic half of the syntactic classifier. It lives in its own file because the C# and Visual Basic
// syntax APIs share type and extension method names (SyntaxKind, SyntaxFacts, Kind()).
public static partial class SyntaxClassifier
{
    private static readonly VisualBasicParseOptions BasicParseOptions = VisualBasic.VisualBasicCompiler.CreateParseOptions();

    /// <summary>Classifies a whole Visual Basic source text.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// Ranges in document order, with the classification names the C# pass uses: keywords and control keywords,
    /// comments (<c>'</c>, <c>REM</c> and <c>'''</c>), string, character and interpolated string literals, numbers and
    /// dates, preprocessor directives, excluded code and XML literals, which are colored as strings. Plain
    /// identifiers, operators and punctuation are not reported.
    /// </returns>
    public static IReadOnlyList<ClassifiedRange> ClassifyVisualBasic(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tree = VisualBasicSyntaxTree.ParseText(SourceText.From(text), BasicParseOptions, string.Empty, cancellationToken);
        var root = tree.GetRoot(cancellationToken);
        var ranges = new List<ClassifiedRange>();

        foreach (var token in root.DescendantTokens(descendIntoTrivia: false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddVisualBasicTrivia(token.LeadingTrivia, ranges);
            if (ClassifyVisualBasicToken(token) is { } name && token.Span.Length > 0)
            {
                ranges.Add(new ClassifiedRange(token.SpanStart, token.Span.Length, name));
            }

            AddVisualBasicTrivia(token.TrailingTrivia, ranges);
        }

        return ranges;
    }

    /// <summary>The classification of one Visual Basic token, or <see langword="null"/> for tokens shown in the default color.</summary>
    internal static string? ClassifyVisualBasicToken(SyntaxToken token)
    {
        var kind = token.Kind();
        if (IsInXmlLiteral(token))
        {
            return ClassificationTypeNames.StringLiteral;
        }

        if (SyntaxFacts.IsKeywordKind(kind))
        {
            return IsVisualBasicControlKeyword(token, kind) ? ClassificationTypeNames.ControlKeyword : ClassificationTypeNames.Keyword;
        }

        return kind switch
        {
            SyntaxKind.IntegerLiteralToken
                or SyntaxKind.FloatingLiteralToken
                or SyntaxKind.DecimalLiteralToken
                or SyntaxKind.DateLiteralToken => ClassificationTypeNames.NumericLiteral,
            SyntaxKind.StringLiteralToken
                or SyntaxKind.CharacterLiteralToken
                or SyntaxKind.InterpolatedStringTextToken
                or SyntaxKind.EndOfInterpolatedStringToken => ClassificationTypeNames.StringLiteral,
            SyntaxKind.DollarSignDoubleQuoteToken or SyntaxKind.DoubleQuoteToken
                when token.Parent is InterpolatedStringExpressionSyntax => ClassificationTypeNames.StringLiteral,
            _ => null,
        };
    }

    private static void AddVisualBasicTrivia(SyntaxTriviaList trivia, List<ClassifiedRange> ranges)
    {
        foreach (var item in trivia)
        {
            if (item.IsKind(SyntaxKind.DocumentationCommentTrivia))
            {
                // A ''' comment is structured trivia, and its ''' is trivia inside the structure, which Span leaves out.
                var length = item.ToFullString().TrimEnd().Length;
                if (length > 0)
                {
                    ranges.Add(new ClassifiedRange(item.FullSpan.Start, length, ClassificationTypeNames.Comment));
                }

                continue;
            }

            var name = item.Kind() switch
            {
                SyntaxKind.CommentTrivia => ClassificationTypeNames.Comment,
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

    /// <summary>
    /// Whether a token is part of an XML literal, such as <c>&lt;greeting name="x"&gt;Hello&lt;/greeting&gt;</c>, but
    /// not of an expression embedded in it with <c>&lt;%= %&gt;</c>.
    /// </summary>
    private static bool IsInXmlLiteral(SyntaxToken token) =>
        token.Parent is XmlNodeSyntax or XmlDeclarationSyntax or XmlDeclarationOptionSyntax or XmlPrefixSyntax;

    /// <summary>
    /// Whether a keyword is one of control flow, as Roslyn's Visual Basic classifier decides: <c>If</c>, <c>For</c>,
    /// <c>Return</c> and the like in a control statement, and the whole of <c>Exit Sub</c> or <c>Exit Function</c>.
    /// </summary>
    private static bool IsVisualBasicControlKeyword(SyntaxToken token, SyntaxKind kind)
    {
        if (token.Parent is not { } parent)
        {
            return false;
        }

        var parentKind = parent.Kind();
        if (parentKind is SyntaxKind.ExitFunctionStatement or SyntaxKind.ExitOperatorStatement
            or SyntaxKind.ExitPropertyStatement or SyntaxKind.ExitSubStatement)
        {
            return true;
        }

        return IsVisualBasicControlKeywordKind(kind) && IsVisualBasicControlStatementKind(parentKind);
    }

    private static bool IsVisualBasicControlKeywordKind(SyntaxKind kind) => kind is
        SyntaxKind.CaseKeyword or SyntaxKind.CatchKeyword or SyntaxKind.ContinueKeyword or SyntaxKind.DoKeyword
        or SyntaxKind.EachKeyword or SyntaxKind.ElseKeyword or SyntaxKind.ElseIfKeyword or SyntaxKind.EndKeyword
        or SyntaxKind.ExitKeyword or SyntaxKind.FinallyKeyword or SyntaxKind.ForKeyword or SyntaxKind.GoToKeyword
        or SyntaxKind.IfKeyword or SyntaxKind.InKeyword or SyntaxKind.LoopKeyword or SyntaxKind.NextKeyword
        or SyntaxKind.ResumeKeyword or SyntaxKind.ReturnKeyword or SyntaxKind.SelectKeyword or SyntaxKind.ThenKeyword
        or SyntaxKind.TryKeyword or SyntaxKind.WhileKeyword or SyntaxKind.WendKeyword or SyntaxKind.UntilKeyword
        or SyntaxKind.EndIfKeyword or SyntaxKind.GosubKeyword or SyntaxKind.YieldKeyword or SyntaxKind.ThrowKeyword
        or SyntaxKind.ToKeyword;

    private static bool IsVisualBasicControlStatementKind(SyntaxKind kind) => kind is
        SyntaxKind.CallStatement or SyntaxKind.CaseElseStatement or SyntaxKind.CaseStatement or SyntaxKind.CatchStatement
        or SyntaxKind.ContinueDoStatement or SyntaxKind.ContinueForStatement or SyntaxKind.ContinueWhileStatement
        or SyntaxKind.DoUntilStatement or SyntaxKind.DoWhileStatement or SyntaxKind.ElseIfStatement or SyntaxKind.ElseStatement
        or SyntaxKind.EndIfStatement or SyntaxKind.EndSelectStatement or SyntaxKind.EndTryStatement or SyntaxKind.EndWhileStatement
        or SyntaxKind.ExitDoStatement or SyntaxKind.ExitForStatement or SyntaxKind.ExitSelectStatement or SyntaxKind.ExitTryStatement
        or SyntaxKind.ExitWhileStatement or SyntaxKind.FinallyStatement or SyntaxKind.ForEachStatement or SyntaxKind.ForStatement
        or SyntaxKind.GoToStatement or SyntaxKind.IfStatement or SyntaxKind.LoopUntilStatement or SyntaxKind.LoopWhileStatement
        or SyntaxKind.NextStatement or SyntaxKind.ResumeLabelStatement or SyntaxKind.ResumeNextStatement or SyntaxKind.ReturnStatement
        or SyntaxKind.SelectStatement or SyntaxKind.SimpleDoStatement or SyntaxKind.SimpleLoopStatement or SyntaxKind.SingleLineIfStatement
        or SyntaxKind.ThrowStatement or SyntaxKind.TryStatement or SyntaxKind.UntilClause or SyntaxKind.WhileClause
        or SyntaxKind.WhileStatement or SyntaxKind.YieldStatement or SyntaxKind.TernaryConditionalExpression;
}
