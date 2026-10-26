using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Compilation.Wpf.Editing;

namespace TabStudio.App.ViewModels;

/// <summary>WPF XAML editing: unsaved text, completion, navigation, tooltips, live diagnostics and handler fixes.</summary>
public sealed partial class DocumentViewModel
{
    private RoslynWorkspaceHost? _xamlHost;
    private WpfXamlFeatures? _xamlEngine;
    private WpfXamlLanguageFeatures? _xamlFeatures;
    private Action? _synchronizeXamlWorkspace;

    /// <summary>Whether this file has the WPF XAML editor's language features.</summary>
    public bool HasXamlFeatures => _xamlFeatures is not null;

    /// <summary>Connects a WPF XAML document to its workspace before its editor is created.</summary>
    /// <param name="host">The local workspace host, or <see langword="null"/> while a linked host owns it.</param>
    /// <param name="synchronize">Sends unsaved text from the other open documents.</param>
    internal void AttachWpfXaml(RoslynWorkspaceHost? host, Action synchronize)
    {
        if (_disposed || _language is not null || IsComparison)
        {
            return;
        }

        if (host?.IsWpfXamlDocument(FilePath) != true)
        {
            host = null;
        }

        _xamlFeatures ??= new WpfXamlLanguageFeatures(() => _xamlEngine, synchronize);
        if (ReferenceEquals(_xamlHost, host))
        {
            return;
        }

        _analysis?.Cancel();
        _syncedEdits = -1;
        _xamlHost = host;
        _synchronizeXamlWorkspace = host is null ? null : synchronize;
        _xamlEngine = host is null ? null : new WpfXamlFeatures(host);
        Diagnostics = [];
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(HasXamlFeatures));
        OnPropertyChanged(nameof(Features));
    }

    /// <summary>Sends unsaved XAML to the generator and the editing engine before a query or build.</summary>
    private void SyncXamlText()
    {
        if (_xamlHost is { } host && _syncedEdits != _edits)
        {
            host.UpdateText(FilePath, Document.Text);
            _syncedEdits = _edits;
        }
    }

    /// <summary>Analyzes a snapshot after the typing delay and discards results superseded by edits or closure.</summary>
    private async Task AnalyzeXamlAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (_xamlEngine is not { } engine)
        {
            return;
        }

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _synchronizeXamlWorkspace?.Invoke();
            SyncXamlText();
            var edits = _edits;
            var found = await Task.Run(() => engine.GetDiagnosticsAsync(FilePath, cancellationToken), cancellationToken);
            if (IsCurrent(edits, cancellationToken) && ReferenceEquals(engine, _xamlEngine))
            {
                Diagnostics = found;
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer edit, a workspace change or closing the file replaced this request.
        }
        catch (ObjectDisposedException)
        {
            // Closing the workspace or switching to a linked host disposed the local compilation.
        }
    }
}
