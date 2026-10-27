using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Services.Live;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels.Live;

namespace TabStudio.App.Editing;

/// <summary>
/// The other participants of a TabStudio Live session in the editor: each one's selection as a tint of their color
/// behind the text (<see cref="Selections"/>), and each one's caret as a bar in their color with their name on a tag
/// above it (<see cref="Carets"/>), over the text, as a person sees where everyone else types.
/// </summary>
public sealed class LivePresenceRenderer
{
    /// <summary>How opaque a selection's tint is.</summary>
    private const byte SelectionAlpha = 0x44;

    private IReadOnlyList<LiveRemoteCaret> _carets = [];

    /// <summary>Creates the two layers; add them to the text view's background renderers.</summary>
    public LivePresenceRenderer()
    {
        Selections = new SelectionLayer(this);
        Carets = new CaretLayer(this);
    }

    /// <summary>The selections, behind the text.</summary>
    public IBackgroundRenderer Selections { get; }

    /// <summary>The carets and their name tags, over the text.</summary>
    public IBackgroundRenderer Carets { get; }

    /// <summary>The carets drawn now, for tests.</summary>
    public IReadOnlyList<LiveRemoteCaret> Shown => _carets;

    /// <summary>Shows the others' carets and selections.</summary>
    /// <param name="carets">Them, in this device's offsets.</param>
    public void Show(IReadOnlyList<LiveRemoteCaret> carets)
    {
        ArgumentNullException.ThrowIfNull(carets);
        _carets = carets;
    }

    private static bool IsVisible(TextView textView, int offset, out int start, out int end)
    {
        start = 0;
        end = 0;
        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0 || textView.Document is not { } document || offset > document.TextLength)
        {
            return false;
        }

        start = textView.VisualLines[0].FirstDocumentLine.Offset;
        end = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        return offset >= start && offset <= end;
    }

    /// <summary>The others' selections, as tints of their colors.</summary>
    private sealed class SelectionLayer(LivePresenceRenderer owner) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView.Document is not { } document || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
            {
                return;
            }

            var visibleStart = textView.VisualLines[0].FirstDocumentLine.Offset;
            var visibleEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
            foreach (var caret in owner._carets)
            {
                var start = Math.Clamp(Math.Min(caret.Anchor, caret.Head), 0, document.TextLength);
                var end = Math.Clamp(Math.Max(caret.Anchor, caret.Head), 0, document.TextLength);
                if (start == end || end < visibleStart || start > visibleEnd)
                {
                    continue;
                }

                var color = LiveAvatarViewModel.ColorFor(caret.Color);
                var brush = new ImmutableSolidColorBrush(Color.FromArgb(SelectionAlpha, color.R, color.G, color.B));
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = start, EndOffset = end }))
                {
                    drawingContext.DrawRectangle(brush, null, rect, 2, 2);
                }
            }
        }
    }

    /// <summary>The others' carets, each a bar in its color with the participant's name on a tag.</summary>
    private sealed class CaretLayer(LivePresenceRenderer owner) : IBackgroundRenderer
    {
        private static readonly Typeface TagFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

        public KnownLayer Layer => KnownLayer.Caret;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView.Document is not { } document)
            {
                return;
            }

            var foreground = ThemeBrushes.Get("Brush.OnAccent");
            foreach (var caret in owner._carets)
            {
                var offset = Math.Clamp(caret.Head, 0, document.TextLength);
                if (!IsVisible(textView, offset, out _, out _))
                {
                    continue;
                }

                var location = new TextViewPosition(document.GetLocation(offset));
                var top = textView.GetVisualPosition(location, VisualYPosition.LineTop) - textView.ScrollOffset;
                var bottom = textView.GetVisualPosition(location, VisualYPosition.LineBottom) - textView.ScrollOffset;
                var brush = LiveAvatarViewModel.BrushFor(caret.Color);
                drawingContext.DrawRectangle(brush, null, new Rect(top.X - 1, top.Y, 2, Math.Max(1, bottom.Y - top.Y)));

                // The name on a tag at the caret's top, inside the view: above the line, or below it on the first line.
                var name = new FormattedText(caret.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TagFace, 11, foreground);
                var width = name.Width + 10;
                var height = name.Height + 4;
                var x = Math.Clamp(top.X - 1, 0, Math.Max(0, textView.Bounds.Width - width));
                var y = top.Y - height >= 0 ? top.Y - height : bottom.Y;
                drawingContext.DrawRectangle(brush, null, new Rect(x, y, width, height), 3, 3);
                drawingContext.DrawText(name, new Point(x + 5, y + 2));
            }
        }
    }
}
