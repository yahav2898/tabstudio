namespace TabStudio.Swarm.Coordinator;

/// <summary>
/// The hashes of package assemblies on the tablet, kept while their files stay the same, so a save only hashes the
/// app's own assemblies again.
/// </summary>
/// <remarks>Thread-safe.</remarks>
internal sealed class SwarmPackageHashes
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (long Length, DateTime Written, string Hash)> _known = new(StringComparer.Ordinal);

    /// <summary>The hash and length of a file.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public (string Hash, long Length) Of(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException($"The package assembly '{Path.GetFileName(path)}' is missing. Restore the project's packages again.", path);
        }

        lock (_gate)
        {
            if (_known.TryGetValue(path, out var known) && known.Length == info.Length && known.Written == info.LastWriteTimeUtc)
            {
                return (known.Hash, known.Length);
            }
        }

        var hash = SwarmFiles.Hash(File.ReadAllBytes(path));
        lock (_gate)
        {
            _known[path] = (info.Length, info.LastWriteTimeUtc, hash);
        }

        return (hash, info.Length);
    }
}
