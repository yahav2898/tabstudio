using AvaloniaEdit.Document;
using TabStudio.App.Services;
using TabStudio.App.Services.LanguageServers;
using TabStudio.Compilation;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A document a language server owns, for any language Roslyn does not: its edits reach the server as they happen,
/// and the server's diagnostics, semantic colors, inlay hints and folding ranges come back to the editor.
/// IntelliSense goes through <see cref="Features"/>, as for C#.
/// </summary>
public sealed partial class DocumentViewModel
{
    private LanguageServerDocument? _server;
    private LanguageServerFeatures? _serverFeatures;
    private IUiDispatcher? _serverUi;
    private string _serverProject = string.Empty;
    private CancellationTokenSource? _serverRefresh;

    /// <summary>Whether a language server analyzes this document.</summary>
    public bool HasLanguageServer => _server is not null;

    /// <summary>The language server's document, while one analyzes this document.</summary>
    public LanguageServerDocument? LanguageServer => _server;

    /// <summary>The language server's IntelliSense for this document, with the requests only language servers have.</summary>
    internal LanguageServerFeatures? ServerFeatures => _serverFeatures;

    /// <summary>The inlay hints of the document, such as parameter names, in document order.</summary>
    public IReadOnlyList<LspInlayHint> InlayHints { get; private set; } = [];

    /// <summary>The ranges of lines that can be folded.</summary>
    public IReadOnlyList<LspFoldingRange> FoldingRanges { get; private set; } = [];

    /// <summary>Raised on the UI thread when <see cref="InlayHints"/> or <see cref="FoldingRanges"/> change.</summary>
    public event EventHandler? DecorationsChanged;

    /// <summary>
    /// Connects the document to its language server. Call on the UI thread, before the editor is created, so it
    /// sets up IntelliSense, squiggles and semantic colors for it.
    /// </summary>
    /// <param name="server">The document as the server knows it, with this document's text.</param>
    /// <param name="ui">The UI dispatcher, for the server's notifications.</param>
    /// <param name="projectName">What the Error List's Project column shows, such as the workspace folder's name.</param>
    /// <param name="readText">Reads a file that is not open, for results in other files.</param>
    public void AttachLanguageServer(LanguageServerDocument server, IUiDispatcher ui, string projectName, Func<string, string?> readText)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(projectName);
        ArgumentNullException.ThrowIfNull(readText);
        if (_language is not null || _server is not null)
        {
            return;
        }

        _server = server;
        _serverUi = ui;
        _serverProject = projectName;
        _serverFeatures = new LanguageServerFeatures(server, readText);
        Document.Changed += OnDocumentChangedForServer;
        server.DiagnosticsChanged += OnServerDiagnosticsChanged;
        server.ServerChanged += OnServerChanged;
        server.RefreshRequested += OnServerRefreshRequested;
        ApplyServerDiagnostics();
        ScheduleServerRefresh(TimeSpan.Zero);
    }

    /// <summary>Formats the document with its language server, as one undo step.</summary>
    /// <param name="cancellationToken">Cancels formatting.</param>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public async Task<bool> FormatWithServerAsync(CancellationToken cancellationToken = default)
    {
        if (_server is not { } server || !server.Capabilities.Formatting)
        {
            return false;
        }

        var edits = _edits;
        IReadOnlyList<LspTextEdit> changes;
        try
        {
            changes = await server.FormatAsync(4, insertSpaces: true, cancellationToken);
        }
        catch (JsonRpcException)
        {
            return false;
        }

        if (changes.Count == 0 || edits != _edits || _disposed)
        {
            return false;
        }

        ApplyEdits(LspMapping.ToTextEdits(changes));
        return true;
    }

    /// <summary>Formats the line just typed when the server formats on a character, such as <c>;</c> or <c>}</c>.</summary>
    /// <param name="offset">The caret, just after the typed character.</param>
    /// <param name="character">The typed character.</param>
    public async Task FormatOnTypeAsync(int offset, char character)
    {
        if (_server is not { } server || !server.Capabilities.OnTypeFormatting || !server.Capabilities.OnTypeFormattingTriggerCharacters.Contains(character))
        {
            return;
        }

        var edits = _edits;
        IReadOnlyList<LspTextEdit> changes;
        try
        {
            changes = await server.FormatOnTypeAsync(offset, character, 4, insertSpaces: true, CancellationToken.None);
        }
        catch (JsonRpcException)
        {
            return;
        }

        if (changes.Count > 0 && edits == _edits && !_disposed)
        {
            ApplyEdits(LspMapping.ToTextEdits(changes));
        }
    }

    /// <summary>Tells the server the document was saved.</summary>
    internal void NotifySavedToServer() => _server?.NotifySaved();

    /// <summary>Stops sending edits and closes the document on its server. Called by <see cref="Dispose"/>.</summary>
    private void DisposeLanguageServer()
    {
        if (_server is not { } server)
        {
            return;
        }

        _serverRefresh?.Cancel();
        _serverRefresh?.Dispose();
        _serverRefresh = null;
        Document.Changed -= OnDocumentChangedForServer;
        server.DiagnosticsChanged -= OnServerDiagnosticsChanged;
        server.ServerChanged -= OnServerChanged;
        server.RefreshRequested -= OnServerRefreshRequested;
        server.Dispose();
        _server = null;
        _serverFeatures = null;
    }

    private void OnDocumentChangedForServer(object? sender, DocumentChangeEventArgs e)
    {
        _server?.ApplyChange(e.Offset, e.RemovalLength, e.InsertedText.Text);
        ScheduleServerRefresh(_profile.AnalysisDelay);
    }

    private void OnServerDiagnosticsChanged(object? sender, EventArgs e) => _serverUi?.Post(ApplyServerDiagnostics);

    private void OnServerChanged(object? sender, EventArgs e) => _serverUi?.Post(() =>
    {
        if (_server is { ServerState: LanguageServerState.Running })
        {
            ScheduleServerRefresh(TimeSpan.Zero);
        }
    });

    private void OnServerRefreshRequested(object? sender, EventArgs e) => _serverUi?.Post(() => ScheduleServerRefresh(TimeSpan.Zero));

    private void ApplyServerDiagnostics()
    {
        if (_server is not { } server || _disposed)
        {
            return;
        }

        // The server's diagnostics are about the text it last analyzed; ranges past the end are clamped by the editor.
        Diagnostics = LspMapping.ToBuildDiagnostics(server.Diagnostics, _serverProject);
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks the server again for colors, hints and folds a moment after the user stops typing.</summary>
    private void ScheduleServerRefresh(TimeSpan delay)
    {
        _serverRefresh?.Cancel();
        _serverRefresh?.Dispose();
        _serverRefresh = new CancellationTokenSource();
        _ = RefreshFromServerAsync(delay, _serverRefresh.Token);
    }

    private async Task RefreshFromServerAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (_server is not { } server)
        {
            return;
        }

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            if (!await server.WaitUntilReadyAsync(cancellationToken))
            {
                return;
            }

            var edits = _edits;
            var capabilities = server.Capabilities;
            if (capabilities.SemanticTokens && await server.GetSemanticTokensAsync(cancellationToken) is { } tokens && IsCurrent(edits, cancellationToken))
            {
                Classifications = tokens
                    .Select(t => (Token: t, Classification: LspMapping.Classification(t)))
                    .Where(t => t.Classification is not null)
                    .Select(t => new ClassifiedRange(t.Token.Span.Start, t.Token.Span.Length, t.Classification!))
                    .ToList();
                ClassificationsChanged?.Invoke(this, EventArgs.Empty);
            }

            var changed = false;
            if (capabilities.InlayHint)
            {
                var hints = await server.GetInlayHintsAsync(new TextSpan(0, Document.TextLength), cancellationToken);
                if (IsCurrent(edits, cancellationToken))
                {
                    InlayHints = hints;
                    changed = true;
                }
            }

            if (capabilities.FoldingRange)
            {
                var ranges = await server.GetFoldingRangesAsync(cancellationToken);
                if (IsCurrent(edits, cancellationToken))
                {
                    FoldingRanges = ranges;
                    changed = true;
                }
            }

            if (changed)
            {
                DecorationsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // The user typed again, or the document closed.
        }
        catch (JsonRpcException)
        {
            // The server stopped; it is started again with the document's text, which refreshes these.
        }
    }
}
