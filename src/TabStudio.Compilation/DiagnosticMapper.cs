using System.Globalization;
using Microsoft.CodeAnalysis;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation;

/// <summary>
/// Converts Roslyn diagnostics into the compiler-independent <see cref="BuildDiagnostic"/> shown in the Error List.
/// </summary>
public static class DiagnosticMapper
{
    /// <summary>Converts diagnostics, dropping hidden ones and sorting the rest by severity, then position.</summary>
    /// <param name="diagnostics">Roslyn diagnostics.</param>
    /// <param name="projectName">The project they belong to, if any.</param>
    public static IReadOnlyList<BuildDiagnostic> Map(IEnumerable<Diagnostic> diagnostics, string? projectName = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics
            .Where(d => d.Severity != DiagnosticSeverity.Hidden)
            .Select(d => Map(d, projectName))
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.Location?.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location?.Start ?? 0)
            .ToList();
    }

    /// <summary>Converts one diagnostic.</summary>
    /// <param name="diagnostic">A Roslyn diagnostic.</param>
    /// <param name="projectName">The project it belongs to, if any.</param>
    public static BuildDiagnostic Map(Diagnostic diagnostic, string? projectName = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return new BuildDiagnostic(
            diagnostic.Id,
            MapSeverity(diagnostic.Severity),
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            MapLocation(diagnostic.Location),
            projectName);
    }

    private static DiagnosticSeverityLevel MapSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => DiagnosticSeverityLevel.Error,
        DiagnosticSeverity.Warning => DiagnosticSeverityLevel.Warning,
        DiagnosticSeverity.Info => DiagnosticSeverityLevel.Info,
        _ => DiagnosticSeverityLevel.Hidden,
    };

    private static DiagnosticLocation? MapLocation(Location location)
    {
        if (!location.IsInSource || location.SourceTree is null)
        {
            return null;
        }

        var span = location.SourceSpan;
        var start = location.GetLineSpan().StartLinePosition;
        return new DiagnosticLocation(location.SourceTree.FilePath, span.Start, span.Length, start.Line + 1, start.Character + 1);
    }
}
