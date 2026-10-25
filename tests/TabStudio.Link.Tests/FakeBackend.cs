using System.Collections.Concurrent;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Debugging.Symbols;
using TabStudio.Link.Language;
using TabStudio.LiveTesting.Building;

namespace TabStudio.Link.Tests;

/// <summary>
/// A language service that records what it was asked and answers with results on the host's paths, so tests can see
/// the server map them. Given a <see cref="Roslyn"/> host, it also loads the mirror there and makes real Live Unit
/// Testing builds, as the IDE's backend does.
/// </summary>
internal sealed class FakeBackend : ILinkLanguageBackend
{
    private readonly ConcurrentDictionary<string, int> _slots = new(StringComparer.Ordinal);
    private CoverageBuilder? _coverage;

    /// <summary>A Roslyn host that loads the mirror and compiles Live Unit Testing builds, or null to fake them.</summary>
    public RoslynWorkspaceHost? Roslyn { get; init; }

    /// <summary>The assemblies that stand in for the restored packages of test projects.</summary>
    public IReadOnlyList<string> TestPackages { get; init; } = [];

    /// <summary>How many Live Unit Testing builds the host made.</summary>
    public int CoverageBuilds { get; private set; }
    public Workspace? Workspace { get; private set; }

    public IReadOnlyList<KeyValuePair<string, string>> PathMap { get; private set; } = [];

    public int Loads { get; private set; }

    public bool Optimize { get; private set; }

    public ConcurrentDictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);

    public bool IsDisposed { get; private set; }

    private string Program => Path.Combine(Workspace!.Projects[0].DirectoryPath, "Program.cs");

    public void Load(Workspace workspace, IReadOnlyList<KeyValuePair<string, string>> pathMap)
    {
        Workspace = workspace;
        PathMap = pathMap;
        Loads++;
        if (Roslyn is { } roslyn)
        {
            roslyn.SetPathMap(pathMap);
            roslyn.Load(workspace, FileSystemTextProvider.Instance);
            foreach (var project in workspace.Projects.Where(p => p.IsTestProject))
            {
                roslyn.SetPackageReferences(project.FilePath, TestPackages);
            }
        }
    }

    public bool ContainsDocument(string filePath) => filePath.EndsWith(".cs", StringComparison.Ordinal);

    public void UpdateText(string filePath, string text)
    {
        Texts[filePath] = text;
        Roslyn?.UpdateText(filePath, text);
    }

    public bool AddDocument(string filePath, string text)
    {
        Texts[filePath] = text;
        return true;
    }

    public void SetOptimization(bool optimize) => Optimize = optimize;

    public void SetActiveProjects(IEnumerable<string> projectFilePaths)
    {
    }

    public Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BuildDiagnostic>>(
            [new BuildDiagnostic("CS0103", DiagnosticSeverityLevel.Error, "The name 'Consol' does not exist in the current context", new DiagnosticLocation(filePath, 0, 6, 1, 1), "HelloTablet")]);

    public Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BuildDiagnostic>>([]);

    public Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ClassifiedRange>>([new ClassifiedRange(0, 7, "class name")]);

    public Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TextEdit>>([]);

    public Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        Task.FromResult(Build(project));

    public Task<DebugBuild> BuildForDebuggingAsync(ProjectInfo project, CancellationToken cancellationToken)
    {
        var span = new DebugSourceSpan(Program, 1, 1, 1, 22, 0, 21);
        var map = DebugInfoMap.Create([new DebugStatement(1, 1, span, [])], [new DebugMethod(1, "Program.<Main>$(string[])", span, [])]);
        return Task.FromResult(new DebugBuild(Build(project), map));
    }

    public Task<CoverageBuild> BuildForCoverageAsync(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, IReadOnlyDictionary<string, int> slots, CancellationToken cancellationToken)
    {
        var roslyn = Roslyn ?? throw new NotSupportedException("Give the fake backend a Roslyn host for Live Unit Testing builds.");
        foreach (var (path, slot) in slots)
        {
            _slots[path] = slot;
        }

        CoverageBuilds++;
        _coverage ??= new CoverageBuilder(roslyn, path => _slots[path]);
        return _coverage.BuildAsync(testProject, uninstrumented, cancellationToken);
    }

    public Task<LinkRestoreOutcome> RestorePackagesAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        Task.FromResult(new LinkRestoreOutcome(true, [], FromCache: true, TimeSpan.Zero));

    public Task<IReadOnlyList<BuildDiagnostic>> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BuildDiagnostic>>([]);

    public Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken) =>
        Task.FromResult(new CompletionResult(
            [new CompletionEntry(new CompletionHandle(1, 0), "WriteLine", string.Empty, string.Empty, "WriteLine", "WriteLine", string.Empty, SymbolGlyph.Method, IsPreselected: true, position, 0)],
            position,
            0,
            IsSuggestionMode: false));

    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken) =>
        Task.FromResult(new CompletionCommit([new TextEdit(entry.SpanStart, 0, entry.DisplayText + commitCharacter)], null, commitCharacter is not null));

    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TaggedRun>>([new TaggedRun("void", TaggedTextKind.Keyword)]);

    public Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult<QuickInfoResult?>(null);

    public Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult<SignatureHelpResult?>(null);

    public Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult<DefinitionResult?>(new DefinitionResult("Greeter", SymbolGlyph.Class, [new SourceLocation(filePath, 0, 7, 1, 1)], null));

    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DefinitionResult>>([]);

    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReferenceItem>>([new ReferenceItem(filePath, 0, 7, 1, 1, "Console.WriteLine();", IsDefinition: false)]);

    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentHighlight>>([]);

    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult(new RenameTarget(true, null, "Greeter", 0, 7));

    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) =>
        Task.FromResult(new RenameResult(true, null, [new DocumentEdit(filePath, DocumentEditKind.Modified, [new TextEdit(0, 7, newName)])], []));

    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CodeActionItem>>([]);

    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentEdit>>([]);

    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentSymbol>>([]);

    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentSymbol>>([]);

    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<BuildDiagnostic>> GetSnapshotDiagnosticsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BuildDiagnostic>>(
            [.. changes.Keys.Select(path => new BuildDiagnostic("CS1002", DiagnosticSeverityLevel.Error, "; expected", new DiagnosticLocation(path, 0, 1, 1, 1), "HelloTablet"))]);

    public Task<IReadOnlyList<LinkProjectBuild>> BuildSnapshotTestProjectsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LinkProjectBuild>>([new LinkProjectBuild(Workspace!.Projects[0].FilePath, Build(Workspace.Projects[0]))]);

    public void Dispose() => IsDisposed = true;

    private static BuildResult Build(ProjectInfo project) =>
        new(project.Name, [], new CompiledAssembly(project.Name, [1, 2, 3], [4, 5]), [new CompiledAssembly("Greetings", [6, 7], null)], TimeSpan.FromMilliseconds(12));
}
