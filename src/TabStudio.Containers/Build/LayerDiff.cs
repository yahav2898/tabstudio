using System.Formats.Tar;
using System.Security.Cryptography;

namespace TabStudio.Containers.Build;

/// <summary>
/// Makes a build step's layer: the root file system is recorded before the step, compared after it, and what was
/// added or changed goes into an OCI layer tar, with a whiteout for what was deleted. Without a union file system
/// this is how a step's changes become a layer of their own.
/// </summary>
public static class LayerDiff
{
    /// <summary>One path's state: enough to see that it changed.</summary>
    public readonly record struct Entry(char Type, long Size, long Modified, UnixFileMode Mode, string? Link);

    /// <summary>Records every path under <paramref name="root"/>, not following links.</summary>
    public static Dictionary<string, Entry> Snapshot(string root)
    {
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var pending = new Stack<(string Device, string Relative)>();
        pending.Push((root, ""));
        while (pending.TryPop(out var folder))
        {
            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(folder.Device).EnumerateFileSystemInfos().ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                var relative = folder.Relative.Length == 0 ? entry.Name : folder.Relative + "/" + entry.Name;
                var mode = OperatingSystem.IsWindows() ? 0 : entry.UnixFileMode;
                if (entry.LinkTarget is { } link)
                {
                    result[relative] = new('l', 0, 0, mode, link);
                }
                else if (entry is DirectoryInfo)
                {
                    result[relative] = new('d', 0, entry.LastWriteTimeUtc.Ticks, mode, null);
                    pending.Push((entry.FullName, relative));
                }
                else
                {
                    result[relative] = new('f', ((FileInfo)entry).Length, entry.LastWriteTimeUtc.Ticks, mode, null);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Writes the layer of what changed since <paramref name="before"/> to <paramref name="destination"/>.
    /// Returns its digest and whether it holds anything.
    /// </summary>
    public static async Task<(string Digest, long Size, bool Empty)> WriteAsync(string root, Dictionary<string, Entry> before, string destination, CancellationToken cancellationToken = default)
    {
        var after = Snapshot(root);
        var changed = after.Where(e => !before.TryGetValue(e.Key, out var old) || old != e.Value)
            .Select(e => e.Key).Order(StringComparer.Ordinal).ToList();
        var deleted = before.Keys.Where(k => !after.ContainsKey(k)).ToHashSet(StringComparer.Ordinal);
        // A deleted folder needs one whiteout, not one per file it held.
        var whiteouts = deleted.Where(k => !deleted.Contains(Parent(k))).Order(StringComparer.Ordinal).ToList();
        if (changed.Count == 0 && whiteouts.Count == 0)
        {
            return ("", 0, true);
        }

        // Folders on the way to a change are written too, so the layer unpacks on its own.
        var withParents = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in changed.Concat(whiteouts))
        {
            for (var parent = Parent(path); parent.Length > 0; parent = Parent(parent))
            {
                if (after.TryGetValue(parent, out var p) && p.Type == 'd') withParents.Add(parent);
            }
        }

        foreach (var path in changed) withParents.Add(path);
        await using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous))
        {
            await using var writer = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: true);
            foreach (var path in withParents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = after[path];
                var device = Path.Combine(root, path);
                TarEntry entry = state.Type switch
                {
                    'd' => new PaxTarEntry(TarEntryType.Directory, path + "/"),
                    'l' => new PaxTarEntry(TarEntryType.SymbolicLink, path) { LinkName = state.Link! },
                    _ => new PaxTarEntry(TarEntryType.RegularFile, path),
                };
                entry.Mode = state.Mode;
                entry.ModificationTime = state.Type == 'l' ? DateTimeOffset.UnixEpoch : new DateTimeOffset(state.Modified, TimeSpan.Zero);
                if (state.Type == 'f')
                {
                    await using var data = File.OpenRead(device);
                    entry.DataStream = data;
                    await writer.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var path in whiteouts)
            {
                var parent = Parent(path);
                var name = parent.Length == 0 ? ".wh." + path : parent + "/.wh." + path[(parent.Length + 1)..];
                await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, name) { Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite }, cancellationToken).ConfigureAwait(false);
            }
        }

        await using var read = File.OpenRead(destination);
        var digest = "sha256:" + Convert.ToHexStringLower(await SHA256.HashDataAsync(read, cancellationToken).ConfigureAwait(false));
        return (digest, new FileInfo(destination).Length, false);
    }

    private static string Parent(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }
}
