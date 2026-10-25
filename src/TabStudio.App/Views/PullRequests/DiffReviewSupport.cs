using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using TabStudio.App.ViewModels.PullRequests;

namespace TabStudio.App.Views.PullRequests;

/// <summary>
/// Pull request review in a file's diff: the review bar under the header, the threads and drafts under their
/// lines, and three ways to start a comment: tapping the review margin (a long press on a line with threads),
/// the editor's Add Comment menu item, and Ctrl+Shift+Enter, each on the selected lines or the caret's line.
/// </summary>
internal sealed class DiffReviewSupport : IDisposable
{
    /// <summary>The shortcut that starts a comment, as shown in the menu.</summary>
    public static readonly KeyGesture AddCommentGesture = new(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift);

    private readonly FileReviewViewModel _review;
    private readonly TextEditor _editor;
    private readonly ReviewDiffBar _bar;
    private readonly MenuItem _addComment;

    /// <summary>Adds the review to a diff.</summary>
    /// <param name="review">The file's review.</param>
    /// <param name="editor">The diff's editor.</param>
    /// <param name="host">The panel that holds the editor, where the review's cards are laid.</param>
    /// <param name="layout">The diff's layout, whose first child is the header; the review bar goes under it.</param>
    public DiffReviewSupport(FileReviewViewModel review, TextEditor editor, Panel host, DockPanel layout)
    {
        _review = review;
        _editor = editor;
        _bar = new ReviewDiffBar { DataContext = review };
        DockPanel.SetDock(_bar, Dock.Top);
        layout.Children.Insert(1, _bar);
        _bar.AddCommentRequested += OnAddCommentRequested;

        // The review margin goes between the line numbers and the text, where GitHub puts its plus.
        Adorner = new ReviewAdorner(editor, host, review, marginIndex: 1);
        Adorner.LineMenuRequested += OnLineMenuRequested;

        _addComment = new MenuItem { Header = "Add Comment", InputGesture = AddCommentGesture };
        _addComment.Click += OnAddCommentRequested;
        var copy = new MenuItem { Header = "Copy", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) };
        copy.Click += (_, _) => _editor.Copy();
        var menu = new ContextMenu { Items = { _addComment, new Separator(), copy } };
        menu.Opening += OnMenuOpening;
        editor.ContextMenu = menu;
        editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        review.PropertyChanged += OnReviewPropertyChanged;
    }

    /// <summary>The threads under their lines, for tests.</summary>
    public ReviewAdorner Adorner { get; }

    /// <summary>Starts a comment on the selected lines, or on the caret's line when nothing is selected.</summary>
    public void StartCommentAtSelection()
    {
        var document = _editor.Document;
        int first, last;
        if (_editor.SelectionLength > 0)
        {
            var end = _editor.SelectionStart + _editor.SelectionLength;
            first = document.GetLineByOffset(_editor.SelectionStart).LineNumber;
            last = document.GetLineByOffset(end).LineNumber;

            // A selection that ends at the very start of a line does not include that line.
            if (last > first && document.GetLineByNumber(last).Offset == end)
            {
                last--;
            }
        }
        else
        {
            first = last = _editor.TextArea.Caret.Line;
        }

        Adorner.StartComment(first, last);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _bar.AddCommentRequested -= OnAddCommentRequested;
        _addComment.Click -= OnAddCommentRequested;
        _review.PropertyChanged -= OnReviewPropertyChanged;
        Adorner.LineMenuRequested -= OnLineMenuRequested;
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        Adorner.Dispose();
    }

    private void OnAddCommentRequested(object? sender, EventArgs e) => StartCommentAtSelection();

    /// <summary>A long press on the margin starts a comment on that line, even where threads are.</summary>
    private void OnLineMenuRequested(object? sender, int line) => Adorner.StartComment(line, line);

    private void OnMenuOpening(object? sender, CancelEventArgs e) => _addComment.IsEnabled = _review.CanComment;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (AddCommentGesture.Matches(e) && _review.CanComment)
        {
            StartCommentAtSelection();
            e.Handled = true;
        }
    }

    private void OnReviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileReviewViewModel.CanComment))
        {
            Adorner.Margin.InvalidateVisual();
        }
    }
}
