using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;

namespace TabStudio.WindowsCompatibility.Building;

/// <summary>
/// Moves diagnostics in code the WPF markup compiler generated to the XAML that code came from, through its
/// <c>#line</c> directives, as Visual Studio's Error List shows them: an event handler a page names that the class
/// lacks is an error on that line of the page.
/// </summary>
internal static class GeneratedCodeMapper
{
    /// <summary>The diagnostics, with those in the generated files moved to their XAML where the code says where it came from.</summary>
    /// <param name="diagnostics">The compiler's diagnostics.</param>
    /// <param name="generated">The generated files' paths and texts.</param>
    public static List<BuildDiagnostic> Map(IEnumerable<BuildDiagnostic> diagnostics, IReadOnlyDictionary<string, string> generated)
    {
        var trees = new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>(StringComparer.Ordinal);
        var mapped = new List<BuildDiagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Location is not { } location || !generated.TryGetValue(location.FilePath, out var text))
            {
                mapped.Add(diagnostic);
                continue;
            }

            if (!trees.TryGetValue(location.FilePath, out var tree))
            {
                tree = CSharpSyntaxTree.ParseText(text, path: location.FilePath);
                trees[location.FilePath] = tree;
            }

            var span = tree.GetMappedLineSpan(new TextSpan(Math.Min(location.Start, text.Length), 0));
            if (!span.HasMappedPath || string.IsNullOrEmpty(span.Path))
            {
                mapped.Add(diagnostic);
                continue;
            }

            var file = Path.GetFullPath(span.Path, Path.GetDirectoryName(location.FilePath)!);
            mapped.Add(diagnostic with
            {
                Location = new DiagnosticLocation(file, 0, 0, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1),
            });
        }

        return mapped;
    }
}
