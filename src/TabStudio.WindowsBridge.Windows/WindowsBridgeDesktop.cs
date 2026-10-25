using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;
using TabStudio.WindowsBridge.Windows.Capture;
using TabStudio.WindowsBridge.Windows.Desktop;
using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows;

/// <summary>
/// The Windows Bridge's desktop session on Windows: finds the top-level windows and dialogs of the programs it runs,
/// captures them with Windows.Graphics.Capture (or PrintWindow where that is not available), plays the tablet's
/// input into them, reads and writes the clipboard, and hears when the PC locks, sleeps or shuts down.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class WindowsBridgeDesktop : IBridgeDesktop
{
    private readonly DesktopEvents _events = new();
    private readonly WindowsInputPlayer _input = new();
    private readonly Lazy<bool> _graphicsCapture = new(GraphicsCapture.IsSupported);
    private int _disposed;

    /// <summary>Starts the session.</summary>
    public WindowsBridgeDesktop()
    {
        // Pixels, not the 96-DPI coordinates Windows gives programs that do not ask, as the pictures are pixels too.
        _ = NativeMethods.SetProcessDpiAwarenessContext(-4);
        _events.StateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public string OperatingSystem
    {
        get
        {
            var version = Environment.OSVersion.Version;
            var name = version.Build >= 22000 ? "Windows 11" : "Windows 10";
            return $"{name} ({version.Major}.{version.Minor}.{version.Build})";
        }
    }

    /// <inheritdoc />
    public BridgePcState State => _events.State;

    /// <inheritdoc />
    public BridgeInputPolicy InputPolicy
    {
        get => _input.Policy;
        set => _input.Policy = value;
    }

    /// <summary>How the last input was played, <c>SendInput</c> or <c>window messages</c>.</summary>
    public string? LastInputMode => _input.LastMode;

    /// <summary>Whether windows are captured with Windows.Graphics.Capture, rather than PrintWindow.</summary>
    public bool UsesGraphicsCapture => _graphicsCapture.Value;

    /// <inheritdoc />
    public IReadOnlyCollection<int> FindProcessTree(int processId) => ProcessTree.Of(processId);

    /// <inheritdoc />
    public IReadOnlyList<DesktopWindow> FindWindows(IReadOnlyCollection<int> processIds)
    {
        ArgumentNullException.ThrowIfNull(processIds);
        var search = new WindowSearch(processIds.ToHashSet());
        var handle = GCHandle.Alloc(search);
        try
        {
            _ = NativeMethods.EnumWindows(&OnWindow, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        // Windows lists the frontmost first; the tablet wants the newest, usually the frontmost, last.
        search.Found.Reverse();
        return search.Found;
    }

    /// <inheritdoc />
    public IWindowCapture StartCapture(long windowId)
    {
        var window = (nint)windowId;
        if (NativeMethods.IsIconic(window))
        {
            // A minimized window draws nothing: show it again, without taking the focus.
            _ = NativeMethods.ShowWindow(window, NativeMethods.SwShowNoActivate);
        }

        return (_graphicsCapture.Value ? GraphicsCapture.TryStart(window) : null) is { } graphicsCapture
            ? new FallbackCapture(graphicsCapture, () => new PrintWindowCapture(window))
            : new PrintWindowCapture(window);
    }

    /// <inheritdoc />
    public void SendInput(long windowId, IReadOnlyList<BridgeInputEvent> events) => _input.Play((nint)windowId, events);

    /// <inheritdoc />
    public void Resize(long windowId, int width, int height)
    {
        var window = (nint)windowId;
        var whole = WindowBounds.Whole(window);
        var visible = WindowBounds.Visible(window);

        // The size asked for is the visible frame's; the window's rectangle has invisible borders around it.
        var extraWidth = Math.Max(0, whole.Width - visible.Width);
        var extraHeight = Math.Max(0, whole.Height - visible.Height);
        _ = NativeMethods.SetWindowPos(window, 0, 0, 0, width + extraWidth, height + extraHeight, NativeMethods.SwpNoMove | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    /// <inheritdoc />
    public string? GetClipboardText() => WindowsClipboard.GetText();

    /// <inheritdoc />
    public void SetClipboardText(string text) => WindowsClipboard.SetText(text);

    /// <inheritdoc />
    public void KeepAwake(bool keepAwake) => _events.KeepAwake(keepAwake);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _events.Dispose();
    }

    [UnmanagedCallersOnly]
    private static int OnWindow(nint window, nint state)
    {
        var search = (WindowSearch)GCHandle.FromIntPtr(state).Target!;
        _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (!search.Processes.Contains((int)processId) || !NativeMethods.IsWindowVisible(window) || WindowBounds.IsCloaked(window))
        {
            return 1;
        }

        var style = (long)NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle);
        var owner = NativeMethods.GetWindow(window, NativeMethods.GwOwner);
        if ((style & NativeMethods.WsExToolWindow) != 0 && owner == 0)
        {
            // A tool window of its own, such as a hidden helper: not one the program shows.
            return 1;
        }

        var bounds = WindowBounds.Visible(window);
        if (bounds.Width < 8 || bounds.Height < 8)
        {
            return 1;
        }

        var length = NativeMethods.GetWindowTextLength(window);
        var title = string.Empty;
        if (length > 0)
        {
            var buffer = new char[length + 1];
            fixed (char* text = buffer)
            {
                var copied = NativeMethods.GetWindowText(window, text, buffer.Length);
                title = new string(text, 0, Math.Max(0, copied));
            }
        }

        var dpi = NativeMethods.GetDpiForWindow(window);
        search.Found.Add(new DesktopWindow(window, (int)processId, title, bounds.Width, bounds.Height, owner != 0, dpi == 0 ? 1 : dpi / 96.0));
        return 1;
    }

    /// <summary>The state of one search through the PC's windows.</summary>
    private sealed class WindowSearch(HashSet<int> processes)
    {
        public HashSet<int> Processes { get; } = processes;

        public List<DesktopWindow> Found { get; } = [];
    }
}
