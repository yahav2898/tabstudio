namespace TabStudio.Core.Diagnostics;

/// <summary>
/// How serious a diagnostic is. Mirrors the levels a developer sees in the Error List.
/// </summary>
public enum DiagnosticSeverityLevel
{
    /// <summary>Not shown to the user; used for tooling only.</summary>
    Hidden,

    /// <summary>A message or suggestion.</summary>
    Info,

    /// <summary>Code that compiles but is probably wrong.</summary>
    Warning,

    /// <summary>Code that does not compile.</summary>
    Error,
}

/// <summary>
/// Where a diagnostic points in a source file.
/// </summary>
/// <param name="FilePath">Absolute path of the file.</param>
/// <param name="Start">Zero-based character offset where the span starts.</param>
/// <param name="Length">Length of the span in characters. May be zero.</param>
/// <param name="Line">One-based line number of <paramref name="Start"/>, as shown to the user.</param>
/// <param name="Column">One-based column number of <paramref name="Start"/>, as shown to the user.</param>
public sealed record DiagnosticLocation(string FilePath, int Start, int Length, int Line, int Column)
{
    /// <summary>Zero-based character offset just past the end of the span.</summary>
    public int End => Start + Length;
}

/// <summary>
/// A single entry in the Error List: an error, warning or message produced by a build or by live analysis.
/// </summary>
/// <param name="Id">The diagnostic code, for example <c>CS0103</c>.</param>
/// <param name="Severity">How serious the diagnostic is.</param>
/// <param name="Message">The human-readable description.</param>
/// <param name="Location">Where the diagnostic points, or <see langword="null"/> when it applies to the whole project.</param>
/// <param name="ProjectName">The project the diagnostic belongs to, when known.</param>
public sealed record BuildDiagnostic(
    string Id,
    DiagnosticSeverityLevel Severity,
    string Message,
    DiagnosticLocation? Location,
    string? ProjectName = null)
{
    /// <summary>The file name without its directory, or an empty string for project-wide diagnostics.</summary>
    public string FileName => Location is null ? string.Empty : Path.GetFileName(Location.FilePath);

    /// <inheritdoc />
    public override string ToString()
    {
        var where = Location is null ? string.Empty : $"{FileName}({Location.Line},{Location.Column}): ";
        return $"{where}{Severity.ToString().ToLowerInvariant()} {Id}: {Message}";
    }
}
