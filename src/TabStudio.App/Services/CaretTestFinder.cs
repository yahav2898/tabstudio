using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;
using TabStudio.TestRunner;

namespace TabStudio.App.Services;

/// <summary>How much "Run Tests" in the editor runs, depending on where the caret is.</summary>
public enum CaretTestScope
{
    /// <summary>One data row: the caret is on an attribute such as <c>[InlineData(1, 2)]</c>.</summary>
    DataRow,

    /// <summary>One test method, with all its rows: the caret is in the method.</summary>
    Test,

    /// <summary>Every test of a class: the caret is in the class, outside its tests.</summary>
    Class,

    /// <summary>Every test of the file: the caret is outside the test classes.</summary>
    File,
}

/// <summary>The tests at the caret, which "Run Tests" (Ctrl+R, T) runs.</summary>
/// <param name="Names">
/// The names that select the tests in a <see cref="TestSession"/>: a row's id, a method's full name, or full class names.
/// </param>
/// <param name="Description">What runs: the test or row's display name, the class name or the file name.</param>
/// <param name="Scope">Whether a row, a test, a class or a file runs.</param>
public sealed record CaretTests(IReadOnlyList<string> Names, string Description, CaretTestScope Scope)
{
    /// <summary>The editor's context menu item, such as <c>Run Test 'Adds'</c>.</summary>
    public string MenuText => Scope switch
    {
        CaretTestScope.DataRow or CaretTestScope.Test => $"Run Test '{Description}'",
        _ => $"Run Tests in '{Description}'",
    };
}

/// <summary>
/// Works out which tests the caret is on, from the document's text alone, as Visual Studio's "Run Tests" does:
/// the data row under the caret, else the test method around it, else its class, else every test in the file.
/// </summary>
public static class CaretTestFinder
{
    /// <summary>Finds the tests at a position in a C# document.</summary>
    /// <param name="text">The document's current text.</param>
    /// <param name="filePath">The document's path, used for test locations.</param>
    /// <param name="offset">The caret offset.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The tests, or <see langword="null"/> when the document has none.</returns>
    public static CaretTests? Find(string text, string filePath, int offset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filePath);

        var tree = CSharpSyntaxTree.ParseText(SourceText.From(text), CSharpCompiler.CreateParseOptions(), filePath, cancellationToken);
        var classes = SourceTestDiscoverer.Discover(tree, cancellationToken);
        if (classes.Count == 0)
        {
            return null;
        }

        var root = tree.GetRoot(cancellationToken);
        var position = Math.Clamp(offset, 0, root.FullSpan.End);
        var token = root.FindToken(Math.Min(position, Math.Max(root.FullSpan.End - 1, 0)));
        var methods = classes.SelectMany(c => c.Methods).Where(m => !m.IsInherited).ToList();
        foreach (var node in token.Parent?.AncestorsAndSelf() ?? [])
        {
            // A caret in the blank lines between two members belongs to neither of them.
            if (position < node.Span.Start || position > node.Span.End)
            {
                continue;
            }

            switch (node)
            {
                case AttributeSyntax attribute when FindRow(methods, attribute) is { } row:
                    return new CaretTests([row.Id], row.DisplayName, CaretTestScope.DataRow);
                case MethodDeclarationSyntax method when methods.Find(m => m.SpanStart == method.Identifier.SpanStart) is { } test:
                    return new CaretTests([test.FullyQualifiedName], test.DisplayName, CaretTestScope.Test);
                case ClassDeclarationSyntax type when classes.FirstOrDefault(c => c.SpanStart == type.Identifier.SpanStart) is { } found:
                    return new CaretTests([found.FullName], found.Name, CaretTestScope.Class);
            }
        }

        return new CaretTests([.. classes.Select(c => c.FullName)], Path.GetFileName(filePath), CaretTestScope.File);
    }

    private static SourceDataRow? FindRow(List<SourceTestMethod> methods, AttributeSyntax attribute)
    {
        var start = attribute.GetLocation().GetLineSpan().StartLinePosition;
        return methods
            .SelectMany(m => m.DataRows)
            .FirstOrDefault(r => r.Location.Line == start.Line + 1 && r.Location.Column == start.Character + 1);
    }
}
