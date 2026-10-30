namespace TabStudio.App.ViewModels;

/// <summary>Noticing that another program changed the document's file: a formatter or git in the Terminal, say.</summary>
public sealed partial class DocumentViewModel
{
    private (long Length, DateTime WrittenUtc)? _diskStamp;

    /// <summary>
    /// Whether the file on disk changed since the editor last read or wrote it. Nothing noticed this before: a clean
    /// tab kept the old text, and its next save, or F5's save-all, put the old text back over the change.
    /// </summary>
    public bool ChangedOnDisk => _diskStamp is { } recorded && StampOf(FilePath) is { } now && now != recorded;

    /// <summary>Remembers the file as it is on disk now, after reading or writing it.</summary>
    internal void RecordDiskStamp() => _diskStamp = StampOf(FilePath);

    private static (long Length, DateTime WrittenUtc)? StampOf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.Length, info.LastWriteTimeUtc) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
