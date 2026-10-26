using System.Globalization;
using NuGet.Versioning;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages.Feeds;
using TabStudio.Packages.Installation;
using TabStudio.Packages.Uno;

namespace TabStudio.Packages;

/// <summary>
/// Uno Platform and WinUI 3 projects: the packages Uno.Sdk adds by itself, with the versions its package list gives,
/// and for a WinUI 3 project, Uno Platform's packages in place of the Windows App SDK's, which only Windows runs.
/// </summary>
public sealed partial class PackageRestorer
{
    /// <summary>The MSBuild SDK package whose package list names the versions of Uno Platform's packages.</summary>
    public const string UnoSdkPackage = "Uno.Sdk";

    /// <summary>
    /// Restores the package list of a version of <c>Uno.Sdk</c>, the MSBuild SDK of Uno Platform's projects, which is
    /// downloaded once and from then on read offline.
    /// </summary>
    /// <param name="version">The version, such as a <c>global.json</c> names under <c>msbuild-sdks</c>.</param>
    /// <param name="projectName">The project, for diagnostics.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The list, or <see langword="null"/> with the reason.</returns>
    public async Task<(UnoSdkManifest? Manifest, IReadOnlyList<BuildDiagnostic> Diagnostics)> RestoreUnoSdkAsync(string version, string projectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        if (!NuGetVersion.TryParse(version, out var parsed))
        {
            return (null, [PackageDiagnostics.Error(
                PackageDiagnosticIds.InvalidVersion,
                $"'{version}' is not a valid version of Uno.Sdk. Set a version such as \"{UnoProjects.DefaultSdkVersion}\" under msbuild-sdks in global.json.",
                projectName)]);
        }

        try
        {
            if (!_packages.IsInstalled(UnoSdkPackage, parsed))
            {
                var installed = false;
                foreach (var feed in CreateFeeds())
                {
                    var available = await feed.GetVersionsAsync(UnoSdkPackage, cancellationToken).ConfigureAwait(false);
                    if (available.Contains(parsed))
                    {
                        await _packages.InstallAsync(feed, UnoSdkPackage, parsed, cancellationToken).ConfigureAwait(false);
                        installed = true;
                        break;
                    }
                }

                if (!installed)
                {
                    return (null, [PackageDiagnostics.Error(
                        PackageDiagnosticIds.VersionNotFound,
                        $"Uno.Sdk {parsed.ToNormalizedString()} was not found on the package sources, so the project's Uno Platform packages cannot be restored.",
                        projectName)]);
                }
            }

            return (UnoSdkManifest.Load(_packages.GetPackageDirectory(UnoSdkPackage, parsed), parsed.ToNormalizedString()), []);
        }
        catch (PackageSourceException ex) when (ex.IsUnreachable)
        {
            return (null, [PackageDiagnostics.Error(
                PackageDiagnosticIds.NetworkUnreachable,
                $"Uno.Sdk {parsed.ToNormalizedString()} is not on this device yet, and {ex.PackageSource?.Name ?? "the package source"} cannot be reached to download it. Connect to the internet and restore again.",
                projectName)]);
        }
        catch (Exception ex) when (ex is PackageSourceException or InvalidPackageException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return (null, [PackageDiagnostics.Error(PackageDiagnosticIds.InvalidPackage, $"Uno.Sdk {parsed.ToNormalizedString()} could not be restored: {ex.Message}", projectName)]);
        }
    }

    /// <summary>
    /// For an Uno or WinUI 3 project, the project as Uno.Sdk builds it for TabStudio's preview: with the packages
    /// Uno.Sdk adds, and without the Windows App SDK packages of a WinUI 3 project, which Uno Platform's replace.
    /// Other projects come back as they are.
    /// </summary>
    private async Task<ProjectInfo> WithUnoPackagesAsync(ProjectInfo project, List<BuildDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        if (!UnoProjects.UsesUno(project))
        {
            return project;
        }

        var (sdkVersion, _) = UnoProjects.FindSdkVersion(project);
        var (manifest, problems) = await RestoreUnoSdkAsync(sdkVersion, project.Name, cancellationToken).ConfigureAwait(false);
        diagnostics.AddRange(problems);
        if (manifest is null)
        {
            return project;
        }

        var references = project.PackageReferences.ToList();
        if (UnoProjects.IsWinUI(project))
        {
            var windowsOnly = references.Where(r => UnoImplicitPackages.WindowsOnlyPackages.Contains(r.Name)).ToList();
            references.RemoveAll(r => UnoImplicitPackages.WindowsOnlyPackages.Contains(r.Name));
            var replaced = windowsOnly.Count == 0
                ? "the Windows App SDK"
                : string.Join(", ", windowsOnly.Select(r => r.Version is null ? r.Name : $"{r.Name} {r.Version}"));
            diagnostics.Add(new BuildDiagnostic(
                UnoPackageDiagnosticIds.WindowsAppSdkReplaced,
                DiagnosticSeverityLevel.Info,
                string.Create(CultureInfo.InvariantCulture, $"This WinUI 3 app runs on Uno Platform {manifest.UnoVersion}, which implements WinUI outside Windows: TabStudio restores Uno Platform's packages in place of {replaced}, which only Windows can run."),
                null,
                project.Name));
        }

        var adjusted = project with { PackageReferences = references };
        return adjusted with { PackageReferences = [.. references, .. UnoImplicitPackages.For(adjusted, manifest)] };
    }
}
