using Avalonia;
using Avalonia.Threading;

namespace TabStudio.App.Views;

/// <summary>
/// The on-screen keyboard over the caret. When it opens, Android makes the window shorter (or, drawn edge to edge, the
/// IDE pads itself by the keyboard), so the editor loses its lower part; the line being typed on was often in that
/// part, under the keyboard, and the text went in where nobody could see it. Rotating to landscape and a split screen
/// that grows shorter did the same. The caret that was in view stays in view.
/// </summary>
public sealed partial class CodeEditorView
{
    /// <summary>Starts keeping the caret in view as the editor gets shorter. Called by the constructor.</summary>
    private void InitializeCaretKeeping()
    {
        TextView.PropertyChanged += OnTextViewBoundsChanged;
        AttachedToVisualTree += (_, _) => _windowHeight = AvailableHeight();
    }

    /// <summary>
    /// The window's height above the on-screen keyboard: Android resizes the window for it, or, drawn edge to edge,
    /// keeps the window and reports the keyboard's height.
    /// </summary>
    private double AvailableHeight() => TopLevel.GetTopLevel(this) is { } top
        ? top.ClientSize.Height - (top.InputPane is { State: Avalonia.Controls.Platform.InputPaneState.Open } pane ? pane.OccludedRect.Height : 0)
        : 0;

    private double _windowHeight;

    private void OnTextViewBoundsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != BoundsProperty || e.OldValue is not Rect before || e.NewValue is not Rect after
            || after.Height >= before.Height - 0.5 || after.Height < 1 || !_editor.TextArea.IsKeyboardFocusWithin)
        {
            return;
        }

        // Only when the window itself got shorter (the keyboard, a split screen, rotation): a change of the text size
        // or a panel's height also shortens the editor, and must not scroll it and close Quick Info at the caret.
        var window = AvailableHeight();
        var shorter = window < _windowHeight - 0.5;
        _windowHeight = window;
        if (!shorter)
        {
            return;
        }

        var caret = _editor.TextArea.Caret;
        var top = TextView.GetVisualTopByDocumentLine(caret.Line) - TextView.ScrollOffset.Y;
        var bottom = top + TextView.DefaultLineHeight;

        // Only a caret the person could see: one already scrolled away stays where they left it.
        if (top < -0.5 || top > before.Height || bottom <= after.Height)
        {
            return;
        }

        // Once the lines are laid out at the new height: only a caret that is still hidden is scrolled to, since a
        // scroll also closes Quick Info and the other popups at the caret.
        Dispatcher.UIThread.Post(() =>
        {
            var view = TextView;
            var line = view.GetVisualTopByDocumentLine(caret.Line) - view.ScrollOffset.Y;
            if (line < -0.5 || line + view.DefaultLineHeight > view.Bounds.Height + 0.5)
            {
                caret.BringCaretToView(view.DefaultLineHeight);
            }
        }, DispatcherPriority.Loaded);
    }
}
