using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// One window of a running app on the desktop: its frame, drawn as Windows 11 draws it, and its client area, replayed
/// from the frame the app drew. The desktop places it at the window's bounds and routes the input itself.
/// </summary>
internal sealed class WindowView : Control
{
    private readonly DisplayListRenderer _renderer;

    /// <summary>Creates the view of a window.</summary>
    /// <param name="renderer">Draws the app's display lists.</param>
    /// <param name="frame">The window's first frame.</param>
    public WindowView(DisplayListRenderer renderer, WindowFrame frame)
    {
        _renderer = renderer;
        Frame = frame;
        IsHitTestVisible = false;
    }

    /// <summary>The window as it looks now.</summary>
    public WindowFrame Frame { get; private set; }

    /// <summary>The caption button under the pointer, or <see cref="WindowPart.None"/>.</summary>
    public WindowPart HotPart { get; private set; }

    /// <summary>How the caption button under the pointer looks.</summary>
    public ButtonState HotState { get; private set; }

    /// <summary>Whether the frame flashes its other look, as a dialog does when the window it blocks is pressed.</summary>
    public bool Flashed { get; private set; }

    /// <summary>Whether the frame looks active.</summary>
    public bool LooksActive => Frame.IsActive != Flashed;

    /// <summary>Shows a new frame of the window.</summary>
    /// <param name="frame">The frame.</param>
    public void Update(WindowFrame frame)
    {
        var moved = frame.Frame != Frame.Frame;
        Frame = frame;
        if (moved)
        {
            InvalidateArrange();
        }

        InvalidateVisual();
    }

    /// <summary>Shows a caption button under the pointer or pressed.</summary>
    /// <param name="part">The button, or <see cref="WindowPart.None"/>.</param>
    /// <param name="state">How it looks.</param>
    public void SetHot(WindowPart part, ButtonState state)
    {
        if (HotPart != part || HotState != state)
        {
            HotPart = part;
            HotState = state;
            InvalidateVisual();
        }
    }

    /// <summary>Shows the frame's other look, or its own again.</summary>
    /// <param name="flashed">Whether it shows the other look.</param>
    public void SetFlashed(bool flashed)
    {
        if (Flashed != flashed)
        {
            Flashed = flashed;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var frame = Frame;
        if (frame.IsMinimized)
        {
            return;
        }

        var active = LooksActive;
        using (context.PushTransform(Matrix.CreateTranslation(-frame.Frame.X, -frame.Frame.Y)))
        {
            ChromePainter.DrawShadow(context, frame, active);
            using (context.PushClip(ChromePainter.Outline(frame, ChromePainter.ToRect(frame.Frame))))
            {
                ChromePainter.DrawTitleBar(context, frame, active, HotPart, HotState);
                using (context.PushTransform(Matrix.CreateTranslation(frame.Client.X, frame.Client.Y)))
                {
                    using (context.PushClip(new Rect(0, 0, frame.Client.Width, frame.Client.Height)))
                    {
                        _renderer.Render(context, frame.Content);
                    }
                }
            }

            ChromePainter.DrawBorder(context, frame, active);
        }
    }
}
