namespace TabStudio.Packages;

/// <summary>
/// The ids of the diagnostics a restore reports. Problems NuGet also knows use NuGet's own codes, so that
/// documentation and search results for them apply; problems specific to TabStudio use <c>TS3xxx</c>.
/// </summary>
public static class PackageDiagnosticIds
{
    /// <summary>A package reference has no version, so the lowest available version is used.</summary>
    public const string MissingVersion = "NU1015";

    /// <summary>No source has a package with this id.</summary>
    public const string PackageNotFound = "NU1101";

    /// <summary>The sources have the package, but no version in the requested range.</summary>
    public const string VersionNotFound = "NU1102";

    /// <summary>The sources only have prerelease versions in the requested range, and the range asks for a stable one.</summary>
    public const string OnlyPrereleaseVersions = "NU1103";

    /// <summary>Two packages need versions of a third that no single version satisfies.</summary>
    public const string VersionConflict = "NU1107";

    /// <summary>A package depends, directly or indirectly, on itself.</summary>
    public const string DependencyCycle = "NU1108";

    /// <summary>A package has assemblies, but none for the target framework.</summary>
    public const string IncompatibleFramework = "NU1202";

    /// <summary>A source answered with an error.</summary>
    public const string SourceFailed = "NU1301";

    /// <summary>A direct reference names a package the target framework already provides.</summary>
    public const string RedundantFrameworkPackage = "NU1510";

    /// <summary>The exact lower bound of a range was not found, so the next higher version was used.</summary>
    public const string ApproximateMatch = "NU1603";

    /// <summary>A nearer reference selected a lower version than a package further down needs.</summary>
    public const string Downgrade = "NU1605";

    /// <summary>The selected version is above the upper bound a package declares for its dependency.</summary>
    public const string VersionAboveUpperBound = "NU1608";

    /// <summary>A package was restored with the assets of an <c>AssetTargetFallback</c> framework.</summary>
    public const string FallbackFramework = "NU1701";

    /// <summary>A package source could not be reached: the device is offline or the server did not answer.</summary>
    public const string NetworkUnreachable = "TS3001";

    /// <summary>A downloaded package is not a valid <c>.nupkg</c>, or contains paths that would escape its folder.</summary>
    public const string InvalidPackage = "TS3002";

    /// <summary>A package reference's version is not a valid NuGet version or range.</summary>
    public const string InvalidVersion = "TS3003";

    /// <summary>The global packages folder could not be written.</summary>
    public const string PackagesFolderFailed = "TS3004";
}
