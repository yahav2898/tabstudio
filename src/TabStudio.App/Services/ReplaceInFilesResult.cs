using TabStudio.Compilation;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Services;

/// <summary>What Replace All in Files did: how many replacements in how many files, and what is left for the editors.</summary>
internal sealed class ReplaceInFilesResult
{
    /// <summary>The replaced matches, by file and position in the text before the change; at most <see cref="FileTextSearch.MaximumResults"/>.</summary>
    public List<ReferenceItem> Matches { get; } = [];

    /// <summary>How many matches were replaced, in every file.</summary>
    public int Replacements { get; private set; }

    /// <summary>How many files had matches.</summary>
    public int Files { get; private set; }

    /// <summary>The edits for open documents, which their editors apply so that Undo reverts them.</summary>
    public List<DocumentEdit> OpenDocumentEdits { get; } = [];

    /// <summary>The closed files rewritten on disk, with their new text.</summary>
    public List<(string Path, string Text)> WrittenFiles { get; } = [];

    /// <summary>The files that could not be written, each with the reason.</summary>
    public List<string> Failures { get; } = [];

    /// <summary>Counts a file's replacements and lists them.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">Its text before the change.</param>
    /// <param name="edits">The replacements, in order.</param>
    public void Add(string file, string text, IReadOnlyList<TextEdit> edits)
    {
        Files++;
        Replacements += edits.Count;
        FileTextSearch.AddMatches(file, text, edits.Select(e => (e.Start, e.Length)), Matches);
    }
}
