using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Instrumentation;

/// <summary>
/// Turns a compilation into its debug build: every user syntax tree is rewritten by
/// <see cref="InstrumentingRewriter"/>, and the debugger runtime is added as one more tree.
/// </summary>
/// <remarks>
/// Generated trees, such as the SDK's implicit global usings, are left untouched, except that the interceptors among
/// them are pointed at their calls in the rewritten trees (see <see cref="InterceptorRelocation"/>).
/// </remarks>
internal static class DebugInstrumenter
{
    /// <summary>Instruments a compilation, registering its statements and methods in <paramref name="info"/>.</summary>
    /// <param name="compilation">The compilation, which must compile without errors.</param>
    /// <param name="info">Numbers statements and methods; shared by every project of a build.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    public static CSharpCompilation Instrument(CSharpCompilation compilation, DebugInfoBuilder info, CancellationToken cancellationToken)
    {
        // Calls that generated interceptors replace are marked, so the interceptors follow them through the rewrite.
        compilation = InterceptorRelocation.MarkInterceptedCalls(compilation, cancellationToken);
        var result = compilation;
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GeneratedCode.IsGenerated(tree, cancellationToken))
            {
                continue;
            }

            var model = compilation.GetSemanticModel(tree);
            var root = new InstrumentingRewriter(model, info, cancellationToken).Visit(tree.GetRoot(cancellationToken));
            result = result.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(root!, tree.Options));
        }

        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        var runtime = CSharpSyntaxTree.ParseText(
            DebugRuntimeSource.Text,
            parseOptions,
            DebugRuntimeSource.FilePath,
            Encoding.UTF8,
            cancellationToken: cancellationToken);
        return InterceptorRelocation.RelocateInterceptors(result.AddSyntaxTrees(runtime), cancellationToken);
    }
}
