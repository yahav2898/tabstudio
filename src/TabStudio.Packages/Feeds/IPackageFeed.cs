using NuGet.Versioning;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Feeds;

/// <summary>
/// Reads packages from one <see cref="PackageSource"/>.
/// </summary>
/// <remarks>Every method throws <see cref="PackageSourceException"/> when the source cannot be read.</remarks>
internal interface IPackageFeed
{
    /// <summary>The source this feed reads.</summary>
    PackageSource Source { get; }

    /// <summary>Lists every version of a package the source has.</summary>
    /// <param name="id">The package id. Case-insensitive.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The versions, or an empty list when the source does not have the package.</returns>
    Task<IReadOnlyList<NuGetVersion>> GetVersionsAsync(string id, CancellationToken cancellationToken);

    /// <summary>Reads the manifest (<c>.nuspec</c>) of one package version.</summary>
    /// <param name="id">The package id.</param>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The manifest, or <see langword="null"/> when the source does not have that version.</returns>
    Task<PackageManifest?> GetManifestAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

    /// <summary>Downloads a package (<c>.nupkg</c>) to a file.</summary>
    /// <param name="id">The package id.</param>
    /// <param name="version">The version.</param>
    /// <param name="destinationPath">The file to write. It is created or overwritten.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns><see langword="false"/> when the source does not have that version.</returns>
    Task<bool> DownloadAsync(string id, NuGetVersion version, string destinationPath, CancellationToken cancellationToken);
}
