using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>
/// A desktop the tests paint themselves: windows are pictures with a button, which turns green when it is clicked,
/// as a program's would; input, the clipboard and the PC's state are recorded.
/// </summary>
internal sealed class FakeDesktop : IBridgeDesktop
{
    private readonly Lock _gate = new();
    private readonly List<FakeWindow> _windows = [];
    private readonly List<(long Window, BridgeInputEvent Event)> _input = [];
    private BridgePcState _state = BridgePcState.Awake;
    private string? _clipboard;
    private long _nextWindow = 0x1000;

    public event EventHandler? StateChanged;

    public string OperatingSystem => "Windows 11 (10.0.26100)";

    public BridgePcState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool IsKeptAwake { get; private set; }

    /// <summary>Every input played so far, in order.</summary>
    public IReadOnlyList<(long Window, BridgeInputEvent Event)> Input
    {
        get
        {
            lock (_gate)
            {
                return [.. _input];
            }
        }
    }

    /// <summary>The clipboard's text.</summary>
    public string? Clipboard
    {
        get
        {
            lock (_gate)
            {
                return _clipboard;
            }
        }

        set
        {
            lock (_gate)
            {
                _clipboard = value;
            }
        }
    }

    /// <summary>Opens a window for a process.</summary>
    public FakeWindow Show(int processId, string title, int width, int height, bool isDialog = false)
    {
        lock (_gate)
        {
            var window = new FakeWindow(++_nextWindow, processId, title, width, height, isDialog);
            _windows.Add(window);
            return window;
        }
    }

    /// <summary>Changes the PC's state, as sleep or the lock screen does.</summary>
    public void SetState(BridgePcState state)
    {
        lock (_gate)
        {
            _state = state;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyCollection<int> FindProcessTree(int processId) => [processId];

    public IReadOnlyList<DesktopWindow> FindWindows(IReadOnlyCollection<int> processIds)
    {
        lock (_gate)
        {
            return [.. _windows.Where(w => !w.IsClosed && processIds.Contains(w.ProcessId)).Select(w => w.Describe())];
        }
    }

    public IWindowCapture StartCapture(long windowId) => new FakeCapture(Find(windowId));

    public void SendInput(long windowId, IReadOnlyList<BridgeInputEvent> events)
    {
        var window = Find(windowId);
        lock (_gate)
        {
            _input.AddRange(events.Select(e => (windowId, e)));
        }

        foreach (var e in events)
        {
            window.Play(e);
        }
    }

    public void Resize(long windowId, int width, int height) => Find(windowId).Resize(width, height);

    public string? GetClipboardText() => Clipboard;

    public void SetClipboardText(string text) => Clipboard = text;

    public void KeepAwake(bool keepAwake) => IsKeptAwake = keepAwake;

    public void Dispose()
    {
    }

    private FakeWindow Find(long windowId)
    {
        lock (_gate)
        {
            return _windows.Single(w => w.Id == windowId);
        }
    }

    private sealed class FakeCapture(FakeWindow window) : IWindowCapture
    {
        private int _version = -1;

        public string Method => "Fake capture";

        public CaptureResult Capture(FrameBitmap? reuse)
        {
            if (window.IsClosed)
            {
                return CaptureResult.Closed;
            }

            var (picture, version) = window.Snapshot(reuse);
            if (version == _version)
            {
                return CaptureResult.Unchanged;
            }

            _version = version;
            return CaptureResult.Of(picture);
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>A window of the fake desktop: a light picture with a gray button that turns green when clicked.</summary>
internal sealed class FakeWindow
{
    /// <summary>The button's color before a click.</summary>
    public const uint Gray = 0xFFC0C0C0;

    /// <summary>The button's color after a click.</summary>
    public const uint Green = 0xFF20A040;

    private readonly Lock _gate = new();
    private FrameBitmap _picture;
    private int _version;
    private bool _pressed;

    public FakeWindow(long id, int processId, string title, int width, int height, bool isDialog)
    {
        Id = id;
        ProcessId = processId;
        Title = title;
        IsDialog = isDialog;
        _picture = Paint(width, height, Gray);
    }

    public long Id { get; }

    public int ProcessId { get; }

    public string Title { get; }

    public bool IsDialog { get; }

    public bool IsClosed { get; private set; }

    /// <summary>How many times the button was clicked.</summary>
    public int Clicks { get; private set; }

    /// <summary>Where the button is: the middle third of the window.</summary>
    public PixelRect Button
    {
        get
        {
            lock (_gate)
            {
                return ButtonOf(_picture.Width, _picture.Height);
            }
        }
    }

    public DesktopWindow Describe()
    {
        lock (_gate)
        {
            return new DesktopWindow(Id, ProcessId, Title, _picture.Width, _picture.Height, IsDialog, 1);
        }
    }

    public void Close() => IsClosed = true;

    public (FrameBitmap Picture, int Version) Snapshot(FrameBitmap? reuse)
    {
        lock (_gate)
        {
            var copy = reuse is { } existing && existing.Width == _picture.Width && existing.Height == _picture.Height ? existing : new FrameBitmap(_picture.Width, _picture.Height);
            _picture.Pixels.CopyTo(copy.Pixels, 0);
            return (copy, _version);
        }
    }

    public void Play(BridgeInputEvent e)
    {
        lock (_gate)
        {
            var inside = ButtonOf(_picture.Width, _picture.Height).Contains(e.X, e.Y);
            switch (e.Kind)
            {
                case BridgeInputKind.PointerDown when e.Button == BridgePointerButton.Left:
                    _pressed = inside;
                    break;
                case BridgeInputKind.PointerUp when e.Button == BridgePointerButton.Left && _pressed && inside:
                    _pressed = false;
                    Clicks++;
                    _picture.Fill(ButtonOf(_picture.Width, _picture.Height), Green);
                    _version++;
                    break;
            }
        }
    }

    public void Resize(int width, int height)
    {
        lock (_gate)
        {
            _picture = Paint(width, height, Clicks > 0 ? Green : Gray);
            _version++;
        }
    }

    private static PixelRect ButtonOf(int width, int height) => new(width / 3, height / 3, width / 3, height / 3);

    private static FrameBitmap Paint(int width, int height, uint button)
    {
        var picture = Pictures.Window(width, height);
        picture.Fill(ButtonOf(width, height), button);
        return picture;
    }
}
