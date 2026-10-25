using System.Collections.Concurrent;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Link.Language;
using TabStudio.LiveTesting.Building;

namespace TabStudio.App.Services.Link;

/// <summary>
/// What a host runs for a tablet: the IDE's own <see cref="LocalLanguageService"/>, over the host's mirror of the
/// tablet's workspace.
/// </summary>
internal sealed class LocalLinkBackend(LocalLanguageService language) : ILinkLanguageBackend
{
    /// <summary>The slots of the tablet's coverage recorder, by project path on this host.</summary>
    private readonly ConcurrentDictionary<string, int> _slots = new(StringComparer.Ordinal);
    private CoverageBuilder? _coverage;

    /// <summary>The language service, for its settings such as concurrent builds.</summary>
    public LocalLanguageService Language => language;

    public void Load(Workspace workspace, IReadOnlyList<KeyValuePair<string, string>> pathMap)
    {
        language.Host.SetPathMap(pathMap);
        language.Reload(workspace, FileSystemTextProvider.Instance);
    }

    public bool ContainsDocument(string filePath) => language.ContainsDocument(filePath);

    public void UpdateText(string filePath, string text) => language.UpdateText(filePath, text);

    public bool AddDocument(string filePath, string text) => language.AddDocument(filePath, text);

    public void SetOptimization(bool optimize) => language.SetOptimization(optimize);

    public void SetActiveProjects(IEnumerable<string> projectFilePaths) => language.SetActiveProjects(projectFilePaths);

    public Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken) =>
        language.GetDocumentDiagnosticsAsync(filePath, cancellationToken);

    public Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken) =>
        language.GetProjectDiagnosticsAsync(projectFilePath, cancellationToken);

    public Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken) =>
        language.GetClassificationsAsync(filePath, cancellationToken);

    public Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken) =>
        language.GetFormattingEditsAsync(filePath, cancellationToken);

    public Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        language.BuildAsync(project, cancellationToken);

    public Task<DebugBuild> BuildForDebuggingAsync(ProjectInfo project, CancellationToken cancellationToken) =>
        language.BuildForDebuggingAsync(project, cancellationToken);

    /// <remarks>
    /// The host's own Live Unit Testing builds report to the host's slots; these report to the tablet's, so they
    /// have a builder of their own, which keeps what it compiled across the tablet's builds.
    /// </remarks>
    public Task<CoverageBuild> BuildForCoverageAsync(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, IReadOnlyDictionary<string, int> slots, CancellationToken cancellationToken)
    {
        foreach (var (path, slot) in slots)
        {
            _slots[path] = slot;
        }

        var builder = LazyInitializer.EnsureInitialized(ref _coverage, () => new CoverageBuilder(language.Host, SlotOf));
        return Task.Run(() => builder.BuildAsync(testProject, uninstrumented, cancellationToken), cancellationToken);
    }

    public async Task<LinkRestoreOutcome> RestorePackagesAsync(ProjectInfo project, CancellationToken cancellationToken)
    {
        var result = await language.RestorePackagesAsync(project, null, cancellationToken).ConfigureAwait(false);
        return new LinkRestoreOutcome(result.Success, result.Diagnostics, result.FromCache, result.Duration);
    }

    public Task<IReadOnlyList<BuildDiagnostic>> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken) =>
        language.AddFrameworkGeneratorsAsync(cancellationToken);

    public Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken) =>
        language.Features.GetCompletionsAsync(filePath, position, trigger, cancellationToken);

    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken) =>
        language.Features.GetCompletionChangeAsync(filePath, entry, commitCharacter, cancellationToken);

    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken) =>
        language.Features.GetCompletionDescriptionAsync(entry, cancellationToken);

    public Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.GetQuickInfoAsync(filePath, position, cancellationToken);

    public Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.GetSignatureHelpAsync(filePath, position, cancellationToken);

    public Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.FindDefinitionAsync(filePath, position, cancellationToken);

    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.FindImplementationsAsync(filePath, position, cancellationToken);

    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.FindReferencesAsync(filePath, position, cancellationToken);

    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.GetDocumentHighlightsAsync(filePath, position, cancellationToken);

    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.CanRenameAsync(filePath, position, cancellationToken);

    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) =>
        language.Features.GetRenameEditsAsync(filePath, position, newName, options, cancellationToken);

    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        language.Features.GetCodeActionsAsync(filePath, start, length, cancellationToken);

    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        language.Features.GetCodeActionEditsAsync(id, cancellationToken);

    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) =>
        language.Features.GetDocumentSymbolsAsync(filePath, cancellationToken);

    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) =>
        language.Features.GetSymbolPathAsync(filePath, position, cancellationToken);

    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) =>
        language.Features.WarmUpAsync(filePath, cancellationToken);

    public Task<IReadOnlyList<BuildDiagnostic>> GetSnapshotDiagnosticsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        Task.Run(() => language.Host.CreateSnapshot(changes).GetDiagnosticsAsync(cancellationToken), cancellationToken);

    public Task<IReadOnlyList<LinkProjectBuild>> BuildSnapshotTestProjectsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        Task.Run(() => SnapshotBuilds.BuildTestProjectsAsync(language.Host, changes, cancellationToken), cancellationToken);

    public void Dispose() => language.Dispose();

    private int SlotOf(string projectFilePath) =>
        _slots.TryGetValue(projectFilePath, out var slot)
            ? slot
            : throw new InvalidOperationException($"The tablet gave '{Path.GetFileName(projectFilePath)}' no Live Unit Testing slot.");
}
