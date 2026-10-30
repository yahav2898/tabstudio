using System.Runtime.InteropServices;
using NuGet.Versioning;
using TabStudio.Core.Diagnostics;
using TabStudio.Packages.Assets;
using TabStudio.Packages.Feeds;
using TabStudio.Packages.Installation;

namespace TabStudio.Packages;

/// <summary>The assemblies of a shared framework, such as ASP.NET Core, as restored for one project.</summary>
/// <param name="Name">The framework, such as <c>Microsoft.AspNetCore.App</c>.</param>
/// <param name="Version">Its version.</param>
/// <param name="CompileAssemblies">Its reference assemblies, to compile against.</param>
/// <param name="RuntimeAssemblies">Its implementation assemblies, to run with.</param>
public sealed record SharedFrameworkAssets(string Name, string Version, IReadOnlyList<string> CompileAssemblies, IReadOnlyList<string> RuntimeAssemblies)
{
    /// <summary>The analyzers and source generators its targeting pack brings, such as ASP.NET Core's.</summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];
}

/// <summary>
/// Restores shared frameworks other than .NET itself from NuGet, the way the .NET SDK gets them for a
/// self-contained app: the targeting pack (<c>.Ref</c>) to compile against and the runtime pack to run with.
/// </summary>
/// <remarks>
/// TabStudio runs programs in its own process, so a framework's runtime pack is loaded like any other set of
/// assemblies. The runtime pack of the device's operating system and processor is used; its assemblies are IL,
/// so a pack built for Linux also serves Android.
/// </remarks>
internal sealed class SharedFrameworks(GlobalPackagesFolder packages, Func<IReadOnlyList<IPackageFeed>> feeds, string targetFramework, Version? compilerVersion)
{
    /// <summary>.NET itself: its targeting pack brings the source generators of the base library, such as the regular expression and JSON ones.</summary>
    public const string NetCoreFramework = "Microsoft.NETCore.App";

    /// <summary>The frameworks TabStudio can restore, and the package id prefix of their packs.</summary>
    private static readonly Dictionary<string, string> PackPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.AspNetCore.App"] = "Microsoft.AspNetCore.App",
        [NetCoreFramework] = "Microsoft.NETCore.App",
    };

    /// <summary>The runtime identifier whose runtime packs this device uses, such as <c>linux-arm64</c>.</summary>
    public static string RuntimeIdentifier
    {
        get
        {
            var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() ? "osx" : "linux";
            var architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                Architecture.X86 => "x86",
                _ => "x64",
            };
            return $"{os}-{architecture}";
        }
    }

    /// <summary>Whether a shared framework is one TabStudio restores, such as <c>Microsoft.AspNetCore.App</c>.</summary>
    /// <param name="name">The framework's name.</param>
    public static bool IsRestorable(string name) => PackPrefixes.ContainsKey(name);

    /// <summary>Restores one framework, using the newest version for the target framework's major version.</summary>
    /// <param name="name">The framework, such as <c>Microsoft.AspNetCore.App</c>.</param>
    /// <param name="projectName">The project, for diagnostics.</param>
    /// <param name="diagnostics">Receives problems.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>The framework's assets, or <see langword="null"/> when it could not be restored.</returns>
    public async Task<SharedFrameworkAssets?> RestoreAsync(string name, string projectName, List<BuildDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        if (!PackPrefixes.TryGetValue(name, out var prefix))
        {
            diagnostics.Add(PackageDiagnostics.Error(
                PackageDiagnosticIds.UnsupportedFramework,
                $"The shared framework {name} does not run on this device; ASP.NET Core (Microsoft.AspNetCore.App) is the one a project can add here.",
                projectName));
            return null;
        }

        var referencePack = $"{prefix}.Ref";

        // .NET's own runtime is the one TabStudio runs on, so only its targeting pack is needed, for the generators.
        var runtimePack = string.Equals(name, NetCoreFramework, StringComparison.OrdinalIgnoreCase) ? referencePack : $"{prefix}.Runtime.{RuntimeIdentifier}";
        var major = MajorVersion(targetFramework);

        NuGetVersion? version;
        IPackageFeed? feed;
        try
        {
            (version, feed) = await FindVersionAsync(referencePack, runtimePack, major, cancellationToken).ConfigureAwait(false);
        }
        catch (PackageSourceException ex) when (ex.IsUnreachable)
        {
            (version, feed) = (Installed(referencePack, runtimePack, major), null);
            if (version is null)
            {
                diagnostics.Add(PackageDiagnostics.Error(
                    PackageDiagnosticIds.NetworkUnreachable,
                    $"{name} is not on this device yet, and {ex.PackageSource?.Name ?? "the package source"} cannot be reached to download it. Connect to the internet and build again.",
                    projectName));
                return null;
            }
        }

        if (version is null)
        {
            diagnostics.Add(PackageDiagnostics.Error(
                PackageDiagnosticIds.UnsupportedFramework,
                $"No version of {name} for .NET {major} was found.",
                projectName));
            return null;
        }

        try
        {
            var reference = feed is null ? packages.GetPackageDirectory(referencePack, version) : await packages.InstallAsync(feed, referencePack, version, cancellationToken).ConfigureAwait(false);
            var runtime = feed is null ? packages.GetPackageDirectory(runtimePack, version) : await packages.InstallAsync(feed, runtimePack, version, cancellationToken).ConfigureAwait(false);
            var isNetCore = string.Equals(name, NetCoreFramework, StringComparison.OrdinalIgnoreCase);
            return new SharedFrameworkAssets(
                name,
                version.ToNormalizedString(),
                isNetCore ? [] : Assemblies(Path.Combine(reference, "ref", targetFramework)),
                isNetCore ? [] : Assemblies(Path.Combine(runtime, "runtimes", RuntimeIdentifier, "lib", targetFramework)))
            {
                Analyzers = Analyzers(reference),
            };
        }
        catch (Exception ex) when (ex is PackageSourceException or InvalidPackageException or IOException)
        {
            diagnostics.Add(PackageDiagnostics.Error(PackageDiagnosticIds.NetworkUnreachable, $"{name} could not be downloaded: {ex.Message}", projectName));
            return null;
        }
    }

    /// <summary>The newest stable version both packs have, on the first feed that has them.</summary>
    private async Task<(NuGetVersion? Version, IPackageFeed? Feed)> FindVersionAsync(string referencePack, string runtimePack, int major, CancellationToken cancellationToken)
    {
        if (Installed(referencePack, runtimePack, major) is { } installed)
        {
            return (installed, null);
        }

        foreach (var feed in feeds())
        {
            var references = await feed.GetVersionsAsync(referencePack, cancellationToken).ConfigureAwait(false);
            if (references.Count == 0)
            {
                continue;
            }

            var runtimes = (await feed.GetVersionsAsync(runtimePack, cancellationToken).ConfigureAwait(false)).ToHashSet();
            var best = references.Where(v => v.Major == major && !v.IsPrerelease && runtimes.Contains(v)).Max();
            if (best is not null)
            {
                return (best, feed);
            }
        }

        return (null, null);
    }

    /// <summary>The newest version of both packs already in the packages folder, so offline builds work.</summary>
    private NuGetVersion? Installed(string referencePack, string runtimePack, int major) =>
        packages.GetInstalledVersions(referencePack)
            .Where(v => v.Major == major && packages.IsInstalled(runtimePack, v))
            .Max();

    private static int MajorVersion(string targetFramework) =>
        int.TryParse(targetFramework.AsSpan(3, targetFramework.IndexOf('.', StringComparison.Ordinal) - 3), out var major) ? major : 10;

    private static List<string> Assemblies(string folder) =>
        Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "*.dll").Order(StringComparer.Ordinal)] : [];

    /// <summary>The C# analyzers of a targeting pack that the compiler can run.</summary>
    private List<string> Analyzers(string packDirectory)
    {
        var analyzers = Path.Combine(packDirectory, "analyzers");
        if (compilerVersion is null || !Directory.Exists(analyzers))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(analyzers, "*.dll", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(packDirectory, file).Replace(Path.DirectorySeparatorChar, '/'));
        return [.. AssetSelector.SelectAnalyzers(files, compilerVersion).Select(path => Path.Combine(packDirectory, path.Replace('/', Path.DirectorySeparatorChar)))];
    }
}
