using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Link.Messaging;

namespace TabStudio.Link.Language;

/// <summary>
/// The host's language service for one tablet: answers the tablet's requests from a mirror of its workspace and a
/// <see cref="ILinkLanguageBackend"/>, mapping every path between the two devices.
/// </summary>
/// <remarks>
/// <para>
/// One server serves one tablet across reconnections, so its compilations stay warm when Wi-Fi drops for a moment.
/// Requests run concurrently; edits that arrive while a workspace loads are applied after it.
/// </para>
/// <para>
/// <see cref="Activity"/> says what the host is doing for the tablet, such as <c>Building HelloTablet</c>.
/// </para>
/// </remarks>
public sealed class LinkLanguageServer : ILinkMessageHandler, IDisposable
{
    private readonly LinkWorkspaceMirror _mirror;
    private readonly Func<CancellationToken, Task<ILinkLanguageBackend>> _createBackend;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly List<string> _activities = [];
    private readonly List<(TextChange Change, bool IsNew)> _pendingEdits = [];
    private ILinkLanguageBackend? _backend;
    private LinkPathMap? _map;
    private Workspace? _workspace;
    private bool _loading;
    private bool _optimize;
    private long _requests;
    private bool _disposed;

    /// <summary>Creates the server for one tablet.</summary>
    /// <param name="mirrorDirectory">The folder, on the host, for this tablet's workspaces.</param>
    /// <param name="createBackend">Creates the language service when the first workspace loads.</param>
    public LinkLanguageServer(string mirrorDirectory, Func<CancellationToken, Task<ILinkLanguageBackend>> createBackend)
    {
        ArgumentException.ThrowIfNullOrEmpty(mirrorDirectory);
        ArgumentNullException.ThrowIfNull(createBackend);
        _mirror = new LinkWorkspaceMirror(mirrorDirectory);
        _createBackend = createBackend;
    }

    /// <summary>Raised on the thread pool when <see cref="Activity"/> or <see cref="WorkspaceName"/> changed.</summary>
    public event EventHandler? ActivityChanged;

    /// <summary>What the host is doing for the tablet, such as <c>Building HelloTablet</c>, or <see langword="null"/> when idle.</summary>
    public string? Activity
    {
        get
        {
            lock (_gate)
            {
                return _activities.Count > 0 ? _activities[^1] : null;
            }
        }
    }

    /// <summary>The name of the workspace the tablet has open, once loaded.</summary>
    public string? WorkspaceName
    {
        get
        {
            lock (_gate)
            {
                return _workspace?.Name;
            }
        }
    }

    /// <summary>How many requests the server answered.</summary>
    public long RequestCount => Interlocked.Read(ref _requests);

    /// <inheritdoc />
    public async Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Interlocked.Increment(ref _requests);
        return method switch
        {
            LinkLanguageMethods.PrepareWorkspace => Prepare(payload),
            LinkLanguageMethods.UploadFiles => Upload(payload),
            LinkLanguageMethods.LoadWorkspace => await LoadAsync(payload.Read(LinkLanguageJson.Info<LoadWorkspaceRequest>()), cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.DocumentDiagnostics => await FileQueryAsync(payload, (b, path, ct) => b.GetDocumentDiagnosticsAsync(path, ct), (m, r) => m.ToClient(r), [], cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.ProjectDiagnostics => await ProjectDiagnosticsAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.Classifications => await FileQueryAsync(payload, (b, path, ct) => b.GetClassificationsAsync(path, ct), (_, r) => r, [], cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.Format => await FileQueryAsync(payload, (b, path, ct) => b.GetFormattingEditsAsync(path, ct), (_, r) => r, [], cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.Build => await BuildAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.BuildForDebugging => await BuildForDebuggingAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.RestorePackages => await RestoreAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.AddFrameworkGenerators => await AddFrameworkGeneratorsAsync(cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.Completions => await CompletionsAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.CompletionChange => await CompletionChangeAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.CompletionDescription => await CompletionDescriptionAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.QuickInfo => await PositionQueryAsync(
                payload,
                async (b, path, position, ct) => new Optional<QuickInfoResult>(await b.GetQuickInfoAsync(path, position, ct).ConfigureAwait(false)),
                (_, r) => r,
                new Optional<QuickInfoResult>(null),
                cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.SignatureHelp => await PositionQueryAsync(
                payload,
                async (b, path, position, ct) => new Optional<SignatureHelpResult>(await b.GetSignatureHelpAsync(path, position, ct).ConfigureAwait(false)),
                (_, r) => r,
                new Optional<SignatureHelpResult>(null),
                cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.Definition => await PositionQueryAsync(
                payload,
                async (b, path, position, ct) => new Optional<DefinitionResult>(await b.FindDefinitionAsync(path, position, ct).ConfigureAwait(false)),
                (m, r) => new Optional<DefinitionResult>(r.Value is { } definition ? m.ToClient(definition) : null),
                new Optional<DefinitionResult>(null),
                cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.Implementations => await PositionQueryAsync(
                payload,
                (b, path, position, ct) => b.FindImplementationsAsync(path, position, ct),
                (m, r) => r.Select(d => m.ToClient(d)).ToList(),
                [],
                cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.References => await PositionQueryAsync(
                payload,
                (b, path, position, ct) => b.FindReferencesAsync(path, position, ct),
                (m, r) => r.Select(item => m.ToClient(item)).ToList(),
                [],
                cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.Highlights => await PositionQueryAsync(payload, (b, path, position, ct) => b.GetDocumentHighlightsAsync(path, position, ct), (_, r) => r, [], cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.CanRename => await PositionQueryAsync(
                payload,
                (b, path, position, ct) => b.CanRenameAsync(path, position, ct),
                (_, r) => r,
                RenameTarget.Unavailable("This file is not part of the workspace on the host."),
                cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.RenameEdits => await RenameAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.CodeActions => await CodeActionsAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.CodeActionEdits => await CodeActionEditsAsync(payload, cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.DocumentSymbols => await FileQueryAsync(payload, (b, path, ct) => b.GetDocumentSymbolsAsync(path, ct), (_, r) => r, [], cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.SymbolPath => await PositionQueryAsync(payload, (b, path, position, ct) => b.GetSymbolPathAsync(path, position, ct), (_, r) => r, [], cancellationToken).ConfigureAwait(false),
            LinkLanguageMethods.WarmUp => await WarmUpAsync(payload, cancellationToken).ConfigureAwait(false),
            _ => throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, $"The host does not know '{method}'."),
        };
    }

    /// <inheritdoc />
    public void HandleNotification(string method, LinkPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        switch (method)
        {
            case LinkLanguageMethods.UpdateText:
                Edit(payload.Read(LinkLanguageJson.Info<TextChange>()), isNew: false);
                break;
            case LinkLanguageMethods.AddDocument:
                Edit(payload.Read(LinkLanguageJson.Info<TextChange>()), isNew: true);
                break;
            case LinkLanguageMethods.SetOptimization:
                var optimize = payload.Read(LinkLanguageJson.Info<OptimizationSetting>()).Optimize;
                lock (_gate)
                {
                    _optimize = optimize;
                    _backend?.SetOptimization(optimize);
                }

                break;
            case LinkLanguageMethods.SetActiveProjects:
                var projects = payload.Read(LinkLanguageJson.Info<ActiveProjectsSetting>()).ProjectPaths;
                lock (_gate)
                {
                    if (_backend is { } backend && _map is { } map)
                    {
                        backend.SetActiveProjects(projects.Select(map.ToHost).OfType<string>().ToList());
                    }
                }

                break;
        }
    }

    /// <summary>Releases the language service and its compilations.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _backend?.Dispose();
            _backend = null;
        }

        _loadGate.Dispose();
    }

    private static LinkReply Reply<T>(T value) => LinkReply.Of(value, LinkLanguageJson.Info<T>());

    private LinkReply Prepare(LinkPayload payload)
    {
        var missing = _mirror.Prepare(payload.Read(LinkLanguageJson.Info<PrepareWorkspaceRequest>()));
        return Reply(new PrepareWorkspaceResponse(missing));
    }

    private LinkReply Upload(LinkPayload payload)
    {
        var request = payload.Read(LinkLanguageJson.Info<UploadFilesRequest>());
        _mirror.Write(request.Files, payload.Attachments);
        return LinkReply.Empty;
    }

    private async Task<LinkReply> LoadAsync(LoadWorkspaceRequest request, CancellationToken cancellationToken)
    {
        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _loading = true;
        }

        try
        {
            var map = _mirror.Commit();
            var entry = map.ToHost(request.EntryPath)
                ?? throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The workspace's solution or folder is outside its files.");
            using var activity = Begin($"Loading {Path.GetFileNameWithoutExtension(entry)}");
            var workspace = await Task.Run(() => WorkspaceLoader.Load(entry), cancellationToken).ConfigureAwait(false);
            var backend = await EnsureBackendAsync(cancellationToken).ConfigureAwait(false);
            await Task.Run(() => backend.Load(workspace, [map.CompilerPathMap]), cancellationToken).ConfigureAwait(false);
            backend.SetOptimization(request.Optimize);
            backend.SetActiveProjects(request.ActiveProjects.Select(map.ToHost).OfType<string>().ToList());

            var documents = Documents(workspace).Where(backend.ContainsDocument).Select(path => map.ToClient(path)).ToList();
            lock (_gate)
            {
                _workspace = workspace;
                _map = map;
                _optimize = request.Optimize;
            }

            RaiseActivityChanged();
            return Reply(new LoadWorkspaceResponse(documents));
        }
        finally
        {
            lock (_gate)
            {
                _loading = false;
                foreach (var (change, isNew) in _pendingEdits)
                {
                    ApplyEdit(change, isNew);
                }

                _pendingEdits.Clear();
            }

            try
            {
                _loadGate.Release();
            }
            catch (ObjectDisposedException)
            {
                // Disposed while loading.
            }
        }
    }

    private async Task<ILinkLanguageBackend> EnsureBackendAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_backend is { } existing)
            {
                return existing;
            }
        }

        var backend = await _createBackend(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (_disposed)
            {
                backend.Dispose();
                throw new ObjectDisposedException(nameof(LinkLanguageServer));
            }

            _backend = backend;
            backend.SetOptimization(_optimize);
            return backend;
        }
    }

    /// <summary>The C# files the loaded projects can contain; the backend says which it loaded.</summary>
    private static IEnumerable<string> Documents(Workspace workspace)
    {
        var directories = workspace.Projects.Select(p => p.DirectoryPath).ToList();
        return workspace.Projects
            .Where(p => p.Language == ProjectLanguage.CSharp && p.Kind != ProjectKind.Unknown)
            .SelectMany(p => ProjectSourceFiles.Enumerate(p, directories))
            .Distinct(StringComparer.Ordinal);
    }

    private void Edit(TextChange change, bool isNew)
    {
        lock (_gate)
        {
            if (_loading)
            {
                _pendingEdits.Add((change, isNew));
            }
            else
            {
                ApplyEdit(change, isNew);
            }
        }
    }

    /// <summary>Applies an edit to the loaded workspace. The caller holds the gate.</summary>
    private void ApplyEdit(TextChange change, bool isNew)
    {
        if (_backend is not { } backend || _map?.ToHost(change.Path) is not { } path)
        {
            return;
        }

        if (isNew)
        {
            backend.AddDocument(path, change.Text);
        }
        else
        {
            backend.UpdateText(path, change.Text);
        }
    }

    /// <summary>The loaded backend and path map, or the error that nothing is loaded.</summary>
    private (ILinkLanguageBackend Backend, LinkPathMap Map) Ready()
    {
        lock (_gate)
        {
            return _backend is { } backend && _map is { } map
                ? (backend, map)
                : throw new LinkRemoteException(LinkErrorCodes.NoWorkspace, "The host has no workspace loaded for this device.");
        }
    }

    private ProjectInfo FindProject(string clientProjectPath)
    {
        lock (_gate)
        {
            if (_workspace is { } workspace && _map?.ToHost(clientProjectPath) is { } path
                && workspace.Projects.FirstOrDefault(p => string.Equals(p.FilePath, path, StringComparison.Ordinal)) is { } project)
            {
                return project;
            }
        }

        throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, $"The host has no project '{Path.GetFileName(clientProjectPath)}'.");
    }

    private async Task<LinkReply> FileQueryAsync<T>(
        LinkPayload payload,
        Func<ILinkLanguageBackend, string, CancellationToken, Task<T>> query,
        Func<LinkPathMap, T, T> toClient,
        T empty,
        CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<FileRequest>());
        var (backend, map) = Ready();
        if (map.ToHost(request.Path) is not { } path)
        {
            return Reply(empty);
        }

        return Reply(toClient(map, await query(backend, path, cancellationToken).ConfigureAwait(false)));
    }

    private async Task<LinkReply> PositionQueryAsync<T>(
        LinkPayload payload,
        Func<ILinkLanguageBackend, string, int, CancellationToken, Task<T>> query,
        Func<LinkPathMap, T, T> toClient,
        T empty,
        CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<PositionRequest>());
        var (backend, map) = Ready();
        if (map.ToHost(request.Path) is not { } path)
        {
            return Reply(empty);
        }

        return Reply(toClient(map, await query(backend, path, request.Position, cancellationToken).ConfigureAwait(false)));
    }

    private async Task<LinkReply> ProjectDiagnosticsAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<ProjectRequest>());
        var (backend, map) = Ready();
        if (map.ToHost(request.ProjectPath) is not { } path)
        {
            return Reply<IReadOnlyList<BuildDiagnostic>>([]);
        }

        using var activity = Begin($"Analyzing {Path.GetFileNameWithoutExtension(path)}");
        return Reply(map.ToClient(await backend.GetProjectDiagnosticsAsync(path, cancellationToken).ConfigureAwait(false)));
    }

    private async Task<LinkReply> BuildAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var project = FindProject(payload.Read(LinkLanguageJson.Info<ProjectRequest>()).ProjectPath);
        var (backend, map) = Ready();
        using var activity = Begin($"Building {project.Name}");
        var build = await backend.BuildAsync(project, cancellationToken).ConfigureAwait(false);
        var attachments = new List<ReadOnlyMemory<byte>>();
        return LinkReply.Of(EncodeBuild(build, map, attachments), LinkLanguageJson.Info<BuildResponse>(), attachments);
    }

    private async Task<LinkReply> BuildForDebuggingAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var project = FindProject(payload.Read(LinkLanguageJson.Info<ProjectRequest>()).ProjectPath);
        var (backend, map) = Ready();
        using var activity = Begin($"Building {project.Name} for the debugger");
        var build = await backend.BuildForDebuggingAsync(project, cancellationToken).ConfigureAwait(false);
        var attachments = new List<ReadOnlyMemory<byte>>();
        var response = new DebugBuildResponse(
            EncodeBuild(build.Result, map, attachments),
            [.. build.DebugInfo.Statements.Select(s => map.ToClient(s))],
            [.. build.DebugInfo.Methods.Select(m => map.ToClient(m))]);
        return LinkReply.Of(response, LinkLanguageJson.Info<DebugBuildResponse>(), attachments);
    }

    private static BuildResponse EncodeBuild(BuildResult build, LinkPathMap map, List<ReadOnlyMemory<byte>> attachments)
    {
        AssemblyReference Add(CompiledAssembly assembly)
        {
            var image = attachments.Count;
            attachments.Add(assembly.Image);
            int? symbols = null;
            if (assembly.Symbols is { } pdb)
            {
                symbols = attachments.Count;
                attachments.Add(pdb);
            }

            return new AssemblyReference(assembly.Name, image, symbols);
        }

        var entry = build.EntryAssembly is { } assembly ? Add(assembly) : null;
        return new BuildResponse(build.ProjectName, map.ToClient(build.Diagnostics), entry, [.. build.Dependencies.Select(Add)], build.Duration);
    }

    private async Task<LinkReply> RestoreAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var project = FindProject(payload.Read(LinkLanguageJson.Info<ProjectRequest>()).ProjectPath);
        var (backend, map) = Ready();
        using var activity = Begin($"Restoring packages of {project.Name}");
        var outcome = await backend.RestorePackagesAsync(project, cancellationToken).ConfigureAwait(false);
        return Reply(outcome with { Diagnostics = map.ToClient(outcome.Diagnostics) });
    }

    private async Task<LinkReply> CompletionsAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<CompletionListRequest>());
        var (backend, map) = Ready();
        return map.ToHost(request.Path) is { } path
            ? Reply(await backend.GetCompletionsAsync(path, request.Position, request.Trigger, cancellationToken).ConfigureAwait(false))
            : Reply(CompletionResult.Empty);
    }

    private async Task<LinkReply> CompletionChangeAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<CompletionChangeRequest>());
        var (backend, map) = Ready();
        var path = map.ToHost(request.Path)
            ?? throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The file is not part of the workspace on the host.");
        char? commit = request.CommitCharacter is { Length: 1 } text ? text[0] : null;
        return Reply(await backend.GetCompletionChangeAsync(path, request.Entry, commit, cancellationToken).ConfigureAwait(false));
    }

    private async Task<LinkReply> RenameAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<RenameRequest>());
        var (backend, map) = Ready();
        if (map.ToHost(request.Path) is not { } path)
        {
            return Reply(RenameResult.Failed("The file is not part of the workspace on the host."));
        }

        using var activity = Begin($"Renaming to {request.NewName}");
        return Reply(map.ToClient(await backend.GetRenameEditsAsync(path, request.Position, request.NewName, request.Options, cancellationToken).ConfigureAwait(false)));
    }

    private async Task<LinkReply> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken)
    {
        var (backend, map) = Ready();
        using var activity = Begin("Downloading .NET's source generators");
        return Reply(map.ToClient(await backend.AddFrameworkGeneratorsAsync(cancellationToken).ConfigureAwait(false)));
    }

    private async Task<LinkReply> CompletionDescriptionAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<CompletionDescriptionRequest>());
        return Reply(await Ready().Backend.GetCompletionDescriptionAsync(request.Entry, cancellationToken).ConfigureAwait(false));
    }

    private async Task<LinkReply> CodeActionEditsAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<CodeActionEditsRequest>());
        var (backend, map) = Ready();
        var edits = await backend.GetCodeActionEditsAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return Reply<IReadOnlyList<DocumentEdit>>([.. edits.Select(e => map.ToClient(e))]);
    }

    private async Task<LinkReply> CodeActionsAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<CodeActionsRequest>());
        var (backend, map) = Ready();
        return map.ToHost(request.Path) is { } path
            ? Reply(await backend.GetCodeActionsAsync(path, request.Start, request.Length, cancellationToken).ConfigureAwait(false))
            : Reply<IReadOnlyList<CodeActionItem>>([]);
    }

    private async Task<LinkReply> WarmUpAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<FileRequest>());
        var (backend, map) = Ready();
        if (map.ToHost(request.Path) is { } path)
        {
            using var activity = Begin("Preparing IntelliSense");
            await backend.WarmUpAsync(path, cancellationToken).ConfigureAwait(false);
        }

        return LinkReply.Empty;
    }

    private ActivityScope Begin(string activity)
    {
        lock (_gate)
        {
            _activities.Add(activity);
        }

        RaiseActivityChanged();
        return new ActivityScope(this, activity);
    }

    private void End(string activity)
    {
        lock (_gate)
        {
            _activities.Remove(activity);
        }

        RaiseActivityChanged();
    }

    private void RaiseActivityChanged()
    {
        var handler = ActivityChanged;
        if (handler is not null)
        {
            _ = Task.Run(() => handler(this, EventArgs.Empty));
        }
    }

    /// <summary>Ends an activity when disposed.</summary>
    private readonly struct ActivityScope(LinkLanguageServer server, string activity) : IDisposable
    {
        public void Dispose() => server.End(activity);
    }
}
