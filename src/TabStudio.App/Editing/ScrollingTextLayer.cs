using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace TabStudio.App.Editing;

/// <summary>
/// Draws the editor's text exactly as AvaloniaEdit's text layer draws it, but so that a scroll moves the text instead of
/// drawing it again: each visual line keeps its drawing at its place in the document, and the layer as a whole moves
/// by the scroll offset. A frame of a scroll then records only the lines that came into view.
/// </summary>
/// <remarks>
/// <para>
/// AvaloniaEdit places every visible line at its place in the viewport with a new render transform on each scroll, and
/// Avalonia records a visual again whenever its transform changes: every visible line, glyph run by glyph run, on every
/// frame of a drag or a fling, with the allocations that come with it.
/// </para>
/// <para>
/// This is a small workaround of AvaloniaEdit's internals, kept here: AvaloniaEdit's own text layer (an internal type,
/// found by its name) is taken out of the text view's layers, where it still receives the visual lines but is no longer
/// part of the window, so it is neither arranged nor drawn; this layer takes its place among the layers, below the caret.
/// When the text view has no such layer, as a later AvaloniaEdit might not, nothing is changed.
/// </para>
/// </remarks>
public sealed class ScrollingTextLayer : Control
{
    private readonly TextView _textView;
    private readonly Control _builtIn;
    private readonly Dictionary<VisualLine, LineDrawing> _drawings = new(ReferenceEqualityComparer.Instance);
    private readonly List<VisualLine> _gone = [];
    private readonly TranslateTransform _scroll = new();

    private ScrollingTextLayer(TextView textView, Control builtIn)
    {
        _textView = textView;
        _builtIn = builtIn;
        IsHitTestVisible = false;
        Focusable = false;
        RenderTransform = _scroll;
    }

    /// <summary>The lines it draws now, for tests.</summary>
    internal int LineCount => _drawings.Count;

    /// <summary>Puts the layer in place of a text view's own text layer.</summary>
    /// <param name="textView">The editor's text view.</param>
    /// <returns>The layer, or <see langword="null"/> when the text view has no text layer to replace.</returns>
    public static ScrollingTextLayer? Install(TextView textView)
    {
        ArgumentNullException.ThrowIfNull(textView);
        var builtIn = textView.Layers.FirstOrDefault(layer => layer.GetType().Name == "TextLayer" && layer.GetType().Namespace == typeof(TextView).Namespace);
        if (builtIn is null)
        {
            return null;
        }

        var layer = new ScrollingTextLayer(textView, builtIn);
        textView.InsertLayer(layer, KnownLayer.Text, LayerInsertionPosition.Below);
        textView.Layers.Remove(builtIn);
        textView.VisualLinesChanged += layer.OnVisualLinesChanged;
        textView.ScrollOffsetChanged += layer.OnScrollOffsetChanged;
        if (textView.VisualLinesValid)
        {
            layer.OnVisualLinesChanged(textView, EventArgs.Empty);
        }

        return layer;
    }

    /// <summary>Gives the text view its own text layer back.</summary>
    public void Uninstall()
    {
        _textView.VisualLinesChanged -= OnVisualLinesChanged;
        _textView.ScrollOffsetChanged -= OnScrollOffsetChanged;
        var index = _textView.Layers.IndexOf(this);
        if (index >= 0)
        {
            _textView.Layers.RemoveAt(index);
            _textView.Layers.Insert(index, _builtIn);
            _textView.Redraw();
        }

        VisualChildren.Clear();
        _drawings.Clear();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => default;

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        MoveToScrollOffset();
        foreach (var (line, drawing) in _drawings)
        {
            // In document coordinates: a line keeps its place, and so its drawing, while the view scrolls.
            drawing.Arrange(new Rect(0, line.VisualTop, finalSize.Width, line.Height));
        }

        return finalSize;
    }

    /// <summary>Keeps a drawing for each visible line: new lines get one, lines that left the view lose theirs.</summary>
    private void OnVisualLinesChanged(object? sender, EventArgs e)
    {
        if (!_textView.VisualLinesValid)
        {
            return;
        }

        var lines = _textView.VisualLines;
        _gone.Clear();
        foreach (var line in _drawings.Keys)
        {
            if (line.IsDisposed || !Contains(lines, line))
            {
                _gone.Add(line);
            }
        }

        foreach (var line in _gone)
        {
            if (_drawings.Remove(line, out var drawing))
            {
                VisualChildren.Remove(drawing);
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!_drawings.ContainsKey(line))
            {
                var drawing = new LineDrawing(line, _textView);
                _drawings.Add(line, drawing);
                VisualChildren.Add(drawing);
            }
        }

        InvalidateArrange();
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        MoveToScrollOffset();
        InvalidateArrange();
    }

    private void MoveToScrollOffset()
    {
        var offset = _textView.ScrollOffset;
        if (_scroll.X != -offset.X)
        {
            _scroll.X = -offset.X;
        }

        if (_scroll.Y != -offset.Y)
        {
            _scroll.Y = -offset.Y;
        }
    }

    private static bool Contains(IReadOnlyList<VisualLine> lines, VisualLine line)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (ReferenceEquals(lines[i], line))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The drawing of one visual line's text, as AvaloniaEdit's own draws it: each text line centered in its row.</summary>
    private sealed class LineDrawing : Layoutable
    {
        private readonly VisualLine _line;
        private readonly TextView _textView;

        public LineDrawing(VisualLine line, TextView textView)
        {
            _line = line;
            _textView = textView;
            IsHitTestVisible = false;

            // Lines sit at fractional positions, as the caret and the selection expect; rounding would shift them.
            UseLayoutRounding = false;
        }

        public override void Render(DrawingContext context)
        {
            var defaultLineHeight = _textView.DefaultLineHeight;
            var textLines = _line.TextLines;
            double top = 0;
            for (var i = 0; i < textLines.Count; i++)
            {
                var textLine = textLines[i];
                var textHeight = textLine.Height;
                var rowHeight = Math.Max(textHeight, defaultLineHeight);
                textLine.Draw(context, new Point(0, top + ((rowHeight - textHeight) / 2)));
                top += rowHeight;
            }
        }
    }
}
