using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// Capturing a person's input: taps, long presses and right clicks from Avalonia's gestures, scrolls from the scroll
/// viewers they move, typed text, and keys that do something other than type, such as Enter or Ctrl+A.
/// </summary>
internal sealed partial class ScreenInput
{
    /// <summary>How long after a person's last touch or wheel a scroll viewer that moves still moves because of them.</summary>
    private static readonly TimeSpan PersonScrollWindow = TimeSpan.FromMilliseconds(900);

    /// <summary>How long a scroll must rest before it is captured as one input.</summary>
    private static readonly TimeSpan ScrollSettleDelay = TimeSpan.FromMilliseconds(220);

    private readonly Dictionary<ScrollViewer, Vector> _pendingScrolls = new(ReferenceEqualityComparer.Instance);
    private readonly DispatcherTimer _scrollSettle = new(DispatcherPriority.Input) { Interval = ScrollSettleDelay };
    private long _personScrollUntil;
    private Point? _lastPointer;

    private void AttachCapture()
    {
        _surface.AddHandler(Gestures.TappedEvent, OnTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(Gestures.RightTappedEvent, OnRightTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(Gestures.HoldingEvent, OnHolding, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(InputElement.PointerPressedEvent, OnPointerActivity, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(InputElement.PointerMovedEvent, OnPointerActivity, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(InputElement.PointerReleasedEvent, OnPointerActivity, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerActivity, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        _surface.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void DetachCapture()
    {
        _surface.RemoveHandler(Gestures.TappedEvent, OnTapped);
        _surface.RemoveHandler(Gestures.RightTappedEvent, OnRightTapped);
        _surface.RemoveHandler(Gestures.HoldingEvent, OnHolding);
        _surface.RemoveHandler(InputElement.PointerPressedEvent, OnPointerActivity);
        _surface.RemoveHandler(InputElement.PointerMovedEvent, OnPointerActivity);
        _surface.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerActivity);
        _surface.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerActivity);
        _surface.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
        _surface.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        _surface.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _surface.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Fence(e, e.Pointer))
        {
            return;
        }

        if (e.Source is Visual source && AimedAt(source) is { } target)
        {
            CaptureAt(AppInputKind.Tap, target, e.GetPosition(target));
        }
    }

    private void OnRightTapped(object? sender, TappedEventArgs e)
    {
        if (Fence(e, e.Pointer))
        {
            return;
        }

        if (e.Source is Visual source && AimedAt(source) is { } target)
        {
            CaptureAt(AppInputKind.LongPress, target, e.GetPosition(target));
        }
    }

    private void OnHolding(object? sender, HoldingRoutedEventArgs e)
    {
        // A replayed long press holds its finger down while the hold is recognized.
        if (_replayPointer is not null)
        {
            e.Handled = true;
            return;
        }

        if (e.HoldingState == HoldingState.Started && e.Source is Visual source && AimedAt(source) is { } target)
        {
            CaptureAt(AppInputKind.LongPress, target, source.TranslatePoint(e.Position, target) ?? default);
        }
    }

    /// <summary>A person touching, moving, lifting or scrolling: a scroll viewer that moves now moves because of them.</summary>
    private void OnPointerActivity(object? sender, PointerEventArgs e)
    {
        if (Fence(e, e.Pointer))
        {
            return;
        }

        // A mouse moving over the app without a button down scrolls nothing.
        if (e.RoutedEvent == InputElement.PointerMovedEvent && e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _lastPointer = e.GetPosition(_root);
        _personScrollUntil = Environment.TickCount64 + (long)PersonScrollWindow.TotalMilliseconds;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is not ScrollViewer viewer || e.OffsetDelta == default || Environment.TickCount64 > _personScrollUntil || !IsInApp(viewer))
        {
            return;
        }

        // A fling scrolls on after the finger lifts; each step keeps the scroll the person's.
        _personScrollUntil = Environment.TickCount64 + (long)PersonScrollWindow.TotalMilliseconds;
        _pendingScrolls[viewer] = _pendingScrolls.GetValueOrDefault(viewer) + e.OffsetDelta;
        _scrollSettle.Stop();
        _scrollSettle.Start();
    }

    /// <summary>A scroll came to rest: one input for each scroll viewer it moved, however many steps it took.</summary>
    private void OnScrollSettled(object? sender, EventArgs e)
    {
        _scrollSettle.Stop();
        var scrolls = _pendingScrolls.ToList();
        _pendingScrolls.Clear();
        foreach (var (viewer, delta) in scrolls)
        {
            if (viewer.GetVisualRoot() is null || ElementPaths.Addressable(_root, viewer) is not { } target || ElementPaths.Describe(_root, target) is not { } path)
            {
                continue;
            }

            // Where the person's finger or pointer was, when it was over the scrolled element.
            var at = _lastPointer is { } pointer && _root.TranslatePoint(pointer, target) is { } local && new Rect(target.Bounds.Size).Contains(local)
                ? local
                : new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
            Emit(new AppInput(AppInputKind.Scroll, path, Fraction(target, at), ScrollDelta: delta));
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (FenceKeys(e) || string.IsNullOrEmpty(e.Text) || e.Source is not Visual source || !IsInApp(source))
        {
            return;
        }

        Emit(new AppInput(AppInputKind.Text, string.Empty, Text: e.Text));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (FenceKeys(e) || e.Source is not Visual source || !IsInApp(source) || !IsCommandKey(e))
        {
            return;
        }

        Emit(new AppInput(
            AppInputKind.Key,
            string.Empty,
            Key: KeyNames.Of(e.Key),
            Modifiers: e.KeyModifiers == KeyModifiers.None ? null : e.KeyModifiers.ToString()));
    }

    private void OnKeyUp(object? sender, KeyEventArgs e) => FenceKeys(e);

    /// <summary>
    /// Whether a key does something other than type, which is captured as a key: Enter, Tab, arrows, Backspace,
    /// function keys, and any key with Ctrl, Alt or the system key. A key that types a character is captured by the
    /// text it types instead, as soft keyboards send text without keys.
    /// </summary>
    internal static bool IsCommandKey(KeyEventArgs e)
    {
        if (e.Key is Key.None or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return false;
        }

        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return true;
        }

        return e.KeySymbol is not { Length: > 0 } symbol || char.IsControl(symbol[0]);
    }

    /// <summary>
    /// The element a tap was meant for: the control that takes it, such as the button around the text a finger
    /// landed on, so the tap replays on that control at every size; otherwise the element under the finger.
    /// </summary>
    private Control? AimedAt(Visual source)
    {
        var target = ElementPaths.Addressable(_root, source);
        for (var element = target; element is not null && !ReferenceEquals(element, _root); element = element.Parent as Control)
        {
            if (LayoutChecks.LayoutScan.IsInteractive(element))
            {
                return element;
            }
        }

        return target;
    }

    /// <summary>A tap, long press or right click on an element, at a point inside it.</summary>
    private void CaptureAt(AppInputKind kind, Control target, Point at)
    {
        if (ElementPaths.Describe(_root, target) is { } path)
        {
            Emit(new AppInput(kind, path, Fraction(target, at)));
        }
    }

    private void Emit(AppInput input)
    {
        if (!_disposed)
        {
            Captured?.Invoke(this, input);
        }
    }

    /// <summary>A point inside an element as fractions of its size, from 0 to 1.</summary>
    private static Point Fraction(Control target, Point at)
    {
        var size = target.Bounds.Size;
        return new Point(
            size.Width > 0 ? Math.Clamp(at.X / size.Width, 0, 1) : 0.5,
            size.Height > 0 ? Math.Clamp(at.Y / size.Height, 0, 1) : 0.5);
    }

    /// <summary>
    /// Stops a replayed pointer's event at the surface, where it has reached the app, so it neither reaches the IDE
    /// nor is captured as a person's input.
    /// </summary>
    /// <returns>Whether the event was replayed.</returns>
    private bool Fence(RoutedEventArgs e, IPointer? pointer)
    {
        if (pointer is null || !ReferenceEquals(pointer, _replayPointer))
        {
            return false;
        }

        e.Handled = true;
        return true;
    }

    /// <summary>Stops a replayed key or text at the surface, as <see cref="Fence"/> does for pointers.</summary>
    /// <returns>Whether the event was replayed.</returns>
    private bool FenceKeys(RoutedEventArgs e)
    {
        if (!_replayingKeys)
        {
            return false;
        }

        e.Handled = true;
        return true;
    }
}
