using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using RoslynProjectInfo = Microsoft.CodeAnalysis.ProjectInfo;
using RoslynSolutionInfo = Microsoft.CodeAnalysis.SolutionInfo;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;
using TsWorkspace = TabStudio.Core.Workspaces.Workspace;

namespace TabStudio.Compilation;

/// <summary>A span of source text and its Roslyn classification, used for syntax highlighting.</summary>
/// <param name="Start">Zero-based character offset.</param>
/// <param name="Length">Length in characters.</param>
/// <param name="Classification">A Roslyn classification name, see <see cref="ClassificationTypeNames"/>.</param>
public readonly record struct ClassifiedRange(int Start, int Length, string Classification);

/// <summary>A replacement of part of a document's text, such as one produced by Format Document.</summary>
/// <param name="Start">Zero-based offset of the replaced range.</param>
/// <param name="Length">Length of the replaced range.</param>
/// <param name="NewText">The text that replaces it.</param>
public readonly record struct TextEdit(int Start, int Length, string NewText);

/// <summary>
/// Keeps a live Roslyn model of every C# project in the open workspace.
/// </summary>
/// <remarks>
/// <para>
/// This is the language service behind the editor. It mirrors the open <see cref="TsWorkspace"/> into a
/// Roslyn <see cref="AdhocWorkspace"/>, keeps it in sync with every keystroke through
/// <see cref="UpdateText"/>, and answers the editor's questions: which diagnostics a file has, how to color
/// it, and how to compile a project. Roslyn caches compilations between edits, so asking again after a small
/// change is cheap.
/// </para>
/// <para>
/// Updates are serialized; queries work on immutable snapshots and can run concurrently with updates.
/// </para>
/// </remarks>
public sealed partial class RoslynWorkspaceHost : IBuildSource, IDisposable
{
    private static readonly Lazy<HostServices> SharedHostServices = new(CreateHostServices);

    private readonly AdhocWorkspace _workspace;
    private readonly IReadOnlyList<MetadataReference> _frameworkReferences;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DocumentId> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProjectId> _projectIds = new(StringComparer.Ordinal);
    private readonly Dictionary<ProjectId, TsProject> _projects = [];
    private TsWorkspace? _loaded;
    private bool _concurrentBuild = true;
    private bool _optimize;

    /// <summary>Creates an empty host that compiles against the in-memory .NET 10 reference assemblies.</summary>
    public RoslynWorkspaceHost()
        : this(ReferenceAssemblies.Net100)
    {
    }

    /// <summary>Creates an empty host with a specific set of framework references.</summary>
    /// <param name="frameworkReferences">
    /// The reference assemblies every project compiles against, such as <see cref="ReferenceAssemblies.LoadMapped"/>.
    /// </param>
    public RoslynWorkspaceHost(IReadOnlyList<MetadataReference> frameworkReferences)
    {
        ArgumentNullException.ThrowIfNull(frameworkReferences);
        _frameworkReferences = frameworkReferences;
        _workspace = new AdhocWorkspace(SharedHostServices.Value);
    }

    /// <summary>The projects currently loaded, keyed by project file path.</summary>
    public IReadOnlyCollection<TsProject> Projects
    {
        get
        {
            lock (_gate)
            {
                return _projects.Values.ToList();
            }
        }
    }

    /// <summary>Replaces the model with the C# projects of a workspace.</summary>
    /// <param name="workspace">The workspace to mirror.</param>
    /// <param name="sources">Supplies the text of each source file, including unsaved edits.</param>
    public void Load(TsWorkspace workspace, ISourceTextProvider sources)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(sources);

        lock (_gate)
        {
            _loaded = workspace;
            LoadUnderGate(workspace, sources);
        }
    }

    /// <summary>
    /// Releases every compilation Roslyn has cached, keeping the current text of every file. The next query
    /// recompiles only what it needs. Called when the device runs low on memory.
    /// </summary>
    public void TrimMemory()
    {
        lock (_gate)
        {
            if (_loaded is null)
            {
                return;
            }

            var solution = _workspace.CurrentSolution;
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (path, id) in _documents)
            {
                if (solution.GetDocument(id) is { } document && document.TryGetText(out var text))
                {
                    texts[path] = text.ToString();
                }
            }

            LoadUnderGate(_loaded, new SnapshotTextProvider(texts));
        }
    }

    private void LoadUnderGate(TsWorkspace workspace, ISourceTextProvider sources)
    {
        _workspace.ClearSolution();
        _documents.Clear();
        _projectIds.Clear();
        _projects.Clear();

        var csharpProjects = workspace.Projects
            .Where(p => p.Language == ProjectLanguage.CSharp && p.Kind != ProjectKind.Unknown)
            .ToList();

        foreach (var project in csharpProjects)
        {
            var id = ProjectId.CreateNewId(project.Name);
            _projectIds[project.FilePath] = id;
            _projects[id] = project;
        }

        var allProjectDirectories = workspace.Projects.Select(p => p.DirectoryPath).ToList();
        var projectInfos = csharpProjects
            .Select(project => CreateProjectInfo(project, allProjectDirectories, sources))
            .ToList();

        _workspace.AddSolution(RoslynSolutionInfo.Create(
            SolutionId.CreateNewId(workspace.Name),
            VersionStamp.Create(),
            workspace.Solution?.FilePath,
            projectInfos));
    }

    /// <summary>Whether a file is a source file of a loaded project.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public bool ContainsDocument(string filePath)
    {
        lock (_gate)
        {
            return _documents.ContainsKey(filePath);
        }
    }

    /// <summary>Finds the loaded project with the given project file.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    public TsProject? FindProject(string projectFilePath)
    {
        lock (_gate)
        {
            return _projectIds.TryGetValue(projectFilePath, out var id) ? _projects[id] : null;
        }
    }

    /// <summary>Replaces the text of a source file. Called as the user types.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="text">The new text.</param>
    /// <returns><see langword="true"/> when the file belongs to a loaded project.</returns>
    public bool UpdateText(string filePath, string text)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(text);

        lock (_gate)
        {
            return _documents.TryGetValue(filePath, out var id) && ReplaceText(id, text);
        }
    }

    /// <summary>Adds a new source file to the project whose folder contains it.</summary>
    /// <param name="filePath">Full path of the new file.</param>
    /// <param name="text">Its text.</param>
    /// <returns><see langword="true"/> when an owning project was found.</returns>
    public bool AddDocument(string filePath, string text)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(text);

        lock (_gate)
        {
            if (_documents.TryGetValue(filePath, out var existing))
            {
                return ReplaceText(existing, text);
            }

            var owner = _projects
                .Where(p => filePath.StartsWith(p.Value.DirectoryPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .OrderByDescending(p => p.Value.DirectoryPath.Length)
                .Select(p => p.Key)
                .FirstOrDefault();

            if (owner is null)
            {
                return false;
            }

            var id = DocumentId.CreateNewId(owner, filePath);
            var solution = _workspace.CurrentSolution.AddDocument(id, Path.GetFileName(filePath), SourceText.From(text, Encoding.UTF8), filePath: filePath);
            if (!_workspace.TryApplyChanges(solution))
            {
                return false;
            }

            _documents[filePath] = id;
            return true;
        }
    }

    /// <summary>Removes a source file, for example after it was deleted.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public void RemoveDocument(string filePath)
    {
        lock (_gate)
        {
            if (_documents.Remove(filePath, out var id))
            {
                _workspace.TryApplyChanges(_workspace.CurrentSolution.RemoveDocument(id));
            }
        }
    }

    /// <summary>Returns the errors and warnings in one source file, including in-process safety checks.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the analysis, typically because the user typed again.</param>
    public async Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var (document, project) = Snapshot(filePath);
        if (document is null || project is null)
        {
            return [];
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return [];
        }

        var diagnostics = DiagnosticMapper.Map(model.GetDiagnostics(cancellationToken: cancellationToken), project.Name).ToList();
        if (project.IsExecutable)
        {
            diagnostics.AddRange(InProcessSafetyAnalyzer.AnalyzeTree(model.Compilation, model.SyntaxTree, project.Name, cancellationToken));
        }

        return diagnostics;
    }

    /// <summary>Returns the classified spans of part of a file, for syntax and semantic coloring.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="start">Start of the range to classify.</param>
    /// <param name="length">Length of the range to classify.</param>
    /// <param name="cancellationToken">Cancels the classification.</param>
    public async Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, int start, int length, CancellationToken cancellationToken = default)
    {
        var (document, _) = Snapshot(filePath);
        if (document is null)
        {
            return [];
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var safeStart = Math.Clamp(start, 0, text.Length);
        var safeLength = Math.Clamp(length, 0, text.Length - safeStart);

        var spans = await Classifier.GetClassifiedSpansAsync(document, new TextSpan(safeStart, safeLength), cancellationToken).ConfigureAwait(false);
        return spans
            .Where(s => !ClassificationTypeNames.AdditiveTypeNames.Contains(s.ClassificationType))
            .Select(s => new ClassifiedRange(s.TextSpan.Start, s.TextSpan.Length, s.ClassificationType))
            .ToList();
    }

    /// <summary>Returns the errors and warnings of a whole project, without emitting it.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The diagnostics, or an empty list when the project is not loaded.</returns>
    public async Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken = default)
    {
        Project? project;
        TsProject info;
        lock (_gate)
        {
            if (!_projectIds.TryGetValue(projectFilePath, out var id))
            {
                return [];
            }

            project = _workspace.CurrentSolution.GetProject(id);
            info = _projects[id];
        }

        if (project is null || await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
        {
            return [];
        }

        var diagnostics = DiagnosticMapper.Map(compilation.GetDiagnostics(cancellationToken), info.Name).ToList();
        if (info.IsExecutable)
        {
            diagnostics.AddRange(InProcessSafetyAnalyzer.Analyze(compilation, info.Name, cancellationToken));
        }

        return diagnostics;
    }

    /// <summary>Computes the edits that format a document with the default C# style.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels formatting.</param>
    /// <returns>Non-overlapping edits in document order, or an empty list when the file is not loaded.</returns>
    public async Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var (document, _) = Snapshot(filePath);
        if (document is null)
        {
            return [];
        }

        var formatted = await Formatter.FormatAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        var changes = await formatted.GetTextChangesAsync(document, cancellationToken).ConfigureAwait(false);
        return changes
            .OrderBy(c => c.Span.Start)
            .Select(c => new TextEdit(c.Span.Start, c.Span.Length, c.NewText ?? string.Empty))
            .ToList();
    }

    /// <summary>
    /// Allows or prevents Roslyn from compiling on several threads at once. TabStudio turns this off while the
    /// device is hot, so a build does not push it into thermal throttling.
    /// </summary>
    /// <param name="enabled">Whether concurrent compilation is allowed.</param>
    public void SetConcurrentBuild(bool enabled)
    {
        lock (_gate)
        {
            _concurrentBuild = enabled;
            var solution = _workspace.CurrentSolution;
            foreach (var project in solution.Projects)
            {
                if (project.CompilationOptions is { } options && options.ConcurrentBuild != enabled)
                {
                    solution = solution.WithProjectCompilationOptions(project.Id, options.WithConcurrentBuild(enabled));
                }
            }

            _workspace.TryApplyChanges(solution);
        }
    }

    /// <summary>Switches every project between the Debug and Release configurations.</summary>
    /// <param name="optimize">
    /// <see langword="true"/> for Release: optimized code and the <c>RELEASE</c> symbol.
    /// <see langword="false"/> for Debug: unoptimized code and the <c>DEBUG</c> symbol.
    /// </param>
    public void SetOptimization(bool optimize)
    {
        lock (_gate)
        {
            _optimize = optimize;
            var parseOptions = CSharpCompiler.CreateParseOptions(optimize);
            var level = optimize ? OptimizationLevel.Release : OptimizationLevel.Debug;
            var solution = _workspace.CurrentSolution;
            foreach (var project in solution.Projects)
            {
                if (project.CompilationOptions is { } options)
                {
                    solution = solution
                        .WithProjectCompilationOptions(project.Id, options.WithOptimizationLevel(level))
                        .WithProjectParseOptions(project.Id, parseOptions);
                }
            }

            _workspace.TryApplyChanges(solution);
        }
    }

    /// <summary>
    /// Returns the projects that must be compiled to run a project, dependencies first and the project itself last.
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    public IReadOnlyList<TsProject> GetBuildOrder(string projectFilePath)
    {
        lock (_gate)
        {
            if (!_projectIds.TryGetValue(projectFilePath, out var id))
            {
                return [];
            }

            var graph = _workspace.CurrentSolution.GetProjectDependencyGraph();
            var needed = graph.GetProjectsThatThisProjectTransitivelyDependsOn(id).Append(id).ToHashSet();

            return graph.GetTopologicallySortedProjects()
                .Where(needed.Contains)
                .Select(p => _projects[p])
                .ToList();
        }
    }

    /// <summary>Compiles one loaded project into memory.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    /// <param name="checkInProcessSafety">Whether to reject calls that would close TabStudio. Use for programs about to run.</param>
    /// <param name="cancellationToken">Cancels compilation.</param>
    /// <exception cref="ArgumentException">The project is not loaded.</exception>
    public async Task<CompilationResult> EmitAsync(string projectFilePath, bool checkInProcessSafety, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Project project;
        TsProject info;
        lock (_gate)
        {
            if (!_projectIds.TryGetValue(projectFilePath, out var id))
            {
                throw new ArgumentException($"The project '{projectFilePath}' is not loaded.", nameof(projectFilePath));
            }

            project = _workspace.CurrentSolution.GetProject(id)!;
            info = _projects[id];
        }

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Roslyn could not create a compilation for '{info.Name}'.");

        return CSharpCompiler.Emit(compilation, checkInProcessSafety, stopwatch, info.Name, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _workspace.Dispose();

    /// <summary>Creates the MEF host Roslyn needs for workspace features such as classification and completion.</summary>
    public static HostServices CreateHostServices()
    {
        var assemblies = MefHostServices.DefaultAssemblies
            .Append(typeof(Classifier).Assembly)
            .Append(Assembly.Load("Microsoft.CodeAnalysis.Features"))
            .Append(Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"))
            .Distinct();

        return MefHostServices.Create(assemblies);
    }

    /// <summary>Replaces a document's text. The caller holds the gate.</summary>
    private bool ReplaceText(DocumentId id, string text) =>
        _workspace.TryApplyChanges(_workspace.CurrentSolution.WithDocumentText(id, SourceText.From(text, Encoding.UTF8)));

    private (Document? Document, TsProject? Project) Snapshot(string filePath)
    {
        lock (_gate)
        {
            if (!_documents.TryGetValue(filePath, out var id))
            {
                return (null, null);
            }

            var document = _workspace.CurrentSolution.GetDocument(id);
            return (document, document is null ? null : _projects.GetValueOrDefault(document.Project.Id));
        }
    }

    private RoslynProjectInfo CreateProjectInfo(TsProject project, IReadOnlyList<string> allProjectDirectories, ISourceTextProvider sources)
    {
        var id = _projectIds[project.FilePath];
        var documents = new List<DocumentInfo>();

        foreach (var path in ProjectSourceFiles.Enumerate(project, allProjectDirectories))
        {
            var documentId = DocumentId.CreateNewId(id, path);
            documents.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
            _documents[path] = documentId;
        }

        if (project.ImplicitUsings)
        {
            var path = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.GlobalUsings.g.cs");
            documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, CSharpCompiler.CreateGlobalUsingsSource(), isGenerated: true));
        }

        var projectReferences = project.ProjectReferences
            .Where(_projectIds.ContainsKey)
            .Select(reference => new ProjectReference(_projectIds[reference]));

        var kind = project.IsExecutable ? AssemblyKind.ConsoleApplication : AssemblyKind.Library;
        var allowUnsafe = string.Equals(project.GetProperty("AllowUnsafeBlocks"), "true", StringComparison.OrdinalIgnoreCase);

        return RoslynProjectInfo.Create(
            id,
            VersionStamp.Create(),
            project.Name,
            project.GetProperty("AssemblyName") ?? project.Name,
            LanguageNames.CSharp,
            filePath: project.FilePath,
            compilationOptions: CSharpCompiler.CreateCompilationOptions(kind, project.NullableEnabled, _optimize, allowUnsafe, _concurrentBuild),
            parseOptions: CSharpCompiler.CreateParseOptions(_optimize),
            documents: documents,
            projectReferences: projectReferences,
            metadataReferences: GetMetadataReferences(project));
    }

    private static DocumentInfo CreateDocumentInfo(DocumentId id, string path, string text, bool isGenerated) =>
        DocumentInfo.Create(
            id,
            Path.GetFileName(path),
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text, Encoding.UTF8), VersionStamp.Create(), path)),
            filePath: path,
            isGenerated: isGenerated);

    /// <summary>Serves the texts captured by <see cref="TrimMemory"/>, and the disk for anything else.</summary>
    private sealed class SnapshotTextProvider(Dictionary<string, string> texts) : ISourceTextProvider
    {
        public string GetText(string filePath) =>
            texts.TryGetValue(filePath, out var text) ? text : FileSystemTextProvider.Instance.GetText(filePath);
    }

    private static string ReadSource(ISourceTextProvider sources, string path)
    {
        try
        {
            return sources.GetText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable file compiles as empty; the editor shows the I/O problem when it is opened.
            return string.Empty;
        }
    }
}
