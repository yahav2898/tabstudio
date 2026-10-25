using Microsoft.CodeAnalysis;
using TabStudio.TestRunner.Source;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.TestRunner;

/// <summary>
/// Finds tests in source code, before anything is built: the Test Explorer tree and the editor's "Run test"
/// markers come from here while the user types.
/// </summary>
/// <remarks>
/// <para>
/// With a semantic model, attributes are matched by their full type names, including attributes derived from
/// the frameworks' (a custom <c>[SkipOnAndroidFact]</c> is a <c>[Fact]</c>), and test methods inherited from
/// base classes in source are listed under each derived class. Attributes whose types do not resolve, for example
/// because NuGet packages are not restored yet, are matched by their written names, as are all attributes in
/// <see cref="Discover(SyntaxTree, CancellationToken)"/>.
/// </para>
/// <para>
/// Names and ids match what <see cref="TestSession"/> reports after a build: <see cref="SourceTestMethod.FullyQualifiedName"/>
/// equals <see cref="TestCase.FullyQualifiedName"/>, and <see cref="SourceDataRow.Id"/> equals <see cref="TestCase.Id"/>.
/// </para>
/// </remarks>
public static class SourceTestDiscoverer
{
    /// <summary>Finds the tests of a whole project. Partial classes are merged into one class.</summary>
    /// <param name="compilation">The project's compilation.</param>
    /// <param name="cancellationToken">Cancels the search, typically because the user typed again.</param>
    public static IReadOnlyList<SourceTestClass> Discover(RoslynCompilation compilation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var classes = new List<SourceTestClass>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            classes.AddRange(new SourceTestFinder(tree, compilation.GetSemanticModel(tree), cancellationToken).Find());
        }

        return classes
            .GroupBy(c => c.FullName, StringComparer.Ordinal)
            .Select(parts => parts.Skip(1).Any() ? parts.First() with { Methods = [.. parts.SelectMany(p => p.Methods)] } : parts.First())
            .ToList();
    }

    /// <summary>Finds the tests declared in one document, for its editor markers.</summary>
    /// <param name="model">The document's semantic model.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public static IReadOnlyList<SourceTestClass> Discover(SemanticModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new SourceTestFinder(model.SyntaxTree, model, cancellationToken).Find();
    }

    /// <summary>
    /// Finds the tests declared in one document from its syntax alone, matching attributes by their written names.
    /// Fast enough to run on every keystroke, and works when nothing else of the project is known.
    /// </summary>
    /// <param name="tree">The document's syntax tree.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public static IReadOnlyList<SourceTestClass> Discover(SyntaxTree tree, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return new SourceTestFinder(tree, null, cancellationToken).Find();
    }
}
