using CommunityToolkit.Mvvm.ComponentModel;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>What the glyph beside a line with review items shows.</summary>
public enum ReviewGlyph
{
    /// <summary>A conversation.</summary>
    Thread,

    /// <summary>Only resolved conversations.</summary>
    Resolved,

    /// <summary>Only conversations whose lines changed since.</summary>
    Outdated,

    /// <summary>A comment of the pending review.</summary>
    Pending,

    /// <summary>A comment Claude drafted.</summary>
    Draft,

    /// <summary>A comment being written.</summary>
    NewComment,
}

/// <summary>
/// The review items under one line of a document, such as a diff or a code file: threads, Claude's drafts and a
/// comment being written. A glyph in the margin marks the line; expanded, the items show under it.
/// </summary>
public sealed partial class ReviewAnchorViewModel : ObservableObject
{
    /// <summary>Creates the anchor.</summary>
    /// <param name="line">The document's 1-based line the items show under.</param>
    /// <param name="items">The items: <see cref="ReviewThreadViewModel"/>, <see cref="DraftCommentViewModel"/> and <see cref="NewCommentViewModel"/>.</param>
    /// <param name="isExpanded">Whether the items show.</param>
    public ReviewAnchorViewModel(int line, IReadOnlyList<object> items, bool isExpanded)
    {
        ArgumentNullException.ThrowIfNull(items);
        Line = line;
        Items = items;
        IsExpanded = isExpanded;
    }

    /// <summary>The document's 1-based line the items show under.</summary>
    public int Line { get; }

    /// <summary>The items, in the order they show.</summary>
    public IReadOnlyList<object> Items { get; }

    /// <summary>Whether the items show under the line, rather than only the glyph.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>The most important kind of item, for the glyph.</summary>
    public ReviewGlyph Glyph
    {
        get
        {
            if (Items.OfType<NewCommentViewModel>().Any())
            {
                return ReviewGlyph.NewComment;
            }

            if (Items.OfType<DraftCommentViewModel>().Any())
            {
                return ReviewGlyph.Draft;
            }

            var threads = Items.OfType<ReviewThreadViewModel>().ToList();
            return threads.Any(t => t.IsPending || t.Comments.Any(c => c.IsPending)) ? ReviewGlyph.Pending
                : threads.Count > 0 && threads.All(t => t.IsResolved) ? ReviewGlyph.Resolved
                : threads.Count > 0 && threads.All(t => t.IsOutdated) ? ReviewGlyph.Outdated
                : ReviewGlyph.Thread;
        }
    }

    /// <summary>How many comments the line's threads have, for the glyph's tooltip.</summary>
    public int CommentCount => Items.OfType<ReviewThreadViewModel>().Sum(t => t.Comments.Count);
}

/// <summary>A document's review items by line, for the editor that shows them. Implemented by a file's diff review and by a code file's comments.</summary>
public interface IReviewAnchorSource
{
    /// <summary>Raised when <see cref="Anchors"/> changed.</summary>
    event EventHandler? AnchorsChanged;

    /// <summary>The lines with review items.</summary>
    IReadOnlyList<ReviewAnchorViewModel> Anchors { get; }

    /// <summary>Whether new comments can be added in this document, as in a pull request's diff.</summary>
    bool CanComment { get; }

    /// <summary>Whether a comment can go on a line: GitHub takes comments only on the lines of the diff.</summary>
    /// <param name="line">The document's 1-based line.</param>
    bool CanCommentOn(int line);

    /// <summary>Opens a comment box under a line, for that line or a range ending there.</summary>
    /// <param name="firstLine">The document's first line of the range.</param>
    /// <param name="lastLine">The document's last line of the range, where the box opens.</param>
    /// <returns>Why the comment cannot go there, or <see langword="null"/> when the box opened.</returns>
    string? StartComment(int firstLine, int lastLine);

    /// <summary>Remembers that the user expanded or collapsed a line's items, so it lasts when the anchors are rebuilt.</summary>
    /// <param name="anchor">The line's anchor, with its new <see cref="ReviewAnchorViewModel.IsExpanded"/>.</param>
    void SetExpanded(ReviewAnchorViewModel anchor);
}
