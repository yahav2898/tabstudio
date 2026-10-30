using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using TabStudio.Containers.Images;

namespace TabStudio.Containers.Store;

/// <summary>What unpacking a layer did.</summary>
/// <param name="DiffId">The SHA-256 of the uncompressed tar, which the image configuration lists.</param>
/// <param name="Files">Entries written.</param>
/// <param name="Skipped">Device nodes and other entries an app cannot create, which PRoot's <c>/dev</c> replaces.</param>
public sealed record LayerResult(string DiffId, int Files, int Skipped);

/// <summary>
/// Applies an image layer to a root file system as the OCI image specification says: whiteouts (<c>.wh.name</c>)
/// remove what lower layers made, an opaque marker (<c>.wh..wh..opq</c>) hides a folder's lower contents, hard
/// links are made (or copied where Android forbids them), symbolic links and permissions are kept, and no entry can
/// write outside the root, through <c>..</c> or a link.
/// </summary>
public static class LayerUnpacker
{
    private const string WhiteoutPrefix = ".wh.";
    private const string OpaqueMarker = ".wh..wh..opq";

    /// <summary>Opens a layer blob for reading as a tar, by its media type or, failing that, its first bytes.</summary>
    /// <exception cref="NotSupportedException">The layer is zstd-compressed, which this runtime cannot read.</exception>
    public static Stream OpenTar(Stream blob, string? mediaType)
    {
        var buffered = blob.CanSeek ? blob : new BufferedStream(blob, 1 << 16);
        Span<byte> magic = stackalloc byte[4];
        var read = 0;
        if (buffered.CanSeek)
        {
            var start = buffered.Position;
            read = buffered.ReadAtLeast(magic, 4, throwOnEndOfStream: false);
            buffered.Position = start;
        }

        if (mediaType?.EndsWith("zstd", StringComparison.Ordinal) == true || (read == 4 && magic is [0x28, 0xB5, 0x2F, 0xFD]))
        {
            throw new NotSupportedException("This image's layers are compressed with zstd, which TabStudio cannot unpack yet; use a gzip build of the image.");
        }

        var gzip = mediaType?.EndsWith("gzip", StringComparison.Ordinal) == true || (read >= 2 && magic[0] == 0x1F && magic[1] == 0x8B)
            || (read == 0 && mediaType is not (MediaTypes.OciLayer or MediaTypes.DevContainerFeatureLayer));
        return gzip ? new GZipStream(buffered, CompressionMode.Decompress) : buffered;
    }

    /// <summary>Unpacks one layer into <paramref name="root"/>.</summary>
    /// <param name="blob">The layer, compressed or not.</param>
    /// <param name="mediaType">Its media type, if known.</param>
    /// <param name="root">The root file system to change.</param>
    /// <param name="expectedDiffId">The digest the image configuration lists for it; a mismatch fails.</param>
    /// <param name="cancellationToken">Cancels between entries.</param>
    public static async Task<LayerResult> ApplyAsync(Stream blob, string? mediaType, string root, string? expectedDiffId, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(root);
        root = Path.GetFullPath(root).TrimEnd('/');
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var tar = OpenTar(blob, mediaType);
        await using var hashing = new HashingStream(tar, hash);
        var created = new HashSet<string>(StringComparer.Ordinal);
        var directories = new List<(string Path, UnixFileMode Mode, DateTimeOffset Modified)>();
        var files = 0;
        var skipped = 0;
        await using (var reader = new TarReader(hashing, leaveOpen: true))
        {
            while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parts = SafePath.Split(entry.Name);
                if (parts.Count == 0)
                {
                    continue;
                }

                var name = parts[^1];
                var relative = string.Join('/', parts);
                if (name == OpaqueMarker)
                {
                    var folder = SafePath.Resolve(root, string.Join('/', parts.Take(parts.Count - 1)));
                    PruneLower(folder, string.Join('/', parts.Take(parts.Count - 1)), created);
                    continue;
                }

                if (name.StartsWith(WhiteoutPrefix, StringComparison.Ordinal))
                {
                    var hidden = SafePath.Resolve(root, string.Join('/', parts.Take(parts.Count - 1).Append(name[WhiteoutPrefix.Length..])));
                    Delete(hidden);
                    continue;
                }

                var path = SafePath.Resolve(root, relative);
                if (path == root)
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                created.Add(relative);
                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        if (!Directory.Exists(path) || new FileInfo(path).LinkTarget is not null)
                        {
                            Delete(path);
                            Directory.CreateDirectory(path);
                        }

                        directories.Add((path, entry.Mode, entry.ModificationTime));
                        break;
                    case TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile:
                        Delete(path);
                        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous))
                        {
                            if (entry.DataStream is { } data)
                            {
                                await data.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        SetMode(path, entry.Mode, isDirectory: false);
                        File.SetLastWriteTimeUtc(path, entry.ModificationTime.UtcDateTime);
                        break;
                    case TarEntryType.SymbolicLink:
                        Delete(path);
                        File.CreateSymbolicLink(path, entry.LinkName);
                        UnixFiles.TrySetModified(path, entry.ModificationTime);
                        break;
                    case TarEntryType.HardLink:
                        var target = SafePath.Resolve(root, entry.LinkName);
                        if (!File.Exists(target))
                        {
                            throw new InvalidDataException($"A layer links /{relative} to /{entry.LinkName}, which it does not contain.");
                        }

                        Delete(path);
                        if (!UnixFiles.TryHardLink(target, path))
                        {
                            File.Copy(target, path);
                            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, File.GetUnixFileMode(target));
                        }

                        break;
                    case TarEntryType.Fifo:
                        Delete(path);
                        if (!UnixFiles.TryMakeFifo(path, entry.Mode | UnixFileMode.UserRead | UnixFileMode.UserWrite))
                        {
                            skipped++;
                        }

                        break;
                    default:
                        // Character and block devices: an app cannot make them, and containers see the device's /dev.
                        skipped++;
                        continue;
                }

                files++;
            }
        }

        // Read whatever padding follows the last entry so the digest covers the whole tar.
        await hashing.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
        foreach (var (path, mode, modified) in directories.AsEnumerable().Reverse())
        {
            SetMode(path, mode, isDirectory: true);
            Directory.SetLastWriteTimeUtc(path, modified.UtcDateTime);
        }

        var diffId = "sha256:" + Convert.ToHexStringLower(hash.GetHashAndReset());
        if (expectedDiffId is not null && !string.Equals(diffId, expectedDiffId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"A layer's content ({diffId}) does not match the image configuration ({expectedDiffId}).");
        }

        return new(diffId, files, skipped);
    }

    /// <summary>
    /// The mode the file really gets. PRoot's simulated root is still this app's user to the kernel, so files keep
    /// read and write (and folders search) permission for that user; the rest of the mode is the layer's.
    /// </summary>
    public static void SetMode(string path, UnixFileMode mode, bool isDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var owner = UnixFileMode.UserRead | UnixFileMode.UserWrite | (isDirectory ? UnixFileMode.UserExecute : 0);
        try
        {
            File.SetUnixFileMode(path, mode | owner);
        }
        catch (UnauthorizedAccessException)
        {
            // A set-user-ID bit an app may not set: keep the rest.
            File.SetUnixFileMode(path, (mode & ~(UnixFileMode.SetUser | UnixFileMode.SetGroup)) | owner);
        }
    }

    /// <summary>Deletes a file, link or folder tree if it exists, without following links.</summary>
    public static void Delete(string path)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null || File.Exists(path))
        {
            if (!OperatingSystem.IsWindows())
            {
                var folder = Path.GetDirectoryName(path)!;
                if (Directory.Exists(folder))
                {
                    var mode = File.GetUnixFileMode(folder);
                    if ((mode & UnixFileMode.UserWrite) == 0)
                    {
                        File.SetUnixFileMode(folder, mode | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    }
                }
            }

            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            MakeWritable(path);
            Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>Gives this app write permission throughout a tree it is about to delete.</summary>
    public static void MakeWritable(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var current))
        {
            var mode = File.GetUnixFileMode(current);
            var wanted = mode | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            if (mode != wanted)
            {
                File.SetUnixFileMode(current, wanted);
            }

            foreach (var child in Directory.EnumerateDirectories(current))
            {
                if (new FileInfo(child).LinkTarget is null)
                {
                    pending.Push(child);
                }
            }
        }
    }

    /// <summary>Removes what a folder holds from lower layers, keeping what this layer wrote.</summary>
    private static void PruneLower(string folder, string relative, HashSet<string> created)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var child in Directory.EnumerateFileSystemEntries(folder).ToList())
        {
            var childRelative = relative.Length == 0 ? Path.GetFileName(child) : relative + "/" + Path.GetFileName(child);
            if (!created.Contains(childRelative))
            {
                Delete(child);
            }
            else if (Directory.Exists(child) && new FileInfo(child).LinkTarget is null)
            {
                PruneLower(child, childRelative, created);
            }
        }
    }

    /// <summary>Hashes what is read through it.</summary>
    private sealed class HashingStream(Stream inner, IncrementalHash hash) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            hash.AppendData(buffer[..read]);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer.Span[..read]);
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

