using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Input.Scrolling;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>What a finger did to the editor in <see cref="TouchScrollProbe"/>.</summary>
/// <param name="Drag">The frames of the drag, as a run.</param>
/// <param name="FollowRatio">How far the text moved per dip the finger moved once the drag had started (1 is one to one).</param>
/// <param name="CaretMoved">Whether the drag moved the caret.</param>
/// <param name="Focused">Whether the drag gave the editor the keyboard focus (on a tablet: opened the on-screen keyboard).</param>
/// <param name="ReleaseVelocity">The finger's velocity when it lifted, in dips per second.</param>
/// <param name="GlideDistance">How far the text glided after the finger lifted.</param>
/// <param name="GlideTime">How long it glided.</param>
/// <param name="GlideFrames">The 120 Hz frames from the release to the end of the glide.</param>
/// <param name="StillFrames">Frames during the glide on which the text did not move.</param>
/// <param name="LateFrames">Frames during the glide that started more than a frame late.</param>
internal sealed record TouchScrollResult(
    ScrollRun Drag, double FollowRatio, bool CaretMoved, bool Focused, double ReleaseVelocity,
    double GlideDistance, TimeSpan GlideTime, int GlideFrames, int StillFrames, int LateFrames)
{
    /// <summary>The fling Android would make of the same release.</summary>
    public FlingSpline Android => FlingSpline.FromVelocity(ReleaseVelocity);

    /// <summary>The result as report lines.</summary>
    public string Report()
    {
        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        text.AppendLine(culture, $"- Drag: the text moved {FollowRatio:0.000} dip per dip of finger movement (1.000 is one to one).");
        text.AppendLine(culture, $"- Starting the drag {(CaretMoved ? "moved" : "did not move")} the caret and {(Focused ? "focused the editor (opening the on-screen keyboard on a tablet)" : "left the focus alone")}.");
        text.AppendLine(culture, $"- Release at {ReleaseVelocity:0} dip/s: glided {GlideDistance:0} dip in {GlideTime.TotalMilliseconds:0} ms over {GlideFrames} frames at 120 Hz; the text stood still on {StillFrames} of them, {LateFrames} started late.");
        text.AppendLine(culture, $"- Android's OverScroller glides {Android.Distance:0} dip in {Android.Duration.TotalMilliseconds:0} ms from the same release.");
        return text.ToString();
    }
}

/// <summary>
/// Scrolls the editor with a finger through Avalonia's real touch device: a slow drag, then a flick, and the glide after
/// the finger lifts, on frames paced at 120 Hz as a fast tablet display shows them.
/// </summary>
internal static class TouchScrollProbe
{
    private static readonly TimeSpan Frame120 = TimeSpan.FromSeconds(1.0 / 120);

    /// <summary>Drags, flicks and lets the editor glide, and says how it went.</summary>
    /// <param name="window">The window.</param>
    /// <param name="view">The editor.</param>
    /// <param name="bench">The benchmark of the editor, for its frames.</param>
    public static TouchScrollResult Measure(Window window, CodeEditorView view, EditorScrollBench bench)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(bench);
        var editor = view.Editor;
        var textView = editor.TextArea.TextView;
        var scroll = bench.ScrollViewer;
        scroll.Offset = new Vector(0, Math.Min(2000, bench.MaxOffset / 4));
        bench.Settle();

        // The focus elsewhere first, as after tapping a panel: a drag that focuses the editor opens the keyboard.
        window.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsEffectivelyVisible && t.Focusable)?.Focus();
        Dispatcher.UIThread.RunJobs();
        var caret = editor.CaretOffset;

        var touch = new HeadlessTouchScreen(window);
        var start = textView.TranslatePoint(new Point(textView.Bounds.Width * 0.45, textView.Bounds.Height * 0.8), window)
            ?? throw new InvalidOperationException("The editor is not in the window.");
        var tracker = new TouchVelocityTracker();
        var finger = start;
        touch.Down(finger);
        tracker.Add(TimeSpan.FromMilliseconds(touch.Now), finger);
        bench.Frame(static () => { });

        // A drag up at 750 dip/s: 6 dips every 8 ms.
        var frames = new List<ScrollFrame>();
        var offsets = new List<(double Finger, double Offset)>();
        for (var i = 0; i < 40; i++)
        {
            touch.Wait(8);
            finger = new Point(finger.X, finger.Y - 6);
            var next = finger;
            frames.Add(bench.Frame(() => touch.Move(next)));
            tracker.Add(TimeSpan.FromMilliseconds(touch.Now), finger);
            offsets.Add((start.Y - finger.Y, scroll.Offset.Y));
        }

        var caretMoved = editor.CaretOffset != caret;
        var focused = editor.TextArea.IsKeyboardFocusWithin;
        var steady = offsets.Skip(10).ToList();
        var followRatio = (steady[^1].Offset - steady[0].Offset) / (steady[^1].Finger - steady[0].Finger);

        // The flick: 40 dips every 8 ms (5000 dip/s), and the finger lifts.
        for (var i = 0; i < 6; i++)
        {
            touch.Wait(8);
            finger = new Point(finger.X, finger.Y - 40);
            var next = finger;
            frames.Add(bench.Frame(() => touch.Move(next)));
            tracker.Add(TimeSpan.FromMilliseconds(touch.Now), finger);
        }

        touch.Wait(4);
        var velocity = -tracker.VelocityAt(TimeSpan.FromMilliseconds(touch.Now)).Y;
        var released = scroll.Offset.Y;
        touch.Up(finger);

        // The glide, on frames paced at 120 Hz in real time: whatever moves the text runs on the UI thread between frames.
        var clock = Stopwatch.StartNew();
        var last = released;
        var lastMotion = TimeSpan.Zero;
        int glideFrames = 0, still = 0, late = 0;
        var stoppedFor = 0;
        for (var frame = 1; frame <= 600 && stoppedFor < 36; frame++)
        {
            var due = Frame120 * frame;
            if (clock.Elapsed > due + Frame120)
            {
                late++;
            }

            while (clock.Elapsed < due)
            {
                Thread.SpinWait(50);
            }

            // Any job lets the dispatcher fire due timers, as the platform's own loop does on a device.
            Dispatcher.UIThread.Post(static () => { }, DispatcherPriority.Background);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var offset = scroll.Offset.Y;
            glideFrames++;
            if (Math.Abs(offset - last) > 0.01)
            {
                still += stoppedFor;
                stoppedFor = 0;
                lastMotion = clock.Elapsed;
            }
            else
            {
                stoppedFor++;
            }

            last = offset;
        }

        glideFrames -= stoppedFor;
        bench.Settle();
        return new TouchScrollResult(
            new ScrollRun("touch drag and flick (real touch input)", frames), followRatio, caretMoved, focused, velocity,
            last - released, lastMotion, glideFrames, still, late);
    }
}
