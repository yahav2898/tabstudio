using AvaloniaEdit.Document;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// A pull request's review threads in the code editor of one of its files, while the folder is on the pull
/// request's branch, as Visual Studio shows pull request comments: a glyph beside each line with a thread, and the
/// thread under the line when the glyph is tapped. A thread follows its line when the code above it changes;
/// an outdated thread shows only while its line still reads as it did.
/// </summary>
public sealed class EditorReviewViewModel : IReviewAnchorSource, IDisposable
{
    /// <summary>How far from its line number a thread's line is looked for, after edits moved it.</summary>
    private const int SearchDistance = 40;

    private readonly TextDocument _document;
    private readonly HashSet<object> _expanded = [];
    private bool _disposed;

    /// <summary>Creates the editor's review.</summary>
    /// <param name="pullRequest">The pull request whose threads show.</param>
    /// <param name="document">The file's text in the editor.</param>
    /// <param name="relativePath">The file's path in the repository, with <c>/</c> separators.</param>
    public EditorReviewViewModel(PullRequestViewModel pullRequest, TextDocument document, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        PullRequest = pullRequest;
        Path = relativePath;
        _document = document;
        pullRequest.ReviewChanged += OnReviewChanged;
        document.Changed += OnDocumentChanged;
        Rebuild();
    }

    /// <inheritdoc />
    public event EventHandler? AnchorsChanged;

    /// <summary>The pull request.</summary>
    public PullRequestViewModel PullRequest { get; }

    /// <summary>The file's path in the repository.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public IReadOnlyList<ReviewAnchorViewModel> Anchors { get; private set; } = [];

    /// <inheritdoc />
    public bool CanComment => false;

    /// <inheritdoc />
    public bool CanCommentOn(int line) => false;

    /// <inheritdoc />
    public string? StartComment(int firstLine, int lastLine) => "New comments are written in the pull request's diff.";

    /// <inheritdoc />
    public void SetExpanded(ReviewAnchorViewModel anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        foreach (var item in anchor.Items)
        {
            if (anchor.IsExpanded)
            {
                _expanded.Add(item);
            }
            else
            {
                _expanded.Remove(item);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PullRequest.ReviewChanged -= OnReviewChanged;
        _document.Changed -= OnDocumentChanged;
    }

    /// <summary>
    /// Finds where a thread belongs in the file as it is now: its own line while that still reads as the comment's,
    /// else the nearest line that does. A current thread whose line was edited stays at its number; an outdated
    /// one is left out.
    /// </summary>
    /// <returns>The 1-based line, or <see langword="null"/> to leave the thread out.</returns>
    internal int? LineFor(ReviewThreadViewModel thread)
    {
        if (thread.Side != DiffSide.Right || thread.Line is not { } expected || _document.LineCount == 0)
        {
            return null;
        }

        if (thread.Thread.Root.CommentedLineText?.TrimEnd() is not { } text)
        {
            return thread.IsOutdated ? null : Math.Clamp(expected, 1, _document.LineCount);
        }

        for (var distance = 0; distance <= SearchDistance; distance++)
        {
            if (Reads(expected + distance, text))
            {
                return expected + distance;
            }

            if (distance > 0 && Reads(expected - distance, text))
            {
                return expected - distance;
            }
        }

        return thread.IsOutdated ? null : Math.Clamp(expected, 1, _document.LineCount);
    }

    /// <summary>Places the file's threads under their lines, telling the editor only when something moved.</summary>
    private void Rebuild()
    {
        if (_disposed)
        {
            return;
        }

        var placed = PullRequest.ThreadsFor(Path)
            .Select(t => (Thread: t, Line: LineFor(t)))
            .Where(p => p.Line is not null)
            .GroupBy(p => p.Line!.Value)
            .OrderBy(g => g.Key)
            .Select(g => (Line: g.Key, Items: g.Select(p => (object)p.Thread).ToList()))
            .ToList();

        var unchanged = placed.Count == Anchors.Count
            && placed.Zip(Anchors).All(p => p.First.Line == p.Second.Line && p.First.Items.SequenceEqual(p.Second.Items));
        if (unchanged)
        {
            return;
        }

        Anchors = [.. placed.Select(p => new ReviewAnchorViewModel(p.Line, p.Items, p.Items.Any(_expanded.Contains)))];
        AnchorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool Reads(int lineNumber, string text) =>
        lineNumber >= 1 && lineNumber <= _document.LineCount
        && string.Equals(_document.GetText(_document.GetLineByNumber(lineNumber)).TrimEnd(), text, StringComparison.Ordinal);

    private void OnReviewChanged(object? sender, EventArgs e) => Rebuild();

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e) => Rebuild();
}
