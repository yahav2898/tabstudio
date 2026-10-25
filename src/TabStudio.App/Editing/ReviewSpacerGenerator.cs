using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;

namespace TabStudio.App.Editing;

/// <summary>
/// Makes room under a line of an editor for what is shown there, such as a review thread: an invisible element at
/// the line's end that hangs below the text, so the line grows taller while its text stays where it was. The code
/// below moves down, as Visual Studio's inline comments push it.
/// </summary>
/// <param name="height">The room wanted under a 1-based document line, or 0 for none.</param>
public sealed class ReviewSpacerGenerator(Func<int, double> height) : VisualLineElementGenerator
{
    /// <inheritdoc />
    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.VisualLine.LastDocumentLine;
        return startOffset <= line.EndOffset && height(line.LineNumber) > 0 ? line.EndOffset : -1;
    }

    /// <inheritdoc />
    public override VisualLineElement? ConstructElement(int offset)
    {
        var line = CurrentContext.VisualLine.LastDocumentLine;
        var room = height(line.LineNumber);
        if (room <= 0 || offset != line.EndOffset)
        {
            return null;
        }

        // The run hangs from the text's baseline, so it adds its height less the font's descent below the text.
        var textView = CurrentContext.TextView;
        return new SpacerElement(room + Math.Max(0, textView.DefaultLineHeight - textView.DefaultBaseline));
    }

    /// <summary>An element of no width and no text that is as tall as the room it makes.</summary>
    private sealed class SpacerElement(double runHeight) : VisualLineElement(1, 0)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) => new SpacerRun(TextRunProperties, runHeight);

        // The caret never stops inside the element: it has no text.
        public override int GetNextCaretPosition(int visualColumn, AvaloniaEdit.Document.LogicalDirection direction, AvaloniaEdit.Document.CaretPositioningMode mode) => -1;

        public override bool IsWhitespace(int visualColumn) => true;
    }

    /// <summary>A run that draws nothing and hangs below the baseline by its height.</summary>
    private sealed class SpacerRun(TextRunProperties properties, double height) : DrawableTextRun
    {
        public override TextRunProperties? Properties => properties;

        public override int Length => 1;

        public override Size Size => new(0, height);

        public override double Baseline => 0;

        public override void Draw(DrawingContext drawingContext, Point origin)
        {
            // Nothing to draw: what fills the room is shown above the editor.
        }
    }
}
