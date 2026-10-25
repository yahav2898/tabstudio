using System.Text;
using AvaloniaEdit.Document;
using TabStudio.Compilation;

namespace TabStudio.App.Editing;

/// <summary>Applies the language service's <see cref="TextEdit"/>s to editor documents and to plain text.</summary>
public static class TextEdits
{
    /// <summary>Applies edits to an editor document as one undo step.</summary>
    /// <param name="document">The document. Its text must be the text the edits were computed for.</param>
    /// <param name="edits">Non-overlapping edits in document order.</param>
    public static void Apply(TextDocument document, IReadOnlyList<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0)
        {
            return;
        }

        document.BeginUpdate();
        try
        {
            foreach (var edit in edits.OrderByDescending(e => e.Start))
            {
                if (edit.Start >= 0 && edit.Length >= 0 && edit.Start + edit.Length <= document.TextLength)
                {
                    document.Replace(edit.Start, edit.Length, edit.NewText);
                }
            }
        }
        finally
        {
            document.EndUpdate();
        }
    }

    /// <summary>Applies edits to text.</summary>
    /// <param name="text">The text the edits were computed for.</param>
    /// <param name="edits">Non-overlapping edits in document order.</param>
    /// <returns>The edited text.</returns>
    public static string Apply(string text, IReadOnlyList<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edits);
        var builder = new StringBuilder(text);
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            if (edit.Start >= 0 && edit.Length >= 0 && edit.Start + edit.Length <= builder.Length)
            {
                builder.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText);
            }
        }

        return builder.ToString();
    }

    /// <summary>Where the end of one edit's new text is once all edits are applied.</summary>
    /// <param name="edits">Non-overlapping edits in document order.</param>
    /// <param name="edit">One of them.</param>
    public static int EndAfterApplying(IReadOnlyList<TextEdit> edits, TextEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var shift = edits.Where(e => e.Start < edit.Start).Sum(e => e.NewText.Length - e.Length);
        return edit.Start + shift + edit.NewText.Length;
    }
}
