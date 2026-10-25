using NuGet.Versioning;
using TabStudio.Core.Diagnostics;
using TabStudio.Packages.Feeds;
using TabStudio.Packages.Installation;

namespace TabStudio.Packages;

/// <summary>A package the .NET SDK brings to a project by itself, such as Blazor's scripts or .NET for WebAssembly.</summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The version, normalized.</param>
/// <param name="DirectoryPath">Where it is extracted in the global packages folder.</param>
public sealed record SdkPack(string Id, string Version, string DirectoryPath);

/// <summary>
/// The packages web apps need beyond their references, which the SDK adds itself: Blazor's scripts for apps with Razor
/// components, and the runtime that runs .NET in the browser for Blazor WebAssembly. They are restored from the same
/// sources into the same packages folder, once, and from then on they work offline.
/// </summary>
public sealed partial class PackageRestorer
{
    /// <summary>The package with Blazor's scripts, which the Web SDK adds to apps with <c>.razor</c> files.</summary>
    public const string AspNetCoreWebAssetsPackage = "Microsoft.AspNetCore.App.Internal.Assets";

    /// <summary>The runtime pack of .NET for the browser, which the Blazor WebAssembly SDK adds.</summary>
    public const string BrowserRuntimePackage = "Microsoft.NETCore.App.Runtime.Mono.browser-wasm";

    /// <summary>
    /// Restores Blazor's scripts (<c>_framework/blazor.web.js</c> and <c>blazor.server.js</c>) for the version of
    /// ASP.NET Core an app runs on.
    /// </summary>
    /// <param name="aspNetCoreVersion">The version of ASP.NET Core, such as a restore's <c>Microsoft.AspNetCore.App</c> framework version.</param>
    /// <param name="projectName">The project, for diagnostics.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The package, or <see langword="null"/> and the reason when it could not be restored.</returns>
    public Task<(SdkPack? Pack, IReadOnlyList<BuildDiagnostic> Diagnostics)> RestoreAspNetCoreWebAssetsAsync(string aspNetCoreVersion, string projectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(aspNetCoreVersion);
        var version = NuGetVersion.TryParse(aspNetCoreVersion, out var parsed) ? parsed : null;
        return RestoreSdkPackAsync(AspNetCoreWebAssetsPackage, version, "Blazor's scripts", projectName, cancellationToken);
    }

    /// <summary>
    /// Restores .NET for WebAssembly, the newest release for the target framework's .NET version, unless one is
    /// installed already.
    /// </summary>
    /// <param name="projectName">The project, for diagnostics.</param>
    /// <param name="cancellationToken">Cancels the download, about 25 MB the first time.</param>
    /// <returns>The package, or <see langword="null"/> and the reason when it could not be restored.</returns>
    public Task<(SdkPack? Pack, IReadOnlyList<BuildDiagnostic> Diagnostics)> RestoreBrowserRuntimeAsync(string projectName, CancellationToken cancellationToken = default) =>
        RestoreSdkPackAsync(BrowserRuntimePackage, exactVersion: null, ".NET for WebAssembly", projectName, cancellationToken);

    private async Task<(SdkPack? Pack, IReadOnlyList<BuildDiagnostic> Diagnostics)> RestoreSdkPackAsync(
        string id,
        NuGetVersion? exactVersion,
        string displayName,
        string projectName,
        CancellationToken cancellationToken)
    {
        var major = _framework.Version.Major;
        bool Wanted(NuGetVersion v) => exactVersion is null ? v.Major == major && !v.IsPrerelease : v == exactVersion;

        var installed = _packages.GetInstalledVersions(id).Where(Wanted).Max();
        if (installed is not null)
        {
            return (Pack(id, installed), []);
        }

        try
        {
            foreach (var feed in CreateFeeds())
            {
                var available = await feed.GetVersionsAsync(id, cancellationToken).ConfigureAwait(false);
                var best = available.Where(Wanted).Max()
                    ?? (exactVersion is null ? null : available.Where(v => v.Major == exactVersion.Major && !v.IsPrerelease).Max());
                if (best is not null)
                {
                    await _packages.InstallAsync(feed, id, best, cancellationToken).ConfigureAwait(false);
                    return (Pack(id, best), []);
                }
            }
        }
        catch (PackageSourceException ex) when (ex.IsUnreachable)
        {
            return (null, [PackageDiagnostics.Error(
                PackageDiagnosticIds.NetworkUnreachable,
                $"{displayName} ({id}) is not on this device yet, and {ex.PackageSource?.Name ?? "the package source"} cannot be reached to download it. Connect to the internet and run the app again.",
                projectName)]);
        }
        catch (Exception ex) when (ex is PackageSourceException or InvalidPackageException or IOException or UnauthorizedAccessException)
        {
            return (null, [PackageDiagnostics.Error(PackageDiagnosticIds.NetworkUnreachable, $"{displayName} ({id}) could not be downloaded: {ex.Message}", projectName)]);
        }

        return (null, [PackageDiagnostics.Error(
            PackageDiagnosticIds.PackageNotFound,
            $"No release of {displayName} ({id}) for .NET {major.ToString(System.Globalization.CultureInfo.InvariantCulture)} was found on the package sources.",
            projectName)]);
    }

    private SdkPack Pack(string id, NuGetVersion version) =>
        new(id, version.ToNormalizedString(), _packages.GetPackageDirectory(id, version));
}
