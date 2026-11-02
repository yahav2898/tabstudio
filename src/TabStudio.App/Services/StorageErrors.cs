namespace TabStudio.App.Services;

/// <summary>Recognizes a full storage, so the user is told to free space rather than shown an error number.</summary>
public static class StorageErrors
{
    // ENOSPC and EDQUOT on Linux and Android, where .NET reports the errno as the HResult; ERROR_HANDLE_DISK_FULL and
    // ERROR_DISK_FULL on Windows.
    private const int NoSpace = 28;
    private const int QuotaExceeded = 122;
    private const int WindowsHandleDiskFull = 0x27;
    private const int WindowsDiskFull = 0x70;

    /// <summary>What the user is told when the storage is full.</summary>
    public const string FullMessage = "The storage is full. Free some space (Settings > Storage), then save again; your changes are kept in the editor.";

    /// <summary>Whether <paramref name="exception"/>, or an error inside it, means there is no space left.</summary>
    /// <param name="exception">The error.</param>
    public static bool IsFull(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is IOException io && (OperatingSystem.IsWindows()
                ? (io.HResult & 0xFFFF) is WindowsHandleDiskFull or WindowsDiskFull
                : io.HResult is NoSpace or QuotaExceeded))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The free space on the storage that holds <paramref name="path"/>, or <see langword="null"/> when unknown.</summary>
    /// <param name="path">A file or folder on it, which need not exist yet.</param>
    public static long? AvailableBytes(string path)
    {
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            while (folder is not null && !Directory.Exists(folder))
            {
                folder = Path.GetDirectoryName(folder);
            }

            return folder is null ? null : new DriveInfo(folder).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
