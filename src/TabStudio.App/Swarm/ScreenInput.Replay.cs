using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// Replaying input the way a person makes it: a finger goes down on what is at the element's point and lifts again,
/// held for a long press; a scroll is a wheel over the element; text and keys go to the element this screen last
/// focused. They travel as Avalonia's own pointer, wheel, text and key events, through hit testing, capture, the
/// gesture recognizers, focus and key bindings, so a button is clicked by its own logic, never by calling its command.
/// </summary>
internal sealed partial class ScreenInput
{
    /// <summary>How long a frame may take before a replay goes on without it, such as on a screen that is not drawn.</summary>
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>How much longer than the platform's hold delay a replayed long press holds.</summary>
    private static readonly TimeSpan HoldMargin = TimeSpan.FromMilliseconds(150);

    /// <summary>How far one notch of a mouse wheel scrolls content that is not scrolled by items, as Avalonia does.</summary>
    private const double WheelStep = 50;

    private IPointer? _replayPointer;
    private bool _replayingKeys;

    /// <summary>Replays an input on the screen, as if a person made it. UI thread only.</summary>
    /// <param name="input">The input.</param>
    /// <param name="cancellationToken">Cancels a long press that is still held; the finger lifts.</param>
    /// <returns>
    /// Whether the input was delivered: its element was found and shown, or for text and keys, the screen has a
    /// focused element.
    /// </returns>
    public async Task<bool> ReplayAsync(AppInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Avalonia's controls check what is drawn where, such as a button that a lifted finger is still over; a
        // frame lets the screen's latest layout be drawn first.
        await NextFramesAsync(cancellationToken);
        return input.Kind switch
        {
            AppInputKind.Tap => await TouchAsync(input, hold: false, cancellationToken),
            AppInputKind.LongPress => await TouchAsync(input, hold: true, cancellationToken),
            AppInputKind.Scroll => Scroll(input),
            AppInputKind.Text => Type(input.Text),
            AppInputKind.Key => PressKey(input.Key, input.Modifiers),
            _ => false,
        };
    }

    /// <summary>A finger down on an element's point and up again, after the platform's hold delay for a long press.</summary>
    private async Task<bool> TouchAsync(AppInput input, bool hold, CancellationToken cancellationToken)
    {
        if (Target(input.ElementPath) is not { } target || target.TranslatePoint(PointIn(target, input.Position), _root) is not { } point)
        {
            return false;
        }

        // The finger lands on whatever is on top there, as a person's would.
        var touched = ScreenHitTest.ElementAt(_root, point) ?? target;
        var focusBefore = TopLevel.GetTopLevel(_surface)?.FocusManager?.GetFocusedElement();
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, isPrimary: true);
        _replayPointer = pointer;
        var lifted = false;
        try
        {
            touched.RaiseEvent(ReplayedPointerEvents.Pressed(touched, pointer, _root, point));
            if (hold)
            {
                var holdDelay = TopLevel.GetTopLevel(_surface)?.PlatformSettings?.HoldWaitDuration ?? TimeSpan.FromMilliseconds(300);
                await Task.Delay(holdDelay + HoldMargin, cancellationToken);
            }

            Lift();
        }
        finally
        {
            if (!lifted)
            {
                Lift();
            }

            pointer.Capture(null);
            _replayPointer = null;
        }

        RestoreFocus(focusBefore);
        return true;

        void Lift()
        {
            lifted = true;
            var receiver = pointer.Captured as Interactive ?? touched;
            receiver.RaiseEvent(ReplayedPointerEvents.Released(receiver, pointer, _root, point));
        }
    }

    /// <summary>A wheel over an element that scrolls it, or the scroll viewer around it, by the input's distance.</summary>
    private bool Scroll(AppInput input)
    {
        if (Target(input.ElementPath) is not { } target || target.TranslatePoint(PointIn(target, input.Position), _root) is not { } point)
        {
            return false;
        }

        var viewer = target as ScrollViewer
            ?? target.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()
            ?? target.FindAncestorOfType<ScrollViewer>();
        if (viewer?.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault() is not { } presenter)
        {
            return false;
        }

        // Avalonia scrolls a wheel notch by an item for content scrolled by items, and by 50 pixels otherwise.
        var step = presenter.Child is ILogicalScrollable { IsLogicalScrollEnabled: true } logical ? logical.ScrollSize : new Size(WheelStep, WheelStep);
        var delta = new Vector(
            step.Width > 0 ? -input.ScrollDelta.X / step.Width : 0,
            step.Height > 0 ? -input.ScrollDelta.Y / step.Height : 0);
        if (presenter.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft)
        {
            // The presenter mirrors a wheel's horizontal direction for right-to-left content.
            delta = new Vector(-delta.X, delta.Y);
        }

        var under = ScreenHitTest.ElementAt(_root, point);
        var receiver = under is not null && presenter.IsVisualAncestorOf(under) ? under : presenter;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        _replayPointer = pointer;
        try
        {
            receiver.RaiseEvent(ReplayedPointerEvents.Wheel(receiver, pointer, _root, point, delta));
        }
        finally
        {
            _replayPointer = null;
        }

        return true;
    }

    /// <summary>Text typed into the element this screen last focused.</summary>
    private bool Type(string? text)
    {
        if (string.IsNullOrEmpty(text) || FocusedElement is not Interactive target)
        {
            return false;
        }

        _replayingKeys = true;
        try
        {
            target.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Source = target, Text = text });
        }
        finally
        {
            _replayingKeys = false;
        }

        return true;
    }

    /// <summary>
    /// A key pressed and released on the element this screen last focused, or on the app's root: its key bindings
    /// first, as Avalonia's keyboard does, then the key's events. Tab moves the focus when nothing handled it.
    /// </summary>
    private bool PressKey(string? name, string? modifiersText)
    {
        if (!Enum.TryParse<Key>(name, ignoreCase: false, out var key) || key == Key.None)
        {
            return false;
        }

        var modifiers = KeyModifiers.None;
        if (!string.IsNullOrWhiteSpace(modifiersText) && !Enum.TryParse(modifiersText, ignoreCase: false, out modifiers))
        {
            return false;
        }

        var target = FocusedElement as Interactive ?? _root;
        var down = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = target, Key = key, KeyModifiers = modifiers };
        _replayingKeys = true;
        try
        {
            RunKeyBindings(target, down);
            target.RaiseEvent(down);
            if (!down.Handled && key == Key.Tab)
            {
                MoveFocus(target, modifiers.HasFlag(KeyModifiers.Shift) ? NavigationDirection.Previous : NavigationDirection.Next);
            }

            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Source = target, Key = key, KeyModifiers = modifiers });
        }
        finally
        {
            _replayingKeys = false;
        }

        return true;
    }

    /// <summary>The key bindings of the element and its ancestors in the app, never the IDE's around it.</summary>
    private void RunKeyBindings(Interactive target, KeyEventArgs down)
    {
        for (Visual? visual = target; visual is not null && !down.Handled; visual = visual.GetVisualParent())
        {
            if (visual is InputElement element)
            {
                foreach (var binding in element.KeyBindings.ToArray())
                {
                    if (down.Handled)
                    {
                        break;
                    }

                    if (binding.Gesture?.Matches(down) == true)
                    {
                        binding.TryHandle(down);
                    }
                }
            }

            if (ReferenceEquals(visual, _root))
            {
                break;
            }
        }
    }

    /// <summary>Tab: the next element of the app that takes the focus, as Avalonia's keyboard navigation finds it.</summary>
    private void MoveFocus(Interactive from, NavigationDirection direction)
    {
        if (from is IInputElement element && KeyboardNavigationHandler.GetNext(element, direction) is { } next and Visual visual && IsInApp(visual))
        {
            var focusBefore = TopLevel.GetTopLevel(_surface)?.FocusManager?.GetFocusedElement();
            next.Focus(NavigationMethod.Tab);
            RestoreFocus(focusBefore);
        }
    }

    /// <summary>The element a path names, if it is shown on this screen.</summary>
    private Control? Target(string path)
    {
        if (ElementPaths.Resolve(_root, path) is not { } target || target.Bounds.Width <= 0 || target.Bounds.Height <= 0)
        {
            return null;
        }

        // Hidden on this screen, as a person would find it, whether or not the screen itself is on view.
        for (Visual? visual = target; visual is not null && !ReferenceEquals(visual, _surface); visual = visual.GetVisualParent())
        {
            if (!visual.IsVisible)
            {
                return null;
            }
        }

        return target;
    }

    /// <summary>
    /// A replayed input on a screen nobody looks at must not take the keyboard from where the person is typing: when
    /// the focus was outside this screen before, it goes back there. This screen remembers the element it focused.
    /// </summary>
    private void RestoreFocus(IInputElement? focusBefore)
    {
        var focusNow = TopLevel.GetTopLevel(_surface)?.FocusManager?.GetFocusedElement();
        if (focusBefore is Visual before && !IsInApp(before) && before.GetVisualRoot() is not null
            && !ReferenceEquals(focusNow, focusBefore) && focusNow is Visual now && IsInApp(now))
        {
            focusBefore.Focus();
        }
    }

    /// <summary>
    /// A point inside an element from fractions of its size. The default position, (0, 0), means the element's
    /// center, so an input made without a position lands where a person aims.
    /// </summary>
    private static Point PointIn(Control target, Point fraction)
    {
        var size = target.Bounds.Size;
        return fraction == default
            ? new Point(size.Width / 2, size.Height / 2)
            : new Point(Math.Clamp(fraction.X, 0, 1) * size.Width, Math.Clamp(fraction.Y, 0, 1) * size.Height);
    }

    /// <summary>Waits until the next frame has been drawn, or a short while on a screen that is not drawn.</summary>
    private async Task NextFramesAsync(CancellationToken cancellationToken)
    {
        if (TopLevel.GetTopLevel(_surface) is not { } topLevel)
        {
            return;
        }

        // The first frame draws the latest layout; the second starts once the first has been composed.
        for (var i = 0; i < 2; i++)
        {
            var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            topLevel.RequestAnimationFrame(_ => frame.TrySetResult());
            await Task.WhenAny(frame.Task, Task.Delay(FrameTimeout, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
