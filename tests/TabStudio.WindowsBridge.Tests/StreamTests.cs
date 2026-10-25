using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;
using Xunit;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>The bridge's stream of one window, without a connection: what it captures, sends and sends again.</summary>
public sealed class StreamTests
{
    [Fact]
    public async Task A_change_to_the_window_is_sent_in_the_next_frame()
    {
        var desktop = new FakeDesktop();
        var window = desktop.Show(1, "Main window", 320, 240);
        var tablet = new FakeTablet();
        using var stream = new BridgeStream(1, 1, window.Id, 1920, 1200, desktop, tablet.ReceiveAsync);
        tablet.Stream = stream;
        string State() => $"{tablet}; {stream.Describe()}; {desktop.Captures}";
        await Waits.UntilAsync(() => tablet.Frames > 0, "the first frame", State);

        var button = window.Button;
        var (x, y) = (button.X + (button.Width / 2), button.Y + (button.Height / 2));
        desktop.SendInput(window.Id, [
            BridgeInputEvent.Mouse(BridgeInputKind.PointerDown, x, y, BridgePointerButton.Left),
            BridgeInputEvent.Mouse(BridgeInputKind.PointerUp, x, y, BridgePointerButton.Left)]);
        stream.Wake();

        await Waits.UntilAsync(() => tablet.ColorAt(x, y) == FakeWindow.Green, "the clicked button", State);
        Assert.Equal(1, window.Clicks);
    }

    [Fact]
    public async Task A_resized_window_is_sent_whole_at_its_new_size()
    {
        var desktop = new FakeDesktop();
        var window = desktop.Show(1, "Main window", 320, 240);
        var tablet = new FakeTablet();
        using var stream = new BridgeStream(1, 1, window.Id, 1920, 1200, desktop, tablet.ReceiveAsync);
        tablet.Stream = stream;
        string State() => $"{tablet}; {stream.Describe()}; {desktop.Captures}";
        await Waits.UntilAsync(() => tablet.Frames > 0, "the first frame", State);

        window.Resize(500, 300);
        stream.Wake();

        await Waits.UntilAsync(() => tablet.Size == (500, 300), "the resized window", State);
    }

    [Fact]
    public async Task A_window_that_closes_ends_its_stream()
    {
        var desktop = new FakeDesktop();
        var window = desktop.Show(1, "Main window", 320, 240);
        var tablet = new FakeTablet();
        using var stream = new BridgeStream(1, 1, window.Id, 1920, 1200, desktop, tablet.ReceiveAsync);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stream.Ended += (_, _) => ended.TrySetResult();
        tablet.Stream = stream;
        await Waits.UntilAsync(() => tablet.Frames > 0, "the first frame");

        window.Close();
        stream.Wake();

        await ended.Task.WaitAsync(Waits.Patience);
        Assert.Null(stream.Failure);
    }

    /// <summary>The tablet's end: decodes each frame and says it was drawn, as the Bridge pane does.</summary>
    private sealed class FakeTablet
    {
        private readonly FrameDecoder _decoder = new();
        private readonly Lock _gate = new();
        private int _frames;

        public BridgeStream? Stream { get; set; }

        public int Frames => Volatile.Read(ref _frames);

        public (int Width, int Height) Size
        {
            get
            {
                lock (_gate)
                {
                    return _decoder.Frame is { } frame ? (frame.Width, frame.Height) : (0, 0);
                }
            }
        }

        public uint ColorAt(int x, int y)
        {
            lock (_gate)
            {
                return _decoder.Frame is { } frame && x < frame.Width && y < frame.Height ? frame.GetPixel(x, y) : 0u;
            }
        }

        public Task ReceiveAsync(FrameHeader header, byte[] data)
        {
            lock (_gate)
            {
                _decoder.Apply(data);
            }

            Interlocked.Increment(ref _frames);
            _ = Task.Run(() => Stream?.OnShown(header.Sequence));
            return Task.CompletedTask;
        }

        public override string ToString() => $"tablet decoded {Frames} frames, {Size.Width}x{Size.Height}";
    }
}
