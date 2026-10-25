using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using TabStudio.App.Editing;
using TabStudio.App.Input.Scrolling;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// Scrolling the editor with a finger, through Avalonia's real touch device: a drag follows one to one without moving
/// the caret or opening the keyboard, a flick glides along Android's fling curve frame by frame, a touch catches a
/// glide, a tap and a long press still place the caret, and the line numbers scroll and select by touch.
/// </summary>
public sealed class EditorTouchScrollingTests
{
    private static readonly TimeSpan Frame120 = TimeSpan.FromSeconds(1.0 / 120);

    [AvaloniaFact]
    public async Task A_drag_follows_the_finger_one_to_one_and_leaves_the_caret_and_the_keyboard_alone()
    {
        using var fixture = new WorkspaceFixture();
        var (window, view) = await OpenLongFileAsync(fixture);
        var scroll = ScrollViewerOf(view);
        scroll.Offset = new Vector(0, 1500);
        Render();
        FocusElsewhere(window);
        var caret = view.Editor.CaretOffset;
        var touch = new HeadlessTouchScreen(window);
        var finger = PointIn(window, view, 0.5, 0.7);

        touch.Down(finger);
        var offsets = new List<double>();
        for (var i = 0; i < 30; i++)
        {
            touch.Wait(8);
            finger = new Point(finger.X, finger.Y - 6);
            touch.Move(finger);
            Render();
            offsets.Add(scroll.Offset.Y);
        }

        // Past the slop, every dip the finger moves up scrolls the text one dip down; the slop itself never jumps.
        Assert.True(view.TouchScrolling.IsDragging);
        Assert.InRange(offsets[0] - 1500, 0, 6);
        for (var i = 1; i < offsets.Count; i++)
        {
            Assert.Equal(6.0, offsets[i] - offsets[i - 1], 1e-6);
        }

        Assert.Equal(caret, view.Editor.CaretOffset);
        Assert.False(view.Editor.TextArea.IsKeyboardFocusWithin);

        // Resting before lifting: no fling.
        touch.Wait(60);
        touch.Up(finger);
        Render();
        Assert.False(view.TouchScrolling.IsFlinging);
        Assert.False(view.TouchScrolling.IsDragging);
        Assert.Equal(caret, view.Editor.CaretOffset);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_flick_glides_along_Androids_fling_curve_on_every_frame()
    {
        using var fixture = new WorkspaceFixture();
        var (window, view) = await OpenLongFileAsync(fixture);
        var scroll = ScrollViewerOf(view);
        var (frames, clock) = DriveFrames(view);
        scroll.Offset = new Vector(0, 1000);
        Render();
        var touch = new HeadlessTouchScreen(window);

        Flick(touch, PointIn(window, view, 0.5, 0.85), up: true);
        var released = scroll.Offset.Y;
        var fling = view.TouchScrolling.VerticalFling;

        // 40 dips every 8 ms is 5000 dip/s: Android's OverScroller glides 3178 dips in 1.8 s.
        Assert.True(view.TouchScrolling.IsFlinging);
        Assert.Equal(FlingSpline.FromVelocity(5000).Distance, fling.Distance, 1.0);
        var previous = released;
        var step = 0;
        while (view.TouchScrolling.IsFlinging)
        {
            step++;
            clock.Advance(Frame120);
            frames.Step();
            Render();
            var expected = Math.Min(released + fling.PositionAt(Frame120 * step), MaxOffset(scroll));
            Assert.Equal(expected, scroll.Offset.Y, 0.01);
            Assert.True(scroll.Offset.Y >= previous, "the glide never goes back");
            previous = scroll.Offset.Y;
            Assert.True(step < 1000, "the glide ends");
        }

        Assert.Equal(released + fling.Distance, scroll.Offset.Y, 0.01);
        Assert.InRange(Frame120 * step, fling.Duration, fling.Duration + Frame120);
        Assert.Equal(0, frames.Pending);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_touch_catches_a_glide_and_is_not_a_tap()
    {
        using var fixture = new WorkspaceFixture();
        var (window, view) = await OpenLongFileAsync(fixture);
        var scroll = ScrollViewerOf(view);
        var (frames, clock) = DriveFrames(view);
        scroll.Offset = new Vector(0, 1000);
        Render();
        FocusElsewhere(window);
        var caret = view.Editor.CaretOffset;
        var touch = new HeadlessTouchScreen(window);
        Flick(touch, PointIn(window, view, 0.5, 0.85), up: true);
        for (var i = 0; i < 10; i++)
        {
            clock.Advance(Frame120);
            frames.Step();
            Render();
        }

        var caught = scroll.Offset.Y;
        var point = PointIn(window, view, 0.3, 0.4);
        touch.Wait(100);
        touch.Down(point);
        touch.Wait(60);
        touch.Up(point);
        clock.Advance(Frame120);
        frames.Step();
        Render();

        Assert.False(view.TouchScrolling.IsFlinging);
        Assert.Equal(caught, scroll.Offset.Y);
        Assert.Equal(caret, view.Editor.CaretOffset);
        Assert.False(view.Editor.TextArea.IsKeyboardFocusWithin);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_fling_glides_to_rest_on_the_render_loop()
    {
        using var fixture = new WorkspaceFixture();
        var (window, view) = await OpenLongFileAsync(fixture);
        var scroll = ScrollViewerOf(view);
        scroll.Offset = new Vector(0, 1000);
        Render();
        var touch = new HeadlessTouchScreen(window);

        Flick(touch, PointIn(window, view, 0.5, 0.85), up: true);
        var released = scroll.Offset.Y;
        var distance = view.TouchScrolling.VerticalFling.Distance;
        await UiTestHelpers.WaitUntilAsync(
            () =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return !view.TouchScrolling.IsFlinging;
            },
            TimeSpan.FromSeconds(10),
            "the glide to end");

        Assert.Equal(released + distance, scroll.Offset.Y, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_tap_still_places_the_caret_and_focuses_the_editor()
    {
        using var fixture = new WorkspaceFixture();
        var (window, view) = await OpenLongFileAsync(fixture);
        FocusElsewhere(window);
        var editor = view.Editor;
        var target = OffsetOf(editor, "public sealed class InventoryService") + "public sealed class Inv".Length;
        editor.ScrollTo(editor.Document.GetLineByOffset(target).LineNumber, 1);
        Render();
        var point = PointOf(window, editor, target);
        var touch = new HeadlessTouchScreen(window);

        touch.Down(point);
        Assert.NotEqual(target, editor.CaretOffset);
        touch.Wait(80);
        touch.Up(point);
        Dispatcher.UIThread.RunJobs();

        Assert.InRange(editor.CaretOffset, target - 1, target + 1);
        Assert.True(editor.TextArea.IsKeyboardFocusWithin);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_long_press_places_the_caret_before_the_context_menu_opens()
    {
        using var fixture = new WorkspaceFixture();
        var (window, view) = await OpenLongFileAsync(fixture);
        var editor = view.Editor;
        var target = OffsetOf(editor, "public sealed class ShippingService") + "public sealed class Ship".Length;
        editor.ScrollTo(editor.Document.GetLineByOffset(target).LineNumber, 1);
        Render();
        var point = PointOf(window, editor, target);
        var touch = new HeadlessTouchScreen(window);
        int? caretWhenOpened = null;
        editor.ContextMenu!.Opening += (_, _) => caretWhenOpened = editor.CaretOffset;

        touch.Down(point);
        await UiTestHelpers.WaitUntilAsync(() => editor.ContextMenu.IsOpen, TimeSpan.FromSeconds(5), "the context menu");
        touch.Up(point);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(caretWhenOpened);
        Assert.InRange(caretWhenOpened.Value, target - 1, target + 1);
        editor.ContextMenu.Close();
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_line_numbers_scroll_by_touch_and_a_tap_selects_a_line()
    {
        using var fixture = new WorkspaceFixture();
        var (window, view) = await OpenLongFileAsync(fixture);
        var editor = view.Editor;
        var scroll = ScrollViewerOf(view);
        var numbers = editor.TextArea.LeftMargins.OfType<CachedLineNumberMargin>().Single();
        Assert.Equal(typeof(CachedLineNumberMargin), editor.TextArea.LeftMargins.OfType<LineNumberMargin>().Single().GetType());
        var touch = new HeadlessTouchScreen(window);
        var finger = numbers.TranslatePoint(new Point(numbers.Bounds.Width / 2, numbers.Bounds.Height * 0.7), window)!.Value;
        var caret = editor.CaretOffset;

        touch.Down(finger);
        for (var i = 0; i < 20; i++)
        {
            touch.Wait(8);
            finger = new Point(finger.X, finger.Y - 10);
            touch.Move(finger);
            Render();
        }

        touch.Wait(60);
        touch.Up(finger);
        Render();
        Assert.True(scroll.Offset.Y > 150, "the drag on the line numbers scrolled");
        Assert.True(editor.TextArea.Selection.IsEmpty, "nor did it select lines");
        Assert.Equal(caret, editor.CaretOffset);

        // A tap anywhere in a line's row of the numbers, even left of a short number, selects the line.
        var textView = editor.TextArea.TextView;
        var line = textView.VisualLines[3].FirstDocumentLine;
        var tap = numbers.TranslatePoint(new Point(2, textView.VisualLines[3].VisualTop - textView.VerticalOffset + 5), window)!.Value;
        var hit = window.InputHitTest(tap) as Visual;
        var taps = 0;
        numbers.AddHandler(Gestures.TappedEvent, (_, _) => taps++, handledEventsToo: true);
        touch.Down(tap);
        touch.Wait(60);
        touch.Up(tap);
        Dispatcher.UIThread.RunJobs();

        var what = $"the tap hit {string.Join(" in ", (hit?.GetVisualAncestors().Prepend(hit) ?? []).Take(4).Select(v => v.GetType().Name))}, {taps} taps on the numbers";
        Assert.True(line.Offset == editor.SelectionStart, $"the selection starts at line {line.LineNumber}: {what}");
        Assert.True(line.TotalLength == editor.SelectionLength, $"the selection is line {line.LineNumber}: {what}");

        // Scrolled through a long file, the margin keeps only the numbers near the view.
        for (var offset = 0.0; offset < 20000; offset += 300)
        {
            scroll.Offset = new Vector(0, offset);
            Render();
        }

        Assert.InRange(numbers.CachedNumbers, textView.VisualLines.Count, (textView.VisualLines.Count * 4) + 64);
        window.Close();
    }

    private static async Task<(Window Window, CodeEditorView View)> OpenLongFileAsync(WorkspaceFixture fixture)
    {
        var solution = await new SampleLibrary(fixture.Paths).EnsureExtractedAsync();
        var path = Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings", "Operations.cs");
        await File.WriteAllTextAsync(path, LongSampleCode.CSharp());
        await fixture.Workspace.LoadAsync(solution);
        fixture.Workspace.IsBottomPanelVisible = false;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        _ = await fixture.Workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The file did not open.");
        var view = ActiveEditor(window);
        Render();
        return (window, view);
    }

    /// <summary>Glides move only when the test steps a frame, on a clock the test advances.</summary>
    private static (ManualFrames Frames, ManualClock Clock) DriveFrames(CodeEditorView view)
    {
        var frames = new ManualFrames();
        var clock = new ManualClock();
        view.TouchScrolling.Frames = frames;
        view.TouchScrolling.Time = clock;
        return (frames, clock);
    }

    /// <summary>A flick: 40 dips every 8 ms, then the finger lifts 4 ms after its last move.</summary>
    private static void Flick(HeadlessTouchScreen touch, Point from, bool up)
    {
        var finger = from;
        touch.Down(finger);
        for (var i = 0; i < 8; i++)
        {
            touch.Wait(8);
            finger = new Point(finger.X, finger.Y + (up ? -40 : 40));
            touch.Move(finger);
            Render();
        }

        touch.Wait(4);
        touch.Up(finger);
    }

    private static ScrollViewer ScrollViewerOf(CodeEditorView view) => view.Editor.GetVisualDescendants().OfType<ScrollViewer>().First();

    private static double MaxOffset(ScrollViewer scroll) => Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);

    private static Point PointIn(Window window, CodeEditorView view, double x, double y)
    {
        var textView = view.Editor.TextArea.TextView;
        return textView.TranslatePoint(new Point(textView.Bounds.Width * x, textView.Bounds.Height * y), window)
            ?? throw new InvalidOperationException("The editor is not in the window.");
    }

    private static void FocusElsewhere(Window window)
    {
        window.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible && t.Focusable).Focus();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class ManualFrames : IFrameScheduler
    {
        private readonly List<Action> _pending = [];

        public int Pending => _pending.Count;

        public bool RequestFrame(Visual visual, Action callback)
        {
            _pending.Add(callback);
            return true;
        }

        public void Step()
        {
            var callbacks = _pending.ToArray();
            _pending.Clear();
            foreach (var callback in callbacks)
            {
                callback();
            }
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks = TimeSpan.TicksPerHour;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan time) => _ticks += time.Ticks;
    }
}
