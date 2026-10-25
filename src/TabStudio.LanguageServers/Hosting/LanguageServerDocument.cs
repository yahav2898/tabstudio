using System.Text.Json;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Protocol;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>
/// A document open in an editor and on its language server: changes go to the server as they are made, and the
/// server's answers about the document come back as offsets into the text they are about. The requests are in
/// <c>LanguageServerDocument.Features.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="ApplyChange"/> for every change, in order, from the thread that edits the text; requests may be
/// made from any thread. A request waits until the server runs, and answers with nothing when the server failed,
/// does not have the feature, or the text changed while it worked.
/// </para>
/// <para>
/// When the server stops unexpectedly and starts again, the document is opened on it again with its current text,
/// so the editor never has to notice.
/// </para>
/// </remarks>
public sealed partial class LanguageServerDocument : IDisposable
{
    private readonly LanguageServerInstance _server;
    private readonly Lock _gate = new();
    private TextSnapshot _snapshot;
    private JsonRpcConnection? _openOn;
    private int _syncKind;
    private IReadOnlyList<LspDiagnostic> _diagnostics = [];
    private bool _disposed;

    internal LanguageServerDocument(LanguageServerInstance server, string filePath, LanguageDefinition language, string text)
    {
        _server = server;
        FilePath = filePath;
        Uri = DocumentUri.FromPath(filePath);
        Language = language;
        _snapshot = new TextSnapshot(text, 1);
    }

    /// <summary>Raised when the server published new diagnostics for the document, on the server's reading thread.</summary>
    public event EventHandler? DiagnosticsChanged;

    /// <summary>Raised when the server started, restarted, failed or registered new capabilities, on a background thread.</summary>
    public event EventHandler? ServerChanged;

    /// <summary>Raised when the server asks the editor to request semantic tokens and inlay hints again.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>Full path of the file.</summary>
    public string FilePath { get; }

    /// <summary>The document's language.</summary>
    public LanguageDefinition Language { get; }

    /// <summary>The server's identifier, such as <c>pyright</c>.</summary>
    public string ServerId => _server.Definition.Id;

    /// <summary>The server's name, such as <c>Pyright</c>.</summary>
    public string ServerName => _server.Definition.Name;

    /// <summary>The folder the server serves.</summary>
    public string RootDirectory => _server.RootDirectory;

    /// <summary>Where the server is in its life.</summary>
    public LanguageServerState ServerState => _server.State;

    /// <summary>Why the server failed, when it did.</summary>
    public string? ServerMessage => _server.StatusMessage;

    /// <summary>What the server can do.</summary>
    public LanguageServerCapabilities Capabilities => _server.Capabilities;

    /// <summary>The text as the server has it.</summary>
    public TextSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    /// <summary>The server's latest diagnostics for the document.</summary>
    public IReadOnlyList<LspDiagnostic> Diagnostics => Volatile.Read(ref _diagnostics);

    /// <summary>The document's URI.</summary>
    internal string Uri { get; }

    /// <summary>Waits until the server runs and has the document open.</summary>
    /// <param name="cancellationToken">Gives up waiting.</param>
    /// <returns>Whether it does; <see langword="false"/> when the server failed.</returns>
    public async Task<bool> WaitUntilReadyAsync(CancellationToken cancellationToken) =>
        await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>Applies one change of the editor's text and sends it to the server.</summary>
    /// <param name="offset">Where the change starts, in the text before it.</param>
    /// <param name="removedLength">How many characters it removes.</param>
    /// <param name="insertedText">The text it inserts.</param>
    public void ApplyChange(int offset, int removedLength, string insertedText)
    {
        ArgumentNullException.ThrowIfNull(insertedText);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var before = _snapshot;
            _snapshot = before.WithChange(offset, removedLength, insertedText);
            if (_openOn is not { } connection || _syncKind == LspValues.TextDocumentSyncKind.None)
            {
                return;
            }

            var change = _syncKind == LspValues.TextDocumentSyncKind.Full
                ? new TextDocumentContentChangeEventDto(null, _snapshot.Text)
                : new TextDocumentContentChangeEventDto(LspConvert.ToRange(before, new TextSpan(offset, Math.Clamp(removedLength, 0, before.Text.Length - Math.Clamp(offset, 0, before.Text.Length)))), insertedText);
            Send(connection, "textDocument/didChange", new DidChangeTextDocumentParamsDto(new VersionedTextDocumentIdentifierDto(Uri, _snapshot.Version), [change]), LspJsonContext.Default.DidChangeTextDocumentParamsDto);
        }
    }

    /// <summary>Replaces the whole text, such as after the file was reloaded from disk.</summary>
    /// <param name="text">The new text.</param>
    public void ReplaceText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int length;
        lock (_gate)
        {
            length = _snapshot.Text.Length;
        }

        ApplyChange(0, length, text);
    }

    /// <summary>Tells the server the document was saved.</summary>
    public void NotifySaved()
    {
        lock (_gate)
        {
            if (_openOn is { } connection && Capabilities.Save)
            {
                var text = Capabilities.SaveIncludesText ? _snapshot.Text : null;
                Send(connection, "textDocument/didSave", new DidSaveTextDocumentParamsDto(new TextDocumentIdentifierDto(Uri), text), LspJsonContext.Default.DidSaveTextDocumentParamsDto);
            }
        }
    }

    /// <summary>Closes the document on the server; the server keeps running for the workspace's other documents.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _server.RemoveDocument(this);
    }

    /// <summary>Opens the document on a connection, unless it is open there already.</summary>
    internal void OpenOn(JsonRpcConnection connection, LanguageServerCapabilities capabilities)
    {
        lock (_gate)
        {
            if (_disposed || ReferenceEquals(_openOn, connection))
            {
                return;
            }

            _openOn = connection;
            _syncKind = capabilities.TextDocumentSync;
            if (capabilities.OpenClose)
            {
                var item = new TextDocumentItemDto(Uri, Language.Id, _snapshot.Version, _snapshot.Text);
                Send(connection, "textDocument/didOpen", new DidOpenTextDocumentParamsDto(item), LspJsonContext.Default.DidOpenTextDocumentParamsDto);
            }
        }
    }

    /// <summary>Closes the document on the server it is open on.</summary>
    internal void CloseOnServer()
    {
        lock (_gate)
        {
            if (_openOn is { } connection && Capabilities.OpenClose)
            {
                Send(connection, "textDocument/didClose", new DidCloseTextDocumentParamsDto(new TextDocumentIdentifierDto(Uri)), LspJsonContext.Default.DidCloseTextDocumentParamsDto);
            }

            _openOn = null;
        }
    }

    /// <summary>Forgets the connection of a server that stopped; the restarted server gets the document anew.</summary>
    internal void OnServerStopped()
    {
        lock (_gate)
        {
            _openOn = null;
        }

        OnServerChanged();
    }

    /// <summary>Tells the editor the server changed.</summary>
    internal void OnServerChanged() => ServerChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Tells the editor to ask for semantic tokens and inlay hints again.</summary>
    internal void OnRefreshRequested() => RefreshRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Replaces the diagnostics with the server's latest.</summary>
    internal void SetDiagnostics(IReadOnlyList<LspDiagnostic> diagnostics)
    {
        Volatile.Write(ref _diagnostics, diagnostics);
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sends a notification without waiting. Notifications sent while holding the document's lock are written in the
    /// order they were sent, so the server sees every change in order.
    /// </summary>
    private static void Send<T>(JsonRpcConnection connection, string method, T parameters, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) =>
        _ = SendQuietlyAsync(connection, method, parameters, type);

    private static async Task SendQuietlyAsync<T>(JsonRpcConnection connection, string method, T parameters, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
    {
        try
        {
            await connection.SendNotificationAsync(method, parameters, type).ConfigureAwait(false);
        }
        catch (JsonRpcException)
        {
            // The server stopped; when it restarts, the document is opened again with its current text.
        }
        catch (JsonException)
        {
            // Cannot happen with TabStudio's own types; nothing to send then.
        }
    }
}
