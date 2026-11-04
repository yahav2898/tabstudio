namespace TabStudio.Containers.Store;

/// <summary>
/// Copies a root file system, keeping links, permissions and times. A container's writable layer starts as such a
/// copy of its image, so whatever the container changes, the image stays as it was pulled. Android has no union
/// file system for apps; where the storage supports it, .NET's copy clones files instead of duplicating them, and
/// <see cref="ShareAsync"/> shares an image's large program and library files with its containers.
/// </summary>
public static class TreeCopy
{
    /// <summary>The smallest file worth sharing: below it, a copy costs little and is more likely to be written.</summary>
    public const long SharedMinimumBytes = 64 * 1024;

    private const UnixFileMode WriteBits = UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

    /// <summary>
    /// The folders whose files are shared: programs and libraries, which make up most of an image and which
    /// containers run and read but do not write in place. Configuration (<c>/etc</c>), data (<c>/var</c>), and the
    /// folders applications live in (<c>/opt</c>, <c>/srv</c>, <c>/usr/src</c>, <c>/usr/share</c>, <c>/home</c>, …) are always copied.
    /// </summary>
    private static readonly string[] SharedFolders =
    [
        "bin/", "sbin/", "lib/", "lib32/", "lib64/", "libx32/",
        "usr/bin/", "usr/sbin/", "usr/lib/", "usr/lib32/", "usr/lib64/", "usr/libx32/", "usr/libexec/",
        "usr/local/bin/", "usr/local/lib/",
    ];

    /// <summary>Copies <paramref name="source"/> to <paramref name="destination"/>, which must not exist yet.</summary>
    /// <param name="source">The tree to copy.</param>
    /// <param name="destination">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the copy.</param>
    /// <returns>The bytes copied.</returns>
    public static Task<long> CopyAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        CopyTreeAsync(source, destination, share: false, cancellationToken);

    /// <summary>
    /// Gives <paramref name="destination"/> the tree of <paramref name="source"/>, as
    /// <see cref="CopyAsync(string, string, CancellationToken)"/> does, but a large program or library file
    /// (<see cref="IsShareable"/>) becomes a hard link to the source's file instead of a copy, and is made read-only
    /// for everyone: a container of a large image does not need the image's space again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A container's processes write through PRoot straight to the device's files, where TabStudio cannot see a
    /// write coming, so a shared file must not be writable at all: written in place, it would change the image and
    /// every other container of it. Without write permission the kernel refuses to open it for writing to an app,
    /// which has no root privileges, so a shared file can only be replaced (written to a new file and renamed, as
    /// package managers and <c>sed -i</c> do, or deleted), and that changes only the container's own folder.
    /// TabStudio's own writes to a container's files remove the file first, which breaks the link.
    /// </para>
    /// <para>
    /// The cost: such a file is read-only on the device, for the image and its containers, where Docker's root could
    /// write it in place. The image's own mode is kept with the file (an extended attribute), and a copy of the image,
    /// such as a build's stage (<see cref="CopyAsync"/>), gets that mode; where the file system keeps no extended
    /// attributes, the file is copied instead. A process that first gives itself write permission (<c>chmod u+w</c>)
    /// changes the permissions of the image's file too, and could then write it. Nothing is shared when TabStudio itself runs as root, which may
    /// write any file, or where the device does not let apps make hard links (Android): there every file is copied,
    /// as before.
    /// </para>
    /// </remarks>
    /// <param name="source">The tree to share, such as an image's root file system.</param>
    /// <param name="destination">Where the new tree goes; it must not exist yet.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The bytes copied; shared files take no new space and are not counted.</returns>
    public static Task<long> ShareAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        CopyTreeAsync(source, destination, share: true, cancellationToken);

    /// <summary>
    /// Whether an image's file is shared with its containers: a regular file of at least
    /// <see cref="SharedMinimumBytes"/> in one of the program and library folders, while this process is not root.
    /// </summary>
    /// <param name="root">The image's root file system.</param>
    /// <param name="path">The file.</param>
    internal static bool IsShareable(string root, string path)
    {
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            return false;
        }

        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (!SharedFolders.Any(folder => relative.StartsWith(folder, StringComparison.Ordinal)))
        {
            return false;
        }

        var info = new FileInfo(path);
        return info.LinkTarget is null && info.Length >= SharedMinimumBytes;
    }

    private static async Task<long> CopyTreeAsync(string source, string destination, bool share, CancellationToken cancellationToken)
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
            if (share && !OperatingSystem.IsWindows() && IsShareable(source, file.From) && UnixFiles.TryHardLink(file.From, file.To))
            {
                // One file for both, its permissions and times the source's. Nobody may write it in place from now
                // on, so neither the container nor the image can change the other through it. The image's own mode
                // is kept with the file first, so a copy of the image, such as a build's stage, still has it.
                var mode = File.GetUnixFileMode(file.From);
                if ((mode & WriteBits) == 0)
                {
                    return ValueTask.CompletedTask;
                }

                if (UnixFiles.TrySetSharedMode(file.From, mode))
                {
                    File.SetUnixFileMode(file.From, mode & ~WriteBits);
                    return ValueTask.CompletedTask;
                }

                // The file system cannot keep the mode: the container gets a copy of its own.
                File.Delete(file.To);
            }

            File.Copy(file.From, file.To);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(file.To, ModeOf(file.From));
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

    /// <summary>
    /// A file's mode as its image gives it. A file shared with containers (<see cref="ShareAsync"/>) is read-only on
    /// the device, for the image too, and keeps the image's mode with it: a copy gets that, so a build from the image
    /// sees the same modes whether or not a container of it was ever created.
    /// </summary>
    /// <param name="path">The file.</param>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public static UnixFileMode ModeOf(string path)
    {
        var mode = File.GetUnixFileMode(path);
        return (mode & WriteBits) == 0 && UnixFiles.SharedMode(path) is { } own ? own : mode;
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
