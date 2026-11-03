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
    private void InitializeCaretKeeping() => TextView.PropertyChanged += OnTextViewBoundsChanged;

    private void OnTextViewBoundsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != BoundsProperty || e.OldValue is not Rect before || e.NewValue is not Rect after
            || after.Height >= before.Height - 0.5 || after.Height < 1 || !_editor.TextArea.IsKeyboardFocusWithin)
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

        Dispatcher.UIThread.Post(() => caret.BringCaretToView(TextView.DefaultLineHeight), DispatcherPriority.Loaded);
    }
}
