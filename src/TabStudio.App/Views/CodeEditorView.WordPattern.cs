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
    // The language's word under a double-click, selected when the pointer is released.
    private (int Start, int End)? _pendingWordSelection;

    private void InitializeWordSelection()
    {
        var textArea = _editor.TextArea;
        textArea.AddHandler(PointerPressedEvent, OnWordSelectionPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        textArea.AddHandler(PointerReleasedEvent, OnWordSelectionPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void DisposeWordSelection()
    {
        var textArea = _editor.TextArea;
        textArea.RemoveHandler(PointerPressedEvent, OnWordSelectionPointerPressed);
        textArea.RemoveHandler(PointerReleasedEvent, OnWordSelectionPointerReleased);
    }

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
        (int Start, int End)? atEnd = null;
        foreach (var (start, end) in Words(pattern, document.GetText(line.Offset, line.Length)))
        {
            if (start > column)
            {
                break;
            }

            // Inside a word, or at the end of one when the next does not start here.
            if (end > column)
            {
                return (line.Offset + start, line.Offset + end);
            }

            if (end == column)
            {
                atEnd = (line.Offset + start, line.Offset + end);
            }
        }

        return atEnd;
    }

    /// <summary>The words of a line by a word pattern, in order; none when the pattern takes too long.</summary>
    private static List<(int Start, int End)> Words(Regex pattern, string line)
    {
        var words = new List<(int Start, int End)>();
        try
        {
            for (var match = pattern.Match(line); match.Success; match = match.NextMatch())
            {
                if (match.Length > 0)
                {
                    words.Add((match.Index, match.Index + match.Length));
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            words.Clear();
        }

        return words;
    }

    private void OnWordSelectionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pendingWordSelection = null;
        if (e.ClickCount == 2 && _rules?.WordPattern is not null && e.GetCurrentPoint(_editor.TextArea).Properties.IsLeftButtonPressed
            && _editor.GetPositionFromPoint(e.GetPosition(_editor)) is { } position)
        {
            _pendingWordSelection = WordAt(_editor.Document.GetOffset(position.Location));
        }
    }

    private void OnWordSelectionPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pendingWordSelection is not { } word)
        {
            return;
        }

        // The editor selected its own idea of the word on the double-click. Unless dragging took the selection on to
        // more words, the language's word replaces it.
        _pendingWordSelection = null;
        var textArea = _editor.TextArea;
        var document = _editor.Document;
        if (textArea.Selection.IsEmpty || textArea.Selection.SurroundingSegment is not { } selected || _rules?.WordPattern is not { } pattern)
        {
            return;
        }

        var line = document.GetLineByOffset(word.Start);
        if (selected.EndOffset > line.EndOffset || selected.Offset < line.Offset)
        {
            return;
        }

        var start = selected.Offset - line.Offset;
        var end = selected.EndOffset - line.Offset;
        var touched = Words(pattern, document.GetText(line.Offset, line.Length)).Where(w => w.Start < end && w.End > start).ToList();
        if (touched is [var only] && only == (word.Start - line.Offset, word.End - line.Offset))
        {
            textArea.Selection = Selection.Create(textArea, word.Start, word.End);
            textArea.Caret.Offset = word.End;
        }
    }
}
