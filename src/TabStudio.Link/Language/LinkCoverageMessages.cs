using TabStudio.Core.Diagnostics;
using TabStudio.LiveTesting.Coverage;

namespace TabStudio.Link.Language;

// The messages of Live Unit Testing builds. The host compiles and instruments; the tablet runs the tests, so the
// instrumented code reports to the slots of the tablet's hit table, which the request carries. Source texts and
// assemblies the tablet already has from earlier builds are named by their hash instead of sent again.

/// <summary>A project's slot in the hit table of the tablet's coverage recorder.</summary>
/// <param name="ProjectPath">The project.</param>
/// <param name="Slot">Its slot.</param>
internal sealed record CoverageSlot(string ProjectPath, int Slot);

/// <summary>Builds a test project for Live Unit Testing.</summary>
/// <param name="ProjectPath">The test project.</param>
/// <param name="Uninstrumented">The projects to leave without coverage.</param>
/// <param name="Slots">The slot of every C# project of the workspace.</param>
/// <param name="Known">The hashes of the source texts and assemblies the tablet has from earlier builds.</param>
internal sealed record CoverageBuildRequest(string ProjectPath, IReadOnlyList<string> Uninstrumented, IReadOnlyList<CoverageSlot> Slots, IReadOnlyList<string> Known);

/// <summary>
/// An assembly of a coverage build: its image and PDB are the response's attachments at these positions, or, when
/// both are absent, the tablet has it under <paramref name="Hash"/>.
/// </summary>
internal sealed record CoverageAssembly(string Name, string Hash, int? Image, int? Symbols);

/// <summary>A source file of a coverage build: its text, or, when absent, the tablet has it under <paramref name="Hash"/>.</summary>
internal sealed record CoverageSource(string Path, string Hash, string? Text);

/// <summary>A project of a coverage build, with the source files it was compiled from.</summary>
internal sealed record CoverageProject(string ProjectPath, bool IsInstrumented, IReadOnlyList<CoverageSource> Sources);

/// <summary>
/// The coverage map of an instrumented project: its members, its statements as point, member, ordinal and line
/// in a row, and the member of every coverage point.
/// </summary>
internal sealed record CoverageMap(string ProjectPath, int Slot, IReadOnlyList<CoverageMember> Members, IReadOnlyList<int> Statements, IReadOnlyList<int> PointMembers);

/// <summary>A coverage build: the build result, its projects and the coverage maps of the instrumented ones.</summary>
internal sealed record CoverageBuildResponse(
    string ProjectName,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    CoverageAssembly? EntryAssembly,
    IReadOnlyList<CoverageAssembly> Dependencies,
    TimeSpan Duration,
    IReadOnlyList<CoverageProject> Projects,
    IReadOnlyList<CoverageMap> Maps);
