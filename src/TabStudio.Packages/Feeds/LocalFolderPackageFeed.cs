using NuGet.Versioning;
using TabStudio.Packages.Installation;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Feeds;

/// <summary>
/// Reads packages from a folder of <c>.nupkg</c> files, in any layout: flat, or in subfolders such as <c>{id}/{version}/</c>.
/// </summary>
/// <remarks>
/// File names are not trusted to tell the id and version apart (<c>A.B.1.0.0.nupkg</c> is ambiguous), so the first
/// request reads the manifest of every package in the folder and keeps an index. Files that are not valid packages
/// are skipped.
/// </remarks>
internal sealed class LocalFolderPackageFeed : IPackageFeed
{
    private readonly Lazy<Dictionary<string, Dictionary<NuGetVersion, LocalPackage>>> _index;

    /// <summary>Creates a feed for a local folder.</summary>
    /// <param name="source">The source; its location is the folder.</param>
    public LocalFolderPackageFeed(PackageSource source)
    {
        Source = source;
        _index = new(BuildIndex, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public PackageSource Source { get; }

    /// <inheritdoc />
    public Task<IReadOnlyList<NuGetVersion>> GetVersionsAsync(string id, CancellationToken cancellationToken)
    {
        IReadOnlyList<NuGetVersion> versions = _index.Value.TryGetValue(id, out var packages)
            ? packages.Keys.ToList()
            : [];
        return Task.FromResult(versions);
    }

    /// <inheritdoc />
    public Task<PackageManifest?> GetManifestAsync(string id, NuGetVersion version, CancellationToken cancellationToken) =>
        Task.FromResult(Find(id, version)?.Manifest);

    /// <inheritdoc />
    public async Task<bool> DownloadAsync(string id, NuGetVersion version, string destinationPath, CancellationToken cancellationToken)
    {
        if (Find(id, version) is not { } package)
        {
            return false;
        }

        try
        {
            await using var source = new FileStream(package.Path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (FileNotFoundException ex)
        {
            throw new PackageSourceException(Source, $"The package file '{package.Path}' of {Source.Name} was removed during the restore.", isUnreachable: false, ex);
        }
    }

    private LocalPackage? Find(string id, NuGetVersion version) =>
        _index.Value.TryGetValue(id, out var packages) && packages.TryGetValue(version, out var package) ? package : null;

    private Dictionary<string, Dictionary<NuGetVersion, LocalPackage>> BuildIndex()
    {
        var root = Source.LocalPath;
        if (!Directory.Exists(root))
        {
            throw new PackageSourceException(Source, $"The package folder '{root}' of {Source.Name} does not exist.", isUnreachable: false);
        }

        var index = new Dictionary<string, Dictionary<NuGetVersion, LocalPackage>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(root, "*.nupkg", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Order(StringComparer.Ordinal))
        {
            PackageManifest manifest;
            try
            {
                manifest = PackageArchive.ReadManifest(file);
            }
            catch (Exception ex) when (ex is InvalidPackageException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (!index.TryGetValue(manifest.Id, out var versions))
            {
                versions = new Dictionary<NuGetVersion, LocalPackage>();
                index[manifest.Id] = versions;
            }

            versions.TryAdd(manifest.Version, new LocalPackage(file, manifest));
        }

        return index;
    }

    /// <summary>A package file in the folder and its manifest.</summary>
    private sealed record LocalPackage(string Path, PackageManifest Manifest);
}
