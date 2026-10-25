using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;
using DrawingRectangle = System.Drawing.Rectangle;

namespace TabStudio.Forms.Hosting;

/// <summary>How a caption button looks now.</summary>
internal enum ButtonState
{
    /// <summary>At rest.</summary>
    Normal,

    /// <summary>The pointer is over it.</summary>
    Hot,

    /// <summary>It is pressed.</summary>
    Pressed,
}

/// <summary>
/// Draws window frames as Windows 11 does: a white title bar with the app's icon, its title and the caption buttons,
/// a thin border, rounded corners and a soft shadow; the close button turns red under the pointer.
/// </summary>
internal static class ChromePainter
{
    private static readonly IBrush TitleBackground = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly IBrush ActiveText = new ImmutableSolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
    private static readonly IBrush InactiveText = new ImmutableSolidColorBrush(Color.FromRgb(0x8C, 0x8C, 0x8C));
    private static readonly IBrush DisabledGlyph = new ImmutableSolidColorBrush(Color.FromRgb(0xC4, 0xC4, 0xC4));
    private static readonly IBrush HotButton = new ImmutableSolidColorBrush(Color.FromArgb(0x1A, 0, 0, 0));
    private static readonly IBrush PressedButton = new ImmutableSolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));
    private static readonly IBrush HotClose = new ImmutableSolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly IBrush PressedClose = new ImmutableSolidColorBrush(Color.FromRgb(0xC9, 0x41, 0x33));
    private static readonly IBrush White = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IPen ActiveBorder = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x5C, 0, 0, 0)), 1);
    private static readonly IPen InactiveBorder = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)), 1);
    private static readonly IBrush IconFrame = new ImmutableSolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0x7E));
    private static readonly IBrush IconTitle = new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));

    private static readonly BoxShadows ActiveShadow = new(
        new BoxShadow { OffsetY = 8, Blur = 26, Color = Color.FromArgb(0x47, 0, 0, 0) },
        [new BoxShadow { OffsetY = 1, Blur = 3, Color = Color.FromArgb(0x24, 0, 0, 0) }]);

    private static readonly BoxShadows InactiveShadow = new(
        new BoxShadow { OffsetY = 3, Blur = 12, Color = Color.FromArgb(0x2E, 0, 0, 0) });

    private static FontSpec? _captionFont;

    /// <summary>The font of window titles: Segoe UI at 9 points, as the device draws it.</summary>
    public static FontSpec CaptionFont => _captionFont ??= System.Drawing.SystemFonts.CaptionFont.Spec;

    /// <summary>The outline of a window: rounded unless it is maximized or has no frame.</summary>
    /// <param name="frame">The window.</param>
    /// <param name="bounds">Its bounds in the coordinates drawn in.</param>
    public static RoundedRect Outline(WindowFrame frame, Rect bounds) =>
        new(bounds, frame.IsMaximized || !frame.Chrome.HasFrame ? 0 : ChromeGeometry.CornerRadius);

    /// <summary>Draws a window's shadow, below everything else.</summary>
    /// <param name="context">The drawing context, with the desktop's coordinates.</param>
    /// <param name="frame">The window.</param>
    /// <param name="active">Whether it looks active.</param>
    public static void DrawShadow(DrawingContext context, WindowFrame frame, bool active)
    {
        if (frame.IsMaximized)
        {
            return;
        }

        context.DrawRectangle(TitleBackground, null, Outline(frame, ToRect(frame.Frame)), active ? ActiveShadow : InactiveShadow);
    }

    /// <summary>Draws the title bar: the icon, the title and the caption buttons.</summary>
    /// <param name="context">The drawing context, with the desktop's coordinates.</param>
    /// <param name="frame">The window.</param>
    /// <param name="active">Whether it looks active.</param>
    /// <param name="hot">The caption button under the pointer, or <see cref="WindowPart.None"/>.</param>
    /// <param name="state">How that button looks.</param>
    public static void DrawTitleBar(DrawingContext context, WindowFrame frame, bool active, WindowPart hot, ButtonState state)
    {
        var title = ChromeGeometry.TitleBar(frame);
        if (title.IsEmpty)
        {
            return;
        }

        context.DrawRectangle(TitleBackground, null, ToRect(title));
        var icon = ChromeGeometry.Icon(frame);
        if (!icon.IsEmpty)
        {
            DrawAppIcon(context, ToRect(icon));
        }

        var text = ChromeGeometry.TitleText(frame);
        if (text.Width > 0 && frame.Title.Length > 0)
        {
            using var layout = TextMetrics.CreateLayout(frame.Title, CaptionFont, active ? ActiveText : InactiveText, text.Width, double.PositiveInfinity, TextAlignment.Left, TextWrapping.NoWrap, trimming: true);
            layout.Draw(context, new Point(text.X, text.Y + Math.Round((text.Height - layout.Height) / 2)));
        }

        foreach (var part in ChromeGeometry.CaptionButtons)
        {
            var bounds = ChromeGeometry.Button(frame, part);
            if (!bounds.IsEmpty)
            {
                DrawButton(context, frame, part, ToRect(bounds), active, part == hot ? state : ButtonState.Normal);
            }
        }
    }

    /// <summary>Draws the thin border around a window, above its content.</summary>
    /// <param name="context">The drawing context, with the desktop's coordinates.</param>
    /// <param name="frame">The window.</param>
    /// <param name="active">Whether it looks active.</param>
    public static void DrawBorder(DrawingContext context, WindowFrame frame, bool active)
    {
        if (!frame.Chrome.HasFrame || frame.IsMaximized)
        {
            return;
        }

        var outline = Outline(frame, ToRect(frame.Frame).Deflate(0.5));
        context.DrawRectangle(null, active ? ActiveBorder : InactiveBorder, outline);
    }

    /// <summary>The generic app icon Windows gives a form without an icon of its own: a small window.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="bounds">The icon's 16-pixel square.</param>
    public static void DrawAppIcon(DrawingContext context, Rect bounds)
    {
        var body = new Rect(bounds.X + 1, bounds.Y + 2, 14, 12);
        context.DrawRectangle(IconFrame, null, new RoundedRect(body, 1.5));
        context.DrawRectangle(White, null, new Rect(body.X + 1, body.Y + 4, body.Width - 2, body.Height - 5));
        context.DrawRectangle(IconTitle, null, new Rect(body.X + 1, body.Y + 1, body.Width - 2, 3));
    }

    /// <summary>Draws a caption button and its glyph.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The window.</param>
    /// <param name="part">The button.</param>
    /// <param name="bounds">Its bounds.</param>
    /// <param name="active">Whether the window looks active.</param>
    /// <param name="state">How the button looks.</param>
    public static void DrawButton(DrawingContext context, WindowFrame frame, WindowPart part, Rect bounds, bool active, ButtonState state)
    {
        var enabled = ChromeGeometry.IsEnabled(frame, part);
        if (enabled && state != ButtonState.Normal)
        {
            var fill = part == WindowPart.Close
                ? state == ButtonState.Pressed ? PressedClose : HotClose
                : state == ButtonState.Pressed ? PressedButton : HotButton;
            context.DrawRectangle(fill, null, bounds);
        }

        var glyph = !enabled ? DisabledGlyph
            : part == WindowPart.Close && state != ButtonState.Normal ? White
            : active ? ActiveText : InactiveText;
        var pen = new ImmutablePen(glyph.ToImmutable(), 1);
        var size = frame.Chrome.IsToolWindow ? 8 : 10;
        var x = Math.Round(bounds.X + ((bounds.Width - size) / 2)) + 0.5;
        var y = Math.Round(bounds.Y + ((bounds.Height - size) / 2)) + 0.5;
        using (context.PushRenderOptions(new RenderOptions { EdgeMode = EdgeMode.Antialias }))
        {
            switch (part)
            {
                case WindowPart.Minimize:
                    context.DrawLine(pen, new Point(x, y + (size / 2)), new Point(x + size, y + (size / 2)));
                    break;
                case WindowPart.Maximize when frame.IsMaximized:
                    // Restore: a window in front of another.
                    context.DrawRectangle(null, pen, new RoundedRect(new Rect(x, y + 2, size - 2, size - 2), 1));
                    context.DrawGeometry(null, pen, new PolylineGeometry([new Point(x + 2, y), new Point(x + size, y), new Point(x + size, y + size - 2)], false));
                    break;
                case WindowPart.Maximize:
                    context.DrawRectangle(null, pen, new RoundedRect(new Rect(x, y, size, size), 1.5));
                    break;
                case WindowPart.Close:
                    context.DrawLine(pen, new Point(x, y), new Point(x + size, y + size));
                    context.DrawLine(pen, new Point(x + size, y), new Point(x, y + size));
                    break;
            }
        }
    }

    /// <summary>Draws a minimized window's button at the bottom of the desktop: its icon, title, restore and close.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The window.</param>
    /// <param name="bounds">The button's bounds.</param>
    /// <param name="hot">The part under the pointer: <see cref="WindowPart.Title"/>, <see cref="WindowPart.Maximize"/> or <see cref="WindowPart.Close"/>.</param>
    /// <param name="state">How that part looks.</param>
    public static void DrawMinimized(DrawingContext context, WindowFrame frame, DrawingRectangle bounds, WindowPart hot, ButtonState state)
    {
        var rect = ToRect(bounds);
        context.DrawRectangle(TitleBackground, null, new RoundedRect(rect, 6), InactiveShadow);
        if (hot == WindowPart.Title && state != ButtonState.Normal)
        {
            context.DrawRectangle(state == ButtonState.Pressed ? PressedButton : HotButton, null, new RoundedRect(rect, 6));
        }

        context.DrawRectangle(null, InactiveBorder, new RoundedRect(rect.Deflate(0.5), 6));
        DrawAppIcon(context, new Rect(rect.X + 8, rect.Y + ((rect.Height - 16) / 2), 16, 16));
        var restore = MinimizedButton(bounds, WindowPart.Maximize);
        var close = MinimizedButton(bounds, WindowPart.Close);
        var textWidth = restore.Left - rect.X - 32 - 4;
        if (textWidth > 0 && frame.Title.Length > 0)
        {
            using var layout = TextMetrics.CreateLayout(frame.Title, CaptionFont, ActiveText, textWidth, double.PositiveInfinity, TextAlignment.Left, TextWrapping.NoWrap, trimming: true);
            layout.Draw(context, new Point(rect.X + 32, rect.Y + Math.Round((rect.Height - layout.Height) / 2)));
        }

        // The restore button has the restore glyph, two windows, as a minimized window's button does in Windows.
        var restoreFrame = frame with { IsMaximized = true, Chrome = frame.Chrome with { MaximizeBox = true } };
        using (context.PushClip(new RoundedRect(rect, 6)))
        {
            DrawButton(context, restoreFrame, WindowPart.Maximize, ToRect(restore), true, hot == WindowPart.Maximize ? state : ButtonState.Normal);
            if (frame.Chrome.ControlBox)
            {
                DrawButton(context, frame, WindowPart.Close, ToRect(close), true, hot == WindowPart.Close ? state : ButtonState.Normal);
            }
        }
    }

    /// <summary>A button of a minimized window's button: <see cref="WindowPart.Maximize"/> restores it, <see cref="WindowPart.Close"/> closes it.</summary>
    /// <param name="bounds">The minimized window's button.</param>
    /// <param name="part">The part.</param>
    public static DrawingRectangle MinimizedButton(DrawingRectangle bounds, WindowPart part)
    {
        const int Width = 34;
        return part == WindowPart.Close
            ? new DrawingRectangle(bounds.Right - Width, bounds.Top, Width, bounds.Height)
            : new DrawingRectangle(bounds.Right - (2 * Width), bounds.Top, Width, bounds.Height);
    }

    /// <summary>A desktop rectangle as an Avalonia one.</summary>
    /// <param name="rectangle">The rectangle.</param>
    public static Rect ToRect(DrawingRectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
