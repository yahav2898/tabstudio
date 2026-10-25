using System.Text.RegularExpressions;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.Editing;

namespace TabStudio.App.Views;

/// <summary>
/// Double-clicking or double-tapping a word selects it as the file's language defines words: by the word pattern of the
/// language configuration an installed extension contributes, such as names with dashes in CSS or Lisp. Without one,
/// the editor's own word selection applies.
/// </summary>
public sealed partial class CodeEditorView
{
    private void InitializeWordSelection() =>
        _editor.TextArea.AddHandler(PointerPressedEvent, OnWordSelectionPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);

    private void DisposeWordSelection() =>
        _editor.TextArea.RemoveHandler(PointerPressedEvent, OnWordSelectionPointerPressed);

    /// <summary>
    /// The word at an offset by the language's word pattern: the word the offset is in or at the end of. Returns
    /// <see langword="null"/> when the language has no word pattern or no word is there.
    /// </summary>
    /// <param name="offset">An offset in the document.</param>
    internal (int Start, int End)? WordAt(int offset)
    {
        var document = _editor.Document;
        if (_rules?.WordPattern is not { } pattern || offset < 0 || offset > document.TextLength)
        {
            return null;
        }

        var line = document.GetLineByOffset(offset);
        var column = offset - line.Offset;
        var text = document.GetText(line.Offset, line.Length);
        try
        {
            (int Start, int End)? atEnd = null;
            for (var match = pattern.Match(text); match.Success && match.Index <= column; match = match.NextMatch())
            {
                var end = match.Index + match.Length;
                if (match.Length == 0 || end < column)
                {
                    continue;
                }

                // Inside a word, or at the end of one when the next does not start here.
                if (end > column)
                {
                    return (line.Offset + match.Index, line.Offset + end);
                }

                atEnd = (line.Offset + match.Index, line.Offset + end);
            }

            return atEnd;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private void OnWordSelectionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // The editor has selected its own idea of the word by now; the language's replaces it.
        var textArea = _editor.TextArea;
        if (e.ClickCount != 2 || _rules?.WordPattern is null || !e.GetCurrentPoint(textArea).Properties.IsLeftButtonPressed
            || _editor.GetPositionFromPoint(e.GetPosition(_editor)) is not { } position)
        {
            return;
        }

        if (WordAt(_editor.Document.GetOffset(position.Location)) is { } word)
        {
            textArea.Selection = Selection.Create(textArea, word.Start, word.End);
            textArea.Caret.Offset = word.End;
        }
    }
}
