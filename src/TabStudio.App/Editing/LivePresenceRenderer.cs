using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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
/// behind the text, and each one's caret as a bar in their color with their name on a tag above it, over the text, as
/// a person sees where everyone else types.
/// </summary>
/// <remarks>
/// The carets are a layer of their own over the text view's caret layer, redrawn whenever they move or the view
/// scrolls: the editor's own caret layer redraws only when its caret blinks.
/// </remarks>
public sealed class LivePresenceRenderer
{
    /// <summary>How opaque a selection's tint is.</summary>
    private const byte SelectionAlpha = 0x44;

    private readonly SelectionLayer _selections;
    private CaretOverlay? _carets;
    private TextView? _textView;
    private IReadOnlyList<LiveRemoteCaret> _shown = [];

    /// <summary>Creates the renderer; <see cref="Attach"/> puts it in an editor.</summary>
    public LivePresenceRenderer()
    {
        _selections = new SelectionLayer(this);
    }

    /// <summary>The carets drawn now, for tests.</summary>
    public IReadOnlyList<LiveRemoteCaret> Shown => _shown;

    /// <summary>Draws in a text view: the selections behind its text, the carets over it.</summary>
    /// <param name="textView">The editor's text view.</param>
    public void Attach(TextView textView)
    {
        ArgumentNullException.ThrowIfNull(textView);
        if (ReferenceEquals(textView, _textView))
        {
            return;
        }

        Detach();
        _textView = textView;
        textView.BackgroundRenderers.Add(_selections);
        _carets = new CaretOverlay(this, textView);
        textView.InsertLayer(_carets, KnownLayer.Caret, LayerInsertionPosition.Above);
        textView.VisualLinesChanged += OnViewChanged;
        textView.ScrollOffsetChanged += OnViewChanged;
    }

    /// <summary>Stops drawing in the text view.</summary>
    public void Detach()
    {
        if (_textView is not { } textView)
        {
            return;
        }

        textView.BackgroundRenderers.Remove(_selections);
        textView.VisualLinesChanged -= OnViewChanged;
        textView.ScrollOffsetChanged -= OnViewChanged;
        if (_carets is not null)
        {
            textView.Layers.Remove(_carets);
        }

        _carets = null;
        _textView = null;
        _shown = [];
        textView.InvalidateLayer(KnownLayer.Selection);
    }

    /// <summary>Shows the others' carets and selections.</summary>
    /// <param name="carets">Them, in this device's offsets.</param>
    public void Show(IReadOnlyList<LiveRemoteCaret> carets)
    {
        ArgumentNullException.ThrowIfNull(carets);
        _shown = carets;
        _textView?.InvalidateLayer(KnownLayer.Selection);
        _carets?.InvalidateVisual();
    }

    private void OnViewChanged(object? sender, EventArgs e) => _carets?.InvalidateVisual();

    private static bool IsVisible(TextView textView, int offset)
    {
        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0 || textView.Document is not { } document || offset > document.TextLength)
        {
            return false;
        }

        var start = textView.VisualLines[0].FirstDocumentLine.Offset;
        var end = textView.VisualLines[^1].LastDocumentLine.EndOffset;
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
            foreach (var caret in owner._shown)
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

    /// <summary>The others' carets, each a bar in its color with the participant's name on a tag, over the text.</summary>
    private sealed class CaretOverlay : Control
    {
        private static readonly Typeface TagFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

        private readonly LivePresenceRenderer _owner;
        private readonly TextView _textView;

        public CaretOverlay(LivePresenceRenderer owner, TextView textView)
        {
            _owner = owner;
            _textView = textView;
            IsHitTestVisible = false;
            Focusable = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var textView = _textView;
            if (textView.Document is not { } document)
            {
                return;
            }

            var foreground = ThemeBrushes.Get("Brush.OnAccent");
            foreach (var caret in _owner._shown)
            {
                var offset = Math.Clamp(caret.Head, 0, document.TextLength);
                if (!IsVisible(textView, offset))
                {
                    continue;
                }

                var location = new TextViewPosition(document.GetLocation(offset));
                var top = textView.GetVisualPosition(location, VisualYPosition.LineTop) - textView.ScrollOffset;
                var bottom = textView.GetVisualPosition(location, VisualYPosition.LineBottom) - textView.ScrollOffset;
                var brush = LiveAvatarViewModel.BrushFor(caret.Color);
                context.DrawRectangle(brush, null, new Rect(top.X - 1, top.Y, 2, Math.Max(1, bottom.Y - top.Y)));

                // The name on a tag at the caret's top, inside the view: above the line, or below it on the first line.
                var name = new FormattedText(caret.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TagFace, 11, foreground);
                var width = name.Width + 10;
                var height = name.Height + 4;
                var x = Math.Clamp(top.X - 1, 0, Math.Max(0, Bounds.Width - width));
                var y = top.Y - height >= 0 ? top.Y - height : bottom.Y;
                context.DrawRectangle(brush, null, new Rect(x, y, width, height), 3, 3);
                context.DrawText(name, new Point(x + 5, y + 2));
            }
        }
    }
}
