using TabStudio.Agent.Local;

namespace TabStudio.App.Editing;

/// <summary>Debounces inline requests and discards stale responses even if a transport ignores cancellation.</summary>
public sealed class InlineCompletionController(
    Func<string, string, int, IReadOnlyList<OpenFileContext>, CancellationToken, Task<string?>> complete,
    TimeSpan? debounce = null) : IDisposable
{
    private CancellationTokenSource? _request;
    private int _version;
    private bool _disposed;
    /// <summary>The proposed insertion, never part of the document until Tab is pressed.</summary>
    public string Text { get; private set; } = string.Empty;
    /// <summary>The document offset the insertion belongs to.</summary>
    public int Offset { get; private set; }
    /// <summary>The most recent non-cancellation error, for the editor status/tooltip.</summary>
    public string? Error { get; private set; }
    /// <summary>The ghost changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Captures immutable document context, waits for typing to settle, then asks the local model.</summary>
    public Task RequestAsync(string path, string text, int caret, IReadOnlyList<OpenFileContext> neighbors) =>
        RequestAsync(path, () => text, caret, () => neighbors);

    /// <summary>
    /// Waits for typing to settle, then reads the document's context and asks the local model. The text is read only
    /// once typing has paused: any edit or caret move before then cancels the request, so it is the same text, and a
    /// keystroke no longer copies the whole file and its neighbors only for the next one to throw the copy away.
    /// </summary>
    /// <param name="path">The document's path.</param>
    /// <param name="text">Reads the document's text, on the UI thread.</param>
    /// <param name="caret">The caret's offset.</param>
    /// <param name="neighbors">Reads the other open files' context, on the UI thread.</param>
    public async Task RequestAsync(string path, Func<string> text, int caret, Func<IReadOnlyList<OpenFileContext>> neighbors)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(neighbors);
        Cancel();
        if (_disposed) return;
        var version = _version;
        using var request = new CancellationTokenSource();
        _request = request;
        try
        {
            await Task.Delay(debounce ?? TimeSpan.FromMilliseconds(450), request.Token);
            if (version != _version || request.IsCancellationRequested || _disposed) return;
            var insertion = await complete(path, text(), caret, neighbors(), request.Token);
            if (version != _version || request.IsCancellationRequested || _disposed) return;
            Offset = caret;
            Text = insertion ?? string.Empty;
            Error = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            if (version != _version || _disposed) return;
            Error = ex.Message;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally { if (ReferenceEquals(_request, request)) _request = null; }
    }

    /// <summary>Clears a visible or pending insertion on typing, movement, power changes or Escape.</summary>
    public void Cancel()
    {
        _version++;
        _request?.Cancel();
        _request = null;
        // Caret movement cancels even while inline completion is disabled. Redrawing an unchanged ghost would
        // invalidate the editor's visual lines while Quick Info or another IntelliSense response is placing itself.
        if (Text.Length == 0) return;
        Text = string.Empty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose() { _disposed = true; Cancel(); }
}
