using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Workspaces;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// What a project's packages bring besides assemblies and analyzers: the props and targets NuGet imports from their
/// <c>build/</c> and <c>buildTransitive/</c> folders. Generators such as Riok.Mapperly, CommunityToolkit.Mvvm and
/// Avalonia's read the properties those files set and make visible to them (<c>CompilerVisibleProperty</c>), the item
/// metadata they make visible, and the additional files they add; and the files add global usings and interceptor
/// namespaces. Without them a generator sees a different project than with the SDK, and may generate nothing.
/// </summary>
public sealed partial class RoslynWorkspaceHost
{
    private readonly Dictionary<string, IReadOnlyList<string>> _packageBuildFiles = new(StringComparer.Ordinal);

    /// <summary>The C# sources each project compiles from its packages' <c>contentFiles/</c>, keyed by project file path.</summary>
    private readonly Dictionary<string, IReadOnlyList<string>> _packageContentSources = new(StringComparer.Ordinal);

    /// <summary>The projects as read from their files, before their packages' props and targets were read into them.</summary>
    private readonly Dictionary<ProjectId, TsProject> _projectsAsRead = [];

    /// <summary>
    /// Sets the props and targets a project imports from its packages, such as <c>RestoreResult.BuildFiles</c>, and
    /// updates the project's build properties, global usings, additional files and parse options from them.
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file. The project does not have to be loaded yet.</param>
    /// <param name="buildFiles">Full paths of the files, in MSBuild's order.</param>
    public void SetPackageBuildFiles(string projectFilePath, IReadOnlyList<string> buildFiles)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        ArgumentNullException.ThrowIfNull(buildFiles);

        lock (_gate)
        {
            if (_packageBuildFiles.TryGetValue(projectFilePath, out var known) && known.SequenceEqual(buildFiles, StringComparer.Ordinal))
            {
                return;
            }

            _packageBuildFiles[projectFilePath] = [.. buildFiles];
            if (!_projectIds.TryGetValue(projectFilePath, out var id) || !_projects.TryGetValue(id, out var current))
            {
                return;
            }

            var project = WithPackageBuild(_projectsAsRead.GetValueOrDefault(id) ?? current);
            _projects[id] = project;
            ApplyPackageBuildUnderGate(id, project);
            InvalidateGenerators(projectFilePath);
        }
    }

    /// <summary>
    /// Sets the C# sources a project compiles from its packages' <c>contentFiles/</c>, such as
    /// <c>RestoreResult.ContentSources</c>, as NuGet adds them to its <c>Compile</c> items: a source-only package such as
    /// Spectre.Console's Wcwidth.Sources is nothing else. They are kept by project file path, so they survive a reload.
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file. The project does not have to be loaded yet.</param>
    /// <param name="files">Full paths of the sources.</param>
    public void SetPackageContentSources(string projectFilePath, IReadOnlyList<string> files)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        ArgumentNullException.ThrowIfNull(files);

        lock (_gate)
        {
            var known = _packageContentSources.GetValueOrDefault(projectFilePath) ?? [];
            if (known.SequenceEqual(files, StringComparer.Ordinal))
            {
                return;
            }

            _packageContentSources[projectFilePath] = [.. files];
            if (!_projectIds.TryGetValue(projectFilePath, out var id) || _workspace.CurrentSolution.GetProject(id) is not { } roslyn)
            {
                return;
            }

            var solution = _workspace.CurrentSolution;
            foreach (var document in roslyn.Documents.Where(d => d.FilePath is { } path && known.Contains(path, StringComparer.Ordinal)).ToList())
            {
                solution = solution.RemoveDocument(document.Id);
            }

            foreach (var document in PackageContentDocuments(id, projectFilePath))
            {
                solution = solution.AddDocument(document);
            }

            _workspace.TryApplyChanges(solution);
        }
    }

    /// <summary>The documents of a project's package content sources that exist. The caller holds the gate.</summary>
    private IEnumerable<DocumentInfo> PackageContentDocuments(ProjectId id, string projectFilePath) =>
        (_packageContentSources.GetValueOrDefault(projectFilePath) ?? [])
            .Where(File.Exists)
            .Select(path => CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, ReadFileOrEmpty(path), isGenerated: true));

    /// <summary>The props and targets a project imports from its packages, as last set. For tests.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    public IReadOnlyList<string> GetPackageBuildFiles(string projectFilePath)
    {
        lock (_gate)
        {
            return _packageBuildFiles.GetValueOrDefault(projectFilePath) ?? [];
        }
    }

    /// <summary>A project with its packages' props and targets read into it, when they are known. The caller holds the gate.</summary>
    private TsProject WithPackageBuild(TsProject project) =>
        _packageBuildFiles.TryGetValue(project.FilePath, out var files) && files.Count > 0
            ? ProjectParser.WithPackageBuildFiles(project, files)
            : project;

    /// <summary>
    /// Brings a loaded project in line with what its packages' build files add: its parse options (language version
    /// and interceptor namespaces), its generated analyzer configuration, its global usings, its additional files and
    /// its analyzers. The caller holds the gate.
    /// </summary>
    private void ApplyPackageBuildUnderGate(ProjectId id, TsProject project)
    {
        var solution = _workspace.CurrentSolution;
        if (solution.GetProject(id) is not { } roslyn)
        {
            return;
        }

        solution = solution.WithProjectParseOptions(id, ParseOptionsFor(project, _optimize));

        var configPath = AnalyzerConfigPath(project);
        if (roslyn.AnalyzerConfigDocuments.FirstOrDefault(d => string.Equals(d.FilePath, configPath, StringComparison.Ordinal)) is { } existingConfig)
        {
            var text = AnalyzerConfigText(project, _razorFiles.GetValueOrDefault(id) ?? [], _unoGeneratorInputs.GetValueOrDefault(id) ?? []);
            solution = solution.WithAnalyzerConfigDocumentText(existingConfig.Id, SourceText.From(text, Encoding.UTF8));
        }

        var usingsPath = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.GlobalUsings.g.cs");
        var usings = project.IsVisualBasic ? null : CSharpCompiler.CreateGlobalUsingsSource(project);
        var existingUsings = roslyn.Documents.FirstOrDefault(d => string.Equals(d.FilePath, usingsPath, StringComparison.Ordinal));
        if (existingUsings is not null && usings is not null)
        {
            solution = solution.WithDocumentText(existingUsings.Id, SourceText.From(usings, Encoding.UTF8));
        }
        else if (existingUsings is null && usings is not null)
        {
            solution = solution.AddDocument(CreateDocumentInfo(DocumentId.CreateNewId(id, usingsPath), usingsPath, usings, isGenerated: true));
        }
        else if (existingUsings is not null)
        {
            solution = solution.RemoveDocument(existingUsings.Id);
        }

        foreach (var path in AdditionalFilePaths(project).Where(path => !_additionalDocuments.ContainsKey(path)))
        {
            var documentId = DocumentId.CreateNewId(id, path);
            solution = solution.AddAdditionalDocument(CreateDocumentInfo(documentId, path, ReadFileOrEmpty(path), isGenerated: false));
            _additionalDocuments[path] = documentId;
        }

        solution = solution.WithProjectAnalyzerReferences(id, GetAnalyzerReferences(project));

        // Assemblies the packages' targets reference by path, such as MSTest's TestFramework.Extensions.
        solution = solution.WithProjectMetadataReferences(id, GetMetadataReferences(project));
        _workspace.TryApplyChanges(solution);
    }

    /// <summary>The files of a project's <c>AdditionalFiles</c> items, from its file, its <c>Directory.Build</c> files and its packages.</summary>
    private static IReadOnlyList<string> AdditionalFilePaths(TsProject project) =>
        project.AdditionalFiles.Count == 0
            ? []
            : ProjectSourceFiles.EnumerateItems(project, [.. project.AdditionalFiles.Select(i => i.Include).Distinct(StringComparer.Ordinal)], []);

    /// <summary>
    /// The sections of a project's analyzer configuration for its additional files: for each file, the metadata of
    /// its item that the project makes visible to generators (<c>CompilerVisibleItemMetadata</c>), empty when the item
    /// has none, as the SDK writes it. Avalonia's XAML files are <c>AvaloniaXaml</c> items.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="skip">Files with sections of their own, such as Razor files.</param>
    private static string AdditionalFileSections(TsProject project, IReadOnlyCollection<string> skip)
    {
        var visible = project.CompilerVisibleItemMetadata
            .Where(m => string.Equals(m.ItemType, "AdditionalFiles", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.MetadataName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (visible.Count == 0)
        {
            return string.Empty;
        }

        var items = new List<(string Path, IReadOnlyDictionary<string, string> Metadata)>();
        foreach (var path in AdditionalFilePaths(project))
        {
            var item = project.AdditionalFiles.LastOrDefault(i => new GlobPattern(i.BaseDirectory, i.Include).IsMatch(path));
            items.Add((path, item?.Metadata ?? new Dictionary<string, string>()));
        }

        if (UsesAvaloniaXaml(project))
        {
            var xaml = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["SourceItemGroup"] = "AvaloniaXaml" };
            items.AddRange(ProjectSourceFiles.Enumerate(project, extension: ".axaml").Select(path => (path, (IReadOnlyDictionary<string, string>)xaml)));
        }

        var text = new StringBuilder();
        foreach (var (path, metadata) in items.Where(i => !skip.Contains(i.Path)).DistinctBy(i => i.Path, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"\n[{path.Replace('\\', '/')}]\n");
            foreach (var name in visible)
            {
                var value = metadata.GetValueOrDefault(name) ?? string.Empty;
                text.Append(CultureInfo.InvariantCulture, $"build_metadata.AdditionalFiles.{name} = {value.ReplaceLineEndings(" ")}\n");
            }
        }

        return text.ToString();
    }

    private static string ReadFileOrEmpty(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
