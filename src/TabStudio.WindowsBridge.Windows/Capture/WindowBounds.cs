using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Capture;

/// <summary>
/// Where a window is on the screen as people see it: Windows 10 and 11 give windows invisible resize borders around
/// their visible frame, and the bridge's pictures and input positions start at the visible frame's corner.
/// </summary>
internal static unsafe class WindowBounds
{
    /// <summary>The window's visible frame on the screen, in pixels; its whole rectangle where Windows does not say.</summary>
    /// <param name="window">The window.</param>
    public static NativeMethods.Rect Visible(nint window)
    {
        NativeMethods.Rect bounds;
        if (NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DwmwaExtendedFrameBounds, &bounds, (uint)sizeof(NativeMethods.Rect)) >= 0 && bounds.Width > 0)
        {
            return bounds;
        }

        _ = NativeMethods.GetWindowRect(window, out bounds);
        return bounds;
    }

    /// <summary>The window's whole rectangle, invisible borders included.</summary>
    /// <param name="window">The window.</param>
    public static NativeMethods.Rect Whole(nint window)
    {
        _ = NativeMethods.GetWindowRect(window, out var bounds);
        return bounds;
    }

    /// <summary>Whether Windows hides the window from view without it being minimized, as it does for suspended apps.</summary>
    /// <param name="window">The window.</param>
    public static bool IsCloaked(nint window)
    {
        int cloaked;
        return NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DwmwaCloaked, &cloaked, sizeof(int)) >= 0 && cloaked != 0;
    }
}
