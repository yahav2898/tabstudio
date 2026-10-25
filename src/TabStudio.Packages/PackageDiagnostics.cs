using TabStudio.Core.Diagnostics;

namespace TabStudio.Packages;

/// <summary>Creates the project-wide diagnostics a restore reports.</summary>
internal static class PackageDiagnostics
{
    /// <summary>An error that applies to the whole project.</summary>
    public static BuildDiagnostic Error(string id, string message, string projectName) =>
        new(id, DiagnosticSeverityLevel.Error, message, null, projectName);

    /// <summary>A warning that applies to the whole project.</summary>
    public static BuildDiagnostic Warning(string id, string message, string projectName) =>
        new(id, DiagnosticSeverityLevel.Warning, message, null, projectName);

    /// <summary>Orders diagnostics most severe first, keeping the order of equally severe ones, and drops duplicates.</summary>
    public static List<BuildDiagnostic> Sort(IEnumerable<BuildDiagnostic> diagnostics) =>
        diagnostics.Distinct().OrderByDescending(d => d.Severity).ToList();
}
