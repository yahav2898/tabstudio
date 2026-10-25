using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Coverage;

namespace TabStudio.Link.Language;

/// <summary>
/// Live Unit Testing builds from the host: the host compiles and instruments, this device runs the tests. The source
/// texts and assemblies of each test project's latest build are kept, so the next build sends only what changed.
/// </summary>
public sealed partial class LinkLanguageClient
{
    private readonly Lock _coverageGate = new();

    /// <summary>The source texts and assemblies of earlier coverage builds, by hash.</summary>
    private readonly Dictionary<string, object> _coverageContent = new(StringComparer.Ordinal);

    /// <summary>The hashes the latest build of each test project used.</summary>
    private readonly Dictionary<string, HashSet<string>> _coverageUses = new(StringComparer.Ordinal);

    /// <summary>
    /// Builds a test project for Live Unit Testing on the host, with the projects it tests instrumented to report to
    /// this process's coverage recorder.
    /// </summary>
    /// <param name="testProject">The test project, of this device's workspace.</param>
    /// <param name="uninstrumented">Projects to leave without coverage.</param>
    /// <param name="slots">
    /// The slot of every C# project in this process's hit table, by path, as <see cref="CoverageBuilder.SlotOf"/> gives it.
    /// </param>
    /// <param name="projects">This device's project for a project path, to put in the build.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The build, as <see cref="CoverageBuilder"/> would have made it on this device.</returns>
    /// <exception cref="LinkProtocolException">The host's answer does not fit this device's workspace.</exception>
    public async Task<CoverageBuild> BuildForCoverageAsync(
        ProjectInfo testProject,
        IReadOnlySet<string> uninstrumented,
        IReadOnlyDictionary<string, int> slots,
        Func<string, ProjectInfo?> projects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(testProject);
        ArgumentNullException.ThrowIfNull(uninstrumented);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(projects);

        Dictionary<string, object> known;
        lock (_coverageGate)
        {
            known = new Dictionary<string, object>(_coverageContent, StringComparer.Ordinal);
        }

        var request = new CoverageBuildRequest(
            testProject.FilePath,
            [.. uninstrumented],
            [.. slots.Select(s => new CoverageSlot(s.Key, s.Value))],
            [.. known.Keys]);
        var payload = await _connection.RequestAsync(LinkLanguageMethods.BuildForCoverage, LinkLanguageJson.ToJson(request), cancellationToken: cancellationToken).ConfigureAwait(false);
        var response = payload.Read(LinkLanguageJson.Info<CoverageBuildResponse>());

        var used = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var source in response.Projects.SelectMany(p => p.Sources))
        {
            if (source.Text is { } text)
            {
                used.TryAdd(source.Hash, SourceText.From(text));
            }
        }

        T Lookup<T>(string hash, string what)
            where T : class
        {
            if ((used.GetValueOrDefault(hash) ?? known.GetValueOrDefault(hash)) is not T found)
            {
                throw new LinkProtocolException($"The host left out {what}, which this device does not have.");
            }

            used[hash] = found;
            return found;
        }

        CompiledAssembly Assembly(CoverageAssembly assembly)
        {
            if (assembly.Image is not { } image)
            {
                return Lookup<CompiledAssembly>(assembly.Hash, $"the assembly {assembly.Name}");
            }

            var built = new CompiledAssembly(assembly.Name, payload.GetAttachment(image), assembly.Symbols is { } symbols ? payload.GetAttachment(symbols) : null);
            used[assembly.Hash] = built;
            return built;
        }

        var entry = response.EntryAssembly is { } entryAssembly ? Assembly(entryAssembly) : null;
        var dependencies = response.Dependencies.Select(Assembly).ToList();
        var buildProjects = new List<CoverageBuildProject>();
        foreach (var project in response.Projects)
        {
            var info = projects(project.ProjectPath)
                ?? throw new LinkProtocolException($"The host built '{Path.GetFileName(project.ProjectPath)}', which is not in this device's workspace.");
            var sources = new Dictionary<string, SourceText>(StringComparer.Ordinal);
            foreach (var source in project.Sources)
            {
                sources[source.Path] = Lookup<SourceText>(source.Hash, $"the text of {Path.GetFileName(source.Path)}");
            }

            buildProjects.Add(new CoverageBuildProject(info, project.IsInstrumented, sources));
        }

        var modules = new List<CoverageModule>();
        foreach (var map in response.Maps)
        {
            try
            {
                modules.Add(CoverageModule.Create(map.ProjectPath, map.Slot, map.Members, Statements(map.Statements), map.PointMembers));
            }
            catch (ArgumentException ex)
            {
                throw new LinkProtocolException($"The host's coverage map of '{Path.GetFileName(map.ProjectPath)}' does not hold together.", ex);
            }
        }

        Remember(testProject.FilePath, used);
        var result = new BuildResult(response.ProjectName, response.Diagnostics, entry, dependencies, response.Duration);
        return new CoverageBuild(result, buildProjects, entry is null ? [] : modules);
    }

    /// <summary>Statements from point, member, ordinal and line in a row.</summary>
    private static List<CoverageStatement> Statements(IReadOnlyList<int> values)
    {
        if (values.Count % 4 != 0)
        {
            throw new LinkProtocolException("The host's coverage statements are cut short.");
        }

        var statements = new List<CoverageStatement>(values.Count / 4);
        for (var i = 0; i < values.Count; i += 4)
        {
            statements.Add(new CoverageStatement(values[i], values[i + 1], values[i + 2], values[i + 3]));
        }

        return statements;
    }

    /// <summary>Keeps what a test project's build used, and forgets what no test project's latest build uses.</summary>
    private void Remember(string testProject, Dictionary<string, object> used)
    {
        lock (_coverageGate)
        {
            _coverageUses[testProject] = [.. used.Keys];
            foreach (var (hash, content) in used)
            {
                _coverageContent[hash] = content;
            }

            var inUse = _coverageUses.Values.SelectMany(u => u).ToHashSet(StringComparer.Ordinal);
            foreach (var hash in _coverageContent.Keys.Where(h => !inUse.Contains(h)).ToList())
            {
                _coverageContent.Remove(hash);
            }
        }
    }
}
