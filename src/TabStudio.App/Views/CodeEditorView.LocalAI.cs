using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using TabStudio.App.Editing;

namespace TabStudio.App.Views;

public sealed partial class CodeEditorView
{
    private GhostTextGenerator? _ghost;
    private bool _acceptingInline;
    /// <summary>The editor's on-device inline insertion, available for every file type.</summary>
    public InlineCompletionController? InlineCompletion { get; private set; }

    private void InitializeInlineCompletion(InlineCompletionController? provider)
    {
        var local = _document.LocalAI;
        if (local is null && provider is null) return;
        InlineCompletion = provider ?? new InlineCompletionController(local!.CompleteAsync);
        _ghost = new GhostTextGenerator(InlineCompletion);
        _editor.TextArea.TextView.ElementGenerators.Add(_ghost);
        InlineCompletion.Changed += OnGhostChanged;
        _document.Document.Changed += OnInlineDocumentChanged;
        _editor.TextArea.Caret.PositionChanged += OnInlineCaretChanged;
        _editor.TextArea.AddHandler(KeyDownEvent, OnInlineKeyDown, RoutingStrategies.Tunnel);
        _editor.LostFocus += OnInlineLostFocus;
        if (local is not null) local.Changed += OnInlinePolicyChanged;
    }

    private void DisposeInlineCompletion()
    {
        if (InlineCompletion is null) return;
        if (_document.LocalAI is { } local) local.Changed -= OnInlinePolicyChanged;
        _document.Document.Changed -= OnInlineDocumentChanged;
        _editor.TextArea.Caret.PositionChanged -= OnInlineCaretChanged;
        _editor.TextArea.RemoveHandler(KeyDownEvent, OnInlineKeyDown);
        _editor.LostFocus -= OnInlineLostFocus;
        InlineCompletion.Changed -= OnGhostChanged;
        InlineCompletion.Dispose();
        _editor.TextArea.TextView.ElementGenerators.Remove(_ghost!);
    }

    private void OnInlineDocumentChanged(object? sender, DocumentChangeEventArgs e) => RequestInline();
    private void OnInlineCaretChanged(object? sender, EventArgs e) => RequestInline();
    private void OnInlineLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => InlineCompletion?.Cancel();
    private void OnInlinePolicyChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => InlineCompletion?.Cancel());
    private void OnGhostChanged(object? sender, EventArgs e) => _editor.TextArea.TextView.Redraw();

    private void RequestInline()
    {
        if (_acceptingInline || InlineCompletion is null) return;
        InlineCompletion.Cancel();
        if (!IsVisible || !_editor.IsKeyboardFocusWithin || _editor.SelectionLength > 0 || _document.LocalAI is not { InlineEnabled: true, PauseReason: null }) return;
        _ = InlineCompletion.RequestAsync(_document.FilePath, _document.Document.Text, _editor.CaretOffset, _document.LocalAINeighbors?.Invoke() ?? []);
    }

    private void OnInlineKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || InlineCompletion is not { Text.Length: > 0 } inline) return;
        if (e.Key == Key.Escape) { inline.Cancel(); e.Handled = true; return; }
        if (e.Key != Key.Tab || _editor.CaretOffset != inline.Offset || _editor.SelectionLength != 0) return;
        var text = inline.Text;
        var offset = inline.Offset;
        _acceptingInline = true;
        try
        {
            inline.Cancel();
            _completion?.Close();
            using (_document.Document.RunUpdate()) _document.Document.Insert(offset, text);
            _editor.CaretOffset = offset + text.Length;
            _editor.TextArea.Caret.BringCaretToView();
            e.Handled = true;
        }
        finally { _acceptingInline = false; }
    }
}
