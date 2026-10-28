using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.Compilation;
using TabStudio.Compilation.VisualBasic;
using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;

namespace TabStudio.App.Services;

/// <summary>
/// Finds the tests of a project in its source files alone, so the Test Explorer lists them before the first build
/// and again whenever a file is saved, without waiting for a compilation.
/// </summary>
/// <remarks>
/// Attributes are matched by their written names (<c>[Fact]</c>, <c>[Test]</c>, <c>[TestMethod]</c>…), which needs
/// neither the project's references nor its NuGet packages. Tests the source cannot show, such as rows of
/// <c>[MemberData]</c> or test methods inherited from a base class, appear once the project is built. A Visual Basic
/// project's <c>.vb</c> files are read with its preprocessor symbols, and its root namespace is put in front of the
/// names, as the compiler does.
/// </remarks>
public static class SourceTestScanner
{
    /// <summary>Finds the test classes of a project. The parts of a partial class are merged into one class.</summary>
    /// <param name="project">The test project.</param>
    /// <param name="otherProjectDirectories">The folders of the workspace's projects; files of other projects are left out.</param>
    /// <param name="sources">Reads the files, including unsaved text of open documents.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public static IReadOnlyList<SourceTestClass> Scan(
        ProjectInfo project,
        IReadOnlyList<string> otherProjectDirectories,
        ISourceTextProvider sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(otherProjectDirectories);
        ArgumentNullException.ThrowIfNull(sources);

        var isVisualBasic = project.IsVisualBasic;
        var csharpOptions = isVisualBasic ? null : CSharpCompiler.CreateParseOptions();
        var visualBasicOptions = isVisualBasic ? VisualBasicCompiler.CreateParseOptions(project) : null;
        var rootNamespace = isVisualBasic ? VisualBasicCompiler.RootNamespaceOf(project) : string.Empty;
        var extension = isVisualBasic ? VisualBasicCompiler.SourceExtension : ".cs";
        var classes = new List<SourceTestClass>();
        foreach (var file in ProjectSourceFiles.Enumerate(project, otherProjectDirectories, extension))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text;
            try
            {
                text = sources.GetText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The file disappeared or cannot be read; the next scan sees the folder as it is then.
                continue;
            }

            var tree = visualBasicOptions is not null
                ? VisualBasicSyntaxTree.ParseText(SourceText.From(text), visualBasicOptions, file, cancellationToken)
                : CSharpSyntaxTree.ParseText(SourceText.From(text), csharpOptions, file, cancellationToken);
            classes.AddRange(SourceTestDiscoverer.Discover(tree, rootNamespace, cancellationToken));
        }

        return classes
            .GroupBy(c => c.FullName, StringComparer.Ordinal)
            .Select(parts => parts.Skip(1).Any() ? parts.First() with { Methods = [.. parts.SelectMany(p => p.Methods)] } : parts.First())
            .ToList();
    }
}
