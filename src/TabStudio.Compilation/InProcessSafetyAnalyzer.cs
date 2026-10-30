using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic;
using VBSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax;
using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation;

/// <summary>
/// Finds code that would take TabStudio down with it when a program runs in-process.
/// </summary>
/// <remarks>
/// Programs started with F5 run inside the IDE's own process. A call to <see cref="Environment.Exit(int)"/>
/// or <see cref="Environment.FailFast(string)"/> would therefore close the IDE, not just the program.
/// These calls are reported as errors before the program starts, with a suggestion that keeps the same behavior.
/// </remarks>
public static class InProcessSafetyAnalyzer
{
    /// <summary>The diagnostic id for <see cref="Environment.Exit(int)"/>.</summary>
    public const string ExitId = "TS1001";

    /// <summary>The diagnostic id for <see cref="Environment.FailFast(string)"/>.</summary>
    public const string FailFastId = "TS1002";

    /// <summary>Analyzes every syntax tree of a compilation.</summary>
    /// <param name="compilation">The compilation to check.</param>
    /// <param name="projectName">The project name to attach to diagnostics.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static IReadOnlyList<BuildDiagnostic> Analyze(RoslynCompilation compilation, string? projectName, CancellationToken cancellationToken = default) =>
        Analyze(compilation, projectName, redirected: false, cancellationToken);

    /// <summary>Analyzes every syntax tree of a compilation.</summary>
    /// <param name="compilation">The compilation to check.</param>
    /// <param name="projectName">The project name to attach to diagnostics.</param>
    /// <param name="redirected">
    /// Whether the program's C# calls are redirected when it is built to run (see <see cref="InProcessExit"/>), as a
    /// workspace's are: they are then warnings that say what happens, and only what cannot be redirected is an error.
    /// </param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static IReadOnlyList<BuildDiagnostic> Analyze(RoslynCompilation compilation, string? projectName, bool redirected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        return compilation.SyntaxTrees
            .SelectMany(tree => AnalyzeTree(compilation, tree, projectName, redirected, cancellationToken))
            .ToList();
    }

    /// <summary>Analyzes one syntax tree. Used for live diagnostics in the editor.</summary>
    /// <param name="compilation">The compilation that contains the tree.</param>
    /// <param name="tree">The tree to check.</param>
    /// <param name="projectName">The project name to attach to diagnostics.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static IReadOnlyList<BuildDiagnostic> AnalyzeTree(RoslynCompilation compilation, SyntaxTree tree, string? projectName, CancellationToken cancellationToken = default) =>
        AnalyzeTree(compilation, tree, projectName, redirected: false, cancellationToken);

    /// <summary>Analyzes one syntax tree. Used for live diagnostics in the editor.</summary>
    /// <param name="compilation">The compilation that contains the tree.</param>
    /// <param name="tree">The tree to check.</param>
    /// <param name="projectName">The project name to attach to diagnostics.</param>
    /// <param name="redirected">Whether C# calls are redirected when the program runs (see <see cref="Analyze(RoslynCompilation, string?, bool, CancellationToken)"/>).</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static IReadOnlyList<BuildDiagnostic> AnalyzeTree(RoslynCompilation compilation, SyntaxTree tree, string? projectName, bool redirected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(tree);

        var root = tree.GetRoot(cancellationToken);
        List<SyntaxNode> invocations = root.Language == LanguageNames.VisualBasic
            ? [.. root.DescendantNodes().OfType<VBSyntax.InvocationExpressionSyntax>()
                .Where(i => i.Expression is VBSyntax.MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name }
                    && (string.Equals(name, "Exit", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "FailFast", StringComparison.OrdinalIgnoreCase)))]
            : [.. root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Exit" or "FailFast" })];
        var results = new List<BuildDiagnostic>();

        // Visual Basic's End statement ends the process through Environment.Exit, as the runtime implements it.
        if (root.Language == LanguageNames.VisualBasic)
        {
            foreach (var end in root.DescendantNodes().OfType<VBSyntax.StopOrEndStatementSyntax>().Where(e => e.RawKind == (int)VB.SyntaxKind.EndStatement))
            {
                var location = end.GetLocation();
                var lineSpan = location.GetLineSpan().StartLinePosition;
                results.Add(new BuildDiagnostic(
                    ExitId,
                    DiagnosticSeverityLevel.Error,
                    "End would close TabStudio, because programs run inside the IDE. In a Windows Forms app call Application.Exit() or the form's Close(); in a console app return from Sub Main.",
                    new DiagnosticLocation(tree.FilePath, location.SourceSpan.Start, location.SourceSpan.Length, lineSpan.Line + 1, lineSpan.Character + 1),
                    projectName));
            }
        }

        if (invocations.Count == 0)
        {
            return results;
        }

        var model = compilation.GetSemanticModel(tree);
        foreach (var invocation in invocations)
        {
            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method ||
                method.ContainingType?.ToDisplayString() != "System.Environment")
            {
                continue;
            }

            var isExit = method.Name == "Exit";
            var location = invocation.GetLocation();
            var lineSpan = location.GetLineSpan().StartLinePosition;

            // A C# program's call is redirected when it runs: the program ends with its exit code, and TabStudio stays.
            if (redirected && root.Language == LanguageNames.CSharp)
            {
                results.Add(new BuildDiagnostic(
                    isExit ? ExitId : FailFastId,
                    DiagnosticSeverityLevel.Warning,
                    $"Environment.{method.Name} ends this program, which runs inside TabStudio, with its exit code; TabStudio keeps running. A catch block around it that catches every exception would keep the program running.",
                    new DiagnosticLocation(tree.FilePath, location.SourceSpan.Start, location.SourceSpan.Length, lineSpan.Line + 1, lineSpan.Character + 1),
                    projectName));
                continue;
            }

            results.Add(new BuildDiagnostic(
                isExit ? ExitId : FailFastId,
                DiagnosticSeverityLevel.Error,
                isExit
                    ? $"Environment.Exit would close TabStudio, because programs run inside the IDE. In a Windows Forms app call Application.Exit() or the form's Close(); in a console app return the exit code from {(root.Language == LanguageNames.VisualBasic ? "Sub Main" : "Main")}."
                    : "Environment.FailFast would close TabStudio, because programs run inside the IDE. Throw an exception instead.",
                new DiagnosticLocation(tree.FilePath, location.SourceSpan.Start, location.SourceSpan.Length, lineSpan.Line + 1, lineSpan.Character + 1),
                projectName));
        }

        return results;
    }
}
