namespace TabStudio.Toolchains.Linux;

/// <summary>Free space and folder sizes, for the checks before an install and the Store's disk usage.</summary>
public static class DiskSpace
{
    /// <summary>The bytes free on the storage a folder is on, or <see langword="null"/> when the system does not say.</summary>
    /// <param name="folder">A folder; it does not need to exist yet, its nearest existing parent is measured.</param>
    public static long? Free(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var existing = Path.GetFullPath(folder);
        while (!Directory.Exists(existing) && Path.GetDirectoryName(existing) is { } parent)
        {
            existing = parent;
        }

        try
        {
            return new DriveInfo(existing).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The bytes the files under a folder take, without following symbolic links.</summary>
    /// <param name="folder">The folder; 0 when it does not exist.</param>
    public static long Size(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!Directory.Exists(folder))
        {
            return File.Exists(folder) ? new FileInfo(folder).Length : 0;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        return new DirectoryInfo(folder).EnumerateFiles("*", options).Sum(f => f.Length);
    }

    /// <summary>A size for the UI, such as <c>18 MB</c> or <c>1.2 GB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    public static string Format(long bytes) => bytes switch
    {
        < 1_000 => $"{bytes} bytes",
        < 1_000_000 => $"{bytes / 1_000.0:0} KB",
        < 10_000_000 => $"{bytes / 1_000_000.0:0.0} MB",
        < 1_000_000_000 => $"{bytes / 1_000_000.0:0} MB",
        _ => $"{bytes / 1_000_000_000.0:0.0} GB",
    };
}
