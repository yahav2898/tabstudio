using System.Text;
using TabStudio.App.Editing;

namespace TabStudio.App.ViewModels;

/// <summary>What a "Compare with Unmodified" tab shows: a file's changes since the last sync with GitHub.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="RelativePath">The path in the repository, with <c>/</c> separators.</param>
/// <param name="BaseLabel">Which version the file is compared with, such as "main @ 1a2b3c4".</param>
/// <param name="Diff">The differences.</param>
public sealed record FileComparison(string FilePath, string RelativePath, string BaseLabel, InlineDiff Diff)
{
    /// <summary>The tab title, such as "Program.cs vs. Unmodified".</summary>
    public string Title => $"{Path.GetFileName(FilePath)} vs. Unmodified";

    /// <summary>
    /// The path the tab is known by: next to the file, named after the title. It never exists on disk, so it
    /// cannot be confused with the file's own tab, and sessions do not reopen it.
    /// </summary>
    public string TabPath => Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, Title);

    /// <summary>Reads a version of a file as text for comparing, the way the editor would open it.</summary>
    /// <param name="content">The bytes, or <see langword="null"/> when that version does not exist.</param>
    /// <param name="text">The text; empty for a missing version.</param>
    /// <returns><see langword="false"/> for binary content, which cannot be compared line by line.</returns>
    public static bool TryReadText(byte[]? content, out string text)
    {
        text = string.Empty;
        if (content is null)
        {
            return true;
        }

        var (encoding, preamble) = content switch
        {
            [0xEF, 0xBB, 0xBF, ..] => (Encoding.UTF8, 3),
            [0xFF, 0xFE, ..] => (Encoding.Unicode, 2),
            [0xFE, 0xFF, ..] => (Encoding.BigEndianUnicode, 2),
            _ => (Encoding.UTF8, 0),
        };
        if (preamble == 0 && content.AsSpan(0, Math.Min(content.Length, 8000)).Contains((byte)0))
        {
            return false;
        }

        text = encoding.GetString(content, preamble, content.Length - preamble);
        return true;
    }
}
