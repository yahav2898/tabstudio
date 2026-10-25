using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// Places diagnostics in Razor files: errors the Razor compiler reports, which point into a <c>.razor</c> or
/// <c>.cshtml</c> file, and C# errors in the code it generates, which point to the line of the Razor file the code
/// came from. Everything else is converted by <see cref="DiagnosticMapper"/>.
/// </summary>
/// <param name="razorText">The current text of a Razor file of the project, or <see langword="null"/> for any other file.</param>
internal sealed class RazorDiagnostics(Func<string, SourceText?> razorText)
{
    /// <summary>Converts diagnostics, most severe first, dropping hidden ones and the copies of one error in several generated files.</summary>
    /// <param name="diagnostics">Roslyn diagnostics of a project.</param>
    /// <param name="projectName">The project.</param>
    public List<BuildDiagnostic> Map(IEnumerable<Diagnostic> diagnostics, string projectName)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var seen = new HashSet<(string, string?, int, int)>();
        var mapped = new List<BuildDiagnostic>();
        foreach (var diagnostic in diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden))
        {
            var converted = MapOne(diagnostic, projectName);

            // An error in _Imports.razor appears in the code of every component that imports it.
            if (seen.Add((converted.Id, converted.Location?.FilePath, converted.Location?.Start ?? -1, converted.Message.GetHashCode(StringComparison.Ordinal))))
            {
                mapped.Add(converted);
            }
        }

        return
        [
            .. mapped
                .OrderByDescending(d => d.Severity)
                .ThenBy(d => d.Location?.FilePath, StringComparer.Ordinal)
                .ThenBy(d => d.Location?.Start ?? 0),
        ];
    }

    private BuildDiagnostic MapOne(Diagnostic diagnostic, string projectName)
    {
        var location = diagnostic.Location;
        if (location.Kind == LocationKind.ExternalFile && location.GetLineSpan() is { IsValid: true } external
            && razorText(external.Path) is { } externalText)
        {
            // Razor's own errors: the compiler gives the file and the line span.
            return WithLocation(diagnostic, projectName, At(external.Path, externalText, external.Span));
        }

        if (!location.IsInSource || location.SourceTree is not { } tree || RazorSourceMap.SourceFileOf(tree) is not { } source)
        {
            return DiagnosticMapper.Map(diagnostic, projectName);
        }

        var mapped = location.GetMappedLineSpan();
        if (mapped.HasMappedPath && razorText(mapped.Path) is { } mappedText)
        {
            return WithLocation(diagnostic, projectName, At(mapped.Path, mappedText, mapped.Span));
        }

        // Code the compiler wrote itself, such as a component's markup: the error belongs to the file as a whole.
        return WithLocation(diagnostic, projectName, razorText(source) is not null ? new DiagnosticLocation(source, 0, 0, 1, 1) : null);
    }

    private static BuildDiagnostic WithLocation(Diagnostic diagnostic, string projectName, DiagnosticLocation? location) =>
        DiagnosticMapper.Map(diagnostic, projectName) with { Location = location };

    private static DiagnosticLocation At(string path, SourceText text, LinePositionSpan span)
    {
        var start = Offset(text, span.Start);
        var end = Math.Max(start, Offset(text, span.End));
        var line = text.Lines.GetLinePosition(start);
        return new DiagnosticLocation(path, start, end - start, line.Line + 1, line.Character + 1);
    }

    private static int Offset(SourceText text, LinePosition position)
    {
        if (text.Lines.Count == 0 || position.Line < 0)
        {
            return 0;
        }

        if (position.Line >= text.Lines.Count)
        {
            return text.Length;
        }

        var line = text.Lines[position.Line];
        return Math.Min(line.Start + Math.Max(0, position.Character), line.End);
    }
}
