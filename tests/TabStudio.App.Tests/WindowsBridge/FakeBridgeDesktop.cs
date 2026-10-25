using TabStudio.App.Services.WindowsBridge;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.Tests.WindowsBridge;

/// <summary>
/// A Windows desktop the app tests paint themselves, for a PC that is a Windows Bridge without being Windows: a
/// program's windows are pictures, a click shows the window's clicked picture, and the input it gets is recorded.
/// </summary>
internal sealed class FakeBridgeDesktop : IBridgeDesktop, IBridgeDesktopFactory
{
    private readonly Lock _gate = new();
    private readonly List<FakeBridgeWindow> _windows = [];
    private readonly List<(long Window, BridgeInputEvent Event)> _input = [];
    private BridgePcState _state = BridgePcState.Awake;
    private string? _clipboard;
    private long _nextWindow = 0x2000;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public string OperatingSystem => "Windows 11 Pro (10.0.26100)";

    /// <inheritdoc />
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

    /// <inheritdoc />
    public BridgeInputPolicy InputPolicy { get; set; }

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

    /// <inheritdoc />
    public IBridgeDesktop Create() => this;

    /// <summary>Opens a window for a process.</summary>
    /// <param name="processId">The process.</param>
    /// <param name="title">The window's title.</param>
    /// <param name="picture">What the window shows.</param>
    /// <param name="clicked">What it shows once clicked, if it changes.</param>
    public FakeBridgeWindow Show(int processId, string title, FrameBitmap picture, FrameBitmap? clicked = null)
    {
        lock (_gate)
        {
            var window = new FakeBridgeWindow(++_nextWindow, processId, title, picture, clicked);
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

    /// <inheritdoc />
    public IReadOnlyCollection<int> FindProcessTree(int processId) => [processId];

    /// <inheritdoc />
    public IReadOnlyList<DesktopWindow> FindWindows(IReadOnlyCollection<int> processIds)
    {
        lock (_gate)
        {
            return [.. _windows.Where(w => !w.IsClosed && processIds.Contains(w.ProcessId)).Select(w => w.Describe())];
        }
    }

    /// <inheritdoc />
    public IWindowCapture StartCapture(long windowId) => new FakeCapture(Find(windowId));

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Resize(long windowId, int width, int height)
    {
        // The pictures keep their size: the tests do not resize.
    }

    /// <inheritdoc />
    public string? GetClipboardText()
    {
        lock (_gate)
        {
            return _clipboard;
        }
    }

    /// <inheritdoc />
    public void SetClipboardText(string text)
    {
        lock (_gate)
        {
            _clipboard = text;
        }
    }

    /// <inheritdoc />
    public void KeepAwake(bool keepAwake)
    {
    }

    /// <summary>Nothing to release: the tests keep the desktop for the next bridge.</summary>
    public void Dispose()
    {
    }

    private FakeBridgeWindow Find(long windowId)
    {
        lock (_gate)
        {
            return _windows.Single(w => w.Id == windowId);
        }
    }

    private sealed class FakeCapture(FakeBridgeWindow window) : IWindowCapture
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

/// <summary>A window of the fake desktop: a picture, and another one after a click anywhere in it.</summary>
internal sealed class FakeBridgeWindow
{
    private readonly Lock _gate = new();
    private readonly FrameBitmap? _clicked;
    private FrameBitmap _picture;
    private int _version;

    public FakeBridgeWindow(long id, int processId, string title, FrameBitmap picture, FrameBitmap? clicked)
    {
        Id = id;
        ProcessId = processId;
        Title = title;
        _picture = picture;
        _clicked = clicked;
    }

    public long Id { get; }

    public int ProcessId { get; }

    public string Title { get; }

    public bool IsClosed { get; private set; }

    /// <summary>How many times the window was clicked.</summary>
    public int Clicks { get; private set; }

    public DesktopWindow Describe()
    {
        lock (_gate)
        {
            return new DesktopWindow(Id, ProcessId, Title, _picture.Width, _picture.Height, false, 1);
        }
    }

    public void Close() => IsClosed = true;

    public (FrameBitmap Picture, int Version) Snapshot(FrameBitmap? reuse)
    {
        lock (_gate)
        {
            var copy = reuse is { } existing && existing.Width == _picture.Width && existing.Height == _picture.Height ? existing : new FrameBitmap(_picture.Width, _picture.Height);
            copy.CopyFrom(_picture, _picture.Bounds);
            return (copy, _version);
        }
    }

    public void Play(BridgeInputEvent e)
    {
        lock (_gate)
        {
            if (e is { Kind: BridgeInputKind.PointerUp, Button: BridgePointerButton.Left })
            {
                Clicks++;
                if (_clicked is not null && !ReferenceEquals(_picture, _clicked))
                {
                    _picture = _clicked;
                    _version++;
                }
            }
        }
    }
}
