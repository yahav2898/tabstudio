using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Views;

/// <summary>Drives the real editor of the active document: typing, moving the caret and finding text on screen.</summary>
internal static class EditorHarness
{
    /// <summary>The editor view of the active document in a window.</summary>
    public static CodeEditorView ActiveEditor(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<CodeEditorView>().Single(v => v.IsVisible);
        view.Editor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    /// <summary>Types text one character at a time, as a keyboard does.</summary>
    public static void Type(TextEditor editor, string text)
    {
        foreach (var character in text)
        {
            editor.TextArea.PerformTextInput(character.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Puts the caret at the end of the line that contains some text and starts a new line under it.</summary>
    public static void StartLineAfter(TextEditor editor, string text)
    {
        editor.CaretOffset = OffsetOf(editor, text) + text.Length;
        editor.TextArea.PerformTextInput("\n");
    }

    /// <summary>The offset of some text, which must be in the document.</summary>
    public static int OffsetOf(TextEditor editor, string text)
    {
        var offset = editor.Text.IndexOf(text, StringComparison.Ordinal);
        Assert.True(offset >= 0, $"'{text}' is in the editor");
        return offset;
    }

    /// <summary>The text of the caret line, without its indentation.</summary>
    public static string CaretLine(TextEditor editor) =>
        editor.Document.GetText(editor.Document.GetLineByOffset(editor.CaretOffset)).Trim();

    /// <summary>Where a character of the document is drawn, in window coordinates.</summary>
    public static Point PointOf(Window window, TextEditor editor, int offset)
    {
        var textView = editor.TextArea.TextView;
        var position = new TextViewPosition(editor.Document.GetLocation(offset));
        var inView = textView.GetVisualPosition(position, VisualYPosition.LineMiddle) - textView.ScrollOffset;
        return textView.TranslatePoint(inView + new Vector(3, 0), window) ?? throw new InvalidOperationException("The editor is not in the window.");
    }
}
