using Avalonia;
using Avalonia.Controls;

namespace TabStudio.App.Input.Scrolling;

/// <summary>Calls back once, on the UI thread, when the next frame of a visual's window is drawn.</summary>
public interface IFrameScheduler
{
    /// <summary>Asks for a callback at the start of the next frame.</summary>
    /// <param name="visual">A visual in the window that draws the frame.</param>
    /// <param name="callback">What to do in that frame, before its layout.</param>
    /// <returns><see langword="false"/> when the visual is not in a window, so there will be no frame.</returns>
    bool RequestFrame(Visual visual, Action callback);
}

/// <summary>
/// The frames of the visual's window, from Avalonia's render loop: one callback per frame the display shows, paced by
/// the display's refresh (Android's Choreographer on a tablet), before the frame's layout and render pass.
/// </summary>
public sealed class RenderLoopFrameScheduler : IFrameScheduler
{
    /// <summary>The scheduler.</summary>
    public static RenderLoopFrameScheduler Instance { get; } = new();

    private RenderLoopFrameScheduler()
    {
    }

    /// <inheritdoc />
    public bool RequestFrame(Visual visual, Action callback)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(callback);
        if (TopLevel.GetTopLevel(visual) is not { } topLevel)
        {
            return false;
        }

        topLevel.RequestAnimationFrame(_ => callback());
        return true;
    }
}
