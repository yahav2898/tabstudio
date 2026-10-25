namespace TabStudio.SourceControl.GitHub;

/// <summary>A conversation on a line of a pull request's diff: its first comment and the replies to it, oldest first.</summary>
/// <param name="Root">The comment that started the thread.</param>
/// <param name="Replies">The replies, oldest first.</param>
public sealed record ReviewThread(ReviewComment Root, IReadOnlyList<ReviewComment> Replies)
{
    /// <summary>The file.</summary>
    public string Path => Root.Path;

    /// <summary>Which version of the file the thread's lines count in.</summary>
    public DiffSide Side => Root.Side;

    /// <summary>
    /// The line the thread belongs under: its line in the current diff, or for an outdated thread the line it was
    /// written on. <see langword="null"/> for a comment on the whole file.
    /// </summary>
    public int? Line => Root.AnchorLine;

    /// <summary>The first line of a multi-line thread, counted like <see cref="Line"/>.</summary>
    public int? StartLine => Root.IsOutdated ? Root.OriginalStartLine : Root.StartLine;

    /// <summary>Whether the lines it was written on changed since.</summary>
    public bool IsOutdated => Root.IsOutdated;

    /// <summary>Every comment, the first one first.</summary>
    public IReadOnlyList<ReviewComment> Comments => [Root, .. Replies];

    /// <summary>
    /// Groups comments into threads, following each reply to the comment that started its thread. Duplicates are
    /// dropped, and a reply whose thread is missing starts a thread of its own.
    /// </summary>
    /// <param name="comments">Comments of one pull request, in any order.</param>
    /// <returns>The threads by file, then by line, then oldest first.</returns>
    public static IReadOnlyList<ReviewThread> Group(IEnumerable<ReviewComment> comments)
    {
        ArgumentNullException.ThrowIfNull(comments);
        var byId = new Dictionary<long, ReviewComment>();
        foreach (var comment in comments)
        {
            byId.TryAdd(comment.Id, comment);
        }

        long RootOf(ReviewComment comment)
        {
            var current = comment;
            for (var depth = 0; depth < byId.Count && current.InReplyToId is { } parent && byId.TryGetValue(parent, out var next); depth++)
            {
                current = next;
            }

            return current.Id;
        }

        return [.. byId.Values
            .GroupBy(RootOf)
            .Select(g => new ReviewThread(
                byId[g.Key],
                [.. g.Where(c => c.Id != g.Key).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)]))
            .OrderBy(t => t.Path, StringComparer.Ordinal)
            .ThenBy(t => t.Line ?? 0)
            .ThenBy(t => t.Root.CreatedAt)];
    }
}
