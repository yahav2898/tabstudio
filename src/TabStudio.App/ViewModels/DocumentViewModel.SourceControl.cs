using System.Text;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

/// <summary>Source control's documents: read-only comparison tabs, and reloading files that a pull rewrote.</summary>
public sealed partial class DocumentViewModel
{
    private DocumentViewModel(FileComparison comparison, DeviceProfile profile)
        : this(comparison.TabPath, new TextFileContent(comparison.Diff.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), language: null, profile)
    {
        Comparison = comparison;

        // The tab's badge and the diff's colors follow the compared file, not the tab's made-up name.
        FileType = FileTypes.For(comparison.FilePath);
    }

    /// <summary>Raised when more of a comparison is shown, after its text changed.</summary>
    public event EventHandler? ComparisonChanged;

    /// <summary>For a "Compare with Unmodified" tab, what it compares; <see langword="null"/> for a file.</summary>
    public FileComparison? Comparison { get; private set; }

    /// <summary>Whether this is a read-only comparison tab rather than a file.</summary>
    public bool IsComparison => Comparison is not null;

    /// <summary>Creates a read-only tab that shows a comparison. Call on the UI thread.</summary>
    /// <param name="comparison">What to show.</param>
    /// <param name="profile">The device profile.</param>
    public static DocumentViewModel ForComparison(FileComparison comparison, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(profile);
        return new DocumentViewModel(comparison, profile);
    }

    /// <summary>Shows the unchanged lines a collapsed line of the comparison hides.</summary>
    /// <param name="lineIndex">The 0-based line of the comparison, which must be a collapsed one.</param>
    public void ExpandComparison(int lineIndex)
    {
        if (Comparison is not { } comparison
            || lineIndex < 0
            || lineIndex >= comparison.Diff.Lines.Count
            || comparison.Diff.Lines[lineIndex] is not { Kind: DiffLineKind.Collapsed } collapsed)
        {
            return;
        }

        var expanded = comparison.Diff.Expand(lineIndex);
        var text = string.Join('\n', expanded.Lines.Skip(lineIndex).Take(collapsed.HiddenCount).Select(l => l.Text));
        var line = Document.GetLineByNumber(lineIndex + 1);

        // The comparison changes first, so anything drawing the new text finds the lines that describe it.
        Comparison = comparison with { Diff = expanded };
        Document.Replace(line.Offset, line.Length, text);
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        ComparisonChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Takes the file's new content from disk after source control rewrote it, as one edit the user can undo.
    /// Only the part that differs is replaced, so the caret and scroll position stay where they were.
    /// </summary>
    /// <param name="text">The file's text on disk.</param>
    public void ReloadFromDisk(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var current = Document.Text;
        if (!string.Equals(current, text, StringComparison.Ordinal))
        {
            var prefix = 0;
            var shorter = Math.Min(current.Length, text.Length);
            while (prefix < shorter && current[prefix] == text[prefix])
            {
                prefix++;
            }

            var suffix = 0;
            while (suffix < shorter - prefix && current[current.Length - 1 - suffix] == text[text.Length - 1 - suffix])
            {
                suffix++;
            }

            Document.Replace(prefix, current.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
        }

        Document.UndoStack.MarkAsOriginalFile();
        RecordDiskStamp();
    }
}
