using Avalonia.Media.Imaging;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Rasterizes the drawing an app records on a <see cref="System.Drawing.Bitmap"/> into the bitmap's surface, with
/// the renderer windows are drawn with, so a shape or text drawn on an image looks as it does on a control.
/// </summary>
internal static class BitmapRasterizer
{
    private static readonly DisplayListRenderer Renderer = new();

    /// <summary>Draws a display list on a surface, over what it holds.</summary>
    /// <param name="surface">The surface, at 96 DPI, so pixels and units are the same.</param>
    /// <param name="list">The drawing.</param>
    public static void Render(RenderTargetBitmap surface, DisplayList list)
    {
        using var context = surface.CreateDrawingContext(false);
        Renderer.Render(context, list, onImage: true);
    }
}
