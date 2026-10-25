using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// The PC's desktop as the bridge uses it: the windows of the programs it runs, their pictures, the mouse and the
/// keyboard, the clipboard, and whether the PC is awake. TabStudio.WindowsBridge.Windows implements it for Windows;
/// tests implement it with pictures they paint themselves.
/// </summary>
/// <remarks>Members may be called from any thread.</remarks>
public interface IBridgeDesktop : IDisposable
{
    /// <summary>The PC's operating system, for the tablet, such as <c>Windows 11 (10.0.26100)</c>.</summary>
    string OperatingSystem { get; }

    /// <summary>Whether the PC is awake, locked or about to sleep.</summary>
    BridgePcState State { get; }

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>A process and every process it started, and they started, as far as they still run.</summary>
    /// <param name="processId">The process.</param>
    IReadOnlyCollection<int> FindProcessTree(int processId);

    /// <summary>The visible top-level windows and dialogs of some processes, the oldest first.</summary>
    /// <param name="processIds">The processes.</param>
    IReadOnlyList<DesktopWindow> FindWindows(IReadOnlyCollection<int> processIds);

    /// <summary>Starts capturing a window's picture.</summary>
    /// <param name="windowId">The window.</param>
    /// <returns>The capture; it ends when disposed.</returns>
    IWindowCapture StartCapture(long windowId);

    /// <summary>Plays input into a window, as its user would: clicks, drags, the wheel, keys and typed text.</summary>
    /// <param name="windowId">The window.</param>
    /// <param name="events">The input, positions in the window's pixels from its top-left corner.</param>
    void SendInput(long windowId, IReadOnlyList<BridgeInputEvent> events);

    /// <summary>Resizes a window, keeping its top-left corner where it is.</summary>
    /// <param name="windowId">The window.</param>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    void Resize(long windowId, int width, int height);

    /// <summary>The clipboard's text, or <see langword="null"/> when it holds none.</summary>
    string? GetClipboardText();

    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    void SetClipboardText(string text);

    /// <summary>Keeps the PC from going to sleep by itself while a tablet uses it, or lets it sleep again.</summary>
    /// <param name="keepAwake">Whether to keep it awake.</param>
    void KeepAwake(bool keepAwake);
}

/// <summary>A visible top-level window or dialog on the PC.</summary>
/// <param name="Id">Its handle.</param>
/// <param name="ProcessId">The process it belongs to.</param>
/// <param name="Title">Its title.</param>
/// <param name="Width">Its width in pixels, frame included.</param>
/// <param name="Height">Its height in pixels, frame included.</param>
/// <param name="IsDialog">Whether another window owns it, as a dialog or a message box.</param>
/// <param name="Scale">Its pixels per device-independent pixel, from the DPI of its monitor.</param>
public sealed record DesktopWindow(long Id, int ProcessId, string Title, int Width, int Height, bool IsDialog, double Scale);

/// <summary>How a capture went.</summary>
public enum CaptureStatus
{
    /// <summary>The buffer holds a new picture.</summary>
    Captured,

    /// <summary>The window has not changed since the last picture; the buffer is as it was.</summary>
    Unchanged,

    /// <summary>The window cannot be seen right now, such as while it is minimized or the PC is locked.</summary>
    Unavailable,

    /// <summary>The window closed.</summary>
    Closed,
}

/// <summary>The capture of one window's picture.</summary>
public interface IWindowCapture : IDisposable
{
    /// <summary>How the window is captured, such as <c>Windows.Graphics.Capture</c> or <c>PrintWindow</c>.</summary>
    string Method { get; }

    /// <summary>Captures the window's picture as it is now.</summary>
    /// <param name="reuse">The previous picture, to capture into when the window's size has not changed.</param>
    /// <returns>Whether there is a new picture, and the picture.</returns>
    CaptureResult Capture(FrameBitmap? reuse);
}

/// <summary>What a capture gave.</summary>
/// <param name="Status">Whether there is a new picture.</param>
/// <param name="Picture">The picture, for <see cref="CaptureStatus.Captured"/>: the reused one, or a new one of the window's new size.</param>
public readonly record struct CaptureResult(CaptureStatus Status, FrameBitmap? Picture)
{
    /// <summary>The window has not changed.</summary>
    public static CaptureResult Unchanged => new(CaptureStatus.Unchanged, null);

    /// <summary>The window cannot be seen right now.</summary>
    public static CaptureResult Unavailable => new(CaptureStatus.Unavailable, null);

    /// <summary>The window closed.</summary>
    public static CaptureResult Closed => new(CaptureStatus.Closed, null);

    /// <summary>A new picture.</summary>
    /// <param name="picture">The picture.</param>
    public static CaptureResult Of(FrameBitmap picture) => new(CaptureStatus.Captured, picture);
}
