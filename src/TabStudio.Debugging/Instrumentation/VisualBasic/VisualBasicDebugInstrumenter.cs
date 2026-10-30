using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Instrumentation.VisualBasic;

/// <summary>
/// Turns a Visual Basic compilation into its debug build, as <see cref="DebugInstrumenter"/> does for C#: every user
/// syntax tree is instrumented by <see cref="VisualBasicInstrumentingWalker"/>, and the debugger runtime
/// (<see cref="VisualBasicRuntimeSource"/>) is added as one more tree.
/// </summary>
/// <remarks>
/// Generated trees, such as <c>AssemblyInfo.g.vb</c>, designer files and trees without a file (Visual Basic's
/// <c>My</c> template), are left untouched. The instrumented trees are parsed again from their new text with their
/// original options and path, so their line numbers stay those of the original files.
/// </remarks>
internal static class VisualBasicDebugInstrumenter
{
    /// <summary>Instruments a compilation, registering its statements and methods in <paramref name="info"/>.</summary>
    /// <param name="compilation">The compilation, which must compile without errors.</param>
    /// <param name="info">Numbers statements and methods; shared by every project of a build.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    public static VisualBasicCompilation Instrument(VisualBasicCompilation compilation, DebugInfoBuilder info, CancellationToken cancellationToken)
    {
        var result = compilation;
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tree.FilePath.Length == 0 || GeneratedCode.IsGenerated(tree, cancellationToken))
            {
                continue;
            }

            var model = compilation.GetSemanticModel(tree);
            if (new VisualBasicInstrumentingWalker(model, info, cancellationToken).Rewrite() is not { } instrumented)
            {
                continue;
            }

            var original = tree.GetText(cancellationToken);
            var text = SourceText.From(instrumented, original.Encoding ?? Encoding.UTF8, original.ChecksumAlgorithm);
            var rewritten = VisualBasicSyntaxTree.ParseText(text, (VisualBasicParseOptions)tree.Options, tree.FilePath, cancellationToken);
            result = result.ReplaceSyntaxTree(tree, rewritten);
        }

        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as VisualBasicParseOptions ?? compilation.Options.ParseOptions;
        var runtime = VisualBasicSyntaxTree.ParseText(
            VisualBasicRuntimeSource.Text,
            parseOptions,
            VisualBasicRuntimeSource.FilePath,
            Encoding.UTF8,
            cancellationToken);

        // The hooks read locals before every statement. Visual Basic allows that, but warns about reference variables
        // that are not assigned yet, and a project that treats warnings as errors would then fail to build. The
        // original code has already compiled with the project's own settings.
        var options = result.Options
            .WithGeneralDiagnosticOption(ReportDiagnostic.Suppress)
            .WithSpecificDiagnosticOptions(result.Options.SpecificDiagnosticOptions.Where(option => option.Value != ReportDiagnostic.Error));
        return result.AddSyntaxTrees(runtime).WithOptions(options);
    }
}
