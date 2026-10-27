namespace TabStudio.Packages;

/// <summary>
/// Settings for a <see cref="PackageRestorer"/>.
/// </summary>
public sealed record PackageRestoreOptions
{
    /// <summary>
    /// The .NET Framework versions whose assets a package may lend to a modern .NET project, as the .NET SDK's
    /// <c>AssetTargetFallback</c> does for every project that targets .NET Core 3.0 or later.
    /// </summary>
    public static IReadOnlyList<string> DefaultAssetTargetFallback { get; } =
        ["net461", "net462", "net47", "net471", "net472", "net48", "net481"];

    /// <summary>
    /// The global packages folder. Every package is extracted into it once, as <c>{id}/{version}/</c> in lower
    /// case like NuGet's own folder, and shared by every project. The app uses <c>&lt;app data&gt;/packages</c>.
    /// </summary>
    public required string GlobalPackagesFolder { get; init; }

    /// <summary>The sources packages come from. All of them are searched together. Defaults to nuget.org.</summary>
    public IReadOnlyList<PackageSource> Sources { get; init; } = [PackageSource.NuGetOrg];

    /// <summary>
    /// The framework packages are restored for. This is the framework TabStudio compiles and runs programs on, so
    /// it applies whatever the project's own <c>TargetFramework</c> says.
    /// </summary>
    public string TargetFramework { get; init; } = "net10.0";

    /// <summary>
    /// Frameworks whose assets are used, with warning NU1701, when a package has none for <see cref="TargetFramework"/>.
    /// Checked in order. See <see cref="DefaultAssetTargetFallback"/>.
    /// </summary>
    public IReadOnlyList<string> AssetTargetFallback { get; init; } = DefaultAssetTargetFallback;

    /// <summary>
    /// The simple names of the assemblies the target framework itself provides, such as <c>System.Runtime</c> and
    /// <c>System.Text.Json</c>. Matching is case-insensitive.
    /// </summary>
    /// <remarks>
    /// Packages named after these assemblies are pruned from the dependency graph instead of downloaded, as the .NET
    /// SDK does, and assemblies with these names are never taken from a package. TabStudio.Compilation provides the
    /// set for its reference assemblies.
    /// </remarks>
    public IReadOnlySet<string> FrameworkAssemblyNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether to keep the outcome of a restore in the project's <c>obj/tabstudio.restore.json</c> and reuse it, without
    /// touching the network, while the inputs stay the same.
    /// </summary>
    public bool UseRestoreCache { get; init; } = true;

    /// <summary>How many packages are downloaded at the same time.</summary>
    public int MaxParallelDownloads { get; init; } = 4;

    /// <summary>
    /// The version of Roslyn that runs the packages' analyzers and source generators, which picks among a package's
    /// <c>analyzers/dotnet/roslyn{version}/</c> folders; <see langword="null"/> restores no analyzers.
    /// </summary>
    public Version? CompilerVersion { get; init; } = new(5, 0);

    /// <summary>
    /// The runtime identifier whose RID-specific package assets (<c>runtimes/{rid}/lib/</c> and
    /// <c>runtimes/{rid}/native/</c>) programs run with, falling back along the RID graph to <c>any</c>. Defaults to this
    /// device's: <c>android-arm64</c> on a tablet (which falls back through <c>linux-arm64</c>, <c>linux</c> and
    /// <c>unix</c>), the host's portable RID such as <c>linux-x64</c> elsewhere. <see langword="null"/> uses
    /// <c>lib/</c> only.
    /// </summary>
    public string? RuntimeIdentifier { get; init; } = Assets.RuntimeGraph.DeviceRuntimeIdentifier;
}
