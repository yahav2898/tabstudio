using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Protocol;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>What the server sends without being asked: logs, messages, progress, diagnostics, and its requests.</summary>
internal sealed partial class LanguageServerInstance
{
    private static readonly string[] RefreshRequests =
    [
        "workspace/semanticTokens/refresh",
        "workspace/inlayHint/refresh",
        "workspace/codeLens/refresh",
        "workspace/diagnostic/refresh",
        "workspace/foldingRange/refresh",
    ];

    private readonly ConcurrentDictionary<string, string> _registrations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _progressTitles = new(StringComparer.Ordinal);
    private List<LspWorkspaceEdit>? _capturedEdits;

    /// <summary>
    /// Runs a command on the server, as a code action asks, and returns the edits it sends back with
    /// <c>workspace/applyEdit</c> while it runs, instead of applying them, so they are applied with the action's own.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    public async Task<IReadOnlyList<LspWorkspaceEdit>> ExecuteCommandCapturingEditsAsync(JsonRpcConnection connection, LspCommand command, CancellationToken cancellationToken)
    {
        var captured = new List<LspWorkspaceEdit>();
        Volatile.Write(ref _capturedEdits, captured);
        try
        {
            await connection.SendRequestAsync(
                "workspace/executeCommand",
                new ExecuteCommandParamsDto(command.Name, command.Arguments),
                LspJsonContext.Default.ExecuteCommandParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _capturedEdits, null);
        }

        lock (captured)
        {
            return [.. captured];
        }
    }

    private void RegisterHandlers(JsonRpcConnection connection)
    {
        connection.AddNotificationHandler("window/logMessage", OnLogMessage);
        connection.AddNotificationHandler("window/showMessage", OnShowMessage);
        connection.AddNotificationHandler("textDocument/publishDiagnostics", OnPublishDiagnostics);
        connection.AddNotificationHandler("$/progress", OnProgress);
        connection.AddNotificationHandler("telemetry/event", static _ => { });
        connection.AddRequestHandler("window/showMessageRequest", OnShowMessageRequestAsync);
        connection.AddRequestHandler("window/showDocument", OnShowDocumentAsync);
        connection.AddRequestHandler("window/workDoneProgress/create", static (_, _) => Task.FromResult<JsonNode?>(null));
        connection.AddRequestHandler("client/registerCapability", OnRegisterCapability);
        connection.AddRequestHandler("client/unregisterCapability", OnUnregisterCapability);
        connection.AddRequestHandler("workspace/configuration", OnConfiguration);
        connection.AddRequestHandler("workspace/workspaceFolders", OnWorkspaceFolders);
        connection.AddRequestHandler("workspace/applyEdit", OnApplyEditAsync);
        foreach (var method in RefreshRequests)
        {
            connection.AddRequestHandler(method, OnRefresh);
        }
    }

    private void OnLogMessage(JsonElement parameters)
    {
        if (Read(parameters, LspJsonContext.Default.MessageParamsDto) is { Message: { } message } log)
        {
            var level = log.Type switch
            {
                LspValues.MessageType.Error => LanguageServerLogLevel.Error,
                LspValues.MessageType.Warning => LanguageServerLogLevel.Warning,
                LspValues.MessageType.Info => LanguageServerLogLevel.Info,
                _ => LanguageServerLogLevel.Log,
            };
            _host.Log(Name, level, message);
        }
    }

    private void OnShowMessage(JsonElement parameters)
    {
        if (Read(parameters, LspJsonContext.Default.MessageParamsDto) is { Message: { } message } shown)
        {
            var type = MessageType(shown.Type);
            _host.Log(Name, type == LanguageServerMessageType.Error ? LanguageServerLogLevel.Error : LanguageServerLogLevel.Info, message);
            _host.OnMessageShown(new LanguageServerMessageEventArgs(Name, type, message));
        }
    }

    private void OnPublishDiagnostics(JsonElement parameters)
    {
        if (Read(parameters, LspJsonContext.Default.PublishDiagnosticsParamsDto) is not { Uri: { } uri } published
            || DocumentUri.ToPath(uri) is not { } path)
        {
            return;
        }

        var document = FindDocument(path);
        var snapshot = document?.Snapshot ?? _host.GetSnapshot(path);
        var diagnostics = (published.Diagnostics ?? [])
            .Select(d => LspConvert.ToDiagnostic(d, document?.FilePath ?? path, snapshot))
            .OfType<LspDiagnostic>()
            .ToList();
        document?.SetDiagnostics(diagnostics);
        _host.OnDiagnosticsPublished(new LanguageServerDiagnosticsEventArgs(document?.FilePath ?? path, diagnostics, Name, RootDirectory));
    }

    private void OnProgress(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("token", out var token)
            || !parameters.TryGetProperty("value", out var value)
            || value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var kind = GetString(value, "kind");
        if (kind is not ("begin" or "report" or "end"))
        {
            return;
        }

        var key = token.GetRawText();
        var title = GetString(value, "title");
        if (kind == "begin" && title is not null)
        {
            _progressTitles[key] = title;
        }
        else
        {
            title = _progressTitles.GetValueOrDefault(key);
        }

        int? percentage = value.TryGetProperty("percentage", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var percent)
            ? percent
            : null;
        var isDone = kind == "end";
        if (isDone)
        {
            _progressTitles.TryRemove(key, out _);
        }

        _host.OnProgress(new LanguageServerProgressEventArgs(Name, title, GetString(value, "message"), percentage, isDone));
    }

    private async Task<JsonNode?> OnShowMessageRequestAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        if (Read(parameters, LspJsonContext.Default.ShowMessageRequestParamsDto) is not { Message: { } message } request)
        {
            return null;
        }

        var actions = (request.Actions ?? []).Select(a => a.Title).Where(t => !string.IsNullOrEmpty(t)).ToList();
        var chosen = await _host.RequestMessageAsync(new LanguageServerMessageRequest(Name, MessageType(request.Type), message, actions), cancellationToken).ConfigureAwait(false);
        return chosen is null ? null : new JsonObject { ["title"] = chosen };
    }

    private async Task<JsonNode?> OnShowDocumentAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var success = false;
        if (Read(parameters, LspJsonContext.Default.ShowDocumentParamsDto) is { Uri: { } uri } request && DocumentUri.ToPath(uri) is { } path)
        {
            TextSpan? selection = null;
            if (request.Selection is { } range && _host.GetSnapshot(path) is { } snapshot)
            {
                selection = LspConvert.ToSpan(snapshot, range);
            }

            success = await _host.ShowDocumentAsync(path, selection, cancellationToken).ConfigureAwait(false);
        }

        return JsonSerializer.SerializeToNode(new ShowDocumentResultDto(success), LspJsonContext.Default.ShowDocumentResultDto);
    }

    private Task<JsonNode?> OnRegisterCapability(JsonElement parameters, CancellationToken cancellationToken)
    {
        foreach (var registration in Read(parameters, LspJsonContext.Default.RegistrationParamsDto)?.Registrations ?? [])
        {
            if (registration.Method is { } method)
            {
                _registrations[registration.Id ?? method] = method;
                _capabilities = _capabilities.WithRegistration(method, registration.RegisterOptions);
            }
        }

        NotifyDocuments(d => d.OnServerChanged());
        return Task.FromResult<JsonNode?>(null);
    }

    private Task<JsonNode?> OnUnregisterCapability(JsonElement parameters, CancellationToken cancellationToken)
    {
        foreach (var registration in Read(parameters, LspJsonContext.Default.UnregistrationParamsDto)?.Unregisterations ?? [])
        {
            var method = registration.Method ?? (registration.Id is { } id ? _registrations.GetValueOrDefault(id) : null);
            if (registration.Id is { } removed)
            {
                _registrations.TryRemove(removed, out _);
            }

            if (method is not null)
            {
                _capabilities = _capabilities.WithoutRegistration(method);
            }
        }

        NotifyDocuments(d => d.OnServerChanged());
        return Task.FromResult<JsonNode?>(null);
    }

    /// <summary>Answers each section the server asks for from its settings, or <c>null</c> when there is none.</summary>
    private Task<JsonNode?> OnConfiguration(JsonElement parameters, CancellationToken cancellationToken)
    {
        var items = Read(parameters, LspJsonContext.Default.ConfigurationParamsDto)?.Items ?? [];
        var settings = Settings;
        var answers = new JsonArray();
        foreach (var item in items)
        {
            answers.Add(JsonSettings.Select(settings, item?.Section)?.DeepClone());
        }

        return Task.FromResult<JsonNode?>(answers);
    }

    private Task<JsonNode?> OnWorkspaceFolders(JsonElement parameters, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var folders = new JsonArray(new JsonObject { ["uri"] = RootUri, ["name"] = name.Length == 0 ? RootDirectory : name });
        return Task.FromResult<JsonNode?>(folders);
    }

    private async Task<JsonNode?> OnApplyEditAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var request = Read(parameters, LspJsonContext.Default.ApplyWorkspaceEditParamsDto);
        var edit = LspConvert.ToWorkspaceEdit(request?.Edit, _host);
        bool applied;
        if (Volatile.Read(ref _capturedEdits) is { } captured)
        {
            lock (captured)
            {
                captured.Add(edit);
            }

            applied = true;
        }
        else
        {
            applied = await _host.ApplyEditAsync(edit, request?.Label, cancellationToken).ConfigureAwait(false);
        }

        var result = new ApplyWorkspaceEditResultDto(applied, applied ? null : "TabStudio could not apply the edit.");
        return JsonSerializer.SerializeToNode(result, LspJsonContext.Default.ApplyWorkspaceEditResultDto);
    }

    private Task<JsonNode?> OnRefresh(JsonElement parameters, CancellationToken cancellationToken)
    {
        NotifyDocuments(d => d.OnRefreshRequested());
        return Task.FromResult<JsonNode?>(null);
    }

    private static LanguageServerMessageType MessageType(int type) => type switch
    {
        LspValues.MessageType.Error => LanguageServerMessageType.Error,
        LspValues.MessageType.Warning => LanguageServerMessageType.Warning,
        LspValues.MessageType.Info => LanguageServerMessageType.Info,
        _ => LanguageServerMessageType.Log,
    };

    private static string? GetString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;

    private T? Read<T>(JsonElement parameters, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
        where T : class
    {
        try
        {
            return parameters.ValueKind == JsonValueKind.Object ? parameters.Deserialize(type) : null;
        }
        catch (JsonException ex)
        {
            _host.Log(Name, LanguageServerLogLevel.Warning, $"A message from {Name} could not be read: {ex.Message}");
            return null;
        }
    }
}
