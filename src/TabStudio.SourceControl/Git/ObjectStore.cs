using System.IO.Compression;
using System.Text;

namespace TabStudio.SourceControl.Git;

/// <summary>
/// A Git object database, as <c>.git/objects</c> holds it: loose objects, packs with their indexes, and the
/// databases <c>info/alternates</c> names. TabStudio reads and writes it the way Git does, so a clone's
/// <c>.git</c> stays usable by Git in the Terminal.
/// </summary>
internal sealed class ObjectStore
{
    private readonly List<ObjectStore> _alternates = [];
    private readonly Lock _gate = new();
    private List<PackFile> _packs = [];

    /// <summary>Opens a database; the folder is created when it does not exist.</summary>
    /// <param name="directory">The <c>objects</c> folder.</param>
    public ObjectStore(string directory)
        : this(directory, 0)
    {
    }

    private ObjectStore(string directory, int depth)
    {
        Directory = Path.GetFullPath(directory);
        System.IO.Directory.CreateDirectory(Path.Combine(Directory, "pack"));
        LoadPacks();
        var alternates = Path.Combine(Directory, "info", "alternates");
        if (depth < 5 && File.Exists(alternates))
        {
            foreach (var line in File.ReadAllLines(alternates))
            {
                var path = line.Trim();
                if (path.Length == 0 || path.StartsWith('#'))
                {
                    continue;
                }

                var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Directory, path));
                if (System.IO.Directory.Exists(full))
                {
                    _alternates.Add(new ObjectStore(full, depth + 1));
                }
            }
        }
    }

    /// <summary>The <c>objects</c> folder.</summary>
    public string Directory { get; }

    /// <summary>Whether the database holds an object.</summary>
    public bool Contains(string id)
    {
        if (File.Exists(LoosePath(id)))
        {
            return true;
        }

        var bytes = GitHash.FromHex(id);
        return Packs().Any(p => p.TryFind(bytes, out _)) || _alternates.Any(a => a.Contains(id));
    }

    /// <summary>Reads an object; <see langword="null"/> when the database does not hold it.</summary>
    public GitObject? TryRead(string id)
    {
        var loose = LoosePath(id);
        if (File.Exists(loose))
        {
            return ReadLoose(loose);
        }

        var bytes = GitHash.FromHex(id);
        foreach (var pack in Packs())
        {
            if (pack.TryFind(bytes, out var offset))
            {
                return pack.Read(offset, TryRead);
            }
        }

        foreach (var alternate in _alternates)
        {
            if (alternate.TryRead(id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Reads an object.</summary>
    /// <exception cref="MissingObjectException">The database does not hold it.</exception>
    public GitObject Read(string id) => TryRead(id) ?? throw new MissingObjectException(id);

    /// <summary>
    /// Copies a blob into a stream without holding it in memory when it is stored whole, as large files are.
    /// </summary>
    /// <returns>Whether the database holds the blob.</returns>
    public bool TryCopyBlob(string id, Stream destination)
    {
        var bytes = GitHash.FromHex(id);
        foreach (var pack in Packs())
        {
            if (!pack.TryFind(bytes, out var offset))
            {
                continue;
            }

            using var whole = pack.OpenWhole(offset, out var kind, out _);
            if (whole is not null && kind == GitObjectKind.Blob)
            {
                whole.CopyTo(destination);
                return true;
            }

            break;
        }

        if (TryRead(id) is not { Kind: GitObjectKind.Blob } blob)
        {
            return false;
        }

        destination.Write(blob.Content);
        return true;
    }

    /// <summary>Stores an object as a loose object, unless the database has it.</summary>
    /// <returns>The object's name.</returns>
    public string Write(GitObjectKind kind, ReadOnlySpan<byte> content)
    {
        var id = GitHash.Of(kind, content);
        var path = LoosePath(id);
        if (File.Exists(path) || Contains(id))
        {
            return id;
        }

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $"tmp_obj_{Guid.NewGuid():N}");
        using (var file = File.Create(temporary))
        using (var deflate = new ZLibStream(file, CompressionLevel.Fastest))
        {
            deflate.Write(GitHash.Header(kind, content.Length));
            deflate.Write(content);
        }

        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            File.Delete(temporary);
        }

        return id;
    }

    /// <summary>
    /// Indexes a complete pack and moves it into the database with its index, as <c>git index-pack</c> and
    /// <c>git fetch</c> do. A pack fetched with a filter is marked as a promisor pack, so Git knows the objects it left
    /// out can be fetched from the remote.
    /// </summary>
    /// <param name="packPath">The downloaded pack; it is moved.</param>
    /// <param name="promisor">Whether to mark it as a promisor pack.</param>
    /// <param name="cancellationToken">Cancels the indexing.</param>
    /// <returns>The objects the pack holds.</returns>
    public IReadOnlyList<string> AddPack(string packPath, bool promisor, CancellationToken cancellationToken)
    {
        var indexed = PackIndexer.Index(packPath, TryRead, cancellationToken);
        var name = Path.Combine(Directory, "pack", $"pack-{indexed.Checksum}");
        if (!File.Exists(name + ".pack"))
        {
            File.Move(packPath, name + ".pack");
            File.WriteAllBytes(name + ".idx.tmp", indexed.Index);
            File.Move(name + ".idx.tmp", name + ".idx", overwrite: true);
        }
        else
        {
            File.Delete(packPath);
        }

        if (promisor && !File.Exists(name + ".promisor"))
        {
            File.WriteAllText(name + ".promisor", string.Empty);
        }

        LoadPacks();
        return indexed.Ids;
    }

    /// <summary>
    /// Keeps the objects that arrived whole from a pack that ended early, as loose objects, so a broken download
    /// does not have to fetch them again; the pack itself is deleted.
    /// </summary>
    /// <returns>The objects kept.</returns>
    public IReadOnlyList<string> Salvage(string packPath, CancellationToken cancellationToken)
    {
        var kept = new List<string>();
        try
        {
            PackIndexer.Salvage(packPath, TryRead, (id, value) =>
            {
                Write(value.Kind, value.Content);
                kept.Add(id);
            }, cancellationToken);
        }
        finally
        {
            File.Delete(packPath);
        }

        return kept;
    }

    private List<PackFile> Packs()
    {
        lock (_gate)
        {
            return _packs;
        }
    }

    private void LoadPacks()
    {
        var packs = new List<PackFile>();
        foreach (var index in System.IO.Directory.EnumerateFiles(Path.Combine(Directory, "pack"), "*.idx"))
        {
            try
            {
                if (PackFile.Open(index) is { } pack)
                {
                    packs.Add(pack);
                }
            }
            catch (IOException)
            {
                // A pack Git is writing at this moment is picked up next time.
            }
        }

        lock (_gate)
        {
            _packs = packs;
        }
    }

    private string LoosePath(string id) => Path.Combine(Directory, id[..2], id[2..]);

    private static GitObject ReadLoose(string path)
    {
        using var file = File.OpenRead(path);
        using var inflate = new ZLibStream(file, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        inflate.CopyTo(buffer);
        var data = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var space = data.IndexOf((byte)' ');
        var nul = data.IndexOf((byte)0);
        if (space <= 0 || nul <= space || GitHash.Parse(Encoding.ASCII.GetString(data[..space])) is not { } kind)
        {
            throw new InvalidDataException($"The loose object {path} is damaged.");
        }

        return new GitObject(kind, data[(nul + 1)..].ToArray());
    }
}
