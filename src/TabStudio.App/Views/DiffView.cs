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

        var header = new DiffHeaderView { DataContext = comparison };
        DockPanel.SetDock(header, Dock.Top);
        Content = new DockPanel { Children = { header, _editor } };

        document.ActionRequested += OnActionRequested;
        document.ComparisonChanged += OnComparisonChanged;
    }

    /// <summary>The editor control, for tests and keyboard focus.</summary>
    public TextEditor Editor => _editor;

    /// <inheritdoc />
    public void Dispose()
    {
        _document.ActionRequested -= OnActionRequested;
        _document.ComparisonChanged -= OnComparisonChanged;
        _editor.TextArea.RemoveHandler(TappedEvent, OnTapped);
        _search.Uninstall();
        _textMate?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Dispatcher.UIThread.Post(() => _editor.TextArea.Focus(), DispatcherPriority.Background);
        }
    }

    private IReadOnlyList<DiffLine>? Lines() => _document.Comparison?.Diff.Lines;

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
