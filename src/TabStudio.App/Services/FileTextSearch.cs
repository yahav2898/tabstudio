using TabStudio.Compilation.Features;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Services;

/// <summary>
/// Find in Files: every line of the workspace's text files that holds some text, as Visual Studio's Find in Files
/// lists them. Build output, dependencies and hidden folders are skipped as the Solution Explorer skips them, and so
/// are binary files and files over <see cref="MaximumFileBytes"/>. Open documents are searched as they are in the
/// editor, saved or not.
/// </summary>
internal static class FileTextSearch
{
    /// <summary>The most matches listed; the search stops there.</summary>
    public const int MaximumResults = 5000;

    /// <summary>The largest file searched: 4 MB. Larger files are generated or data, not code.</summary>
    public const long MaximumFileBytes = 4L * 1024 * 1024;

    /// <summary>Searches a folder.</summary>
    /// <param name="root">The workspace folder.</param>
    /// <param name="text">What to find, on one line.</param>
    /// <param name="matchCase">Whether letter case must match.</param>
    /// <param name="openDocuments">The text of open documents by full path, which wins over what is on disk.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    /// <returns>The matches, by file path and position; at most <see cref="MaximumResults"/>.</returns>
    public static IReadOnlyList<ReferenceItem> Search(string root, string text, bool matchCase, IReadOnlyDictionary<string, string>? openDocuments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentException.ThrowIfNullOrEmpty(text);
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var results = new List<ReferenceItem>();
        foreach (var file in EnumerateFiles(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = openDocuments is not null && openDocuments.TryGetValue(file, out var open) ? open : Read(file);
            if (content is null)
            {
                continue;
            }

            AddMatches(file, content, text, comparison, results);
            if (results.Count >= MaximumResults)
            {
                results.RemoveRange(MaximumResults, results.Count - MaximumResults);
                break;
            }
        }

        return results;
    }

    private static void AddMatches(string file, string content, string text, StringComparison comparison, List<ReferenceItem> results)
    {
        var lineStart = 0;
        var line = 1;
        for (var index = content.IndexOf(text, comparison); index >= 0; index = content.IndexOf(text, index + text.Length, comparison))
        {
            // Lines are counted from the previous match on, so a file is read once.
            for (var newline = content.IndexOf('\n', lineStart); newline >= 0 && newline < index; newline = content.IndexOf('\n', lineStart))
            {
                lineStart = newline + 1;
                line++;
            }

            var lineEnd = content.IndexOf('\n', index);
            var lineText = content[lineStart..(lineEnd < 0 ? content.Length : lineEnd)].TrimEnd('\r');
            results.Add(new ReferenceItem(file, index, text.Length, line, index - lineStart + 1, lineText, IsDefinition: false));
            if (results.Count > MaximumResults)
            {
                return;
            }
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
