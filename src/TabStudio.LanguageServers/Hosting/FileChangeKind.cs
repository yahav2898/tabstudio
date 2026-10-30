namespace TabStudio.LanguageServers.Hosting;

/// <summary>What happened to a file, as <c>workspace/didChangeWatchedFiles</c> numbers it.</summary>
public enum FileChangeKind
{
    /// <summary>The file was created.</summary>
    Created = 1,

    /// <summary>The file's content changed.</summary>
    Changed = 2,

    /// <summary>The file was deleted.</summary>
    Deleted = 3,
}
