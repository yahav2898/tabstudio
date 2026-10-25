using System.Runtime.CompilerServices;
using Avalonia.Controls;
using TabStudio.App.Swarm;

namespace TabStudio.App.Views.Swarm;

/// <summary>
/// Which tile content shows a virtual device's frame: the one that claimed it last, such as the full-size view over
/// its tile. When the last one lets go, the frame waits on the stage, still running.
/// </summary>
internal static class FrameClaims
{
    private static readonly ConditionalWeakTable<Control, List<ScreenTileContent>> Claims = new();

    /// <summary>Shows a frame in a content, over any that showed it before.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="content">The content.</param>
    public static void Claim(Control frame, ScreenTileContent content)
    {
        var claims = Claims.GetOrCreateValue(frame);
        claims.Remove(content);
        claims.Add(content);
        content.Show(frame);
    }

    /// <summary>A content no longer shows a frame: the content that claimed it before does, or the stage.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="content">The content.</param>
    public static void Release(Control frame, ScreenTileContent content)
    {
        if (!Claims.TryGetValue(frame, out var claims))
        {
            return;
        }

        var wasShowing = claims.Count > 0 && ReferenceEquals(claims[^1], content);
        claims.Remove(content);
        if (!wasShowing)
        {
            return;
        }

        if (claims.Count > 0)
        {
            claims[^1].Show(frame);
        }
        else if (frame is VirtualDeviceFrame { IsRetired: false } && content.Stage is { } stage)
        {
            stage.Hold(frame);
        }
        else
        {
            ScreenPlacement.Detach(frame);
        }
    }
}
