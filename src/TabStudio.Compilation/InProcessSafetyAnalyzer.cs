using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    public static IReadOnlyList<BuildDiagnostic> Analyze(RoslynCompilation compilation, string? projectName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        return compilation.SyntaxTrees
            .SelectMany(tree => AnalyzeTree(compilation, tree, projectName, cancellationToken))
            .ToList();
    }

    /// <summary>Analyzes one syntax tree. Used for live diagnostics in the editor.</summary>
    /// <param name="compilation">The compilation that contains the tree.</param>
    /// <param name="tree">The tree to check.</param>
    /// <param name="projectName">The project name to attach to diagnostics.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static IReadOnlyList<BuildDiagnostic> AnalyzeTree(RoslynCompilation compilation, SyntaxTree tree, string? projectName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(tree);

        var invocations = tree.GetRoot(cancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Exit" or "FailFast" })
            .ToList();

        if (invocations.Count == 0)
        {
            return [];
        }

        var results = new List<BuildDiagnostic>();
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

            results.Add(new BuildDiagnostic(
                isExit ? ExitId : FailFastId,
                DiagnosticSeverityLevel.Error,
                isExit
                    ? "Environment.Exit would close TabStudio, because programs run inside the IDE. Return the exit code from Main instead."
                    : "Environment.FailFast would close TabStudio, because programs run inside the IDE. Throw an exception instead.",
                new DiagnosticLocation(tree.FilePath, location.SourceSpan.Start, location.SourceSpan.Length, lineSpan.Line + 1, lineSpan.Character + 1),
                projectName));
        }

        return results;
    }
}
