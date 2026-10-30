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
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Compatibility;
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

    /// <summary>Files that are not C# but that generators read, such as Avalonia's <c>.axaml</c> files.</summary>
    private readonly Dictionary<string, DocumentId> _additionalDocuments = new(StringComparer.Ordinal);
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

            // Skeleton images that are not on disk yet are compiled in the background (see ReloadPending): until
            // then their projects are referenced in full, and a keystroke never waits for a compile.
            LoadUnderGate(workspace, sources);
        }
    }

    /// <summary>
    /// Releases every compilation Roslyn has cached, keeping the current text of every file. The next query
    /// recompiles only what it needs. Called when the device runs low on memory, often on the UI thread: the reload
    /// happens on a background thread, and <see cref="WhenReloadedAsync"/> completes when it has.
    /// </summary>
    public void TrimMemory()
    {
        lock (_gate)
        {
            RequestReloadUnderGate();
        }
    }

    private void LoadUnderGate(TsWorkspace workspace, ISourceTextProvider sources)
    {
        _workspace.ClearSolution();
        _documents.Clear();
        _additionalDocuments.Clear();
        _razorDocuments.Clear();
        _razorFiles.Clear();
        _unoGeneratorInputs.Clear();
        _projectIds.Clear();
        _projects.Clear();
        _projectsAsRead.Clear();
        _skeletons.Clear();
        _generatorProjects.Clear();

        // An Aspire AppHost compiles against code only its SDK generates, and runs only on a computer (see the
        // Compatibility Doctor), so it is not compiled here. Nor is a .NET Framework program that needs Windows:
        // compiled against .NET, its errors would only say that it is the wrong framework. Nor is a project that needs a
        // platform workload, such as a .NET MAUI app or the Android or iOS head of a cross-platform app, which only that
        // workload builds.
        var csharpProjects = workspace.Projects
            .Where(p => p.IsRoslynLanguage && p.Kind is not (ProjectKind.Unknown or ProjectKind.AspireAppHost))
            .Where(p => !NetFrameworkCompatibility.NeedsWindows(p) && !ProjectClassifier.NeedsPlatformWorkload(p))
            .ToList();

        // Projects whose packages were restored before get what their packages' props and targets add.
        var asRead = csharpProjects.ToDictionary(p => p.FilePath, StringComparer.Ordinal);
        csharpProjects = [.. csharpProjects.Select(WithPackageBuild)];
        foreach (var project in csharpProjects)
        {
            var id = ProjectId.CreateNewId(project.Name);
            _projectIds[project.FilePath] = id;
            _projects[id] = project;
            _projectsAsRead[id] = asRead[project.FilePath];
        }

        IndexProjects();

        var allProjectDirectories = workspace.Projects.Select(p => p.DirectoryPath).ToList();
        AddSolution(workspace, csharpProjects, allProjectDirectories, sources);

        // With skeleton references, the second solution references the images of the projects nobody edits, so their
        // full compilations are never kept; images not made yet are compiled from the first in the background.
        if (CreateSkeletons(csharpProjects))
        {
            var texts = new SnapshotTextProvider(CurrentTexts());
            _workspace.ClearSolution();
            _documents.Clear();
            _additionalDocuments.Clear();
            _razorDocuments.Clear();
            AddSolution(workspace, csharpProjects, allProjectDirectories, texts);
        }
    }

    private void AddSolution(TsWorkspace workspace, List<TsProject> csharpProjects, List<string> allProjectDirectories, ISourceTextProvider sources)
    {
        var projectInfos = csharpProjects
            .Select(project => CreateProjectInfo(project, allProjectDirectories, sources))
            .ToList();

        _workspace.AddSolution(RoslynSolutionInfo.Create(
            SolutionId.CreateNewId(workspace.Name),
            VersionStamp.Create(),
            workspace.Solution?.FilePath,
            projectInfos));
    }

    /// <summary>Whether a file is a source file of a loaded project: a C# or Visual Basic file, or a Razor component, view or page.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public bool ContainsDocument(string filePath)
    {
        lock (_gate)
        {
            return _documents.ContainsKey(filePath) || _razorDocuments.Contains(filePath) || IsUnoXamlDocumentUnderGate(filePath);
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
            if (_additionalDocuments.TryGetValue(filePath, out var additional))
            {
                return _workspace.TryApplyChanges(_workspace.CurrentSolution.WithAdditionalDocumentText(additional, SourceText.From(text, Encoding.UTF8)));
            }

            if (!_documents.TryGetValue(filePath, out var id) || !ReplaceText(id, text))
            {
                return false;
            }

            // A project referenced as a skeleton image changed: its image no longer matches, so it is made again in
            // the background. Until then the projects that use it see its declarations as they were.
            if (_skeletons.Count > 0 && _projects.TryGetValue(id.ProjectId, out var project) && _skeletons.ContainsKey(project.FilePath))
            {
                RequestReloadUnderGate();
            }
            else if (_projects.TryGetValue(id.ProjectId, out var edited))
            {
                // A source generator built from this project is built again for the projects that use it.
                InvalidateGenerators(edited.FilePath);
            }

            return true;
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

        // A new Razor file is an additional file with its own configuration; it arrives with the next Load.
        if (Razor.RazorProjects.IsRazorFile(filePath))
        {
            return false;
        }

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

            // A Visual Basic project compiles .vb files, and a C# project never does.
            if (owner is null || _projects[owner].IsVisualBasic != VisualBasic.VisualBasicCompiler.IsSource(filePath))
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
            InvalidateGenerators(_projects[owner].FilePath);
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
                if (_projects.TryGetValue(id.ProjectId, out var project))
                {
                    InvalidateGenerators(project.FilePath);
                }
            }
        }
    }

    /// <summary>Returns the errors and warnings in one source file, including in-process safety checks.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the analysis, typically because the user typed again.</param>
    public async Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (IsRazorDocument(filePath))
        {
            return await GetRazorDocumentDiagnosticsAsync(filePath, cancellationToken).ConfigureAwait(false);
        }

        if (IsUnoXamlDocument(filePath))
        {
            return await GetUnoXamlDiagnosticsAsync(filePath, cancellationToken).ConfigureAwait(false);
        }

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

        var diagnostics = DiagnosticMapper.Map(ExplainForUno(project, model.GetDiagnostics(cancellationToken: cancellationToken)), project.Name).ToList();
        if (WpfProject.UsesWpf(project))
        {
            return WpfDiagnostics.Explain(diagnostics, model, project, cancellationToken);
        }

        if (project.IsExecutable)
        {
            diagnostics.AddRange(InProcessSafetyAnalyzer.AnalyzeTree(model.Compilation, model.SyntaxTree, project.Name, cancellationToken));
        }

        diagnostics.AddRange(Uno.UnoCompatibility.AnalyzeTree(model, project, cancellationToken));
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

        var diagnostics = await Razor.RazorCompilations.MapAsync(project, info, compilation.GetDiagnostics(cancellationToken), cancellationToken).ConfigureAwait(false);
        diagnostics.AddRange(AnalyzerFailuresFor(info, project.AnalyzerReferences.Select(r => r.FullPath)));
        diagnostics.AddRange(GeneratorProjectFailures(info, project.AnalyzerReferences));
        if (WpfProject.UsesWpf(info))
        {
            return WpfDiagnostics.Explain(WpfDiagnostics.Map(compilation.GetDiagnostics(cancellationToken), info), compilation, info, cancellationToken);
        }

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
            var level = optimize ? OptimizationLevel.Release : OptimizationLevel.Debug;
            var solution = _workspace.CurrentSolution;
            foreach (var project in solution.Projects)
            {
                if (project.CompilationOptions is { } options)
                {
                    var parseOptions = ParseOptionsFor(_projects.GetValueOrDefault(project.Id), optimize);
                    var optimized = options.WithOptimizationLevel(level);
                    if (optimized is Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions basicOptions
                        && parseOptions is Microsoft.CodeAnalysis.VisualBasic.VisualBasicParseOptions basicParseOptions)
                    {
                        // Visual Basic's My template is parsed with the compilation's own parse options.
                        optimized = basicOptions.WithParseOptions(basicParseOptions);
                    }

                    solution = solution
                        .WithProjectCompilationOptions(project.Id, optimized)
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

            return BuildOrder.For(_projects[id], _projects.Values.ToDictionary(p => p.FilePath, StringComparer.Ordinal));
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

        if (await EmitWpfAsync(info, compilation, stopwatch, cancellationToken).ConfigureAwait(false) is { } wpf)
        {
            return wpf;
        }

        var result = await Razor.RazorCompilations.EmitAsync(project, info, compilation, checkInProcessSafety, stopwatch, cancellationToken).ConfigureAwait(false);
        List<BuildDiagnostic> failures = [.. AnalyzerFailuresFor(info, project.AnalyzerReferences.Select(r => r.FullPath)), .. GeneratorProjectFailures(info, project.AnalyzerReferences)];
        return failures.Count > 0
            ? result with { Diagnostics = [.. result.Diagnostics, .. failures] }
            : result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        _workspace.Dispose();
    }

    /// <summary>Creates the MEF host Roslyn needs for workspace features such as classification and completion.</summary>
    public static HostServices CreateHostServices()
    {
        var assemblies = MefHostServices.DefaultAssemblies
            .Append(typeof(Classifier).Assembly)
            .Append(Assembly.Load("Microsoft.CodeAnalysis.Features"))
            .Append(Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"))
            .Append(Assembly.Load("Microsoft.CodeAnalysis.VisualBasic.Workspaces"))
            .Append(Assembly.Load("Microsoft.CodeAnalysis.VisualBasic.Features"))
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
        if (project.IsVisualBasic)
        {
            return CreateVisualBasicProjectInfo(project, allProjectDirectories, sources);
        }

        var id = _projectIds[project.FilePath];
        var documents = new List<DocumentInfo>();

        foreach (var path in ProjectSourceFiles.Enumerate(project, allProjectDirectories).Where(path => IsCompiledSource(project, path)))
        {
            var documentId = DocumentId.CreateNewId(id, path);
            documents.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
            _documents[path] = documentId;
        }

        var additionalDocuments = new List<DocumentInfo>();
        if (UsesAvaloniaXaml(project))
        {
            foreach (var path in ProjectSourceFiles.Enumerate(project, allProjectDirectories, ".axaml"))
            {
                var documentId = DocumentId.CreateNewId(id, path);
                additionalDocuments.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
                _additionalDocuments[path] = documentId;
            }
        }

        // The project's and its packages' AdditionalFiles items, which generators read through AdditionalTextsProvider.
        foreach (var path in AdditionalFilePaths(project).Where(path => !_additionalDocuments.ContainsKey(path)))
        {
            var documentId = DocumentId.CreateNewId(id, path);
            additionalDocuments.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
            _additionalDocuments[path] = documentId;
        }

        AddRazorDocuments(project, id, allProjectDirectories, sources, documents, additionalDocuments);
        AddWpfDocuments(project, id, sources, additionalDocuments);

        AddUnoDocuments(project, id, allProjectDirectories, sources, documents, additionalDocuments);

        if (CSharpCompiler.CreateGlobalUsingsSource(project) is { } globalUsings)
        {
            var path = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.GlobalUsings.g.cs");
            documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, globalUsings, isGenerated: true));
        }

        if (CreateAssemblyAttributesSource(project) is { } assemblyInfo)
        {
            var path = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.AssemblyInfo.g.cs");
            documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, assemblyInfo, isGenerated: true));
        }

        documents.AddRange(WindowsFormsDocuments(id, project));
        if (DesktopSettings.Applies(project, documents.Where(d => !d.IsGenerated).Select(d => d.FilePath ?? string.Empty)))
        {
            var path = Path.Combine(project.DirectoryPath, "obj", "TabStudio", "System.Configuration.Settings.g.cs");
            documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, DesktopSettings.Source, isGenerated: true));
        }

        var projectReferences = CompileReferencesOf(project)
            .Where(reference => _projectIds.ContainsKey(reference) && !_skeletons.ContainsKey(reference))
            .Select(reference => new ProjectReference(_projectIds[reference]));

        // An Uno or WinUI 3 app has no Main of its own here: TabStudio's host starts it, as each platform's head does.
        // Nor has a test project, even an executable one such as xUnit v3's, whose Main its package's targets generate:
        // TabStudio's test runner loads it as a library.
        var kind = project.IsExecutable && !project.IsTestProject && !Uno.UnoBuild.UsesUno(project) ? AssemblyKind.ConsoleApplication : AssemblyKind.Library;
        var allowUnsafe = string.Equals(project.GetProperty("AllowUnsafeBlocks"), "true", StringComparison.OrdinalIgnoreCase);

        return RoslynProjectInfo.Create(
            id,
            VersionStamp.Create(),
            project.Name,
            project.GetProperty("AssemblyName") ?? project.Name,
            LanguageNames.CSharp,
            filePath: project.FilePath,
            compilationOptions: WithStrongName(WithPathMap(CSharpCompiler.WithProjectWarnings(CSharpCompiler.CreateCompilationOptions(kind, project.NullableEnabled, _optimize, allowUnsafe, _concurrentBuild), project), project), project),
            parseOptions: ParseOptionsFor(project, _optimize),
            documents: documents,
            projectReferences: projectReferences,
            metadataReferences: GetMetadataReferences(project),
            analyzerReferences: GetAnalyzerReferences(project),
            additionalDocuments: additionalDocuments)
            .WithAnalyzerConfigDocuments([CreateAnalyzerConfig(id, project, _razorFiles.GetValueOrDefault(id) ?? [], _unoGeneratorInputs.GetValueOrDefault(id) ?? [])]);
    }

    /// <summary>The current text of every source and additional file, to load the solution again with. The caller holds the gate.</summary>
    private Dictionary<string, string> CurrentTexts()
    {
        var solution = _workspace.CurrentSolution;
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, id) in _documents)
        {
            if (solution.GetDocument(id) is { } document && document.TryGetText(out var text))
            {
                texts[path] = text.ToString();
            }
        }

        foreach (var (path, id) in _additionalDocuments)
        {
            if (solution.GetAdditionalDocument(id) is { } document && document.TryGetText(out var text))
            {
                texts[path] = text.ToString();
            }
        }

        return texts;
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
