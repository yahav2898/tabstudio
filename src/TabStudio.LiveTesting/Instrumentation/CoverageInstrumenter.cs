using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Debugging.Instrumentation;
using TabStudio.LiveTesting.Coverage;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>
/// Turns a project's compilation into its coverage build: every user syntax tree is rewritten by
/// <see cref="CoverageRewriter"/>, and the coverage runtime is added as one more tree.
/// </summary>
/// <remarks>Generated trees, such as the SDK's implicit global usings, are left untouched, as the debugger leaves them.</remarks>
internal static class CoverageInstrumenter
{
    /// <summary>Instruments a compilation that compiles without errors.</summary>
    /// <param name="compilation">The project's compilation.</param>
    /// <param name="projectFilePath">The project, which decides its slot in the hit table.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    /// <returns>The instrumented compilation and the map of its coverage points.</returns>
    public static (CSharpCompilation Compilation, CoverageModule Module) Instrument(CSharpCompilation compilation, string projectFilePath, CancellationToken cancellationToken) =>
        Instrument(compilation, projectFilePath, CoverageModuleBuilder.IndexOf(projectFilePath), cancellationToken);

    /// <summary>Instruments a compilation that compiles without errors, for a given slot in the hit table.</summary>
    /// <param name="compilation">The project's compilation.</param>
    /// <param name="projectFilePath">The project.</param>
    /// <param name="slot">The project's slot in the hit table of the process that runs the tests.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    /// <returns>The instrumented compilation and the map of its coverage points.</returns>
    public static (CSharpCompilation Compilation, CoverageModule Module) Instrument(CSharpCompilation compilation, string projectFilePath, int slot, CancellationToken cancellationToken)
    {
        var module = new CoverageModuleBuilder(projectFilePath, slot);
        var result = compilation;
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GeneratedCode.IsGenerated(tree, cancellationToken))
            {
                continue;
            }

            var root = new CoverageRewriter(tree, module, cancellationToken).Visit(tree.GetRoot(cancellationToken));
            result = result.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(root!, tree.Options));
        }

        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        var runtime = CSharpSyntaxTree.ParseText(
            CoverageRuntimeSource.Text(module.Index),
            parseOptions,
            CoverageRuntimeSource.FilePath,
            Encoding.UTF8,
            cancellationToken);
        return (result.AddSyntaxTrees(runtime), module.Build());
    }
}
