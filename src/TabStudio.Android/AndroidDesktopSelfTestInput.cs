using System.Globalization;
using Android.App;
using Android.OS;
using Android.Views;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using AvaloniaPixelPoint = Avalonia.PixelPoint;

namespace TabStudio.Android;

/// <summary>
/// The desktop mode self test's keyboard, mouse and screenshots on an emulator.
/// </summary>
/// <remarks>
/// <para>
/// Typing and screenshots are lines in the log that the CI script (build/ci/android-smoke-test.sh) answers with adb:
/// <c>input text</c>, which Android sends to the tablet's window as it does a keyboard's keys, and <c>screencap</c>.
/// </para>
/// <para>
/// The mouse goes through Android's input system, injected into TabStudio's own window, which an app may do: it moves
/// on the tablet, so TabStudio captures it, and then its raw movements and wheel come to TabStudio as a captured
/// mouse's do and move TabStudio's pointer on the display. Only its button is given to TabStudio's capture listener
/// directly: Android refuses an injected button press without an action button, which only the system can set.
/// </para>
/// </remarks>
/// <param name="log">Writes a line to the log the script reads.</param>
/// <param name="displays">The external displays, whose mouse the self test uses.</param>
/// <param name="activity">The activity whose window the mouse is injected into.</param>
internal sealed class AndroidDesktopSelfTestInput(Action<string> log, AndroidExternalDisplays? displays, Func<Activity?> activity) : IDesktopSelfTestInput
{
    /// <summary>The start of the log lines that ask the script to act.</summary>
    public const string StepMarker = "TABSTUDIO_DESKTOP_STEP";

    /// <summary>How long the script may take to take its screenshots.</summary>
    private static readonly TimeSpan ScreenshotTime = TimeSpan.FromSeconds(6);

    /// <summary>How long Android may take to give TabStudio the mouse.</summary>
    private static readonly TimeSpan CaptureTime = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    /// <remarks>Spaces are written as underscores, which the script types as spaces.</remarks>
    public void Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        log($"{StepMarker} type {text.Replace(' ', '_')}");
    }

    /// <inheritdoc />
    public async Task ClickAsync(int displayId, AvaloniaPixelPoint point, CancellationToken cancellationToken)
    {
        if (!await MoveToAsync(point, cancellationToken) || displays is null)
        {
            return;
        }

        // The left button, as a captured mouse gives it: down, pressed, released, up.
        var mouse = displays.Mouse;
        Captured(mouse, MotionEventActions.Down, MotionEventButtonState.Primary);
        Captured(mouse, MotionEventActions.ButtonPress, MotionEventButtonState.Primary);
        Captured(mouse, MotionEventActions.ButtonRelease, 0);
        Captured(mouse, MotionEventActions.Up, 0);
        Log($"clicked display {displayId} at {point.X},{point.Y} with the captured mouse");
    }

    /// <inheritdoc />
    public async Task ScrollAsync(int displayId, AvaloniaPixelPoint point, double notches, CancellationToken cancellationToken)
    {
        if (!await MoveToAsync(point, cancellationToken))
        {
            return;
        }

        await InjectAsync(instrumentation => Inject(instrumentation, Relative(MotionEventActions.Scroll, 0, 0, (float)notches)));
        Log($"turned the wheel {notches} notches on display {displayId} at {point.X},{point.Y}");
    }

    /// <inheritdoc />
    public async Task ScreenshotAsync(CancellationToken cancellationToken)
    {
        log($"{StepMarker} screenshot");
        await Task.Delay(ScreenshotTime, cancellationToken);
    }

    /// <summary>
    /// Moves the mouse on the tablet, which TabStudio captures for the display, then moves TabStudio's pointer to a
    /// point: to the display's corner first, where it stops whatever the speed, then in slow steps, one pixel a count.
    /// </summary>
    /// <returns>Whether TabStudio got the mouse and its pointer is at the point.</returns>
    private async Task<bool> MoveToAsync(AvaloniaPixelPoint point, CancellationToken cancellationToken)
    {
        if (displays is null || activity()?.Window?.DecorView is not { } window)
        {
            Log("has no window to move the mouse in");
            return false;
        }

        var mouse = displays.Mouse;
        if (!mouse.HasCapture)
        {
            var (x, y) = (window.Width / 2f, window.Height / 2f);
            await InjectAsync(instrumentation =>
            {
                using var hover = Event(MotionEventActions.HoverMove, x, y, 0, 0, InputSourceType.Mouse);
                instrumentation.SendPointerSync(hover);
            });
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!mouse.HasCapture && clock.Elapsed < CaptureTime)
            {
                await Task.Delay(50, cancellationToken);
            }

            if (!mouse.HasCapture)
            {
                Log("moved the mouse on the tablet, but TabStudio did not get it");
                return false;
            }
        }

        await InjectAsync(instrumentation =>
        {
            Inject(instrumentation, Relative(MotionEventActions.Move, -100_000, -100_000));
            var (dx, dy) = ((double)point.X, (double)point.Y);
            var steps = (int)Math.Ceiling(Math.Sqrt((dx * dx) + (dy * dy)) / DesktopCursor.PreciseDistance);
            for (var step = 0; step < steps; step++)
            {
                Inject(instrumentation, Relative(MotionEventActions.Move, (float)(dx / steps), (float)(dy / steps)));
            }
        });

        var at = displays.Cursor.PixelPosition;
        Log($"moved the captured mouse to {at.X:0},{at.Y:0} on the display");
        return true;
    }

    /// <summary>Injects events into TabStudio's window on a thread of its own, as injecting waits for the window.</summary>
    private async Task InjectAsync(Action<Instrumentation> inject)
    {
        try
        {
            await Task.Run(() =>
            {
                using var instrumentation = new Instrumentation();
                inject(instrumentation);
            });
        }
        catch (Java.Lang.SecurityException exception)
        {
            Log($"could not inject the mouse's events: {exception.Message}");
        }
    }

    /// <summary>Gives TabStudio's capture listener a captured mouse's button event, on the UI thread.</summary>
    private static void Captured(AndroidDesktopMouse mouse, MotionEventActions action, MotionEventButtonState buttons)
    {
        using var e = Relative(action, 0, 0, buttons: buttons);
        if (e is not null)
        {
            mouse.OnCapturedPointer(null, e);
        }
    }

    /// <summary>Injects a captured mouse's event, which Android gives the window that has the mouse.</summary>
    private static void Inject(Instrumentation instrumentation, MotionEvent? e)
    {
        using (e)
        {
            instrumentation.SendTrackballEventSync(e);
        }
    }

    /// <summary>A captured mouse's event: a movement in counts, a button, or the wheel.</summary>
    private static MotionEvent? Relative(MotionEventActions action, float dx, float dy, float verticalScroll = 0, MotionEventButtonState buttons = 0) =>
        Event(action, dx, dy, verticalScroll, buttons, InputSourceType.MouseRelative);

    private static MotionEvent? Event(MotionEventActions action, float x, float y, float verticalScroll, MotionEventButtonState buttons, InputSourceType source)
    {
        var pointer = new MotionEvent.PointerProperties { Id = 0, ToolType = MotionEventToolType.Mouse };
        var position = new MotionEvent.PointerCoords { X = x, Y = y, Pressure = 1, Size = 1 };
        if (verticalScroll != 0)
        {
            position.SetAxisValue(Axis.Vscroll, verticalScroll);
        }

        var now = SystemClock.UptimeMillis();
        return MotionEvent.Obtain(now, now, action, 1, [pointer], [position], 0, buttons, 1, 1, 0, 0, source, 0);
    }

    private void Log(string message) => log(string.Create(CultureInfo.InvariantCulture, $"Desktop self test: {message}"));
}
