using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>Builds the statement the coverage instrumenter inserts: one call on one line, with no line break.</summary>
internal static class CoverageHooks
{
    private const string Runtime = "global::" + CoverageRuntimeSource.ClassName;

    /// <summary><c>global::__TabStudioCoverage.Hit(point);</c></summary>
    /// <param name="point">The coverage point the statement marks as hit.</param>
    public static StatementSyntax Hit(int point) =>
        SyntaxFactory.ParseStatement(Runtime + ".Hit(" + point.ToString(CultureInfo.InvariantCulture) + ");");
}
