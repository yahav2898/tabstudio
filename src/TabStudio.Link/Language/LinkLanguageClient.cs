using System.Collections.Concurrent;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Debugging.Symbols;
using TabStudio.Link.Messaging;

namespace TabStudio.Link.Language;

/// <summary>
/// The tablet's side of the language service over TabStudio Link: typed requests to the host's
/// <see cref="LinkLanguageServer"/>, with the tablet's own paths.
/// </summary>
/// <remarks>
/// Loading sends only the files the host's mirror lacks, in batches, so a reconnect after a short drop costs a file
/// list. Every method may be called from any thread.
/// </remarks>
public sealed class LinkLanguageClient
{
    /// <summary>The most bytes of files sent in one upload request.</summary>
    private const long UploadBatchLength = 8L * 1024 * 1024;

    private readonly LinkConnection _connection;
    private readonly ConcurrentDictionary<string, FileHash> _hashes = new(StringComparer.Ordinal);

    /// <summary>Creates the client over an authenticated connection to a host.</summary>
    /// <param name="connection">The connection.</param>
    public LinkLanguageClient(LinkConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>The connection to the host.</summary>
    public LinkConnection Connection => _connection;

    /// <summary>Sends a workspace to the host and loads it there.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="sources">The text of C# and XAML files, including unsaved edits.</param>
    /// <param name="optimize">Whether the Release configuration is selected.</param>
    /// <param name="activeProjects">The projects being edited.</param>
    /// <param name="progress">Receives what is being sent, such as <c>Sending 12 files</c>.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The C# documents of the loaded projects.</returns>
    public async Task<IReadOnlyList<string>> LoadAsync(
        Workspace workspace,
        ISourceTextProvider sources,
        bool optimize,
        IReadOnlyList<string> activeProjects,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(activeProjects);

        var content = await Task.Run(() => LinkWorkspaceContent.Collect(workspace, sources, _hashes, cancellationToken), cancellationToken).ConfigureAwait(false);
        var prepare = new PrepareWorkspaceRequest(workspace.Name, content.Root, char.ToString(Path.DirectorySeparatorChar), content.Files);
        var missing = (await RequestAsync<PrepareWorkspaceRequest, PrepareWorkspaceResponse>(LinkLanguageMethods.PrepareWorkspace, prepare, cancellationToken).ConfigureAwait(false)).Missing;

        var sent = 0;
        while (sent < missing.Count)
        {
            var batch = new List<int>();
            var contents = new List<ReadOnlyMemory<byte>>();
            long length = 0;
            while (sent < missing.Count && (batch.Count == 0 || length < UploadBatchLength))
            {
                var index = missing[sent++];
                if (index < 0 || index >= content.Files.Count)
                {
                    throw new LinkProtocolException($"The host asked for file {index}, which does not exist.");
                }

                var bytes = await Task.Run(() => content.Read(index), cancellationToken).ConfigureAwait(false);
                batch.Add(index);
                contents.Add(bytes);
                length += bytes.Length;
            }

            progress?.Report($"Sending {workspace.Name}: {sent} of {missing.Count} files");
            await _connection.RequestAsync(LinkLanguageMethods.UploadFiles, LinkLanguageJson.ToJson(new UploadFilesRequest(batch)), contents, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report($"Loading {workspace.Name}");
        var load = new LoadWorkspaceRequest(content.EntryPath, optimize, activeProjects);
        return (await RequestAsync<LoadWorkspaceRequest, LoadWorkspaceResponse>(LinkLanguageMethods.LoadWorkspace, load, cancellationToken).ConfigureAwait(false)).Documents;
    }

    /// <summary>Sends the editor's text of a file.</summary>
    /// <param name="filePath">The file.</param>
    /// <param name="text">Its text.</param>
    public Task UpdateTextAsync(string filePath, string text) =>
        _connection.NotifyAsync(LinkLanguageMethods.UpdateText, LinkLanguageJson.ToJson(new TextChange(filePath, text)));

    /// <summary>Adds a new C# file.</summary>
    /// <param name="filePath">The file.</param>
    /// <param name="text">Its text.</param>
    public Task AddDocumentAsync(string filePath, string text) =>
        _connection.NotifyAsync(LinkLanguageMethods.AddDocument, LinkLanguageJson.ToJson(new TextChange(filePath, text)));

    /// <summary>Switches between Debug and Release.</summary>
    /// <param name="optimize">Whether to optimize.</param>
    public Task SetOptimizationAsync(bool optimize) =>
        _connection.NotifyAsync(LinkLanguageMethods.SetOptimization, LinkLanguageJson.ToJson(new OptimizationSetting(optimize)));

    /// <summary>Tells the host which projects are being edited.</summary>
    /// <param name="projectFilePaths">The project files.</param>
    public Task SetActiveProjectsAsync(IReadOnlyList<string> projectFilePaths) =>
        _connection.NotifyAsync(LinkLanguageMethods.SetActiveProjects, LinkLanguageJson.ToJson(new ActiveProjectsSetting(projectFilePaths)));

    /// <summary>Errors and warnings in one file.</summary>
    public Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken) =>
        RequestAsync<FileRequest, IReadOnlyList<BuildDiagnostic>>(LinkLanguageMethods.DocumentDiagnostics, new FileRequest(filePath), cancellationToken);

    /// <summary>Errors and warnings in a whole project.</summary>
    public Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken) =>
        RequestAsync<ProjectRequest, IReadOnlyList<BuildDiagnostic>>(LinkLanguageMethods.ProjectDiagnostics, new ProjectRequest(projectFilePath), cancellationToken);

    /// <summary>Semantic classification of a whole file.</summary>
    public Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken) =>
        RequestAsync<FileRequest, IReadOnlyList<ClassifiedRange>>(LinkLanguageMethods.Classifications, new FileRequest(filePath), cancellationToken);

    /// <summary>The edits that format a file.</summary>
    public Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken) =>
        RequestAsync<FileRequest, IReadOnlyList<TextEdit>>(LinkLanguageMethods.Format, new FileRequest(filePath), cancellationToken);

    /// <summary>Builds a project on the host; its assemblies come back to run here.</summary>
    /// <param name="projectFilePath">The project file.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public async Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        var payload = await _connection.RequestAsync(LinkLanguageMethods.Build, LinkLanguageJson.ToJson(new ProjectRequest(projectFilePath)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return DecodeBuild(payload.Read(LinkLanguageJson.Info<BuildResponse>()), payload);
    }

    /// <summary>Builds a project for the debugger on the host; the instrumented assemblies come back to debug here.</summary>
    /// <param name="projectFilePath">The project file.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public async Task<DebugBuild> BuildForDebuggingAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        var payload = await _connection.RequestAsync(LinkLanguageMethods.BuildForDebugging, LinkLanguageJson.ToJson(new ProjectRequest(projectFilePath)), cancellationToken: cancellationToken).ConfigureAwait(false);
        var response = payload.Read(LinkLanguageJson.Info<DebugBuildResponse>());
        var result = DecodeBuild(response.Result, payload);
        var map = result.Success ? DebugInfoMap.Create(response.Statements, response.Methods) : DebugInfoMap.Empty;
        return new DebugBuild(result, map);
    }

    /// <summary>Restores a project's packages on the host, which compiles against them.</summary>
    public Task<LinkRestoreOutcome> RestorePackagesAsync(string projectFilePath, CancellationToken cancellationToken) =>
        RequestAsync<ProjectRequest, LinkRestoreOutcome>(LinkLanguageMethods.RestorePackages, new ProjectRequest(projectFilePath), cancellationToken);

    /// <summary>Gives the host's projects .NET's own source generators.</summary>
    public async Task<IReadOnlyList<BuildDiagnostic>> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken)
    {
        var payload = await _connection.RequestAsync(LinkLanguageMethods.AddFrameworkGenerators, cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload.Read(LinkLanguageJson.Info<IReadOnlyList<BuildDiagnostic>>());
    }

    /// <summary>The completion list at a position.</summary>
    public Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken) =>
        RequestAsync<CompletionListRequest, CompletionResult>(LinkLanguageMethods.Completions, new CompletionListRequest(filePath, position, trigger), cancellationToken);

    /// <summary>The edits that commit a completion item.</summary>
    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken) =>
        RequestAsync<CompletionChangeRequest, CompletionCommit>(
            LinkLanguageMethods.CompletionChange,
            new CompletionChangeRequest(filePath, entry, commitCharacter is { } c ? char.ToString(c) : null),
            cancellationToken);

    /// <summary>The description of a completion item.</summary>
    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken) =>
        RequestAsync<CompletionDescriptionRequest, IReadOnlyList<TaggedRun>>(LinkLanguageMethods.CompletionDescription, new CompletionDescriptionRequest(entry), cancellationToken);

    /// <summary>Quick Info at a position.</summary>
    public async Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        (await RequestAsync<PositionRequest, Optional<QuickInfoResult>>(LinkLanguageMethods.QuickInfo, new PositionRequest(filePath, position), cancellationToken).ConfigureAwait(false)).Value;

    /// <summary>The overloads of the call at a position.</summary>
    public async Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) =>
        (await RequestAsync<PositionRequest, Optional<SignatureHelpResult>>(LinkLanguageMethods.SignatureHelp, new PositionRequest(filePath, position), cancellationToken).ConfigureAwait(false)).Value;

    /// <summary>Where the symbol at a position is defined.</summary>
    public async Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        (await RequestAsync<PositionRequest, Optional<DefinitionResult>>(LinkLanguageMethods.Definition, new PositionRequest(filePath, position), cancellationToken).ConfigureAwait(false)).Value;

    /// <summary>The implementations of the symbol at a position.</summary>
    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        RequestAsync<PositionRequest, IReadOnlyList<DefinitionResult>>(LinkLanguageMethods.Implementations, new PositionRequest(filePath, position), cancellationToken);

    /// <summary>Every reference to the symbol at a position.</summary>
    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) =>
        RequestAsync<PositionRequest, IReadOnlyList<ReferenceItem>>(LinkLanguageMethods.References, new PositionRequest(filePath, position), cancellationToken);

    /// <summary>The occurrences of the symbol at a position in its file.</summary>
    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        RequestAsync<PositionRequest, IReadOnlyList<DocumentHighlight>>(LinkLanguageMethods.Highlights, new PositionRequest(filePath, position), cancellationToken);

    /// <summary>Whether the symbol at a position can be renamed.</summary>
    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) =>
        RequestAsync<PositionRequest, RenameTarget>(LinkLanguageMethods.CanRename, new PositionRequest(filePath, position), cancellationToken);

    /// <summary>The edits that rename the symbol at a position.</summary>
    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) =>
        RequestAsync<RenameRequest, RenameResult>(LinkLanguageMethods.RenameEdits, new RenameRequest(filePath, position, newName, options), cancellationToken);

    /// <summary>The quick fixes and refactorings for a selection.</summary>
    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        RequestAsync<CodeActionsRequest, IReadOnlyList<CodeActionItem>>(LinkLanguageMethods.CodeActions, new CodeActionsRequest(filePath, start, length), cancellationToken);

    /// <summary>What a code action changes.</summary>
    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        RequestAsync<CodeActionEditsRequest, IReadOnlyList<DocumentEdit>>(LinkLanguageMethods.CodeActionEdits, new CodeActionEditsRequest(id), cancellationToken);

    /// <summary>The declarations of a file.</summary>
    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) =>
        RequestAsync<FileRequest, IReadOnlyList<DocumentSymbol>>(LinkLanguageMethods.DocumentSymbols, new FileRequest(filePath), cancellationToken);

    /// <summary>The declarations that contain a position.</summary>
    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) =>
        RequestAsync<PositionRequest, IReadOnlyList<DocumentSymbol>>(LinkLanguageMethods.SymbolPath, new PositionRequest(filePath, position), cancellationToken);

    /// <summary>Prepares IntelliSense on the host.</summary>
    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) =>
        _connection.RequestAsync(LinkLanguageMethods.WarmUp, LinkLanguageJson.ToJson(new FileRequest(filePath)), cancellationToken: cancellationToken);

    /// <summary>The errors and warnings of the workspace with changes that are not applied yet, such as the agent's.</summary>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public Task<IReadOnlyList<BuildDiagnostic>> GetSnapshotDiagnosticsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken) =>
        RequestAsync<SnapshotRequest, IReadOnlyList<BuildDiagnostic>>(LinkLanguageMethods.SnapshotDiagnostics, Snapshot(changes), cancellationToken);

    /// <summary>Builds every test project of the workspace with changes that are not applied yet, on the host.</summary>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the builds.</param>
    /// <returns>Each test project, by its file on this device, and its build.</returns>
    public async Task<IReadOnlyList<LinkProjectBuild>> BuildSnapshotTestProjectsAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken)
    {
        var payload = await _connection.RequestAsync(LinkLanguageMethods.SnapshotTestBuilds, LinkLanguageJson.ToJson(Snapshot(changes)), cancellationToken: cancellationToken).ConfigureAwait(false);
        var response = payload.Read(LinkLanguageJson.Info<SnapshotBuildsResponse>());
        return [.. response.Builds.Select(b => new LinkProjectBuild(b.ProjectPath, DecodeBuild(b.Build, payload)))];
    }

    private static SnapshotRequest Snapshot(IReadOnlyDictionary<string, string?> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new SnapshotRequest([.. changes.Select(c => new PendingFile(c.Key, c.Value))]);
    }

    private async Task<TResult> RequestAsync<TRequest, TResult>(string method, TRequest request, CancellationToken cancellationToken)
    {
        var payload = await _connection.RequestAsync(method, LinkLanguageJson.ToJson(request), cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload.Read(LinkLanguageJson.Info<TResult>());
    }

    private static BuildResult DecodeBuild(BuildResponse response, LinkPayload payload)
    {
        CompiledAssembly Take(AssemblyReference reference) =>
            new(reference.Name, payload.GetAttachment(reference.Image), reference.Symbols is { } symbols ? payload.GetAttachment(symbols) : null);

        return new BuildResult(
            response.ProjectName,
            response.Diagnostics,
            response.EntryAssembly is { } entry ? Take(entry) : null,
            [.. response.Dependencies.Select(Take)],
            response.Duration);
    }
}
