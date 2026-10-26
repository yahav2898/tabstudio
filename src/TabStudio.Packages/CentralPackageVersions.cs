using System.Xml;
using System.Xml.Linq;
using TabStudio.Core.Workspaces;

namespace TabStudio.Packages;

/// <summary>
/// Reads central package management: the <c>&lt;PackageVersion&gt;</c> items of the nearest
/// <c>Directory.Packages.props</c>, which give the versions of <c>&lt;PackageReference&gt;</c> items that have none.
/// </summary>
/// <remarks>
/// The versions apply only when <c>ManagePackageVersionsCentrally</c> is <c>true</c>, in the props file or the
/// project, as in the .NET SDK. Conditions, imports and <c>VersionOverride</c> are not evaluated.
/// </remarks>
internal static partial class CentralPackageVersions
{
    /// <summary>The file name MSBuild looks for.</summary>
    public const string FileName = "Directory.Packages.props";

    private const string EnableProperty = "ManagePackageVersionsCentrally";

    /// <summary>Returns the central versions that apply to a project, keyed by package id (case-insensitive).</summary>
    /// <param name="project">The project.</param>
    public static IReadOnlyDictionary<string, string> Load(ProjectInfo project)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Find(project.DirectoryPath);
        if (path is null)
        {
            return versions;
        }

        XElement? root;
        try
        {
            root = XDocument.Parse(File.ReadAllText(path)).Root;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return versions;
        }

        if (root is null)
        {
            return versions;
        }

        var enabledInFile = root.Elements()
            .Where(e => e.Name.LocalName == "PropertyGroup")
            .SelectMany(g => g.Elements())
            .LastOrDefault(e => e.Name.LocalName == EnableProperty)?.Value.Trim();
        var enabled = project.GetProperty(EnableProperty) ?? enabledInFile;
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return versions;
        }

        // Versions are often properties of the file itself, such as <AspNetVersion>8.0.2</AspNetVersion>; the
        // project's properties come later in MSBuild's evaluation, so they win.
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").SelectMany(g => g.Elements()))
        {
            properties[property.Name.LocalName] = property.Value.Trim();
        }

        foreach (var (name, value) in project.Properties)
        {
            properties[name] = value;
        }

        foreach (var item in root.Descendants().Where(e => e.Name.LocalName == "PackageVersion"))
        {
            var id = item.Attribute("Include")?.Value.Trim();
            var version = item.Attribute("Version")?.Value ??
                item.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrWhiteSpace(version))
            {
                versions.TryAdd(id, ExpandProperties(version.Trim(), properties));
            }
        }

        return versions;
    }

    /// <summary>
    /// Replaces <c>$(Name)</c> references with property values, a few levels deep, as in <c>Version="$(AspNetVersion)"</c>.
    /// A reference to an unknown property stays, so the restore reports the version as invalid.
    /// </summary>
    /// <param name="text">The text, such as a version.</param>
    /// <param name="properties">The properties, case-insensitive.</param>
    public static string ExpandProperties(string text, IReadOnlyDictionary<string, string> properties)
    {
        for (var depth = 0; depth < 8 && text.Contains("$(", StringComparison.Ordinal); depth++)
        {
            var expanded = PropertyReference().Replace(text, match =>
                properties.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Value);
            if (expanded == text)
            {
                break;
            }

            text = expanded;
        }

        return text;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\)", System.Text.RegularExpressions.RegexOptions.CultureInvariant, 100)]
    private static partial System.Text.RegularExpressions.Regex PropertyReference();

    /// <summary>Finds the nearest <c>Directory.Packages.props</c> in a folder or its parents.</summary>
    private static string? Find(string startDirectory)
    {
        for (var directory = string.IsNullOrEmpty(startDirectory) ? null : new DirectoryInfo(startDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
