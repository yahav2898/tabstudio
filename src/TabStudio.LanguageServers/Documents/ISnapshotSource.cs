namespace TabStudio.LanguageServers.Documents;

/// <summary>Gives the current text of any file: an open document's, or the file's on disk.</summary>
internal interface ISnapshotSource
{
    /// <summary>The current text of a file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <returns>Its snapshot, or <see langword="null"/> when the file does not exist or cannot be read.</returns>
    TextSnapshot? GetSnapshot(string filePath);
}
