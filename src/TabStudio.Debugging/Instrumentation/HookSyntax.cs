using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Debugging.Instrumentation;

/// <summary>
/// Builds the statements the instrumenter inserts. Every hook is a single line of code with no line breaks, so the
/// original statements keep their line numbers in sequence points and stack traces.
/// </summary>
internal static class HookSyntax
{
    private const string Runtime = "global::" + DebugRuntimeSource.ClassName;

    /// <summary>A single space, used to separate inserted code from the original code on the same line.</summary>
    public static SyntaxTrivia Space => SyntaxFactory.Space;

    /// <summary>The name of the local that holds a method's frame.</summary>
    public static string FrameVariable(int methodId) => FormattableString.Invariant($"__ts_f{methodId}");

    /// <summary>The name of the exception variable in a method's fault filter.</summary>
    public static string ExceptionVariable(int methodId) => FormattableString.Invariant($"__ts_e{methodId}");

    /// <summary><c>if (Step(frame, id)) Break(frame, id, new object[] { locals });</c></summary>
    public static StatementSyntax Step(string frame, int statementId, IReadOnlyList<string> values) =>
        Parse(FormattableString.Invariant(
            $"if ({Runtime}.Step({frame}, {statementId})) {Runtime}.Break({frame}, {statementId}, {Array(values)});"));

    /// <summary><c>var frame = Enter(methodId);</c></summary>
    public static StatementSyntax Enter(string frame, int methodId) =>
        Parse(FormattableString.Invariant($"var {frame} = {Runtime}.Enter({methodId});"));

    /// <summary><c>Suspend(frame);</c>, before an iterator yields.</summary>
    public static StatementSyntax Suspend(string frame) => Parse($"{Runtime}.Suspend({frame});");

    /// <summary><c>Resume(frame);</c>, when an iterator continues after a yield.</summary>
    public static StatementSyntax Resume(string frame) => Parse($"{Runtime}.Resume({frame});");

    /// <summary>
    /// An empty <c>try</c> whose <c>finally</c> calls <c>Exit(frame)</c>. With an exception variable it also gets a
    /// <c>catch</c> whose filter reports the exception and always returns <see langword="false"/>, so it never catches.
    /// </summary>
    public static TryStatementSyntax Try(string frame, string? exceptionVariable, IReadOnlyList<string> faultValues)
    {
        var text = exceptionVariable is null
            ? $"try {{ }} finally {{ {Runtime}.Exit({frame}); }}"
            : $"try {{ }} catch (global::System.Exception {exceptionVariable}) when ({Runtime}.Fault({frame}, {exceptionVariable}, {Array(faultValues)})) {{ throw; }} finally {{ {Runtime}.Exit({frame}); }}";
        return (TryStatementSyntax)Parse(text);
    }

    /// <summary>Wraps statements in braces on the lines they already occupy.</summary>
    public static BlockSyntax Block(IReadOnlyList<StatementSyntax> statements)
    {
        var list = statements.ToList();
        var leading = list[0].GetLeadingTrivia();
        list[0] = list[0].WithoutLeadingTrivia();
        var trailing = list[^1].GetTrailingTrivia();
        list[^1] = list[^1].WithTrailingTrivia(Space);

        return SyntaxFactory.Block(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithLeadingTrivia(leading).WithTrailingTrivia(Space),
            SyntaxFactory.List(list),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithTrailingTrivia(trailing));
    }

    private static string Array(IReadOnlyList<string> values) =>
        values.Count == 0 ? "null" : "new object[] { " + string.Join(", ", values) + " }";

    private static StatementSyntax Parse(string text) => SyntaxFactory.ParseStatement(text);
}
