using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.LanguageServers.Features;

namespace TabStudio.App.Editing;

/// <summary>
/// Shows a language server's inlay hints in the code, such as the parameter name before an argument or the type a
/// function returns: muted text between the characters, which takes no room in the document. The hints follow edits
/// until the server sends fresh ones.
/// </summary>
public sealed class InlayHintGenerator : VisualLineElementGenerator
{
    private static readonly IBrush HintBrush = new ImmutableSolidColorBrush(Color.Parse("#8B93A1"));

    private (int Offset, string Text)[] _hints = [];

    /// <summary>The hints shown, by offset, for tests.</summary>
    public IReadOnlyList<(int Offset, string Text)> Hints => _hints;

    /// <summary>Replaces the hints.</summary>
    /// <param name="hints">The server's hints for the whole document.</param>
    public void SetHints(IEnumerable<LspInlayHint> hints)
    {
        ArgumentNullException.ThrowIfNull(hints);
        _hints = hints
            .GroupBy(h => h.Offset)
            .Select(g => (g.Key, string.Concat(g.Select(Label))))
            .OrderBy(h => h.Key)
            .ToArray();
    }

    /// <summary>Moves the hints through an edit; hints in deleted text go away. Call from the document's <c>Changed</c> event.</summary>
    /// <param name="change">The edit.</param>
    public void OnDocumentChanged(DocumentChangeEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var delta = change.InsertionLength - change.RemovalLength;
        var end = change.Offset + change.RemovalLength;
        _hints = _hints
            .Where(h => h.Offset <= change.Offset || h.Offset >= end)
            .Select(h => h.Offset > change.Offset || (h.Offset == change.Offset && change.RemovalLength > 0) ? (h.Offset + delta, h.Text) : h)
            .ToArray();
    }

    /// <inheritdoc />
    public override int GetFirstInterestedOffset(int startOffset)
    {
        var hints = _hints;
        var low = 0;
        var high = hints.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (hints[middle].Offset < startOffset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < hints.Length ? hints[low].Offset : -1;
    }

    /// <inheritdoc />
    public override VisualLineElement? ConstructElement(int offset)
    {
        foreach (var (hintOffset, text) in _hints)
        {
            if (hintOffset == offset)
            {
                return new HintElement(text);
            }
        }

        return null;
    }

    private static string Label(LspInlayHint hint) =>
        (hint.PaddingLeft ? " " : string.Empty) + hint.Label.Trim() + (hint.PaddingRight ? " " : string.Empty);

    /// <summary>A hint: text that is shown but is not in the document, so the caret steps over it.</summary>
    private sealed class HintElement(string text) : FormattedTextElement(text, 0)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            TextRunProperties.SetForegroundBrush(HintBrush);
            TextRunProperties.SetFontRenderingEmSize(Math.Max(8, TextRunProperties.FontRenderingEmSize - 1.5));
            return base.CreateTextRun(startVisualColumn, context);
        }

        public override int GetNextCaretPosition(int visualColumn, AvaloniaEdit.Document.LogicalDirection direction, CaretPositioningMode mode) => -1;
    }
}
