using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Webkit;
using TabStudio.App.Platform;
using AndroidWebKitView = Android.Webkit.WebView;
using AvaloniaKey = Avalonia.Input.Key;
using AvaloniaKeyModifiers = Avalonia.Input.KeyModifiers;
using AvaloniaPoint = Avalonia.Point;

namespace TabStudio.Android;

/// <summary>
/// The web preview's browser as a Swarm screen: script through <c>EvaluateJavascript</c>, pictures drawn from the
/// view, and touches and keys dispatched to it as Android dispatches a finger's and a keyboard's, so the page sees
/// trusted events and its own gesture handling.
/// </summary>
internal sealed partial class AndroidWebView : IScriptableWebView
{
    /// <summary>How long after a replayed touch the browser may still take the keyboard.</summary>
    private static readonly TimeSpan FocusSettle = TimeSpan.FromMilliseconds(250);

    /// <inheritdoc />
    public Task<string?> EvaluateAsync(string script, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (_view is not { } view)
        {
            return Task.FromResult<string?>(null);
        }

        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        view.EvaluateJavascript(script, new ScriptAnswer(value => answer.TrySetResult(value)));
        return answer.Task.WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]?> CaptureAsync(int maxPixels, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_view is not { Width: > 0, Height: > 0 } view || maxPixels <= 0)
        {
            return Task.FromResult<byte[]?>(null);
        }

        var scale = Math.Min(1f, (float)maxPixels / Math.Max(view.Width, view.Height));
        var width = Math.Max(1, (int)(view.Width * scale));
        var height = Math.Max(1, (int)(view.Height * scale));
        using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        using (var canvas = new Canvas(bitmap))
        {
            // The view draws its content from the top of the page: the picture is of the part scrolled into view.
            canvas.Scale(scale, scale);
            canvas.Translate(-view.ScrollX, -view.ScrollY);
            view.Draw(canvas);
        }

        using var png = new MemoryStream();
        bitmap.Compress(Bitmap.CompressFormat.Png!, 100, png);
        return Task.FromResult<byte[]?>(png.ToArray());
    }

    /// <inheritdoc />
    public async Task<bool> TouchAsync(AvaloniaPoint pixel, TimeSpan hold, CancellationToken cancellationToken)
    {
        if (_view is not { } view)
        {
            return false;
        }

        var keyboard = KeyboardOutside(view);
        var (x, y) = ((float)pixel.X, (float)pixel.Y);
        var down = SystemClock.UptimeMillis();
        if (hold < TimeSpan.FromMilliseconds(ViewConfiguration.LongPressTimeout))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Replay both short-gesture events without a managed timer between them. Their native
            // timestamps retain the requested duration even when the IDE's dispatcher is busy.
            Touch(view, down, down, MotionEventActions.Down, x, y);
            try
            {
                Touch(view, down, down + (long)Math.Max(0, hold.TotalMilliseconds), MotionEventActions.Up, x, y);
            }
            finally
            {
                GiveKeyboardBack(view, keyboard);
            }

            // Keep subsequent replayed input after this gesture's event time, without holding a finger down.
            await Task.Delay(hold, cancellationToken);
            await Task.Delay(FocusSettle, CancellationToken.None);
            GiveKeyboardBack(view, keyboard);
            return true;
        }

        var lifted = false;
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var touches = new Handler(Looper.MainLooper!);
        void Lift()
        {
            if (lifted)
            {
                return;
            }

            lifted = true;
            Touch(view, down, SystemClock.UptimeMillis(), MotionEventActions.Up, x, y);
            GiveKeyboardBack(view, keyboard);
        }

        void ScheduledLift()
        {
            try
            {
                Lift();
                released.TrySetResult(true);
            }
            catch (Exception ex)
            {
                released.TrySetException(ex);
            }
        }

        Touch(view, down, down, MotionEventActions.Down, x, y);
        try
        {
            // Queue the release with Android's gesture timers. A busy Avalonia dispatcher must not turn
            // the requested short tap into a long press while waiting to resume a managed Task.Delay.
            if (!touches.PostDelayed(ScheduledLift, (long)Math.Max(0, hold.TotalMilliseconds)))
            {
                throw new InvalidOperationException("The browser's touch release could not be queued.");
            }

            await released.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            touches.RemoveCallbacks(ScheduledLift);
            // The finger always lifts, even when the touch is cut short.
            Lift();
        }

        // The browser can ask for the focus a moment after the finger lifted, once the page took the touch.
        await Task.Delay(FocusSettle, CancellationToken.None);
        GiveKeyboardBack(view, keyboard);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> PressKeyAsync(AvaloniaKey key, AvaloniaKeyModifiers modifiers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_view is not { } view || KeycodeOf(key) is not { } code)
        {
            return Task.FromResult(false);
        }

        var keyboard = KeyboardOutside(view);
        var meta = MetaOf(modifiers);
        var down = SystemClock.UptimeMillis();
        view.RequestFocus();
        using var press = new KeyEvent(down, down, KeyEventActions.Down, code, 0, meta);
        var taken = view.DispatchKeyEvent(press);
        using var release = new KeyEvent(down, SystemClock.UptimeMillis(), KeyEventActions.Up, code, 0, meta);
        view.DispatchKeyEvent(release);
        GiveKeyboardBack(view, keyboard);
        return Task.FromResult(taken);
    }

    /// <summary>
    /// The view of the window that has the keyboard, when it is not the browser: TabStudio's own, where a person may be
    /// typing while an input is replayed on the page.
    /// </summary>
    private static View? KeyboardOutside(AndroidWebKitView view) =>
        view.HasFocus ? null : view.RootView?.FindFocus();

    /// <summary>
    /// A replayed input on the page must not take the keyboard from where the person is typing, as on the screens
    /// TabStudio draws itself: the view that had it gets it back. The page keeps its own focused element, where
    /// replayed text goes.
    /// </summary>
    private static void GiveKeyboardBack(AndroidWebKitView view, View? keyboard)
    {
        if (keyboard is { IsAttachedToWindow: true } && view.HasFocus)
        {
            keyboard.RequestFocus();
        }
    }

    /// <summary>One finger, as a touch screen reports it.</summary>
    private static void Touch(AndroidWebKitView view, long downTime, long eventTime, MotionEventActions action, float x, float y)
    {
        var finger = new MotionEvent.PointerProperties { Id = 0, ToolType = MotionEventToolType.Finger };
        var at = new MotionEvent.PointerCoords { X = x, Y = y, Pressure = 1, Size = 0.1f };
        using var motion = MotionEvent.Obtain(
            downTime,
            eventTime,
            action,
            1,
            [finger],
            [at],
            MetaKeyStates.None,
            (MotionEventButtonState)0,
            1,
            1,
            0,
            (Edge)0,
            InputSourceType.Touchscreen,
            (MotionEventFlags)0);
        if (motion is not null)
        {
            view.DispatchTouchEvent(motion);
        }
    }

    /// <summary>Android's key code for an Avalonia key, for the keys a replayed input presses.</summary>
    private static Keycode? KeycodeOf(AvaloniaKey key) => key switch
    {
        AvaloniaKey.Enter => Keycode.Enter,
        AvaloniaKey.Tab => Keycode.Tab,
        AvaloniaKey.Escape => Keycode.Escape,
        AvaloniaKey.Back => Keycode.Del,
        AvaloniaKey.Delete => Keycode.ForwardDel,
        AvaloniaKey.Insert => Keycode.Insert,
        AvaloniaKey.Space => Keycode.Space,
        AvaloniaKey.Left => Keycode.DpadLeft,
        AvaloniaKey.Right => Keycode.DpadRight,
        AvaloniaKey.Up => Keycode.DpadUp,
        AvaloniaKey.Down => Keycode.DpadDown,
        AvaloniaKey.Home => Keycode.MoveHome,
        AvaloniaKey.End => Keycode.MoveEnd,
        AvaloniaKey.PageUp => Keycode.PageUp,
        AvaloniaKey.PageDown => Keycode.PageDown,
        >= AvaloniaKey.A and <= AvaloniaKey.Z => Keycode.A + (key - AvaloniaKey.A),
        >= AvaloniaKey.D0 and <= AvaloniaKey.D9 => Keycode.Num0 + (key - AvaloniaKey.D0),
        >= AvaloniaKey.F1 and <= AvaloniaKey.F12 => Keycode.F1 + (key - AvaloniaKey.F1),
        _ => null,
    };

    private static MetaKeyStates MetaOf(AvaloniaKeyModifiers modifiers)
    {
        var meta = MetaKeyStates.None;
        if (modifiers.HasFlag(AvaloniaKeyModifiers.Control))
        {
            meta |= MetaKeyStates.CtrlOn | MetaKeyStates.CtrlLeftOn;
        }

        if (modifiers.HasFlag(AvaloniaKeyModifiers.Shift))
        {
            meta |= MetaKeyStates.ShiftOn | MetaKeyStates.ShiftLeftOn;
        }

        if (modifiers.HasFlag(AvaloniaKeyModifiers.Alt))
        {
            meta |= MetaKeyStates.AltOn | MetaKeyStates.AltLeftOn;
        }

        if (modifiers.HasFlag(AvaloniaKeyModifiers.Meta))
        {
            meta |= MetaKeyStates.MetaOn | MetaKeyStates.MetaLeftOn;
        }

        return meta;
    }

    /// <summary>Receives a script's result, which Android gives as JSON.</summary>
    private sealed class ScriptAnswer(Action<string?> received) : Java.Lang.Object, IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => received(value?.ToString());
    }
}
