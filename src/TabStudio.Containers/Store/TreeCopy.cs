namespace TabStudio.Containers.Store;

/// <summary>
/// Copies a root file system, keeping links, permissions and times. A container's writable layer starts as such a
/// copy of its image, so whatever the container changes, the image stays as it was pulled. Android has no union
/// file system for apps; where the storage supports it, .NET's copy clones files instead of duplicating them.
/// </summary>
public static class TreeCopy
{
    /// <summary>Copies <paramref name="source"/> to <paramref name="destination"/>, which must not exist yet.</summary>
    /// <returns>The bytes copied.</returns>
    public static async Task<long> CopyAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        var files = new List<(string From, string To)>();
        var directories = new List<(string From, string To)>();
        Directory.CreateDirectory(destination);
        directories.Add((source, destination));
        var pending = new Stack<(string From, string To)>();
        pending.Push((source, destination));
        while (pending.TryPop(out var folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in new DirectoryInfo(folder.From).EnumerateFileSystemInfos())
            {
                var target = Path.Combine(folder.To, entry.Name);
                if (entry.LinkTarget is { } link)
                {
                    File.CreateSymbolicLink(target, link);
                }
                else if (entry is DirectoryInfo)
                {
                    Directory.CreateDirectory(target);
                    directories.Add((entry.FullName, target));
                    pending.Push((entry.FullName, target));
                }
                else if (UnixFiles.IsSpecial(entry.FullName))
                {
                    // A FIFO or socket: make a FIFO where possible, as the image had. .NET sees a FIFO as a file, and
                    // File.Copy opened it and waited for a writer forever: creating a mysql:8.4 container never ended.
                    UnixFiles.TryMakeFifo(target, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                else
                {
                    files.Add((entry.FullName, target));
                }
            }
        }

        long bytes = 0;
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4), CancellationToken = cancellationToken }, (file, _) =>
        {
            File.Copy(file.From, file.To);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(file.To, File.GetUnixFileMode(file.From));
            }

            File.SetLastWriteTimeUtc(file.To, File.GetLastWriteTimeUtc(file.From));
            Interlocked.Add(ref bytes, new FileInfo(file.To).Length);
            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);

        foreach (var (from, to) in directories.AsEnumerable().Reverse())
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(to, File.GetUnixFileMode(from));
            }

            Directory.SetLastWriteTimeUtc(to, Directory.GetLastWriteTimeUtc(from));
        }

        return bytes;
    }

    /// <summary>The bytes a folder tree takes, not following links.</summary>
    public static long Size(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        long total = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(folder));
        while (pending.TryPop(out var current))
        {
            try
            {
                foreach (var entry in current.EnumerateFileSystemInfos())
                {
                    if (entry.LinkTarget is not null)
                    {
                        continue;
                    }

                    if (entry is DirectoryInfo directory)
                    {
                        pending.Push(directory);
                    }
                    else if (entry is FileInfo file)
                    {
                        total += file.Length;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return total;
    }
}
