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

    /// <summary>Whether a save is writing the file now; it looks unsaved-free while it does.</summary>
    internal bool IsSaving { get; private set; }

    /// <summary>
    /// Takes the file's new content from disk, in the encoding it now has: converted to UTF-8 in the Terminal, a
    /// reloaded tab kept saving in its old encoding and undid the conversion.
    /// </summary>
    /// <param name="content">The file as read now.</param>
    internal void ReloadFromDisk(Services.TextFileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _file = content;
        ReloadFromDisk(content.Text);
        OnPropertyChanged(nameof(EncodingLabel));
        OnPropertyChanged(nameof(NewlineLabel));
    }

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
