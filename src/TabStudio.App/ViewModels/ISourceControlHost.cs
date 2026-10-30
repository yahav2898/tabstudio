namespace TabStudio.App.ViewModels;

/// <summary>What the Git Changes window needs from the IDE around it. Implemented by the workspace.</summary>
public interface ISourceControlHost
{
    /// <summary>The in-app dialog for names and confirmations.</summary>
    PromptViewModel Prompt { get; }

    /// <summary>Saves every document with unsaved changes, before files are committed or replaced.</summary>
    /// <returns><see langword="true"/> when everything was saved.</returns>
    Task<bool> SaveDocumentsAsync();

    /// <summary>Opens a file in an editor tab.</summary>
    /// <param name="path">Full path of the file.</param>
    Task OpenFileAsync(string path);

    /// <summary>Shows a comparison in a read-only tab, replacing an earlier comparison of the same file.</summary>
    /// <param name="comparison">The comparison.</param>
    void ShowComparison(FileComparison comparison);

    /// <summary>
    /// Reads what each open document's file holds before source control starts changing files. The editors stay
    /// editable while a pull or a branch switch runs; this is the common ancestor that lets
    /// <see cref="ReloadDocumentsAsync"/> merge what was typed meanwhile instead of replacing it.
    /// </summary>
    /// <returns>The file text of each open document, by full path.</returns>
    Task<IReadOnlyDictionary<string, string>> CaptureDocumentsAsync();

    /// <summary>
    /// Brings open documents up to date after source control rewrote or deleted their files. A document edited since
    /// <paramref name="before"/> was captured keeps those edits: they are merged into the new file, with conflict
    /// markers where both changed the same lines, and the document stays unsaved.
    /// </summary>
    /// <param name="paths">Full paths of the files that changed on disk.</param>
    /// <param name="before">What <see cref="CaptureDocumentsAsync"/> read before the files changed, if it was called.</param>
    /// <returns>How many documents have conflicts between their new edits and the new files.</returns>
    Task<int> ReloadDocumentsAsync(IReadOnlyCollection<string> paths, IReadOnlyDictionary<string, string>? before = null);

    /// <summary>Reads the workspace again, after files were added, removed or replaced.</summary>
    Task ReloadWorkspaceAsync();

    /// <summary>Shows which files changed, for the Solution Explorer's glyphs.</summary>
    /// <param name="files">The status of each changed file, by full path.</param>
    void ShowSourceControlStatus(IReadOnlyDictionary<string, GitFileStatus> files);

    /// <summary>Shows a short message in the status bar.</summary>
    /// <param name="message">The message.</param>
    void ReportStatus(string message);
}
