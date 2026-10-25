using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using TabStudio.App.Input.Scrolling;

namespace TabStudio.App.Views;

/// <summary>
/// Scrolling the editor with a finger, as a native Android editor scrolls: the text follows a drag one to one, a flick
/// glides and slows down along Android's fling curve on every display frame, and a touch catches a glide.
/// </summary>
/// <remarks>
/// A touch on the text does what it always did (focus the editor, which opens the on-screen keyboard, and put the caret
/// where the finger is) only once it turns out to be a tap or a long press: a touch that starts a scroll leaves the
/// caret, the focus and the keyboard alone, and a touch that stops a glide is not a tap. Touches on the margins keep
/// their own handling. Mouse input is unchanged.
/// </remarks>
public sealed partial class CodeEditorView
{
    private readonly TouchScrollGestureRecognizer _touchScroll = new();
    private PendingTouch? _pendingTouch;

    /// <summary>The editor's touch scrolling, for tests.</summary>
    internal TouchScrollGestureRecognizer TouchScrolling => _touchScroll;

    /// <summary>Adds touch scrolling to the editor. Called once, by the constructor, after the other pointer handlers.</summary>
    private void InitializeTouchScrolling()
    {
        var textArea = _editor.TextArea;
        textArea.GestureRecognizers.Add(_touchScroll);
        textArea.AddHandler(PointerPressedEvent, OnTouchPressed, RoutingStrategies.Tunnel);
        textArea.AddHandler(PointerReleasedEvent, OnTouchReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        textArea.AddHandler(ContextRequestedEvent, OnTouchContextRequested, RoutingStrategies.Tunnel, handledEventsToo: true);
        textArea.AddHandler(HoldingEvent, OnTouchHolding, RoutingStrategies.Bubble, handledEventsToo: true);
        _touchScroll.ScrollStarted += OnTouchScrollStarted;
    }

    /// <summary>Removes the handlers and stops a glide. Called by <see cref="Dispose"/>.</summary>
    private void DisposeTouchScrolling()
    {
        var textArea = _editor.TextArea;
        textArea.RemoveHandler(PointerPressedEvent, OnTouchPressed);
        textArea.RemoveHandler(PointerReleasedEvent, OnTouchReleased);
        textArea.RemoveHandler(ContextRequestedEvent, OnTouchContextRequested);
        textArea.RemoveHandler(HoldingEvent, OnTouchHolding);
        _touchScroll.ScrollStarted -= OnTouchScrollStarted;
        _touchScroll.StopFling();
        _pendingTouch = null;
    }

    private void OnTouchPressed(object? sender, PointerPressedEventArgs e)
    {
        _pendingTouch = null;
        if (e.Handled || e.Pointer.Type is not (PointerType.Touch or PointerType.Pen) || e.Source is not Visual source
            || !(ReferenceEquals(source, TextView) || TextView.IsVisualAncestorOf(source))
            || !e.GetCurrentPoint(TextView).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // The editor would focus itself and move the caret now; that waits until the touch is a tap or a long press.
        _pendingTouch = new PendingTouch(e.Pointer, e.GetPosition(TextView), e.ClickCount, e.KeyModifiers);
        e.Handled = true;
    }

    private void OnTouchReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pendingTouch is { } touch && touch.Pointer == e.Pointer)
        {
            CommitTouch(isTap: true);
        }
    }

    private void OnTouchHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState == HoldingState.Started)
        {
            CommitTouch(isTap: false);
        }
    }

    // A long press opens the context menu, whose commands act at the caret: the caret goes there first.
    private void OnTouchContextRequested(object? sender, ContextRequestedEventArgs e) => CommitTouch(isTap: false);

    private void OnTouchScrollStarted(object? sender, EventArgs e) => _pendingTouch = null;

    /// <summary>Does what the editor did at once for a touch before touch scrolling: focus, then place the caret.</summary>
    /// <param name="isTap">A tap rather than a long press: a tap on the selection deselects it, a long press keeps it for the context menu.</param>
    private void CommitTouch(bool isTap)
    {
        if (_pendingTouch is not { } touch)
        {
            return;
        }

        _pendingTouch = null;
        if (_touchScroll.CaughtFling)
        {
            return;
        }

        var textArea = _editor.TextArea;
        textArea.Focus();
        if (TouchPosition(touch.Position) is not { } position)
        {
            return;
        }

        var offset = _editor.Document.GetOffset(position.Location);
        var onSelection = textArea.Options.EnableTextDragDrop && touch.ClickCount == 1
            && !touch.Modifiers.HasFlag(KeyModifiers.Shift) && textArea.Selection.Contains(offset);
        if (onSelection && !isTap)
        {
            return;
        }

        textArea.Caret.Position = position;
        textArea.Caret.DesiredXPos = double.NaN;
        if (onSelection)
        {
            textArea.ClearSelection();
        }
    }

    /// <summary>The caret position under a point of the text view, as the editor finds it for a pointer.</summary>
    private TextViewPosition? TouchPosition(Point point)
    {
        var textView = TextView;
        var y = Math.Clamp(point.Y, 0, textView.Bounds.Height) + textView.VerticalOffset;
        y = Math.Min(y, textView.DocumentHeight - 1e-6);
        if (textView.GetVisualLineFromVisualTop(y) is not { TextLines: not null } line)
        {
            return null;
        }

        var x = point.X + textView.HorizontalOffset;
        var textLine = line.GetTextLineByVisualYPosition(y);
        var column = line.GetVisualColumn(textLine, x, _editor.TextArea.Selection.EnableVirtualSpace);
        var offset = line.GetRelativeOffset(column) + line.FirstDocumentLine.Offset;
        return new TextViewPosition(_editor.Document.GetLocation(offset), column)
        {
            IsAtEndOfLine = column >= line.GetTextLineVisualStartColumn(textLine) + textLine.Length,
        };
    }

    /// <summary>A touch on the text whose caret placement waits to see whether it scrolls.</summary>
    private sealed record PendingTouch(IPointer Pointer, Point Position, int ClickCount, KeyModifiers Modifiers);
}
