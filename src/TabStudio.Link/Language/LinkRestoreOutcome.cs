using TabStudio.Core.Diagnostics;

namespace TabStudio.Link.Language;

/// <summary>How restoring a project's NuGet packages went on the host, which compiles against them.</summary>
/// <param name="Success">Whether every package was restored.</param>
/// <param name="Diagnostics">Errors and warnings, such as a package that does not exist.</param>
/// <param name="FromCache">Whether the restore was answered from the host's restore cache.</param>
/// <param name="Duration">How long it took.</param>
public sealed record LinkRestoreOutcome(bool Success, IReadOnlyList<BuildDiagnostic> Diagnostics, bool FromCache, TimeSpan Duration);
