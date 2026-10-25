using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting.Coverage;

namespace TabStudio.LiveTesting.Building;

/// <summary>One project of a coverage build.</summary>
/// <param name="Project">The project.</param>
/// <param name="IsInstrumented">Whether its code records coverage: the projects under test do, test projects never.</param>
/// <param name="Sources">The text of each of its source files as the build compiled it, by full path.</param>
public sealed record CoverageBuildProject(ProjectInfo Project, bool IsInstrumented, IReadOnlyDictionary<string, SourceText> Sources);

/// <summary>A test project built for Live Unit Testing, with the projects it tests instrumented for coverage.</summary>
/// <param name="Result">The assemblies and the diagnostics of the original code, as for a normal build.</param>
/// <param name="Projects">Every project of the build, dependencies first and the test project last.</param>
/// <param name="Modules">The coverage maps of the instrumented projects.</param>
public sealed record CoverageBuild(BuildResult Result, IReadOnlyList<CoverageBuildProject> Projects, IReadOnlyList<CoverageModule> Modules)
{
    /// <summary>Whether the build produced the test project's assembly.</summary>
    public bool Success => Result.Success;

    private IReadOnlyDictionary<string, SourceText>? _sources;

    /// <summary>The source text of every file of the build, by full path.</summary>
    public IReadOnlyDictionary<string, SourceText> Sources => _sources ??=
        Projects.SelectMany(p => p.Sources).GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);

    /// <summary>The project of the build that a source file belongs to, if any.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public CoverageBuildProject? ProjectOf(string filePath) => Projects.FirstOrDefault(p => p.Sources.ContainsKey(filePath));
}
