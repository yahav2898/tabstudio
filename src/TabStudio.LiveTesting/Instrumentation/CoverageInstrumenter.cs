using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.LiveTesting.Coverage;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>
/// Turns a project's compilation into its coverage build: every user syntax tree is rewritten by
/// <see cref="CoverageRewriter"/> (C#) or <see cref="VisualBasicCoverageRewriter"/> (Visual Basic), and the coverage
/// runtime is added as one more tree, in the project's language.
/// </summary>
/// <remarks>
/// Generated trees, such as the SDK's implicit global usings or <c>AssemblyInfo.g.vb</c>, and trees without a file,
/// such as Visual Basic's My template, are left untouched, as the debugger leaves them.
/// </remarks>
internal static class CoverageInstrumenter
{
    /// <summary>Instruments a compilation that compiles without errors.</summary>
    /// <param name="compilation">The project's C# or Visual Basic compilation.</param>
    /// <param name="projectFilePath">The project, which decides its slot in the hit table.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    /// <returns>The instrumented compilation and the map of its coverage points.</returns>
    public static (RoslynCompilation Compilation, CoverageModule Module) Instrument(RoslynCompilation compilation, string projectFilePath, CancellationToken cancellationToken) =>
        Instrument(compilation, projectFilePath, CoverageModuleBuilder.IndexOf(projectFilePath), cancellationToken);

    /// <summary>Instruments a compilation that compiles without errors, for a given slot in the hit table.</summary>
    /// <param name="compilation">The project's C# or Visual Basic compilation.</param>
    /// <param name="projectFilePath">The project.</param>
    /// <param name="slot">The project's slot in the hit table of the process that runs the tests.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    /// <returns>The instrumented compilation and the map of its coverage points.</returns>
    public static (RoslynCompilation Compilation, CoverageModule Module) Instrument(RoslynCompilation compilation, string projectFilePath, int slot, CancellationToken cancellationToken)
    {
        var module = new CoverageModuleBuilder(projectFilePath, slot);
        var visualBasicOptions = compilation.Options as VisualBasicCompilationOptions;
        var rootNamespace = visualBasicOptions?.RootNamespace ?? string.Empty;
        var result = compilation;
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GeneratedSource.IsGenerated(tree, cancellationToken))
            {
                continue;
            }

            var original = tree.GetRoot(cancellationToken);
            var root = visualBasicOptions is not null
                ? new VisualBasicCoverageRewriter(tree, module, rootNamespace, cancellationToken).Visit(original)
                : new CoverageRewriter(tree, module, cancellationToken).Visit(original);
            result = result.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(root!, tree.Options));
        }

        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options;
        var runtime = visualBasicOptions is not null
            ? VisualBasicSyntaxTree.ParseText(
                CoverageRuntimeSource.VisualBasicText(module.Index),
                options as VisualBasicParseOptions ?? visualBasicOptions.ParseOptions,
                CoverageRuntimeSource.VisualBasicFilePath,
                Encoding.UTF8,
                cancellationToken)
            : CSharpSyntaxTree.ParseText(
                CoverageRuntimeSource.Text(module.Index),
                options as CSharpParseOptions,
                CoverageRuntimeSource.FilePath,
                Encoding.UTF8,
                cancellationToken);
        return (result.AddSyntaxTrees(runtime), module.Build());
    }
}
