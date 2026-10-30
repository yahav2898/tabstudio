using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels;
using TabStudio.App.Views.PullRequests;

namespace TabStudio.App.Views;

/// <summary>
/// A "Compare with Unmodified" tab, like Visual Studio's inline diff: the file's changes in one read-only
/// editor, with the line numbers of both versions, removed lines on red and added lines on green, and long
/// unchanged runs collapsed into a line that expands when tapped. The text keeps its syntax colors.
/// </summary>
public sealed class DiffView : UserControl, IDisposable
{
    private readonly DocumentViewModel _document;
    private readonly TextEditor _editor;
    private readonly DiffLineNumberMargin _margin;
    private readonly TextMate.Installation? _textMate;
    private readonly SearchPanel _search;
    private readonly DiffHeaderView _header;
    private bool _shownFirstChange;

    /// <summary>Creates the view of a comparison tab.</summary>
    /// <param name="document">A document made by <see cref="DocumentViewModel.ForComparison"/>.</param>
    public DiffView(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var comparison = document.Comparison ?? throw new ArgumentException("The document is not a comparison.", nameof(document));
        _document = document;
        DataContext = document;

        _editor = new TextEditor
        {
            Document = document.Document,
            IsReadOnly = true,
            ShowLineNumbers = false,
            WordWrap = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0, 4, 0, 0),
        };
        _editor.Options.EnableHyperlinks = false;
        Avalonia.Automation.AutomationProperties.SetName(_editor, "Changes in " + document.FileName);
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.HighlightCurrentLine = false;

        _margin = new DiffLineNumberMargin(() => _document.Comparison?.Diff);
        _editor.TextArea.LeftMargins.Insert(0, _margin);
        _editor.TextArea.TextView.BackgroundRenderers.Add(new DiffBackgroundRenderer(Lines));
        if (document.FileType.HighlightingExtension is { } extension)
        {
            _textMate = TextMateHighlighting.TryInstall(_editor, extension);
        }

        // Added after TextMate's colorizer, so collapsed lines keep their own color.
        _editor.TextArea.TextView.LineTransformers.Add(new DiffColorizer(Lines));
        _search = SearchPanel.Install(_editor);
        _editor.TextArea.AddHandler(TappedEvent, OnTapped, RoutingStrategies.Bubble, handledEventsToo: true);

        _header = new DiffHeaderView { DataContext = comparison };
        DockPanel.SetDock(_header, Dock.Top);
        var editorHost = new Panel { Children = { _editor } };
        var layout = new DockPanel { Children = { _header, editorHost } };
        Content = layout;
        if (document.Review is { } review)
        {
            ReviewSupport = new DiffReviewSupport(review, _editor, editorHost, layout);
        }

        _header.MoveRequested += OnMoveRequested;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        document.ActionRequested += OnActionRequested;
        document.ComparisonChanged += OnComparisonChanged;
    }

    /// <summary>The editor control, for tests and keyboard focus.</summary>
    public TextEditor Editor => _editor;

    /// <summary>The pull request review of the file, when the diff is a pull request's file.</summary>
    internal DiffReviewSupport? ReviewSupport { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        ReviewSupport?.Dispose();
        _document.ActionRequested -= OnActionRequested;
        _document.ComparisonChanged -= OnComparisonChanged;
        _header.MoveRequested -= OnMoveRequested;
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.TextArea.RemoveHandler(TappedEvent, OnTapped);
        RemoveHandler(KeyDownEvent, OnKeyDown);
        _search.Uninstall();
        _textMate?.Dispose();
    }

    /// <summary>Moves the caret to the first line of the next or previous change and scrolls it into view.</summary>
    /// <param name="direction">1 for the next change, -1 for the previous one.</param>
    /// <returns><see langword="true"/> when there was a change to move to.</returns>
    public bool MoveToChange(int direction)
    {
        var step = Math.Sign(direction);
        if (step == 0 || Lines() is not { } lines)
        {
            return false;
        }

        var caret = _editor.TextArea.Caret;
        for (var index = caret.Line - 1 + step; index >= 0 && index < lines.Count; index += step)
        {
            if (IsChange(lines, index) && !IsChange(lines, index - 1))
            {
                caret.Line = index + 1;
                caret.Column = 1;
                _editor.ScrollToLine(index + 1);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            // The first time, go to the first change, as Visual Studio does; the editor needs its size for that.
            Dispatcher.UIThread.Post(() =>
            {
                if (!_shownFirstChange && Lines() is { Count: > 0 } lines)
                {
                    _shownFirstChange = true;
                    if (!IsChange(lines, 0))
                    {
                        MoveToChange(1);
                    }
                }

                _editor.TextArea.Focus();
            }, DispatcherPriority.Background);
        }
    }

    private static bool IsChange(IReadOnlyList<DiffLine> lines, int index) =>
        index >= 0 && index < lines.Count && lines[index].Kind is DiffLineKind.Added or DiffLineKind.Removed;

    private IReadOnlyList<DiffLine>? Lines() => _document.Comparison?.Diff.Lines;

    /// <summary>The status bar shows the line in the file the caret is on: the working copy's, or the unmodified one's for a removed line.</summary>
    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        var caret = _editor.TextArea.Caret;
        var line = DiffColors.LineAt(Lines(), caret.Line);
        _document.CaretLine = line?.NewNumber ?? line?.OldNumber ?? caret.Line;
        _document.CaretColumn = caret.Column;
    }

    private void OnMoveRequested(object? sender, int direction)
    {
        MoveToChange(direction);
        _editor.TextArea.Focus();
    }

    /// <summary>F8 and Shift+F8 go to the next and previous change, as in Visual Studio's diff.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F8 && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            MoveToChange(e.KeyModifiers == KeyModifiers.Shift ? -1 : 1);
            e.Handled = true;
        }
    }

    /// <summary>A tap on a collapsed line shows the unchanged lines it stands for.</summary>
    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var textView = _editor.TextArea.TextView;
        if (!textView.VisualLinesValid)
        {
            return;
        }

        var line = textView.GetDocumentLineByVisualTop(e.GetPosition(textView).Y + textView.VerticalOffset);
        if (line is not null && DiffColors.LineAt(Lines(), line.LineNumber) is { Kind: DiffLineKind.Collapsed })
        {
            _document.ExpandComparison(line.LineNumber - 1);
            e.Handled = true;
        }
    }

    private void OnComparisonChanged(object? sender, EventArgs e)
    {
        _margin.InvalidateVisual();
        _editor.TextArea.TextView.Redraw();
    }

    private void OnActionRequested(object? sender, EditorAction action)
    {
        switch (action)
        {
            case EditorAction.Find or EditorAction.Replace:
                _search.IsReplaceMode = false;
                _search.Open();
                _search.Reactivate();
                break;
            case EditorAction.SelectAll:
                _editor.SelectAll();
                break;
            case EditorAction.Focus:
                _editor.TextArea.Focus();
                break;
        }
    }
}
