using System.ComponentModel;
using TabStudio.App.ViewModels;
using TabStudio.App.Views.PullRequests;

namespace TabStudio.App.Views;

/// <summary>
/// Pull request comments in the editor, while the folder is on a branch with an open pull request: a bubble in
/// the margin beside each line with a thread, and the thread under its line, with its replies and a Reply box,
/// when the bubble is tapped.
/// </summary>
public sealed partial class CodeEditorView
{
    private ReviewAdorner? _reviewAdorner;

    /// <summary>The pull request threads under their lines, for tests; <see langword="null"/> when the file has none to show.</summary>
    internal ReviewAdorner? ReviewThreads => _reviewAdorner;

    private void InitializeReviewThreads()
    {
        _document.PropertyChanged += OnReviewDocumentPropertyChanged;
        AttachReviewThreads();
    }

    private void DisposeReviewThreads()
    {
        _document.PropertyChanged -= OnReviewDocumentPropertyChanged;
        _reviewAdorner?.Dispose();
        _reviewAdorner = null;
    }

    private void AttachReviewThreads()
    {
        _reviewAdorner?.Dispose();
        _reviewAdorner = null;
        if (_document.EditorReview is { } review && _popupHost is not null)
        {
            // Beside the text, after the breakpoints and the line numbers.
            _reviewAdorner = new ReviewAdorner(_editor, _popupHost, review, _editor.TextArea.LeftMargins.Count);
        }
    }

    private void OnReviewDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.EditorReview))
        {
            AttachReviewThreads();
        }
    }
}
