using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>
/// Builds what the coverage instrumenter inserts into Visual Basic: one call per hook, joined to the statements of
/// its line with <c>:</c>, so no line break is ever added.
/// </summary>
internal static class VisualBasicCoverageHooks
{
    private const string Runtime = "Global." + CoverageRuntimeSource.ClassName;

    /// <summary>What separates two statements on one line: <c> : </c>.</summary>
    public static SyntaxTriviaList Separator { get; } = SyntaxFactory.TriviaList(
        SyntaxFactory.WhitespaceTrivia(" "),
        SyntaxFactory.ColonTrivia(":"),
        SyntaxFactory.WhitespaceTrivia(" "));

    /// <summary><c>Global.__TabStudioCoverage.Hit(point)</c>, without trivia.</summary>
    /// <param name="point">The coverage point the statement marks as hit.</param>
    public static StatementSyntax Hit(int point) =>
        SyntaxFactory.ParseExecutableStatement(Runtime + ".Hit(" + point.ToString(CultureInfo.InvariantCulture) + ")").WithoutTrivia();
}
