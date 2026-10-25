using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// IntelliSense in the editor: the shared plumbing for the completion list, Parameter Info, Quick Info, the light
/// bulb and the context menu (each in its own <c>CodeEditorView.*.cs</c> file), keyboard shortcuts, and the work
/// that follows the caret: reference highlights, the breadcrumb and the light bulb. Two-key chords such as
/// Ctrl+R, Ctrl+R belong to the shell's chord map (<c>WorkspaceView.Chords.cs</c>).
/// </summary>
public sealed partial class CodeEditorView
{
    private static readonly TimeSpan CaretIdleDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan LightBulbDelay = TimeSpan.FromMilliseconds(300);

    private ILanguageFeatures? _features;
    private Panel? _popupHost;
    private ReferenceHighlightRenderer? _highlights;
    private CancellationTokenSource? _caretIdle;

    /// <summary>The highlights of the symbol under the caret, for tests.</summary>
    public ReferenceHighlightRenderer? ReferenceHighlights => _highlights;

    /// <summary>The IDE this editor is in, for commands that work across files.</summary>
    private WorkspaceViewModel? Workspace => this.FindAncestorOfType<WorkspaceView>()?.DataContext as WorkspaceViewModel;

    private TextView TextView => _editor.TextArea.TextView;

    private void InitializeIntelliSense()
    {
        // The popups need a place in the logical tree to find their styles; they render in the window's overlay layer.
        var root = new Panel();
        Content = null;
        root.Children.Add(_editor);
        Content = root;
        _popupHost = root;

        _features = _document.Features;
        _editor.ContextMenu = CreateContextMenu();
        _editor.TextArea.AddHandler(KeyDownEvent, OnIntelliSenseKeyDown, RoutingStrategies.Tunnel);
        _document.ActionRequested += OnIntelliSenseActionRequested;
        if (_features is null)
        {
            return;
        }

        _highlights = new ReferenceHighlightRenderer();
        TextView.BackgroundRenderers.Insert(0, _highlights);

        InitializeCompletion(_features);
        InitializeSignatureHelp();
        InitializeQuickInfo();
        InitializeQuickActions();

        _editor.TextArea.Caret.PositionChanged += OnIntelliSenseCaretMoved;
        _editor.Document.Changed += OnIntelliSenseTextChanged;
        TextView.ScrollOffsetChanged += OnIntelliSenseScrolled;
        _editor.TextArea.AddHandler(PointerPressedEvent, OnIntelliSensePointerPressed, RoutingStrategies.Tunnel);
        _editor.TextArea.LostFocus += OnIntelliSenseLostFocus;
    }

    private void DisposeIntelliSense()
    {
        _caretIdle?.Cancel();
        _editor.TextArea.RemoveHandler(KeyDownEvent, OnIntelliSenseKeyDown);
        _document.ActionRequested -= OnIntelliSenseActionRequested;
        if (_features is null)
        {
            return;
        }

        _editor.TextArea.Caret.PositionChanged -= OnIntelliSenseCaretMoved;
        _editor.Document.Changed -= OnIntelliSenseTextChanged;
        TextView.ScrollOffsetChanged -= OnIntelliSenseScrolled;
        _editor.TextArea.RemoveHandler(PointerPressedEvent, OnIntelliSensePointerPressed);
        _editor.TextArea.LostFocus -= OnIntelliSenseLostFocus;
        DisposeCompletion();
        DisposeSignatureHelp();
        DisposeQuickInfo();
        DisposeQuickActions();
    }

    private void OnIntelliSenseActionRequested(object? sender, EditorAction action)
    {
        if (_features is null)
        {
            return;
        }

        switch (action)
        {
            case EditorAction.QuickActions:
                _ = ShowQuickActionsAsync();
                break;
            case EditorAction.ListMembers:
                _ = OpenCompletionAsync(completeWord: false);
                break;
            case EditorAction.CompleteWord:
                _ = OpenCompletionAsync(completeWord: true);
                break;
            case EditorAction.ParameterInfo:
                _ = RequestSignatureHelpAsync();
                break;
            case EditorAction.QuickInfo:
                _ = ShowQuickInfoAtCaretAsync();
                break;
        }
    }

    private void OnIntelliSenseKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleCompletionKey(e) || HandleSignatureHelpKey(e) || HandleIntelliSenseShortcut(e))
        {
            e.Handled = true;
        }
    }

    private bool HandleIntelliSenseShortcut(KeyEventArgs e)
    {
        if (_features is null)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.F12 when e.KeyModifiers == KeyModifiers.None:
                _ = Workspace?.GoToDefinitionCommand.ExecuteAsync(null);
                return true;
            case Key.F12 when e.KeyModifiers == KeyModifiers.Control:
                _ = Workspace?.GoToImplementationCommand.ExecuteAsync(null);
                return true;
            case Key.F12 when e.KeyModifiers == KeyModifiers.Alt:
                _ = Workspace?.PeekDefinitionCommand.ExecuteAsync(null);
                return true;
            case Key.F12 when e.KeyModifiers == KeyModifiers.Shift:
                _ = Workspace?.FindAllReferencesCommand.ExecuteAsync(null);
                return true;
            case Key.F2 when e.KeyModifiers == KeyModifiers.None:
                _ = Workspace?.RenameSymbolCommand.ExecuteAsync(null);
                return true;
            case Key.Space when e.KeyModifiers == KeyModifiers.Control:
                _ = OpenCompletionAsync(completeWord: true);
                return true;
            case Key.J when e.KeyModifiers == KeyModifiers.Control:
                _ = OpenCompletionAsync(completeWord: false);
                return true;
            case Key.Space when e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift):
                _ = RequestSignatureHelpAsync();
                return true;
            case Key.OemPeriod when e.KeyModifiers == KeyModifiers.Control:
            case Key.Enter when e.KeyModifiers == KeyModifiers.Alt:
                _ = ShowQuickActionsAsync();
                return true;
            case Key.Escape when e.KeyModifiers == KeyModifiers.None:
                return ClosePeek() | CloseQuickInfo();
            default:
                return false;
        }
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    private void OnIntelliSenseCaretMoved(object? sender, EventArgs e)
    {
        _completion?.Refilter();
        OnSignatureHelpCaretMoved();
        CloseQuickInfo();
        HideLightBulb();
        ScheduleCaretWork();
    }

    private void OnIntelliSenseTextChanged(object? sender, DocumentChangeEventArgs e) => _highlights?.OnDocumentChanged(e);

    private void OnIntelliSenseScrolled(object? sender, EventArgs e)
    {
        PlaceCompletionPopup();
        PlaceSignatureHelpPopup();
        CloseQuickInfo();
        HideLightBulb();
    }

    private void OnIntelliSenseLostFocus(object? sender, RoutedEventArgs e)
    {
        // A tap on a popup never takes focus; focus moving elsewhere, such as to a panel or a dialog, ends the session.
        if (!_editor.TextArea.IsKeyboardFocusWithin && (_completion?.IsCommitting != true))
        {
            _completion?.Close();
            CloseSignatureHelp();
        }
    }

    /// <summary>Ctrl+click goes to the definition of the symbol under the pointer.</summary>
    private void OnIntelliSensePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control
            || !e.GetCurrentPoint(_editor.TextArea).Properties.IsLeftButtonPressed
            || OffsetAt(e.GetPosition(TextView)) is not { } offset)
        {
            return;
        }

        e.Handled = true;
        _editor.TextArea.ClearSelection();
        _editor.CaretOffset = offset;
        _editor.TextArea.Focus();
        _ = Workspace?.GoToDefinitionCommand.ExecuteAsync(null);
    }

    private void ScheduleCaretWork()
    {
        _caretIdle?.Cancel();
        _caretIdle?.Dispose();
        _caretIdle = new CancellationTokenSource();
        _ = RunCaretWorkAsync(_caretIdle.Token);
    }

    /// <summary>Once the caret rests: highlight the symbol's references, update the breadcrumb, then the light bulb.</summary>
    private async Task RunCaretWorkAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CaretIdleDelay, cancellationToken);
            var offset = _editor.CaretOffset;
            await UpdateReferenceHighlightsAsync(offset, cancellationToken);
            await _document.UpdateSymbolPathAsync(offset, cancellationToken);
            await Task.Delay(LightBulbDelay, cancellationToken);
            await UpdateLightBulbAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The caret moved again.
        }
    }

    private async Task UpdateReferenceHighlightsAsync(int offset, CancellationToken cancellationToken)
    {
        if (_features is null || _highlights is null)
        {
            return;
        }

        if (_editor.SelectionLength > 0)
        {
            _highlights.Clear();
            TextView.InvalidateLayer(KnownLayer.Selection);
            return;
        }

        _document.SyncText();
        var edits = _document.EditCount;
        var highlights = await _features.GetDocumentHighlightsAsync(_document.FilePath, offset, cancellationToken);
        if (cancellationToken.IsCancellationRequested || edits != _document.EditCount)
        {
            return;
        }

        _highlights.SetHighlights(highlights, _editor.Document.TextLength);
        TextView.InvalidateLayer(KnownLayer.Selection);
    }

    /// <summary>Creates a popup that renders in the window's overlay layer, placed relative to the text.</summary>
    private Popup CreatePopup(Control child, PopupAnchor anchor, PopupGravity gravity)
    {
        var popup = new Popup
        {
            Child = child,
            PlacementTarget = TextView,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = anchor,
            PlacementGravity = gravity,
            PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipY | PopupPositionerConstraintAdjustment.SlideX,
            ShouldUseOverlayLayer = true,
            IsLightDismissEnabled = false,
        };
        _popupHost!.Children.Add(popup);
        return popup;
    }

    /// <summary>The line at an offset as a thin rectangle in text view coordinates, or null when it is out of view.</summary>
    private Rect? LineRect(int offset)
    {
        var document = _editor.Document;
        if (document is null || !TextView.VisualLinesValid)
        {
            return null;
        }

        var position = new TextViewPosition(document.GetLocation(Math.Clamp(offset, 0, document.TextLength)));
        var top = TextView.GetVisualPosition(position, VisualYPosition.LineTop) - TextView.ScrollOffset;
        var bottom = TextView.GetVisualPosition(position, VisualYPosition.LineBottom) - TextView.ScrollOffset;
        if (bottom.Y <= 0 || top.Y >= TextView.Bounds.Height)
        {
            return null;
        }

        return new Rect(Math.Max(0, top.X), top.Y, 1, Math.Max(1, bottom.Y - top.Y));
    }

    /// <summary>The offset of the character under a point of the text view, or null past the end of a line.</summary>
    private int? OffsetAt(Point point)
    {
        var document = _editor.Document;
        if (document is null || !TextView.VisualLinesValid || TextView.GetPositionFloor(point + TextView.ScrollOffset) is not { } position)
        {
            return null;
        }

        var line = document.GetLineByNumber(Math.Clamp(position.Line, 1, document.LineCount));
        var offset = document.GetOffset(position.Location);
        return offset < line.EndOffset ? offset : null;
    }
}
