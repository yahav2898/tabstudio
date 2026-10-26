using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Theming;

namespace TabStudio.App.Editing;

/// <summary>Draws an insertion in muted text without changing the document, saved text or undo stack.</summary>
public sealed class GhostTextGenerator(InlineCompletionController completion) : VisualLineElementGenerator
{
    /// <inheritdoc />
    public override int GetFirstInterestedOffset(int startOffset) => completion.Text.Length > 0 && completion.Offset >= startOffset ? completion.Offset : -1;

    /// <inheritdoc />
    public override VisualLineElement? ConstructElement(int offset) => offset == completion.Offset && completion.Text.Length > 0 ? new GhostElement(completion.Text) : null;

    private sealed class GhostElement(string text) : FormattedTextElement(text, 0)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            TextRunProperties.SetForegroundBrush(ThemeBrushes.Editor("Editor.InlayHint"));
            return base.CreateTextRun(startVisualColumn, context);
        }

        public override int GetNextCaretPosition(int visualColumn, AvaloniaEdit.Document.LogicalDirection direction, CaretPositioningMode mode) => -1;
    }
}
