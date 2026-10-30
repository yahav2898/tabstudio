using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.Containers.Images;
using TabStudio.Containers.Registry;
using TabStudio.Toolchains.Downloads;

namespace TabStudio.Containers.Store;

/// <summary>An image in the store: its configuration, its layers and the names it has.</summary>
public sealed record StoredImage
{
    /// <summary>The image ID: its configuration's digest.</summary>
    public required string Id { get; init; }
    /// <summary>Names, fully qualified: <c>docker.io/library/postgres:17-alpine</c>.</summary>
    public List<string> Tags { get; init; } = [];
    /// <summary>Pinned names, <c>postgres@sha256:…</c>.</summary>
    public List<string> RepoDigests { get; init; } = [];
    /// <summary>The platform manifest's digest, for a pulled image.</summary>
    public string? ManifestDigest { get; init; }
    /// <summary>The platform it was built for.</summary>
    public required OciPlatform Platform { get; init; }
    /// <summary>The configuration.</summary>
    public required ImageConfigFile Config { get; init; }
    /// <summary>The layer blobs, bottom first.</summary>
    public List<Descriptor> Layers { get; init; } = [];
    /// <summary>The unpacked size in bytes.</summary>
    public long Size { get; init; }
    /// <summary>When it was pulled or built.</summary>
    public DateTimeOffset Stored { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>The short ID people see: the first 12 hex digits.</summary>
    [JsonIgnore] public string ShortId => Id.Length > 19 ? Id[7..19] : Id;
    /// <summary>The name to show: the first tag, or the short ID.</summary>
    [JsonIgnore] public string DisplayName => Tags.Count > 0 && ImageReference.TryParse(Tags[0], out var r, out _) ? r.Familiar : ShortId;
}

/// <summary>Where a pull is, for the long-operations UI.</summary>
/// <param name="Image">The image.</param>
/// <param name="Phase">What is happening: resolving, downloading, unpacking, done.</param>
/// <param name="Layer">The layer being worked on, 1-based; 0 before the first.</param>
/// <param name="Layers">The image's layer count.</param>
/// <param name="Received">Bytes downloaded so far, over all layers.</param>
/// <param name="Total">Bytes to download, over all layers.</param>
public readonly record struct PullProgress(string Image, string Phase, int Layer, int Layers, long Received, long Total)
{
    /// <summary>A fraction for a progress bar.</summary>
    public double Fraction => Phase == "Done" ? 1 : Total > 0 ? Math.Clamp(0.9 * Received / Total + (Phase == "Unpacking" ? 0.1 * Layer / Math.Max(Layers, 1) : 0), 0, 1) : 0;
}

/// <summary>How much space containers use, as <c>docker system df</c> reports.</summary>
/// <param name="Images">Unpacked images.</param>
/// <param name="Blobs">Downloaded layers and configurations, shared between images.</param>
/// <param name="Containers">Containers' writable layers.</param>
/// <param name="Volumes">Named volumes.</param>
/// <param name="BuildCache">Build cache.</param>
public sealed record DiskUsage(long Images, long Blobs, long Containers, long Volumes, long BuildCache)
{
    /// <summary>All of it.</summary>
    public long Total => Images + Blobs + Containers + Volumes + BuildCache;
}

/// <summary>
/// The content-addressed image store: every blob under <c>blobs/sha256/</c> by its digest, so layers shared by
/// images are downloaded and kept once; each image's layers unpacked once into its own root file system; and an
/// index of names. Nothing is used until its digest was checked.
/// </summary>
public sealed class ImageStore : IDisposable
{
    private static readonly JsonSerializerOptions IndexJson = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly SemaphoreSlim _index = new(1, 1);
    // Tags can resolve to the same image and layers; serialize commits and unpacking in this store.
    private readonly SemaphoreSlim _pull = new(1, 1);
    private List<StoredImage>? _images;

    /// <summary>Opens or creates a store.</summary>
    /// <param name="root">Its folder in the app's storage.</param>
    public ImageStore(string root)
    {
        Root = Path.GetFullPath(root);
        foreach (var folder in new[] { BlobFolder, ImagesFolder, TemporaryFolder, ContainersFolder, VolumesFolder, CacheFolder })
        {
            Directory.CreateDirectory(folder);
        }
    }

    /// <summary>The store's folder.</summary>
    public string Root { get; }
    /// <summary>Blobs, by digest.</summary>
    public string BlobFolder => Path.Combine(Root, "blobs", "sha256");
    /// <summary>Unpacked images, by ID.</summary>
    public string ImagesFolder => Path.Combine(Root, "images");
    /// <summary>Containers, by ID.</summary>
    public string ContainersFolder => Path.Combine(Root, "containers");
    /// <summary>Named volumes.</summary>
    public string VolumesFolder => Path.Combine(Root, "volumes");
    /// <summary>The build cache.</summary>
    public string CacheFolder => Path.Combine(Root, "cache");
    /// <summary>Work in progress, removed when it fails.</summary>
    public string TemporaryFolder => Path.Combine(Root, "tmp");
    private string IndexFile => Path.Combine(Root, "images.json");

    /// <summary>The path of a blob.</summary>
    public string BlobPath(string digest) => ImageReference.IsDigest(digest) ? Path.Combine(BlobFolder, digest[7..]) : throw new ArgumentException($"'{digest}' is not a digest.", nameof(digest));

    /// <summary>The unpacked root file system of an image.</summary>
    public string RootfsOf(string imageId) => Path.Combine(ImagesFolder, imageId.Replace("sha256:", "", StringComparison.Ordinal), "rootfs");

    /// <summary>Every image.</summary>
    public async Task<IReadOnlyList<StoredImage>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _index.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return [.. Load()];
        }
        finally
        {
            _index.Release();
        }
    }

    /// <summary>Finds an image by name, ID or ID prefix (at least 4 hex digits).</summary>
    public async Task<StoredImage?> FindAsync(string nameOrId, CancellationToken cancellationToken = default)
    {
        var images = await ListAsync(cancellationToken).ConfigureAwait(false);
        var id = nameOrId.StartsWith("sha256:", StringComparison.Ordinal) ? nameOrId : "sha256:" + nameOrId;
        if (nameOrId.Length >= 4 && nameOrId.All(char.IsAsciiHexDigitLower) || ImageReference.IsDigest(nameOrId))
        {
            var byId = images.Where(i => i.Id.StartsWith(id, StringComparison.Ordinal)).ToList();
            if (byId.Count == 1) return byId[0];
        }

        if (!ImageReference.TryParse(nameOrId, out var reference, out _))
        {
            return null;
        }

        return reference.Digest is { } digest
            ? images.FirstOrDefault(i => i.ManifestDigest == digest && i.RepoDigests.Any(d => d.StartsWith(reference.Name + "@", StringComparison.Ordinal)))
            : images.FirstOrDefault(i => i.Tags.Contains(reference.Canonical, StringComparer.Ordinal));
    }

    /// <summary>
    /// Pulls an image: resolves its platform, downloads the blobs it does not have yet and unpacks its layers.
    /// An image already in the store is checked against the registry and kept when unchanged.
    /// </summary>
    /// <param name="registry">The registry client.</param>
    /// <param name="reference">The image.</param>
    /// <param name="preference">Acceptable platforms, best first.</param>
    /// <param name="progress">Progress, for the long-operations UI.</param>
    /// <param name="cancellationToken">Cancels the pull; what was downloaded is kept for the next try.</param>
    public async Task<StoredImage> PullAsync(RegistryClient registry, ImageReference reference, IReadOnlyList<OciPlatform> preference, IProgress<PullProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var gate = _pull;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progress?.Report(new(reference.Familiar, "Resolving", 0, 0, 0, 0));
            var resolved = await registry.ResolveAsync(reference, preference, cancellationToken).ConfigureAwait(false);
            var manifest = resolved.Manifest;
            if (manifest.Config.MediaType is not (MediaTypes.OciConfig or MediaTypes.DockerConfig) && manifest.Config.MediaType.Length > 0)
            {
                throw new RegistryException($"{reference} is not a container image ({manifest.Config.MediaType}).");
            }

            var configBytes = await registry.GetBlobAsync(reference, manifest.Config, cancellationToken).ConfigureAwait(false);
            var config = OciJson.Read<ImageConfigFile>(configBytes);
            var id = manifest.Config.Digest;
            await WriteBlobAsync(id, configBytes, cancellationToken).ConfigureAwait(false);
            await WriteBlobAsync(resolved.Digest, resolved.ManifestBytes, cancellationToken).ConfigureAwait(false);
            if (config.Rootfs.DiffIds.Count != manifest.Layers.Count)
            {
                throw new RegistryException($"{reference} lists {manifest.Layers.Count} layers but its configuration describes {config.Rootfs.DiffIds.Count}.");
            }

            var total = manifest.Layers.Sum(l => l.Size);
            long done = 0;
            for (var i = 0; i < manifest.Layers.Count; i++)
            {
                var layer = manifest.Layers[i];
                var path = BlobPath(layer.Digest);
                if (File.Exists(path) && new FileInfo(path).Length == layer.Size)
                {
                    await using var existing = File.OpenRead(path);
                    var digest = "sha256:" + Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(existing, cancellationToken).ConfigureAwait(false));
                    if (digest == layer.Digest)
                    {
                        done += layer.Size;
                        progress?.Report(new(reference.Familiar, "Downloading", i + 1, manifest.Layers.Count, done, total));
                        continue;
                    }
                    existing.Close();
                    File.Delete(path);
                }

                var before = done;
                var layerProgress = progress is null ? null : new SyncProgress<DownloadProgress>(p =>
                    progress.Report(new(reference.Familiar, "Downloading", i + 1, manifest.Layers.Count, before + p.Received, total)));
                await registry.DownloadBlobAsync(reference, layer, path, layerProgress, cancellationToken).ConfigureAwait(false);
                done += layer.Size;
            }

            var platform = new OciPlatform { Os = config.Os, Architecture = config.Architecture, Variant = config.Variant };
            var size = await UnpackAsync(id, manifest.Layers, config.Rootfs.DiffIds, l => progress?.Report(new(reference.Familiar, "Unpacking", l, manifest.Layers.Count, total, total)), cancellationToken).ConfigureAwait(false);
            var image = await RecordAsync(new StoredImage
            {
                Id = id,
                Tags = reference.Tag is null ? [] : [reference.Canonical],
                RepoDigests = [$"{reference.Name}@{resolved.Digest}"],
                ManifestDigest = resolved.Digest,
                Platform = platform,
                Config = config,
                Layers = manifest.Layers,
                Size = size,
            }, cancellationToken).ConfigureAwait(false);
            progress?.Report(new(reference.Familiar, "Done", manifest.Layers.Count, manifest.Layers.Count, total, total));
            return image;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Unpacks layers that are in the store into an image's root file system, once.</summary>
    /// <returns>The root file system's size.</returns>
    public async Task<long> UnpackAsync(string imageId, IReadOnlyList<Descriptor> layers, IReadOnlyList<string> diffIds, Action<int>? onLayer, CancellationToken cancellationToken)
    {
        var rootfs = RootfsOf(imageId);
        var ready = rootfs + ".ready";
        if (File.Exists(ready) && Directory.Exists(rootfs))
        {
            return long.TryParse(await File.ReadAllTextAsync(ready, cancellationToken).ConfigureAwait(false), out var known) ? known : TreeCopy.Size(rootfs);
        }

        var work = Path.Combine(TemporaryFolder, Guid.NewGuid().ToString("N"));
        try
        {
            for (var i = 0; i < layers.Count; i++)
            {
                onLayer?.Invoke(i + 1);
                await using var blob = File.OpenRead(BlobPath(layers[i].Digest));
                await LayerUnpacker.ApplyAsync(blob, layers[i].MediaType, work, diffIds.Count > i ? diffIds[i] : null, cancellationToken).ConfigureAwait(false);
            }

            Directory.CreateDirectory(Path.Combine(work, "etc"));
            Directory.CreateDirectory(Path.GetDirectoryName(rootfs)!);
            if (Directory.Exists(rootfs))
            {
                LayerUnpacker.Delete(rootfs);
            }

            Directory.Move(work, rootfs);
            var size = TreeCopy.Size(rootfs);
            await File.WriteAllTextAsync(ready, size.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            return size;
        }
        catch
        {
            if (Directory.Exists(work)) LayerUnpacker.Delete(work);
            throw;
        }
    }

    /// <summary>Writes a small blob by its digest, checking it.</summary>
    public async Task WriteBlobAsync(string digest, byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(RegistryClient.Digest(bytes), digest, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"A blob does not match its digest {digest}.");
        }

        var path = BlobPath(digest);
        if (File.Exists(path)) return;
        var partial = path + "." + Guid.NewGuid().ToString("N")[..8];
        await File.WriteAllBytesAsync(partial, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(partial, path, overwrite: true);
    }

    /// <summary>Adds or updates an image, moving its names from any other image that had them.</summary>
    public async Task<StoredImage> RecordAsync(StoredImage image, CancellationToken cancellationToken = default)
    {
        await _index.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var images = Load();
            var existing = images.FirstOrDefault(i => i.Id == image.Id);
            var merged = existing is null ? image : image with
            {
                Tags = [.. existing.Tags.Union(image.Tags, StringComparer.Ordinal)],
                RepoDigests = [.. existing.RepoDigests.Union(image.RepoDigests, StringComparer.Ordinal)],
                Stored = existing.Stored,
            };
            for (var i = 0; i < images.Count; i++)
            {
                if (images[i].Id != image.Id && images[i].Tags.Any(merged.Tags.Contains))
                {
                    images[i] = images[i] with { Tags = [.. images[i].Tags.Except(merged.Tags, StringComparer.Ordinal)] };
                }
            }

            images.RemoveAll(i => i.Id == image.Id);
            images.Add(merged);
            Save(images);
            return merged;
        }
        finally
        {
            _index.Release();
        }
    }

    /// <summary>Gives an image another name, as <c>docker tag</c> does.</summary>
    public async Task<StoredImage> TagAsync(StoredImage image, ImageReference name, CancellationToken cancellationToken = default) =>
        await RecordAsync(image with { Tags = [name.Canonical] }, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Removes a name from an image, or the image itself when it has no names left or <paramref name="nameOrId"/>
    /// is its ID. Returns what was untagged and deleted.
    /// </summary>
    /// <param name="nameOrId">A name or an ID.</param>
    /// <param name="inUse">Whether a container uses an image ID; such an image is never deleted.</param>
    /// <param name="cancellationToken">Cancels before the index is changed.</param>
    public async Task<(IReadOnlyList<string> Untagged, IReadOnlyList<string> Deleted)> RemoveAsync(string nameOrId, Func<string, bool> inUse, CancellationToken cancellationToken = default)
    {
        var image = await FindAsync(nameOrId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"No such image: {nameOrId}");
        var untagged = new List<string>();
        var deleted = new List<string>();
        await _index.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var images = Load();
            var current = images.First(i => i.Id == image.Id);
            var byName = ImageReference.TryParse(nameOrId, out var reference, out _) && current.Tags.Contains(reference.Canonical, StringComparer.Ordinal);
            if (byName && current.Tags.Count > 1)
            {
                images[images.IndexOf(current)] = current with { Tags = [.. current.Tags.Where(t => t != reference!.Canonical)] };
                untagged.Add(reference!.Familiar);
            }
            else
            {
                if (inUse(current.Id))
                {
                    throw new InvalidOperationException($"{current.DisplayName} is used by a container; remove the container first.");
                }

                untagged.AddRange(current.Tags);
                images.Remove(current);
                deleted.Add(current.Id);
            }

            Save(images);
        }
        finally
        {
            _index.Release();
        }

        foreach (var id in deleted)
        {
            var rootfs = RootfsOf(id);
            File.Delete(rootfs + ".ready");
            if (Directory.Exists(Path.GetDirectoryName(rootfs)!)) LayerUnpacker.Delete(Path.GetDirectoryName(rootfs)!);
        }

        return (untagged, deleted);
    }

    /// <summary>
    /// Keeps the store tidy: deletes images without names that no container uses (all unused images when
    /// <paramref name="all"/>), blobs no image needs, and unfinished work.
    /// </summary>
    /// <returns>The bytes freed.</returns>
    public async Task<long> PruneAsync(Func<string, bool> inUse, bool all, CancellationToken cancellationToken = default)
    {
        long freed = 0;
        var images = await ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var image in images.Where(i => (all || i.Tags.Count == 0) && !inUse(i.Id)))
        {
            freed += image.Size;
            await RemoveAsync(image.Id, inUse, cancellationToken).ConfigureAwait(false);
        }

        var keep = (await ListAsync(cancellationToken).ConfigureAwait(false))
            .SelectMany(i => i.Layers.Select(l => l.Digest).Append(i.Id).Concat(i.ManifestDigest is { } m ? [m] : []))
            .Concat(BuildCacheBlobs())
            .Select(d => d[7..]).ToHashSet(StringComparer.Ordinal);
        foreach (var blob in Directory.EnumerateFiles(BlobFolder))
        {
            if (!keep.Contains(Path.GetFileName(blob)))
            {
                freed += new FileInfo(blob).Length;
                File.Delete(blob);
            }
        }

        foreach (var folder in Directory.EnumerateFileSystemEntries(TemporaryFolder))
        {
            freed += Directory.Exists(folder) ? TreeCopy.Size(folder) : new FileInfo(folder).Length;
            LayerUnpacker.Delete(folder);
        }

        return freed;
    }

    /// <summary>Space used by each part of the store.</summary>
    public async Task<DiskUsage> UsageAsync(CancellationToken cancellationToken = default)
    {
        var images = await ListAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() => new DiskUsage(
            images.Sum(i => i.Size),
            Directory.EnumerateFiles(BlobFolder).Sum(f => new FileInfo(f).Length),
            TreeCopy.Size(ContainersFolder),
            TreeCopy.Size(VolumesFolder),
            TreeCopy.Size(CacheFolder)), cancellationToken).ConfigureAwait(false);
    }

    private IEnumerable<string> BuildCacheBlobs()
    {
        foreach (var file in Directory.EnumerateFiles(CacheFolder, "*.json"))
        {
            string? layer = null;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(file));
                if (document.RootElement.TryGetProperty("Layer", out var value) && value.ValueKind == JsonValueKind.String) layer = value.GetString();
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
            }

            if (layer is not null && ImageReference.IsDigest(layer)) yield return layer;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _index.Dispose();
        _pull.Dispose();
    }

    private List<StoredImage> Load()
    {
        if (_images is not null) return _images;
        try
        {
            _images = File.Exists(IndexFile) ? JsonSerializer.Deserialize<List<StoredImage>>(File.ReadAllBytes(IndexFile), IndexJson) ?? [] : [];
        }
        catch (JsonException)
        {
            // A damaged index is rebuilt from nothing: blobs stay and are reused by the next pull.
            _images = [];
        }

        _images.RemoveAll(i => !Directory.Exists(RootfsOf(i.Id)));
        return _images;
    }

    private void Save(List<StoredImage> images)
    {
        _images = images;
        var partial = IndexFile + ".partial";
        File.WriteAllBytes(partial, JsonSerializer.SerializeToUtf8Bytes(images, IndexJson));
        File.Move(partial, IndexFile, overwrite: true);
    }

    /// <summary>A progress that reports on the calling thread, so totals stay in order.</summary>
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

