using Microsoft.CodeAnalysis.CSharp;
using Microsoft.NET.Sdk.Razor.SourceGenerators;
using TabStudio.Compilation.Xaml;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// The Razor compiler: the .NET SDK's Razor source generator, which turns <c>.razor</c> components and <c>.cshtml</c>
/// views and pages into C# as part of every compilation of a project, so its code is there for diagnostics,
/// IntelliSense and builds alike. TabStudio carries the SDK's own build of it (see <c>Razor/Compiler/NOTICE.txt</c>).
/// </summary>
internal static class RazorCompiler
{
    /// <summary>The parse option feature the SDK turns on for .NET 10 projects, which has Razor read C# with Roslyn's tokenizer.</summary>
    public const string RoslynTokenizerFeature = "use-roslyn-tokenizer";

    /// <summary>The generator, shared by every project that compiles Razor.</summary>
    public static GeneratorReference Reference { get; } = new(new RazorSourceGenerator(), "Razor");

    /// <summary>
    /// The parse options of a project: the compiler's defaults for the configuration, the interceptor namespaces of
    /// the generators it runs, and for Razor projects the Roslyn tokenizer, as the SDK's <c>Features</c> property sets it.
    /// </summary>
    /// <param name="project">The project, or <see langword="null"/> for the defaults.</param>
    /// <param name="optimize">Whether this is a release build.</param>
    public static CSharpParseOptions ParseOptionsFor(TsProject? project, bool optimize)
    {
        var options = Wpf.WpfProject.ParseOptions(project, CSharpCompiler.CreateParseOptions(
            optimize,
            project is null ? null : SdkGenerators.InterceptorsNamespaces(project),
            CSharpCompiler.LanguageVersionOf(project)));
        if (CSharpCompiler.DefinedSymbols(project) is { Count: > 0 } defined)
        {
            options = options.WithPreprocessorSymbols(options.PreprocessorSymbolNames.Concat(defined).Distinct(StringComparer.Ordinal).ToArray());
        }

        return project is not null && RazorProjects.UsesRazor(project)
            ? options.WithFeatures([.. options.Features, new(RoslynTokenizerFeature, "true")])
            : options;
    }
}
