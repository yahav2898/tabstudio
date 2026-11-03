using System.Text.RegularExpressions;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Services;

/// <summary>
/// Find in Files and Replace in Files: every match of a <see cref="FindInFilesQuery"/> in the workspace's text files,
/// as Visual Studio's Find in Files lists them, and Replace All across them. Build output, dependencies and hidden
/// folders are skipped as the Solution Explorer skips them, and so are binary files and files over
/// <see cref="MaximumFileBytes"/>. Open documents are searched as they are in the editor, saved or not.
/// </summary>
internal static class FileTextSearch
{
    /// <summary>The most matches listed; the search stops there.</summary>
    public const int MaximumResults = 5000;

    /// <summary>The largest file searched: 4 MB. Larger files are generated or data, not code.</summary>
    public const long MaximumFileBytes = 4L * 1024 * 1024;

    /// <summary>Searches a folder for plain text.</summary>
    /// <param name="root">The workspace folder.</param>
    /// <param name="text">What to find, on one line.</param>
    /// <param name="matchCase">Whether letter case must match.</param>
    /// <param name="openDocuments">The text of open documents by full path, which wins over what is on disk.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    /// <returns>The matches, by file path and position; at most <see cref="MaximumResults"/>.</returns>
    public static IReadOnlyList<ReferenceItem> Search(string root, string text, bool matchCase, IReadOnlyDictionary<string, string>? openDocuments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        if (!FileTextMatcher.TryCreate(new FindInFilesQuery(text, matchCase), out var matcher, out var error))
        {
            throw new ArgumentException(error, nameof(text));
        }

        return Search(root, matcher, openDocuments, cancellationToken);
    }

    /// <summary>Searches a folder.</summary>
    /// <param name="root">The workspace folder.</param>
    /// <param name="matcher">What to find, and how.</param>
    /// <param name="openDocuments">The text of open documents by full path, which wins over what is on disk.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    /// <returns>The matches, by file path and position; at most <see cref="MaximumResults"/>.</returns>
    /// <exception cref="RegexMatchTimeoutException">The regular expression took too long on a file.</exception>
    public static IReadOnlyList<ReferenceItem> Search(string root, FileTextMatcher matcher, IReadOnlyDictionary<string, string>? openDocuments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(matcher);
        var results = new List<ReferenceItem>();
        foreach (var file in EnumerateFiles(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = openDocuments is not null && openDocuments.TryGetValue(file, out var open) ? open : Read(file);
            if (content is null)
            {
                continue;
            }

            AddMatches(file, content, matcher.Find(content, MaximumResults - results.Count), results);
            if (results.Count >= MaximumResults)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Replace All in Files: replaces every match in the files Search looks in. An open document is not written: its
    /// edits are returned for its editor, so they can be undone there and its unsaved changes are kept. Every other file
    /// is rewritten on disk in its own encoding, with its byte order mark and line endings as they were. Files in
    /// <paramref name="leftAlone"/> are not changed at all, and neither is a file the regular expression takes too long on.
    /// </summary>
    /// <param name="root">The workspace folder.</param>
    /// <param name="matcher">What to find, and how.</param>
    /// <param name="replacement">What replaces each match; with a regular expression, <c>$1</c> is its first group.</param>
    /// <param name="openDocuments">The text of open documents by full path.</param>
    /// <param name="leftAlone">Files open in an editor that is not a text editor, such as a notebook, by full path.</param>
    /// <param name="cancellationToken">Stops before the next file; a file being written is finished.</param>
    /// <returns>What was replaced, the edits for the open documents, and the files that could not be changed.</returns>
    public static async Task<ReplaceInFilesResult> ReplaceAsync(
        string root, FileTextMatcher matcher, string replacement, IReadOnlyDictionary<string, string>? openDocuments, IReadOnlySet<string>? leftAlone, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(replacement);
        var result = new ReplaceInFilesResult();
        foreach (var file in EnumerateFiles(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (leftAlone is not null && leftAlone.Contains(file))
            {
                continue;
            }

            if (openDocuments is not null && openDocuments.TryGetValue(file, out var open))
            {
                if (ReplacementsIn(file, open, matcher, replacement, result) is not { Count: > 0 } edits)
                {
                    continue;
                }

                result.Add(file, open, edits);
                result.OpenDocumentEdits.Add(new DocumentEdit(file, DocumentEditKind.Modified, edits));
                continue;
            }

            if (await ReadForReplaceAsync(file, cancellationToken).ConfigureAwait(false) is not { } content)
            {
                continue;
            }

            if (ReplacementsIn(file, content.Text, matcher, replacement, result) is not { Count: > 0 } changes)
            {
                continue;
            }

            var text = FileTextMatcher.Apply(content.Text, changes);
            try
            {
                // Once begun, a file is written in full: cancelling stops before the next one.
                await TextFile.WriteAsync(file, text, content.Encoding, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                continue;
            }

            result.Add(file, content.Text, changes);
            result.WrittenFiles.Add((file, text));
        }

        return result;
    }

    /// <summary>A file's replacements; <see langword="null"/>, with the file among the failures, when the regular expression takes too long on it.</summary>
    private static List<TextEdit>? ReplacementsIn(string file, string text, FileTextMatcher matcher, string replacement, ReplaceInFilesResult result)
    {
        try
        {
            return matcher.Replacements(text, replacement);
        }
        catch (RegexMatchTimeoutException)
        {
            result.Failures.Add($"{Path.GetFileName(file)}: the regular expression took too long on it, so it was left as it was");
            return null;
        }
    }

    /// <summary>A closed file's text and encoding for Replace All; <see langword="null"/> for a binary, unreadable or very large file.</summary>
    private static async Task<TextFileContent?> ReadForReplaceAsync(string file, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length > MaximumFileBytes)
            {
                return null;
            }

            var content = await TextFile.ReadAsync(file, cancellationToken).ConfigureAwait(false);
            return content.Text.AsSpan(0, Math.Min(content.Text.Length, 8000)).Contains('\0') ? null : content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Lists matches with their lines and columns, counting lines once through the file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="content">Its text.</param>
    /// <param name="matches">The matches, in order.</param>
    /// <param name="results">Where they are added.</param>
    /// <param name="limit">The most results there may be.</param>
    internal static void AddMatches(string file, string content, IEnumerable<(int Start, int Length)> matches, List<ReferenceItem> results, int limit = MaximumResults)
    {
        var lineStart = 0;
        var line = 1;
        foreach (var (index, length) in matches)
        {
            if (results.Count >= limit)
            {
                return;
            }

            // Lines are counted from the previous match on, so a file is read once.
            for (var newline = content.IndexOf('\n', lineStart); newline >= 0 && newline < index; newline = content.IndexOf('\n', lineStart))
            {
                lineStart = newline + 1;
                line++;
            }

            var lineEnd = content.IndexOf('\n', index);
            var lineText = content[lineStart..(lineEnd < 0 ? content.Length : lineEnd)].TrimEnd('\r');
            results.Add(new ReferenceItem(file, index, length, line, index - lineStart + 1, lineText, IsDefinition: false));
        }
    }

    /// <summary>The workspace's files, in path order, without ignored folders.</summary>
    private static IEnumerable<string> EnumerateFiles(string root, CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        visited.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
        while (pending.TryPop(out var folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] files;
            string[] folders;
            try
            {
                files = Directory.GetFiles(folder);
                folders = Directory.GetDirectories(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                yield return file;
            }

            // Pushed in reverse, so folders are searched in name order.
            Array.Sort(folders, StringComparer.Ordinal);
            for (var i = folders.Length - 1; i >= 0; i--)
            {
                if (!PathUtilities.IsIgnoredDirectory(Path.GetFileName(folders[i])) && PathUtilities.TryVisitDirectory(folders[i], visited))
                {
                    pending.Push(folders[i]);
                }
            }
        }
    }

    /// <summary>A text file's content; <see langword="null"/> for a binary, unreadable or very large file.</summary>
    private static string? Read(string file)
    {
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length > MaximumFileBytes)
            {
                return null;
            }

            var content = PathUtilities.ReadAllTextBounded(file, MaximumFileBytes);
            return content.AsSpan(0, Math.Min(content.Length, 8000)).Contains('\0') ? null : content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
