using System.ComponentModel;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.Editing;
using TabStudio.App.Editing;
using TabStudio.App.Services.Live;
using TabStudio.App.ViewModels;
using TabStudio.Live;

namespace TabStudio.App.Views;

/// <summary>
/// A shared document in a TabStudio Live session: the others' carets and selections in their colors, this
/// participant's place reported as it moves, follow mode, a read-only editor for a participant that only watches,
/// and Undo that takes back only this participant's edits.
/// </summary>
public sealed partial class CodeEditorView
{
    private LivePresenceRenderer? _livePresence;
    private LiveDocumentBinder? _liveBinder;
    private bool _followingMove;

    /// <summary>The others' carets drawn in this editor, for tests.</summary>
    internal LivePresenceRenderer? LivePresence => _livePresence;

    /// <summary>Starts showing the session, when the document is shared. Called once, by the constructor.</summary>
    private void InitializeLive()
    {
        _document.LiveChanged += OnLiveChanged;
        _document.PropertyChanged += OnLiveDocumentPropertyChanged;
        var area = _editor.TextArea;
        area.Caret.PositionChanged += OnLivePlaceChanged;
        area.SelectionChanged += OnLivePlaceChanged;
        area.TextView.ScrollOffsetChanged += OnLivePlaceChanged;
        area.TextEntering += OnLiveTextEntering;
        area.AddHandler(PointerPressedEvent, OnLivePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        area.AddHandler(KeyDownEvent, OnLiveKeyDown, RoutingStrategies.Tunnel);
        AttachLive(_document.Live);
    }

    private void DisposeLive()
    {
        _document.LiveChanged -= OnLiveChanged;
        _document.PropertyChanged -= OnLiveDocumentPropertyChanged;
        var area = _editor.TextArea;
        area.Caret.PositionChanged -= OnLivePlaceChanged;
        area.SelectionChanged -= OnLivePlaceChanged;
        area.TextView.ScrollOffsetChanged -= OnLivePlaceChanged;
        area.TextEntering -= OnLiveTextEntering;
        area.RemoveHandler(PointerPressedEvent, OnLivePointerPressed);
        area.RemoveHandler(KeyDownEvent, OnLiveKeyDown);
        AttachLive(null);
    }

    private void OnLiveChanged(object? sender, EventArgs e) => AttachLive(_document.Live);

    private void AttachLive(LiveDocumentBinder? binder)
    {
        _editor.IsReadOnly = _document.IsLiveReadOnly;
        if (ReferenceEquals(binder, _liveBinder))
        {
            return;
        }

        if (_liveBinder is { } old)
        {
            old.CaretsChanged -= OnLiveCaretsChanged;
            old.FollowMoved -= OnLiveFollowMoved;
            _livePresence?.Detach();
        }

        _liveBinder = binder;
        if (binder is null)
        {
            return;
        }

        _livePresence ??= new LivePresenceRenderer();
        _livePresence.Attach(_editor.TextArea.TextView);
        binder.CaretsChanged += OnLiveCaretsChanged;
        binder.FollowMoved += OnLiveFollowMoved;
        OnLiveCaretsChanged(binder, EventArgs.Empty);
        ReportLivePlace();
    }

    private void OnLiveDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.IsLiveReadOnly))
        {
            _editor.IsReadOnly = _document.IsLiveReadOnly;
        }
    }

    private void OnLiveCaretsChanged(object? sender, EventArgs e)
    {
        if (_liveBinder is not { } binder || _livePresence is not { } presence)
        {
            return;
        }

        presence.Show(binder.Carets);
    }

    private void OnLivePlaceChanged(object? sender, EventArgs e)
    {
        if (!_followingMove)
        {
            ReportLivePlace();
        }
    }

    /// <summary>Says where this participant is: the selection's ends and the first visible line.</summary>
    private void ReportLivePlace()
    {
        if (_liveBinder is not { } binder || !IsVisible)
        {
            return;
        }

        var area = _editor.TextArea;
        var document = _editor.Document;
        var head = area.Caret.Offset;
        var anchor = head;
        if (!area.Selection.IsEmpty)
        {
            var start = document.GetOffset(area.Selection.StartPosition.Location);
            var end = document.GetOffset(area.Selection.EndPosition.Location);
            anchor = start == head ? end : start;
        }

        var textView = area.TextView;
        var top = textView.GetDocumentLineByVisualTop(textView.ScrollOffset.Y)?.LineNumber ?? 1;
        binder.ReportPlace(anchor, head, top);
    }

    /// <summary>The participant this device follows moved: the editor shows its selection and its first line.</summary>
    private void OnLiveFollowMoved(object? sender, LivePresence presence)
    {
        if (_liveBinder is not { } binder || binder.OffsetsOf(presence) is not { } offsets)
        {
            return;
        }

        var document = _editor.Document;
        var anchor = Math.Clamp(offsets.Anchor, 0, document.TextLength);
        var head = Math.Clamp(offsets.Head, 0, document.TextLength);
        _followingMove = true;
        try
        {
            var area = _editor.TextArea;
            area.Selection = anchor == head ? Selection.Create(area, head, head) : Selection.Create(area, anchor, head);
            area.Caret.Offset = head;
            var line = Math.Clamp(presence.TopLine, 1, document.LineCount);
            _editor.ScrollVerticallyTo(area.TextView.GetVisualTopByDocumentLine(line));
        }
        finally
        {
            _followingMove = false;
        }
    }

    private void OnLiveTextEntering(object? sender, TextInputEventArgs e) => _liveBinder?.StopFollowing();

    private void OnLivePointerPressed(object? sender, PointerPressedEventArgs e) => _liveBinder?.StopFollowing();

    /// <summary>Ctrl+Z and Ctrl+Y undo and redo this participant's own edits; editing keys stop following.</summary>
    private void OnLiveKeyDown(object? sender, KeyEventArgs e)
    {
        if (_liveBinder is not { } binder)
        {
            return;
        }

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (control && e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            binder.Undo();
            e.Handled = true;
        }
        else if (control && (e.Key == Key.Y || (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift))))
        {
            binder.Redo();
            e.Handled = true;
        }
        else if (e.Key is Key.Back or Key.Delete or Key.Enter or Key.Tab)
        {
            binder.StopFollowing();
        }
    }

    /// <summary>Undo, as the Edit menu and the toolbar ask it: this participant's own edits in a shared document.</summary>
    private void UndoEdit()
    {
        if (_liveBinder is { } binder)
        {
            binder.Undo();
        }
        else
        {
            _editor.Undo();
        }
    }

    /// <summary>Redo, as the Edit menu and the toolbar ask it.</summary>
    private void RedoEdit()
    {
        if (_liveBinder is { } binder)
        {
            binder.Redo();
        }
        else
        {
            _editor.Redo();
        }
    }
}
