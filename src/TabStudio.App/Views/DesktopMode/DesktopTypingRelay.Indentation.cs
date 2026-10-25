using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// Tab and Shift+Tab from the typing bar. The editor indents only for a Tab key it gets while it has the keyboard
/// focus, which the tablet's field has, so the relay indents as the editor does: one indentation at the caret, or
/// one more or one less at the start of each selected line.
/// </summary>
internal sealed partial class DesktopTypingRelay
{
    /// <summary>Indents as Tab does in the editor.</summary>
    /// <param name="area">The editor.</param>
    internal static void Indent(TextArea area)
    {
        if (area.Document is not { } document)
        {
            return;
        }

        using (document.RunUpdate())
        {
            if (area.Selection.IsMultiline)
            {
                foreach (var line in SelectedLines(area, document))
                {
                    if (area.ReadOnlySectionProvider.CanInsert(line.Offset))
                    {
                        document.Replace(line.Offset, 0, area.Options.IndentationString, OffsetChangeMappingType.KeepAnchorBeforeInsertion);
                    }
                }
            }
            else
            {
                area.Selection.ReplaceSelectionWithText(area.Options.GetIndentationString(area.Caret.Column));
            }
        }

        area.Caret.BringCaretToView();
    }

    /// <summary>Takes one indentation off the caret's line, or off each selected line, as Shift+Tab does in the editor.</summary>
    /// <param name="area">The editor.</param>
    internal static void Outdent(TextArea area)
    {
        if (area.Document is not { } document)
        {
            return;
        }

        using (document.RunUpdate())
        {
            foreach (var line in SelectedLines(area, document))
            {
                var indentation = TextUtilities.GetSingleIndentationSegment(document, line.Offset, area.Options.IndentationSize);
                if (indentation.Length > 0 && area.ReadOnlySectionProvider.GetDeletableSegments(indentation).FirstOrDefault() is { Length: > 0 } deletable)
                {
                    document.Remove(deletable.Offset, deletable.Length);
                }
            }
        }

        area.Caret.BringCaretToView();
    }

    /// <summary>
    /// The lines the selection touches, or the caret's line without a selection. A selection that ends at the start
    /// of a line leaves that line out, as in the editor.
    /// </summary>
    private static List<DocumentLine> SelectedLines(TextArea area, TextDocument document)
    {
        if (area.Selection.IsEmpty)
        {
            return [document.GetLineByNumber(area.Caret.Line)];
        }

        var segment = area.Selection.SurroundingSegment;
        var first = document.GetLineByOffset(segment.Offset);
        var last = document.GetLineByOffset(segment.EndOffset);
        if (last != first && last.Offset == segment.EndOffset)
        {
            last = last.PreviousLine;
        }

        var lines = new List<DocumentLine>();
        for (var line = first; line is not null && line.LineNumber <= last.LineNumber; line = line.NextLine)
        {
            lines.Add(line);
        }

        return lines;
    }
}
