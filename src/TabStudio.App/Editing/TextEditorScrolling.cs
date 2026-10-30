using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AvaloniaEdit;

namespace TabStudio.App.Editing;

/// <summary>Scrolling an AvaloniaEdit editor to a place.</summary>
internal static class TextEditorScrolling
{
    /// <summary>
    /// Scrolls an editor so that a vertical offset is at the top of its view. AvaloniaEdit 11's own
    /// <see cref="TextEditor.ScrollToVerticalOffset"/> does nothing (its body is commented out), so Peek Definition
    /// never scrolled to the definition and following a Live participant never showed their first line.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="offset">The vertical offset, in device-independent pixels; kept within the scrollable range.</param>
    /// <returns>Whether the editor has a scroll viewer to scroll.</returns>
    public static bool ScrollVerticallyTo(this TextEditor editor, double offset)
    {
        ArgumentNullException.ThrowIfNull(editor);
        editor.ApplyTemplate();
        if (editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll)
        {
            return false;
        }

        var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        scroll.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(scroll.Offset.X, Math.Clamp(offset, 0, max)));
        return true;
    }
}
