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

    /// <summary>Brings open documents up to date after source control rewrote or deleted their files.</summary>
    /// <param name="paths">Full paths of the files that changed on disk.</param>
    Task ReloadDocumentsAsync(IReadOnlyCollection<string> paths);

    /// <summary>Reads the workspace again, after files were added, removed or replaced.</summary>
    Task ReloadWorkspaceAsync();

    /// <summary>Shows which files changed, for the Solution Explorer's glyphs.</summary>
    /// <param name="files">The status of each changed file, by full path.</param>
    void ShowSourceControlStatus(IReadOnlyDictionary<string, GitFileStatus> files);

    /// <summary>Shows a short message in the status bar.</summary>
    /// <param name="message">The message.</param>
    void ReportStatus(string message);
}
