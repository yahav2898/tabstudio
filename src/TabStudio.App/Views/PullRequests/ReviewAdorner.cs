using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels.PullRequests;

namespace TabStudio.App.Views.PullRequests;

/// <summary>
/// Shows a document's review items under their lines in an editor, as Visual Studio shows pull request comments:
/// the review margin marks the lines, a spacer under each expanded line makes room, and the items' cards are laid
/// over the editor in that room, scrolling with the code. The cards live beside the editor rather than inside its
/// text area, so typing a reply never reaches the code.
/// </summary>
public sealed class ReviewAdorner : IDisposable
{
    private readonly TextEditor _editor;
    private readonly TextView _textView;
    private readonly Panel _host;
    private readonly IReviewAnchorSource _source;
    private readonly ReviewSpacerGenerator _generator;
    private readonly ReviewOverlay _overlay;
    private readonly Dictionary<int, double> _heights = [];
    private readonly Dictionary<object, ReviewAnchorView> _views = [];
    private IReadOnlyList<ReviewAnchorViewModel> _anchors = [];
    private bool _disposed;

    /// <summary>Installs the review items in an editor.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="host">A panel that holds the editor and nothing in front of it, where the cards are laid.</param>
    /// <param name="source">The review items by line.</param>
    /// <param name="marginIndex">Where the review margin goes among the editor's left margins.</param>
    public ReviewAdorner(TextEditor editor, Panel host, IReviewAnchorSource source, int marginIndex)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(source);
        _editor = editor;
        _textView = editor.TextArea.TextView;
        _host = host;
        _source = source;

        _generator = new ReviewSpacerGenerator(SpacerHeight);
        _textView.ElementGenerators.Add(_generator);
        Margin = new ReviewMargin(source);
        editor.TextArea.LeftMargins.Insert(Math.Clamp(marginIndex, 0, editor.TextArea.LeftMargins.Count), Margin);
        Margin.LineTapped += OnLineTapped;
        Margin.LineMenuRequested += OnLineMenuRequested;
        _overlay = new ReviewOverlay(this);
        host.Children.Add(_overlay);

        source.AnchorsChanged += OnAnchorsChanged;
        _textView.ScrollOffsetChanged += OnTextViewLayoutChanged;
        _textView.VisualLinesChanged += OnTextViewLayoutChanged;
        Synchronize();
    }

    /// <summary>Raised with the 1-based line when the user long-presses or right-clicks the margin.</summary>
    public event EventHandler<int>? LineMenuRequested;

    /// <summary>The review margin.</summary>
    public ReviewMargin Margin { get; }

    /// <summary>The cards shown, by the first item of their line, for tests.</summary>
    internal IReadOnlyCollection<ReviewAnchorView> Views => _views.Values;

    /// <summary>The editor's text view, which the cards follow.</summary>
    internal TextView TextView => _textView;

    /// <summary>Shows or hides a line's items, as tapping its glyph does.</summary>
    /// <param name="line">The 1-based line.</param>
    /// <returns><see langword="true"/> when the line has items.</returns>
    public bool Toggle(int line)
    {
        if (AnchorAt(line) is not { } anchor)
        {
            return false;
        }

        anchor.IsExpanded = !anchor.IsExpanded;
        return true;
    }

    /// <summary>Opens a comment box on a range of lines; the review says why when a comment cannot go there.</summary>
    /// <param name="firstLine">The first 1-based line.</param>
    /// <param name="lastLine">The last 1-based line, where the box opens.</param>
    public void StartComment(int firstLine, int lastLine) => _source.StartComment(firstLine, lastLine);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.AnchorsChanged -= OnAnchorsChanged;
        _textView.ScrollOffsetChanged -= OnTextViewLayoutChanged;
        _textView.VisualLinesChanged -= OnTextViewLayoutChanged;
        Margin.LineTapped -= OnLineTapped;
        Margin.LineMenuRequested -= OnLineMenuRequested;
        foreach (var anchor in _anchors)
        {
            anchor.PropertyChanged -= OnAnchorPropertyChanged;
        }

        _textView.ElementGenerators.Remove(_generator);
        _editor.TextArea.LeftMargins.Remove(Margin);
        _host.Children.Remove(_overlay);
        _views.Clear();
        _heights.Clear();
        _textView.Redraw();
    }

    /// <summary>The room under a line: its card's height while it is expanded.</summary>
    internal double SpacerHeight(int line) => _heights.GetValueOrDefault(line);

    /// <summary>The expanded anchors with their cards.</summary>
    internal IEnumerable<(ReviewAnchorViewModel Anchor, ReviewAnchorView View)> Shown() =>
        _anchors.Where(a => a.IsExpanded && a.Items.Count > 0 && _views.ContainsKey(a.Items[0])).Select(a => (a, _views[a.Items[0]]));

    /// <summary>Takes a card's measured height; when it changed, the line is laid out again with room for it.</summary>
    internal void OnCardMeasured(int line, double height)
    {
        if (Math.Abs(_heights.GetValueOrDefault(line) - height) < 0.5)
        {
            return;
        }

        _heights[line] = height;
        Dispatcher.UIThread.Post(() => RedrawLine(line), DispatcherPriority.Render);
    }

    private ReviewAnchorViewModel? AnchorAt(int line) => _anchors.FirstOrDefault(a => a.Line == line);

    /// <summary>Makes the cards match the anchors: one per expanded line, reused for the same items so typing is not lost.</summary>
    private void Synchronize()
    {
        foreach (var anchor in _anchors)
        {
            anchor.PropertyChanged -= OnAnchorPropertyChanged;
        }

        var previousHeights = new Dictionary<int, double>(_heights);
        _anchors = _source.Anchors;
        foreach (var anchor in _anchors)
        {
            anchor.PropertyChanged += OnAnchorPropertyChanged;
        }

        var shown = _anchors.Where(a => a.IsExpanded && a.Items.Count > 0).ToList();
        var keys = shown.Select(a => a.Items[0]).ToHashSet();
        foreach (var gone in _views.Keys.Where(k => !keys.Contains(k)).ToList())
        {
            _overlay.Children.Remove(_views[gone]);
            _views.Remove(gone);
        }

        _heights.Clear();
        foreach (var anchor in shown)
        {
            if (!_views.TryGetValue(anchor.Items[0], out var view))
            {
                view = new ReviewAnchorView();
                _views[anchor.Items[0]] = view;
                _overlay.Children.Add(view);
            }

            view.DataContext = anchor;

            // A card keeps the room it had while it is measured again.
            if (previousHeights.TryGetValue(anchor.Line, out var height))
            {
                _heights[anchor.Line] = height;
            }
        }

        foreach (var line in previousHeights.Keys.Union(_heights.Keys).ToList())
        {
            RedrawLine(line);
        }

        Margin.InvalidateVisual();
        _overlay.InvalidateMeasure();
    }

    private void RedrawLine(int line)
    {
        if (_disposed || _textView.Document is not { } document || line < 1 || line > document.LineCount)
        {
            return;
        }

        var documentLine = document.GetLineByNumber(line);
        _textView.Redraw(documentLine.Offset, documentLine.Length);
        _overlay.InvalidateArrange();
    }

    private void OnAnchorsChanged(object? sender, EventArgs e) => Synchronize();

    private void OnAnchorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviewAnchorViewModel.IsExpanded) && sender is ReviewAnchorViewModel anchor)
        {
            _source.SetExpanded(anchor);
            Synchronize();
        }
    }

    private void OnTextViewLayoutChanged(object? sender, EventArgs e) => _overlay.InvalidateArrange();

    /// <summary>A tap on a line's glyph shows or hides its items; elsewhere, where comments can go, it starts one.</summary>
    private void OnLineTapped(object? sender, int line)
    {
        if (!Toggle(line) && _source.CanComment)
        {
            StartComment(line, line);
        }
    }

    private void OnLineMenuRequested(object? sender, int line) => LineMenuRequested?.Invoke(this, line);

    /// <summary>
    /// The layer the cards are laid in: over the whole editor, clipped to its text, each card filling the room
    /// under its line across the text's width.
    /// </summary>
    private sealed class ReviewOverlay(ReviewAdorner adorner) : Panel
    {
        private double _measuredWidth;

        protected override Size MeasureOverride(Size availableSize)
        {
            _measuredWidth = Math.Max(240, adorner.TextView.Bounds.Width);
            foreach (var (anchor, view) in adorner.Shown())
            {
                view.Measure(new Size(_measuredWidth, double.PositiveInfinity));
                adorner.OnCardMeasured(anchor.Line, view.DesiredSize.Height);
            }

            return default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var textView = adorner.TextView;
            if (Math.Abs(Math.Max(240, textView.Bounds.Width) - _measuredWidth) > 0.5)
            {
                // The text got wider or narrower since the cards were measured: their heights follow.
                Dispatcher.UIThread.Post(InvalidateMeasure, DispatcherPriority.Render);
            }

            var origin = textView.TranslatePoint(default, this) ?? default;
            Clip = new RectangleGeometry(new Rect(origin, textView.Bounds.Size));
            foreach (var (anchor, view) in adorner.Shown())
            {
                var line = textView.VisualLinesValid ? textView.GetVisualLine(anchor.Line) : null;
                if (line is null)
                {
                    // Scrolled out of view: kept measured, laid outside the clip.
                    view.Arrange(new Rect(new Point(origin.X, -100_000), view.DesiredSize));
                    continue;
                }

                var top = line.VisualTop - textView.VerticalOffset + textView.DefaultLineHeight;
                var height = Math.Max(view.DesiredSize.Height, line.Height - textView.DefaultLineHeight);
                view.Arrange(new Rect(origin.X, origin.Y + top, textView.Bounds.Width, height));
            }

            return finalSize;
        }
    }
}
