using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.TestRunner.Source;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.TestRunner;

/// <summary>
/// Finds tests in C# and Visual Basic source code, before anything is built: the Test Explorer tree and the editor's
/// "Run test" markers come from here while the user types.
/// </summary>
/// <remarks>
/// <para>
/// With a semantic model, attributes are matched by their full type names, including attributes derived from
/// the frameworks' (a custom <c>[SkipOnAndroidFact]</c> is a <c>[Fact]</c>), and test methods inherited from
/// base classes in source are listed under each derived class. Attributes whose types do not resolve, for example
/// because NuGet packages are not restored yet, are matched by their written names, as are all attributes in
/// <see cref="Discover(SyntaxTree, CancellationToken)"/>. Visual Basic names are matched without regard to case, as
/// its compiler matches them.
/// </para>
/// <para>
/// Names and ids match what <see cref="TestSession"/> reports after a build: <see cref="SourceTestMethod.FullyQualifiedName"/>
/// equals <see cref="TestCase.FullyQualifiedName"/>, and <see cref="SourceDataRow.Id"/> equals <see cref="TestCase.Id"/>.
/// The Visual Basic compiler puts every type in the project's root namespace: a semantic model knows it, while the
/// syntax alone needs it from <see cref="Discover(SyntaxTree, string, CancellationToken)"/>.
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

        var rootNamespace = RootNamespaceOf(compilation);
        var classes = new List<SourceTestClass>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            classes.AddRange(new SourceTestFinder(tree, compilation.GetSemanticModel(tree), rootNamespace, cancellationToken).Find());
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
        return new SourceTestFinder(model.SyntaxTree, model, RootNamespaceOf(model.Compilation), cancellationToken).Find();
    }

    /// <summary>
    /// Finds the tests declared in one document from its syntax alone, matching attributes by their written names.
    /// Fast enough to run on every keystroke, and works when nothing else of the project is known.
    /// </summary>
    /// <param name="tree">The document's syntax tree.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <remarks>
    /// The names of Visual Basic tests lack the project's root namespace here; use
    /// <see cref="Discover(SyntaxTree, string, CancellationToken)"/> for them.
    /// </remarks>
    public static IReadOnlyList<SourceTestClass> Discover(SyntaxTree tree, CancellationToken cancellationToken = default) =>
        Discover(tree, string.Empty, cancellationToken);

    /// <summary>
    /// Finds the tests declared in one document from its syntax alone, as
    /// <see cref="Discover(SyntaxTree, CancellationToken)"/> does, for a project with a root namespace.
    /// </summary>
    /// <param name="tree">The document's syntax tree.</param>
    /// <param name="rootNamespace">
    /// The Visual Basic project's <c>RootNamespace</c>, which the compiler puts in front of every namespace that does
    /// not start with <c>Global</c>, or an empty string. C# has no root namespace and ignores it.
    /// </param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public static IReadOnlyList<SourceTestClass> Discover(SyntaxTree tree, string rootNamespace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(rootNamespace);
        return new SourceTestFinder(tree, null, rootNamespace, cancellationToken).Find();
    }

    private static string RootNamespaceOf(RoslynCompilation compilation) =>
        (compilation.Options as VisualBasicCompilationOptions)?.RootNamespace ?? string.Empty;
}
