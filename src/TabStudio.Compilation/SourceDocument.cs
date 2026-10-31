namespace TabStudio.Compilation;

/// <summary>
/// One C# source file to compile.
/// </summary>
/// <param name="FilePath">Full path of the file. Diagnostics point back to this path.</param>
/// <param name="Text">The current text, which may include unsaved edits.</param>
public sealed record SourceDocument(string FilePath, string Text);

/// <summary>
/// Supplies the current text of source files, so that unsaved edits in the editor are compiled
/// instead of what is on disk.
/// </summary>
public interface ISourceTextProvider
{
    /// <summary>Returns the text of a file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <returns>The text of the open editor buffer if the file is open, otherwise the file on disk.</returns>
    string GetText(string filePath);
}

/// <summary>Reads source files straight from disk.</summary>
public sealed class FileSystemTextProvider : ISourceTextProvider
{
    /// <summary>A shared instance.</summary>
    public static FileSystemTextProvider Instance { get; } = new();

    /// <inheritdoc />
    public string GetText(string filePath) => TabStudio.Core.Workspaces.PathUtilities.ReadAllTextBounded(filePath);
}
