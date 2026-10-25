using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// What is drawn above every window: the open menus and drop-downs, tooltips, and the buttons of minimized windows
/// along the bottom of the desktop.
/// </summary>
internal sealed class OverlayView : Control
{
    /// <summary>The size of a minimized window's button.</summary>
    public static readonly DrawingSize MinimizedSize = new(200, 32);

    /// <summary>The space between minimized windows' buttons and around them.</summary>
    private const int MinimizedGap = 6;

    private static readonly BoxShadows PopupShadow = new(new BoxShadow { OffsetX = 1, OffsetY = 2, Blur = 6, Color = Color.FromArgb(0x40, 0, 0, 0) });

    private readonly DisplayListRenderer _renderer;
    private IReadOnlyList<WindowFrame> _windows = [];

    /// <summary>Creates the overlay.</summary>
    /// <param name="renderer">Draws the app's display lists.</param>
    public OverlayView(DisplayListRenderer renderer)
    {
        _renderer = renderer;
        IsHitTestVisible = false;
    }

    /// <summary>The minimized window whose button is under the pointer, or 0.</summary>
    public int HotMinimized { get; private set; }

    /// <summary>The part of that button under the pointer.</summary>
    public WindowPart HotPart { get; private set; }

    /// <summary>How that part looks.</summary>
    public ButtonState HotState { get; private set; }

    /// <summary>The buttons of the minimized windows, in the order they are drawn.</summary>
    public IReadOnlyList<(WindowFrame Frame, DrawingRectangle Bounds)> Minimized { get; private set; } = [];

    /// <summary>Where a minimized window's button goes: in rows from the bottom-left corner of the desktop.</summary>
    /// <param name="index">The button's position among the minimized windows.</param>
    /// <param name="desktop">The desktop's size.</param>
    public static DrawingRectangle MinimizedBounds(int index, DrawingSize desktop)
    {
        var perRow = Math.Max(1, (desktop.Width - MinimizedGap) / (MinimizedSize.Width + MinimizedGap));
        var column = index % perRow;
        var row = index / perRow;
        return new DrawingRectangle(
            MinimizedGap + (column * (MinimizedSize.Width + MinimizedGap)),
            desktop.Height - ((row + 1) * (MinimizedSize.Height + MinimizedGap)),
            MinimizedSize.Width,
            MinimizedSize.Height);
    }

    /// <summary>Shows the windows as they look now.</summary>
    /// <param name="windows">The windows, from the bottom of the z-order.</param>
    /// <param name="desktop">The desktop's size.</param>
    public void Update(IReadOnlyList<WindowFrame> windows, DrawingSize desktop)
    {
        _windows = windows;
        Minimized = [.. windows.Where(w => w.IsMinimized).OrderBy(w => w.Id).Select((w, i) => (w, MinimizedBounds(i, desktop)))];
        InvalidateVisual();
    }

    /// <summary>Shows a part of a minimized window's button under the pointer or pressed.</summary>
    /// <param name="windowId">The window, or 0.</param>
    /// <param name="part">The part.</param>
    /// <param name="state">How it looks.</param>
    public void SetHot(int windowId, WindowPart part, ButtonState state)
    {
        if (HotMinimized != windowId || HotPart != part || HotState != state)
        {
            HotMinimized = windowId;
            HotPart = part;
            HotState = state;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        foreach (var (frame, bounds) in Minimized)
        {
            ChromePainter.DrawMinimized(context, frame, bounds, frame.Id == HotMinimized ? HotPart : WindowPart.None, HotState);
        }

        foreach (var window in _windows)
        {
            if (window.IsMinimized)
            {
                continue;
            }

            using (context.PushTransform(Matrix.CreateTranslation(window.Client.X, window.Client.Y)))
            {
                foreach (var popup in window.Popups)
                {
                    DrawLifted(context, popup.Bounds, popup.Content);
                }

                if (window.ToolTip is { } tip)
                {
                    DrawLifted(context, tip.Bounds, tip.Content);
                }
            }
        }
    }

    /// <summary>Draws a popup with the shadow Windows gives menus and tooltips.</summary>
    private void DrawLifted(DrawingContext context, DrawingRectangle bounds, DisplayList content)
    {
        context.DrawRectangle(Brushes.White, null, new RoundedRect(ChromePainter.ToRect(bounds)), PopupShadow);
        using (context.PushClip(ChromePainter.ToRect(bounds)))
        {
            _renderer.Render(context, content);
        }
    }
}
