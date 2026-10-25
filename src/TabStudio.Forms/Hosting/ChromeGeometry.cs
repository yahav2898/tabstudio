using System.Drawing;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Hosting;

/// <summary>A part of a window's frame, for drawing it and for what a press on it does.</summary>
internal enum WindowPart
{
    /// <summary>Outside the window.</summary>
    None,

    /// <summary>The client area, where the form's controls are.</summary>
    Client,

    /// <summary>The title bar, which drags the window.</summary>
    Title,

    /// <summary>The minimize button.</summary>
    Minimize,

    /// <summary>The maximize or restore button.</summary>
    Maximize,

    /// <summary>The close button.</summary>
    Close,

    /// <summary>The left edge, which resizes the window.</summary>
    Left,

    /// <summary>The top edge.</summary>
    Top,

    /// <summary>The right edge.</summary>
    Right,

    /// <summary>The bottom edge.</summary>
    Bottom,

    /// <summary>The top-left corner.</summary>
    TopLeft,

    /// <summary>The top-right corner.</summary>
    TopRight,

    /// <summary>The bottom-left corner.</summary>
    BottomLeft,

    /// <summary>The bottom-right corner.</summary>
    BottomRight,
}

/// <summary>
/// Where the parts of a window's frame are, as Windows 11 draws them at 100% scale: a title bar 30 pixels high, with
/// the icon, the title, and caption buttons 46 pixels wide; tool windows have a smaller bar with only a close button.
/// Coordinates are on the desktop.
/// </summary>
internal static class ChromeGeometry
{
    /// <summary>A caption button's width.</summary>
    public const int ButtonWidth = 46;

    /// <summary>A tool window's close button's width.</summary>
    public const int ToolButtonWidth = 30;

    /// <summary>The corner radius of a window that is not maximized.</summary>
    public const double CornerRadius = 8;

    /// <summary>How far outside the visible frame a mouse can grab an edge to resize the window.</summary>
    private const int MouseGrip = 8;

    /// <summary>How far outside the visible frame a finger can grab an edge: more than a mouse, as fingers are wide.</summary>
    private const int TouchGrip = 16;

    /// <summary>How far along an edge from a corner the corner's grip reaches.</summary>
    private const int CornerGrip = 16;

    /// <summary>The caption buttons, from the right.</summary>
    public static IReadOnlyList<WindowPart> CaptionButtons { get; } = [WindowPart.Close, WindowPart.Maximize, WindowPart.Minimize];

    /// <summary>The title bar, between the top border and the client area.</summary>
    /// <param name="frame">The window.</param>
    public static Rectangle TitleBar(WindowFrame frame)
    {
        if (!frame.Chrome.HasFrame)
        {
            return Rectangle.Empty;
        }

        var top = frame.IsMaximized ? frame.Frame.Top : frame.Frame.Top + 1;
        return Rectangle.FromLTRB(frame.Frame.Left + 1, top, frame.Frame.Right - 1, frame.Client.Top);
    }

    /// <summary>A caption button's bounds, or an empty rectangle when the window does not have it.</summary>
    /// <param name="frame">The window.</param>
    /// <param name="part">The button: <see cref="WindowPart.Minimize"/>, <see cref="WindowPart.Maximize"/> or <see cref="WindowPart.Close"/>.</param>
    public static Rectangle Button(WindowFrame frame, WindowPart part)
    {
        var title = TitleBar(frame);
        if (title.IsEmpty || !frame.Chrome.ControlBox)
        {
            return Rectangle.Empty;
        }

        var width = frame.Chrome.IsToolWindow ? ToolButtonWidth : ButtonWidth;
        var index = part switch
        {
            WindowPart.Close => 0,
            WindowPart.Maximize when frame.Chrome.HasSizeButtons => 1,
            WindowPart.Minimize when frame.Chrome.HasSizeButtons => 2,
            _ => -1,
        };

        return index < 0 ? Rectangle.Empty : new Rectangle(title.Right - ((index + 1) * width), title.Top, width, title.Height);
    }

    /// <summary>Whether a caption button can be pressed: a form can show a disabled minimize or maximize button.</summary>
    /// <param name="frame">The window.</param>
    /// <param name="part">The button.</param>
    public static bool IsEnabled(WindowFrame frame, WindowPart part) => part switch
    {
        WindowPart.Minimize => frame.Chrome.MinimizeBox,
        WindowPart.Maximize => frame.Chrome.MaximizeBox,
        _ => true,
    };

    /// <summary>The icon's bounds, or an empty rectangle when the title bar has none.</summary>
    /// <param name="frame">The window.</param>
    public static Rectangle Icon(WindowFrame frame)
    {
        var title = TitleBar(frame);
        return title.IsEmpty || !frame.Chrome.HasIcon
            ? Rectangle.Empty
            : new Rectangle(title.Left + 8, title.Top + ((title.Height - 16) / 2), 16, 16);
    }

    /// <summary>Where the title's text goes: after the icon, before the buttons.</summary>
    /// <param name="frame">The window.</param>
    public static Rectangle TitleText(WindowFrame frame)
    {
        var title = TitleBar(frame);
        if (title.IsEmpty)
        {
            return Rectangle.Empty;
        }

        var icon = Icon(frame);
        var left = icon.IsEmpty ? title.Left + (frame.Chrome.IsToolWindow ? 6 : 10) : icon.Right + 8;
        var right = title.Right;
        foreach (var part in CaptionButtons)
        {
            var button = Button(frame, part);
            if (!button.IsEmpty)
            {
                right = Math.Min(right, button.Left);
            }
        }

        return Rectangle.FromLTRB(left, title.Top, Math.Max(left, right - 8), title.Bottom);
    }

    /// <summary>The part of a window at a point on the desktop.</summary>
    /// <param name="frame">The window.</param>
    /// <param name="point">The point.</param>
    /// <param name="touch">Whether a finger asks, which grabs the edges from further away.</param>
    public static WindowPart HitTest(WindowFrame frame, Point point, bool touch)
    {
        var bounds = frame.Frame;
        if (frame.Chrome.IsSizable && !frame.IsMaximized && ResizeEdge(bounds, point, touch ? TouchGrip : MouseGrip) is var edge and not WindowPart.None)
        {
            return edge;
        }

        if (!bounds.Contains(point))
        {
            return WindowPart.None;
        }

        if (frame.Client.Contains(point))
        {
            return WindowPart.Client;
        }

        foreach (var part in CaptionButtons)
        {
            if (Button(frame, part).Contains(point))
            {
                return part;
            }
        }

        return TitleBar(frame).Contains(point) ? WindowPart.Title : WindowPart.None;
    }

    /// <summary>Whether a part resizes the window, and which edges it moves.</summary>
    /// <param name="part">The part.</param>
    public static (bool Left, bool Top, bool Right, bool Bottom) Edges(WindowPart part) => part switch
    {
        WindowPart.Left => (true, false, false, false),
        WindowPart.Top => (false, true, false, false),
        WindowPart.Right => (false, false, true, false),
        WindowPart.Bottom => (false, false, false, true),
        WindowPart.TopLeft => (true, true, false, false),
        WindowPart.TopRight => (false, true, true, false),
        WindowPart.BottomLeft => (true, false, false, true),
        WindowPart.BottomRight => (false, false, true, true),
        _ => (false, false, false, false),
    };

    /// <summary>
    /// The edge or corner of a sizable window at a point: a band outside the visible frame, and a couple of pixels
    /// inside it, except at the top, where the title bar is.
    /// </summary>
    private static WindowPart ResizeEdge(Rectangle bounds, Point point, int grip)
    {
        var outer = Rectangle.Inflate(bounds, grip, grip);
        if (!outer.Contains(point))
        {
            return WindowPart.None;
        }

        var inner = Rectangle.FromLTRB(bounds.Left + 2, bounds.Top + 1, bounds.Right - 2, bounds.Bottom - 2);
        if (inner.Contains(point))
        {
            return WindowPart.None;
        }

        var left = point.X < inner.Left;
        var right = point.X >= inner.Right;
        var top = point.Y < inner.Top;
        var bottom = point.Y >= inner.Bottom;
        var nearLeft = point.X < bounds.Left + CornerGrip;
        var nearRight = point.X >= bounds.Right - CornerGrip;
        var nearTop = point.Y < bounds.Top + CornerGrip;
        var nearBottom = point.Y >= bounds.Bottom - CornerGrip;
        return (left || (nearLeft && (top || bottom)), right || (nearRight && (top || bottom)), top || (nearTop && (left || right)), bottom || (nearBottom && (left || right))) switch
        {
            (true, _, true, _) => WindowPart.TopLeft,
            (_, true, true, _) => WindowPart.TopRight,
            (true, _, _, true) => WindowPart.BottomLeft,
            (_, true, _, true) => WindowPart.BottomRight,
            (true, _, _, _) => WindowPart.Left,
            (_, true, _, _) => WindowPart.Right,
            (_, _, true, _) => WindowPart.Top,
            (_, _, _, true) => WindowPart.Bottom,
            _ => WindowPart.None,
        };
    }
}
