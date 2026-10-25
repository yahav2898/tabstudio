using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Input;

namespace TabStudio.App.Views;

/// <summary>
/// The floating touch bar over the editor (docs/design/stage-2.md, 2.7). It shows when the last input was a finger or
/// a pen and fades away (150 ms) while a hardware keyboard types, coming back on the next touch. It keeps out of the
/// code's way: it sits above the bottom dock, and when the caret's line or one of the editor's popups (the completion
/// list, the light bulb, a coverage popup) would be under it, it moves to the editor's top right.
/// </summary>
public sealed partial class WorkspaceView
{
    /// <summary>How long the touch bar takes to fade away when a hardware keyboard types.</summary>
    private static readonly TimeSpan TouchBarFade = TimeSpan.FromMilliseconds(150);

    /// <summary>The touch bar's distance from the editor's edges.</summary>
    private static readonly Thickness TouchBarInset = new(16, 12);

    private readonly DispatcherTimer _fadeTimer = new() { Interval = TouchBarFade };
    private CodeEditorView? _trackedEditor;
    private IDisposable? _popupWatch;
    private bool _placementPending;

    /// <summary>Whether the touch bar moved to the top of the editor, out of the caret's or a popup's way; for tests.</summary>
    internal bool IsTouchBarAtTop => TouchBarHost.VerticalAlignment == VerticalAlignment.Top;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _popupWatch ??= Popup.IsOpenProperty.Changed.Subscribe(new PopupWatcher(this));
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _popupWatch?.Dispose();
        _popupWatch = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Follows the input and the editor. Called by the constructor.</summary>
    private void InitializeTouchBar()
    {
        AddHandler(PointerPressedEvent, OnPointerPressedForTouchBar, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnKeyDownForTouchBar, RoutingStrategies.Tunnel, handledEventsToo: true);
        _fadeTimer.Tick += OnTouchBarFaded;
        EditorPanel.SizeChanged += (_, _) => ScheduleTouchBarPlacement();
        TouchBarHost.SizeChanged += (_, _) => ScheduleTouchBarPlacement();
        RunNotice.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                ScheduleTouchBarPlacement();
            }
        };
    }

    /// <summary>A finger or a pen brings the touch bar back.</summary>
    private void OnPointerPressedForTouchBar(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            _fadeTimer.Stop();
            TouchBarFader.Opacity = 1;
            if (_viewModel is not null)
            {
                _viewModel.IsTouchInput = true;
            }
        }
    }

    /// <summary>
    /// A key from a hardware keyboard fades the touch bar away. Keys typed while the IDE is on an external display in
    /// desktop mode do not count: the bar never shows there, and the tablet keeps it.
    /// </summary>
    private void OnKeyDownForTouchBar(object? sender, KeyEventArgs e)
    {
        if (_viewModel is { IsTouchInput: true } && !_fadeTimer.IsEnabled && !InputModality.GetPrefersKeyboard(this)
            && InputModality.IsHardwareKey(e.Key, e.KeyModifiers))
        {
            TouchBarFader.Opacity = 0;
            _fadeTimer.Start();
        }
    }

    private void OnTouchBarFaded(object? sender, EventArgs e)
    {
        _fadeTimer.Stop();
        if (_viewModel is not null)
        {
            _viewModel.IsTouchInput = false;
        }
    }

    /// <summary>Follows the active editor's caret and scrolling, once its view exists.</summary>
    private void TrackActiveEditorSoon() => Dispatcher.UIThread.Post(TrackActiveEditor, DispatcherPriority.Background);

    private void TrackActiveEditor()
    {
        var editor = ActiveCodeEditor();
        if (ReferenceEquals(editor, _trackedEditor))
        {
            ScheduleTouchBarPlacement();
            return;
        }

        if (_trackedEditor is { } old)
        {
            old.Editor.TextArea.Caret.PositionChanged -= OnTrackedEditorChanged;
            old.Editor.TextArea.TextView.ScrollOffsetChanged -= OnTrackedEditorChanged;
        }

        _trackedEditor = editor;
        if (editor is not null)
        {
            editor.Editor.TextArea.Caret.PositionChanged += OnTrackedEditorChanged;
            editor.Editor.TextArea.TextView.ScrollOffsetChanged += OnTrackedEditorChanged;
        }

        ScheduleTouchBarPlacement();
    }

    private void OnTrackedEditorChanged(object? sender, EventArgs e) => ScheduleTouchBarPlacement();

    /// <summary>The code editor of the active document, also inside a designer that shows code beside its design.</summary>
    private CodeEditorView? ActiveCodeEditor() =>
        _viewModel?.ActiveDocument is { } document && Editors.ViewFor(document) is { } view
            ? view as CodeEditorView ?? view.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(editor => editor.IsEffectivelyVisible)
            : null;

    /// <summary>Places the touch bar once the current layout and input are done, however many changes asked for it.</summary>
    private void ScheduleTouchBarPlacement()
    {
        if (_placementPending)
        {
            return;
        }

        _placementPending = true;
        Dispatcher.UIThread.Post(PlaceTouchBar, DispatcherPriority.Background);
    }

    /// <summary>At the bottom right of the editor, unless the caret's line or a popup is there and the top right is free.</summary>
    private void PlaceTouchBar()
    {
        _placementPending = false;
        var panel = EditorPanel.Bounds.Size;
        var bar = TouchBarHost.Bounds.Size;
        if (!TouchBarHost.IsEffectivelyVisible || bar.Width <= 0 || panel.Height <= 0)
        {
            return;
        }

        var x = panel.Width - TouchBarInset.Right - bar.Width;
        var bottom = new Rect(x, panel.Height - TouchBarInset.Bottom - bar.Height, bar.Width, bar.Height);
        var topInset = TouchBarInset.Top + (RunNotice.IsVisible ? RunNotice.Bounds.Height + 8 : 0);
        var top = new Rect(x, topInset, bar.Width, bar.Height);
        var obstacles = TouchBarObstacles();
        var atTop = obstacles.Any(obstacle => obstacle.Intersects(bottom)) && !obstacles.Any(obstacle => obstacle.Intersects(top));
        TouchBarHost.VerticalAlignment = atTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        TouchBarHost.Margin = atTop ? new Thickness(TouchBarInset.Left, topInset, TouchBarInset.Right, TouchBarInset.Bottom) : TouchBarInset;
    }

    /// <summary>What the touch bar must not cover, in the editor panel's coordinates: the caret's line and the editor's open popups.</summary>
    private List<Rect> TouchBarObstacles()
    {
        var obstacles = new List<Rect>();
        if (ActiveCodeEditor() is not { } view)
        {
            return obstacles;
        }

        var textView = view.Editor.TextArea.TextView;
        if (textView.VisualLinesValid && textView.GetVisualLine(view.Editor.TextArea.Caret.Line) is { } line
            && textView.TranslatePoint(new Point(0, line.VisualTop - textView.ScrollOffset.Y), EditorPanel) is { } lineTop)
        {
            obstacles.Add(new Rect(0, lineTop.Y, EditorPanel.Bounds.Width, line.Height));
        }

        foreach (var popup in view.GetLogicalDescendants().OfType<Popup>())
        {
            if (popup is { IsOpen: true, Child: { IsEffectivelyVisible: true } child } && BoundsIn(child, EditorPanel) is { } bounds)
            {
                obstacles.Add(bounds);
            }
        }

        return obstacles;
    }

    /// <summary>
    /// A control's bounds in another's coordinates, also when it is in a popup window of its own rather than the
    /// window's overlay layer.
    /// </summary>
    private static Rect? BoundsIn(Visual visual, Visual target)
    {
        var size = visual.Bounds.Size;
        if (visual.TranslatePoint(default, target) is { } origin)
        {
            return new Rect(origin, size);
        }

        if (TopLevel.GetTopLevel(target) is not { } top || TopLevel.GetTopLevel(visual) is null)
        {
            return null;
        }

        var screen = visual.PointToScreen(default);
        var targetScreen = target.PointToScreen(default);
        var scale = top.RenderScaling;
        return new Rect((screen.X - targetScreen.X) / scale, (screen.Y - targetScreen.Y) / scale, size.Width, size.Height);
    }

    /// <summary>Places the touch bar again whenever a popup opens or closes, such as the completion list.</summary>
    private sealed class PopupWatcher(WorkspaceView view) : IObserver<AvaloniaPropertyChangedEventArgs<bool>>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(AvaloniaPropertyChangedEventArgs<bool> value) => view.ScheduleTouchBarPlacement();
    }
}
