using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// The desktop's surface, in the app's pixels: windows at their bounds, the native text box over the field it edits,
/// and the overlay above them all. The desktop scales it to fit its pane; its transparent background takes the input.
/// </summary>
internal sealed class DesktopCanvas : Panel
{
    /// <summary>Creates the canvas.</summary>
    public DesktopCanvas()
    {
        Background = Brushes.Transparent;
        ClipToBounds = false;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(SizeOf(child) ?? availableSize);
        }

        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var bounds = child switch
            {
                WindowView view => ChromePainter.ToRect(view.Frame.Frame),
                NativeTextEditor editor => ChromePainter.ToRect(editor.DesktopBounds),
                _ => new Rect(finalSize),
            };
            child.Arrange(bounds);
        }

        return finalSize;
    }

    private static Size? SizeOf(Control child) => child switch
    {
        WindowView view => new Size(view.Frame.Frame.Width, view.Frame.Frame.Height),
        NativeTextEditor editor => new Size(editor.DesktopBounds.Width, editor.DesktopBounds.Height),
        _ => null,
    };
}
