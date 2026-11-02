using System.Security.Cryptography;
using System.Text.Json;
using NuGet.Versioning;
using TabStudio.Packages.Feeds;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Installation;

/// <summary>
/// The folder every package is extracted into once and shared by all projects, laid out like NuGet's
/// global packages folder: <c>{id}/{version}/</c>, both in lower case.
/// </summary>
/// <remarks>
/// <para>
/// A package is downloaded and extracted into a temporary folder next to its final one, and a completion marker
/// (<c>.nupkg.metadata</c>, in NuGet's format) is written last. Only then is the folder renamed into place, so a
/// package folder without the marker is always an interrupted install, and it is installed again.
/// </para>
/// <para>Several restores may install the same package at once; the first to finish wins and the others reuse it.</para>
/// </remarks>
internal sealed class GlobalPackagesFolder
{
    /// <summary>The file that marks a completely installed package.</summary>
    public const string CompletionMarkerName = ".nupkg.metadata";

    /// <summary>Uses a folder as the global packages folder. It is created when the first package is installed.</summary>
    /// <param name="root">The folder.</param>
    public GlobalPackagesFolder(string root)
    {
        Root = Path.GetFullPath(root);
    }

    /// <summary>The full path of the folder.</summary>
    public string Root { get; }

    /// <summary>The folder a package version is (or would be) installed in.</summary>
    public string GetPackageDirectory(string id, NuGetVersion version) =>
        NuGetNames.IsValidId(id)
            ? Path.Combine(Root, NuGetNames.Lower(id), NuGetNames.Lower(version))
            : throw new ArgumentException($"'{id}' is not a valid package id.", nameof(id));

    /// <summary>Whether a package version is completely installed.</summary>
    public bool IsInstalled(string id, NuGetVersion version) =>
        NuGetNames.IsValidId(id) && File.Exists(Path.Combine(GetPackageDirectory(id, version), CompletionMarkerName));

    /// <summary>The completely installed versions of a package.</summary>
    public IReadOnlyList<NuGetVersion> GetInstalledVersions(string id)
    {
        var directory = Path.Combine(Root, NuGetNames.Lower(id));
        if (!NuGetNames.IsValidId(id) || !Directory.Exists(directory))
        {
            return [];
        }

        var versions = new List<NuGetVersion>();
        foreach (var path in Directory.EnumerateDirectories(directory))
        {
            if (NuGetVersion.TryParse(Path.GetFileName(path), out var version) && File.Exists(Path.Combine(path, CompletionMarkerName)))
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    /// <summary>Reads the manifest of an installed package.</summary>
    /// <returns>The manifest, or <see langword="null"/> when the package is not installed or its manifest cannot be read.</returns>
    public PackageManifest? ReadManifest(string id, NuGetVersion version)
    {
        if (!IsInstalled(id, version))
        {
            return null;
        }

        var directory = GetPackageDirectory(id, version);
        try
        {
            var path = Directory.EnumerateFiles(directory, "*.nuspec").Order(StringComparer.Ordinal).FirstOrDefault();
            if (path is null)
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return NuspecReader.Read(stream);
        }
        catch (Exception ex) when (ex is InvalidPackageException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Downloads and extracts a package, unless it is already installed.</summary>
    /// <param name="feed">The feed to download from.</param>
    /// <param name="id">The package id.</param>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The folder the package is installed in.</returns>
    /// <exception cref="PackageSourceException">The feed could not be read, or no longer has the package.</exception>
    /// <exception cref="InvalidPackageException">The download is not a valid package, or not the one requested.</exception>
    /// <exception cref="IOException">The folder could not be written.</exception>
    public async Task<string> InstallAsync(IPackageFeed feed, string id, NuGetVersion version, CancellationToken cancellationToken)
    {
        var directory = GetPackageDirectory(id, version);
        if (IsInstalled(id, version))
        {
            return directory;
        }

        var parent = Path.GetDirectoryName(directory) ?? Root;
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(directory)}.{Guid.NewGuid():N}.partial");
        var download = staging + ".nupkg";
        try
        {
            if (!await feed.DownloadAsync(id, version, download, cancellationToken).ConfigureAwait(false))
            {
                throw new PackageSourceException(feed.Source, $"{feed.Source.Name} lists {id} {version} but did not return the package.", isUnreachable: false);
            }

            var manifest = PackageArchive.Extract(download, staging);
            if (!NuGetNames.SameId(manifest.Id, id) || manifest.Version != version)
            {
                throw new InvalidPackageException($"{feed.Source.Name} returned {manifest.Id} {manifest.Version} when asked for {id} {version}.");
            }

            await WriteCompletionMarkerAsync(staging, download, feed.Source, cancellationToken).ConfigureAwait(false);

            if (Directory.Exists(directory) && !IsInstalled(id, version))
            {
                // An interrupted install from before; replace it.
                Directory.Delete(directory, recursive: true);
            }

            try
            {
                Directory.Move(staging, directory);
            }
            catch (IOException) when (IsInstalled(id, version))
            {
                // Another restore installed the same package first. Its copy is identical.
            }

            return directory;
        }
        finally
        {
            TryDeleteFile(download);
            TryDeleteDirectory(staging);
        }
    }

    /// <summary>Writes NuGet's <c>.nupkg.metadata</c>: the format version, the package's SHA-512 and its source.</summary>
    private static async Task WriteCompletionMarkerAsync(string directory, string packagePath, PackageSource source, CancellationToken cancellationToken)
    {
        string contentHash;
        await using (var package = File.OpenRead(packagePath))
        {
            contentHash = Convert.ToBase64String(await SHA512.HashDataAsync(package, cancellationToken).ConfigureAwait(false));
        }

        await using var marker = File.Create(Path.Combine(directory, CompletionMarkerName));
        await using var writer = new Utf8JsonWriter(marker, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("version", 2);
        writer.WriteString("contentHash", contentHash);
        writer.WriteString("source", source.Location);
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file is harmless and has a unique name.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary folder is harmless and has a unique name.
        }
    }
}
