using System.Xml.Linq;

namespace TabStudio.Web.StaticWebAssets;

/// <summary>
/// The static web assets of NuGet packages: a Razor class library package serves the files of its
/// <c>staticwebassets</c> folder under <c>_content/&lt;PackageId&gt;</c>, as the SDK's
/// <c>Microsoft.AspNetCore.StaticWebAssets.props</c> in such a package declares them.
/// </summary>
public static class PackageWebAssets
{
    /// <summary>The folder of a package with its web assets.</summary>
    public const string AssetsFolder = "staticwebassets";

    /// <summary>The folders of the packages that a set of package assemblies come from, each once.</summary>
    /// <param name="packageAssemblies">Assembly files in the global packages folder, such as a restore's runtime assemblies.</param>
    public static IReadOnlyList<string> FindPackageDirectories(IEnumerable<string> packageAssemblies)
    {
        ArgumentNullException.ThrowIfNull(packageAssemblies);
        var directories = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in packageAssemblies)
        {
            if (FindPackageDirectory(assembly) is { } directory && seen.Add(directory))
            {
                directories.Add(directory);
            }
        }

        return directories;
    }

    /// <summary>
    /// The package folder a file is in: the nearest folder above it with a <c>.nuspec</c>, at most five levels up, as in
    /// <c>runtimes/browser-wasm/lib/net10.0</c>; <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="filePath">A file of the package, such as <c>lib/net10.0/Library.dll</c>.</param>
    public static string? FindPackageDirectory(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var directory = Path.GetDirectoryName(filePath);
        for (var level = 0; level < 5 && !string.IsNullOrEmpty(directory); level++, directory = Path.GetDirectoryName(directory))
        {
            try
            {
                if (Directory.EnumerateFiles(directory, "*.nuspec").Any())
                {
                    return directory;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>The id of the package in a folder, from its <c>.nuspec</c>, with the case its author gave it.</summary>
    /// <param name="packageDirectory">The package folder.</param>
    public static string? ReadPackageId(string packageDirectory)
    {
        ArgumentNullException.ThrowIfNull(packageDirectory);
        var nuspec = Directory.EnumerateFiles(packageDirectory, "*.nuspec").FirstOrDefault();
        if (nuspec is null)
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(nuspec);
            return document.Descendants().FirstOrDefault(e => e.Name.LocalName == "id")?.Value.Trim();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>The web assets of packages: the files of each one's <c>staticwebassets</c> folder, under <c>_content/&lt;PackageId&gt;</c>.</summary>
    /// <param name="packageDirectories">The packages' folders.</param>
    public static IReadOnlyList<StaticWebAsset> Collect(IEnumerable<string> packageDirectories)
    {
        ArgumentNullException.ThrowIfNull(packageDirectories);
        var assets = new List<StaticWebAsset>();
        foreach (var package in packageDirectories)
        {
            var folder = Path.Combine(package, AssetsFolder);
            if (!Directory.Exists(folder) || ReadPackageId(package) is not { Length: > 0 } id)
            {
                continue;
            }

            foreach (var file in ProjectWebAssets.EnumerateVisibleFiles(folder))
            {
                assets.Add(StaticWebAsset.Create(StaticWebAsset.Combine("_content/" + id, Path.GetRelativePath(folder, file)), file));
            }
        }

        return assets;
    }
}
