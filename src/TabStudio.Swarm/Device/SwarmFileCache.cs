namespace TabStudio.Swarm.Device;

/// <summary>
/// The files of the builds a device received, named by the SHA-256 of their bytes, so a build after a save only needs
/// the files that changed. Package assemblies keep their file names, by which programs find them.
/// </summary>
/// <remarks>
/// Assemblies are kept under <c>Files/</c>, package assemblies under <c>Packages/&lt;hash&gt;/&lt;file name&gt;</c>. When
/// the cache grows past its limit, the files used least recently go. Thread-safe.
/// </remarks>
internal sealed class SwarmFileCache
{
    private readonly string _files;
    private readonly string _packages;
    private readonly long _limit;
    private readonly Lock _gate = new();

    /// <summary>Creates a cache in a folder, keeping what is already there.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="limit">How many bytes it keeps.</param>
    public SwarmFileCache(string directory, long limit)
    {
        _files = Path.Combine(directory, "Files");
        _packages = Path.Combine(directory, "Packages");
        _limit = limit;
        Directory.CreateDirectory(_files);
        Directory.CreateDirectory(_packages);
    }

    /// <summary>Whether the cache has a file.</summary>
    /// <param name="hash">The file's hash.</param>
    public bool Has(string hash)
    {
        if (!SwarmFiles.IsHash(hash))
        {
            return false;
        }

        lock (_gate)
        {
            return File.Exists(FilePath(hash)) || PackageFile(hash) is not null;
        }
    }

    /// <summary>Keeps a file the tablet sent, after checking that it is what its hash says.</summary>
    /// <param name="hash">The hash the tablet gave it.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <param name="packageFileName">Its file name, for a package assembly; <see langword="null"/> for the app's own assemblies.</param>
    /// <exception cref="InvalidDataException">The bytes do not match the hash.</exception>
    public void Store(string hash, byte[] bytes, string? packageFileName)
    {
        if (!SwarmFiles.IsHash(hash) || SwarmFiles.Hash(bytes) != hash)
        {
            throw new InvalidDataException("A file of the build arrived damaged. Run the app again.");
        }

        lock (_gate)
        {
            if (packageFileName is not null)
            {
                var folder = Path.Combine(_packages, hash);
                Directory.CreateDirectory(folder);
                WriteAtomically(Path.Combine(folder, SafeName(packageFileName)), bytes);
            }
            else
            {
                WriteAtomically(FilePath(hash), bytes);
            }
        }
    }

    /// <summary>Reads one of the app's own assemblies.</summary>
    /// <param name="hash">The file's hash.</param>
    /// <exception cref="FileNotFoundException">The cache does not have it.</exception>
    public byte[] Read(string hash)
    {
        lock (_gate)
        {
            var path = FilePath(hash);
            if (!File.Exists(path))
            {
                // The bytes may have arrived as a package of another build.
                path = PackageFile(hash) ?? throw new FileNotFoundException("A file of the build is missing from this device. Run the app again.", hash);
            }

            Touch(path);
            return File.ReadAllBytes(path);
        }
    }

    /// <summary>Where a package assembly is, with its file name, so a program loads it by name.</summary>
    /// <param name="hash">The file's hash.</param>
    /// <param name="fileName">Its file name, such as <c>CommunityToolkit.Mvvm.dll</c>.</param>
    /// <exception cref="FileNotFoundException">The cache does not have it.</exception>
    public string PackagePath(string hash, string fileName)
    {
        lock (_gate)
        {
            var folder = Path.Combine(_packages, hash);
            var path = Path.Combine(folder, SafeName(fileName));
            if (!File.Exists(path))
            {
                var source = File.Exists(FilePath(hash)) ? FilePath(hash) : PackageFile(hash)
                    ?? throw new FileNotFoundException("A package of the build is missing from this device. Run the app again.", fileName);
                Directory.CreateDirectory(folder);
                File.Copy(source, path, overwrite: true);
            }

            Touch(path);
            return path;
        }
    }

    /// <summary>Deletes the files used least recently until the cache is within its limit, keeping the ones in use.</summary>
    /// <param name="inUse">The hashes of the files the running build needs.</param>
    public void Prune(IReadOnlySet<string> inUse)
    {
        lock (_gate)
        {
            var entries = Directory.EnumerateFiles(_files)
                .Select(path => (Path: path, Hash: Path.GetFileName(path), Info: new FileInfo(path)))
                .Concat(Directory.EnumerateDirectories(_packages)
                    .SelectMany(folder => Directory.EnumerateFiles(folder).Select(path => (Path: path, Hash: Path.GetFileName(folder), Info: new FileInfo(path)))))
                .ToList();
            var total = entries.Sum(e => e.Info.Length);
            foreach (var entry in entries.Where(e => !inUse.Contains(e.Hash)).OrderBy(e => e.Info.LastWriteTimeUtc))
            {
                if (total <= _limit)
                {
                    return;
                }

                try
                {
                    File.Delete(entry.Path);
                    total -= entry.Info.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A file a program still holds open on this platform; it goes next time.
                }
            }
        }
    }

    private string FilePath(string hash) => Path.Combine(_files, hash);

    private string? PackageFile(string hash)
    {
        var folder = Path.Combine(_packages, hash);
        return Directory.Exists(folder) ? Directory.EnumerateFiles(folder).FirstOrDefault() : null;
    }

    private static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            ? throw new InvalidDataException($"'{fileName}' is not a valid file name.")
            : name;
    }

    private static void WriteAtomically(string path, byte[] bytes)
    {
        var temporary = path + ".part";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    private static void Touch(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only the order of pruning depends on it.
        }
    }
}
