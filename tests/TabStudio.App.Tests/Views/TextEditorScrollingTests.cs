using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using TabStudio.App.Editing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// An editor scrolls to an offset: AvaloniaEdit's own ScrollToVerticalOffset does nothing, so Peek Definition stayed at
/// the top of the definition's file and following a Live participant never showed their first line.
/// </summary>
public sealed class TextEditorScrollingTests
{
    [AvaloniaFact]
    public void An_editor_scrolls_to_an_offset_kept_within_its_text()
    {
        var editor = new TextEditor { Document = new TextDocument(string.Join('\n', Enumerable.Range(1, 200).Select(i => $"line {i}"))) };
        var window = new Window { Width = 400, Height = 300, Content = editor };
        window.Show();
        try
        {
            window.UpdateLayout();
            var lineHeight = editor.TextArea.TextView.DefaultLineHeight;
            Assert.True(editor.ViewportHeight > 0 && editor.ExtentHeight > editor.ViewportHeight, $"Viewport {editor.ViewportHeight}, extent {editor.ExtentHeight}.");

            Assert.True(editor.ScrollVerticallyTo(40 * lineHeight));
            window.UpdateLayout();
            Assert.Equal(40 * lineHeight, editor.VerticalOffset, 1);

            // Past the end, it stops at the last page; before the start, at the top.
            Assert.True(editor.ScrollVerticallyTo(1_000_000));
            window.UpdateLayout();
            Assert.Equal(editor.ExtentHeight - editor.ViewportHeight, editor.VerticalOffset, 1);
            Assert.True(editor.ScrollVerticallyTo(-5));
            window.UpdateLayout();
            Assert.Equal(0, editor.VerticalOffset, 1);
        }
        finally
        {
            window.Close();
        }
    }
}
