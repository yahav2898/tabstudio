using System.Xml;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;
using TabStudio.Packages.Assets;
using TabStudio.Packages.Installation;

namespace TabStudio.Packages.Manifests;

/// <summary>
/// Reads the identity and dependencies from a <c>.nuspec</c> file.
/// </summary>
/// <remarks>
/// Elements are matched by local name, so every version of the nuspec schema namespace works. As in NuGet,
/// <c>&lt;group&gt;</c> elements define per-framework dependencies; when there are none, plain
/// <c>&lt;dependency&gt;</c> elements apply to every framework. A dependency without a version accepts any version.
/// </remarks>
internal static class NuspecReader
{
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
    };

    /// <summary>Reads a manifest.</summary>
    /// <param name="stream">The <c>.nuspec</c> contents.</param>
    /// <exception cref="InvalidPackageException">The manifest is not valid XML or has no valid id and version.</exception>
    public static PackageManifest Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument document;
        try
        {
            using var reader = XmlReader.Create(stream, Settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidPackageException($"The package manifest is not valid XML ({ex.Message}).", ex);
        }

        var metadata = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata")
            ?? throw new InvalidPackageException("The package manifest has no <metadata> element.");

        var id = ChildValue(metadata, "id");
        var versionText = ChildValue(metadata, "version");
        if (string.IsNullOrWhiteSpace(id) || !NuGetVersion.TryParse(versionText, out var version))
        {
            throw new InvalidPackageException("The package manifest has no valid <id> and <version>.");
        }

        return new PackageManifest(id, version, ReadDependencyGroups(metadata));
    }

    private static string? ChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value.Trim();

    private static List<PackageDependencyGroup> ReadDependencyGroups(XElement metadata)
    {
        var dependencies = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "dependencies");
        if (dependencies is null)
        {
            return [];
        }

        var groups = dependencies.Elements()
            .Where(e => e.Name.LocalName == "group")
            .Select(group => new PackageDependencyGroup(
                ParseFramework(group.Attribute("targetFramework")?.Value),
                ReadDependencies(group)))
            .ToList();

        if (groups.Count > 0)
        {
            return groups;
        }

        // The legacy format: dependencies without groups apply to every framework.
        var ungrouped = ReadDependencies(dependencies);
        if (ungrouped.Count == 0)
        {
            return [];
        }

        return [new PackageDependencyGroup(NuGetFramework.AnyFramework, ungrouped)];
    }

    private static List<PackageDependency> ReadDependencies(XElement parent) =>
        parent.Elements()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => (Id: e.Attribute("id")?.Value.Trim(), Version: e.Attribute("version")?.Value, Include: e.Attribute("include")?.Value, Exclude: e.Attribute("exclude")?.Value))
            .Where(d => !string.IsNullOrEmpty(d.Id))
            .Select(d => new PackageDependency(d.Id!, ParseRange(d.Version)) { Excluded = AssetFlagNames.Excluded(d.Include, d.Exclude) })
            .DistinctBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static VersionRange ParseRange(string? text) =>
        !string.IsNullOrWhiteSpace(text) && VersionRange.TryParse(text, out var range) ? range : VersionRange.All;

    private static NuGetFramework ParseFramework(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return NuGetFramework.AnyFramework;
        }

        try
        {
            return NuGetFramework.Parse(text);
        }
        catch (ArgumentException)
        {
            return NuGetFramework.UnsupportedFramework;
        }
        catch (FrameworkException)
        {
            return NuGetFramework.UnsupportedFramework;
        }
    }
}
