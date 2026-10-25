using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Changes the references of an SDK-style project file the way Visual Studio does: items are added to an
/// existing item group of the same kind when there is one, and the rest of the file keeps its formatting.
/// </summary>
public static class ProjectFileEditor
{
    private const string CentralVersionsFileName = "Directory.Packages.props";

    /// <summary>Adds a project reference, unless the project already references it.</summary>
    /// <param name="projectFilePath">The project to change.</param>
    /// <param name="referencedProjectPath">The project to reference.</param>
    /// <returns>Whether the file changed.</returns>
    public static bool AddProjectReference(string projectFilePath, string referencedProjectPath)
    {
        var include = RelativeInclude(projectFilePath, referencedProjectPath);
        return Edit(projectFilePath, root =>
        {
            if (FindItems(root, "ProjectReference").Any(e => SamePath(projectFilePath, Include(e), referencedProjectPath)))
            {
                return false;
            }

            AddItem(root, "ProjectReference", new XElement("ProjectReference", new XAttribute("Include", include)));
            return true;
        });
    }

    /// <summary>Removes a project reference.</summary>
    /// <param name="projectFilePath">The project to change.</param>
    /// <param name="referencedProjectPath">The referenced project.</param>
    /// <returns>Whether the file changed.</returns>
    public static bool RemoveProjectReference(string projectFilePath, string referencedProjectPath) =>
        Edit(projectFilePath, root => RemoveItems(root, FindItems(root, "ProjectReference").Where(e => SamePath(projectFilePath, Include(e), referencedProjectPath)).ToList()));

    /// <summary>
    /// Adds a package reference or changes its version. With central package management, the version goes into
    /// <c>Directory.Packages.props</c> and the project's reference has none, as the .NET SDK expects.
    /// </summary>
    /// <param name="projectFilePath">The project to change.</param>
    /// <param name="packageId">The package.</param>
    /// <param name="version">The version to use.</param>
    /// <returns>Whether any file changed.</returns>
    public static bool SetPackageReference(string projectFilePath, string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var central = FindCentralVersionsFile(projectFilePath);
        var changed = Edit(projectFilePath, root =>
        {
            var existing = FindItems(root, "PackageReference").FirstOrDefault(e => SameId(e, packageId));
            if (existing is null)
            {
                var item = new XElement("PackageReference", new XAttribute("Include", packageId));
                if (central is null)
                {
                    item.SetAttributeValue("Version", version);
                }

                AddItem(root, "PackageReference", item);
                return true;
            }

            if (central is not null || string.Equals(VersionOf(existing), version, StringComparison.Ordinal))
            {
                return false;
            }

            SetVersion(existing, version);
            return true;
        });

        if (central is not null)
        {
            changed |= Edit(central, root =>
            {
                var entry = FindItems(root, "PackageVersion").FirstOrDefault(e => SameId(e, packageId));
                if (entry is null)
                {
                    AddItem(root, "PackageVersion", new XElement("PackageVersion", new XAttribute("Include", packageId), new XAttribute("Version", version)));
                    return true;
                }

                if (string.Equals(VersionOf(entry), version, StringComparison.Ordinal))
                {
                    return false;
                }

                SetVersion(entry, version);
                return true;
            });
        }

        return changed;
    }

    /// <summary>Removes a package reference. A central version entry is left for other projects.</summary>
    /// <param name="projectFilePath">The project to change.</param>
    /// <param name="packageId">The package.</param>
    /// <returns>Whether the file changed.</returns>
    public static bool RemovePackageReference(string projectFilePath, string packageId) =>
        Edit(projectFilePath, root => RemoveItems(root, FindItems(root, "PackageReference").Where(e => SameId(e, packageId)).ToList()));

    /// <summary>The <c>Directory.Packages.props</c> that manages a project's versions, if central package management is on.</summary>
    /// <param name="projectFilePath">The project.</param>
    public static string? FindCentralVersionsFile(string projectFilePath)
    {
        for (var folder = Path.GetDirectoryName(Path.GetFullPath(projectFilePath)); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            var candidate = Path.Combine(folder, CentralVersionsFileName);
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                var document = XDocument.Load(candidate);
                var enabled = document.Root?.Descendants().FirstOrDefault(e => e.Name.LocalName == "ManagePackageVersionsCentrally")?.Value;
                return string.Equals(enabled?.Trim(), "true", StringComparison.OrdinalIgnoreCase) ? candidate : null;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Loads a file with its whitespace, lets the change run, and saves it only when something changed.</summary>
    private static bool Edit(string path, Func<XElement, bool> change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = XmlFile.Load(path);
        if (!change(file.Root))
        {
            return false;
        }

        file.Save();
        return true;
    }

    private static IEnumerable<XElement> FindItems(XElement root, string itemType) =>
        root.Elements().Where(e => e.Name.LocalName == "ItemGroup").SelectMany(g => g.Elements().Where(e => e.Name.LocalName == itemType));

    /// <summary>Appends an item to the first item group holding items of its type, or to a new item group.</summary>
    private static void AddItem(XElement root, string itemType, XElement item)
    {
        var ns = root.Name.Namespace;
        item.Name = ns + item.Name.LocalName;
        const string newline = "\n";
        var indent = DetectIndent(root);
        var group = root.Elements().FirstOrDefault(g => g.Name.LocalName == "ItemGroup" && !g.HasAttributes && g.Elements().Any(e => e.Name.LocalName == itemType));
        if (group is not null)
        {
            var last = group.Elements().Last(e => e.Name.LocalName == itemType);
            last.AddAfterSelf(new XText(newline + indent + indent), item);
            return;
        }

        group = new XElement(ns + "ItemGroup", new XText(newline + indent + indent), item, new XText(newline + indent));
        var anchor = root.Elements().LastOrDefault(e => e.Name.LocalName == "ItemGroup") ?? root.Elements().LastOrDefault();
        if (anchor is null)
        {
            root.Add(new XText(newline + indent), group, new XText(newline));
        }
        else
        {
            anchor.AddAfterSelf(new XText(newline + newline + indent), group);
        }
    }

    private static bool RemoveItems(XElement root, List<XElement> items)
    {
        foreach (var item in items)
        {
            var group = item.Parent;
            if (item.PreviousNode is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
            {
                whitespace.Remove();
            }

            item.Remove();
            if (group is not null && !group.Elements().Any())
            {
                if (group.PreviousNode is XText before && string.IsNullOrWhiteSpace(before.Value))
                {
                    before.Remove();
                }

                group.Remove();
            }
        }

        return items.Count > 0;
    }

    private static string? Include(XElement item) => item.Attribute("Include")?.Value;

    private static bool SameId(XElement item, string packageId) =>
        string.Equals(Include(item), packageId, StringComparison.OrdinalIgnoreCase);

    private static string? VersionOf(XElement item) =>
        item.Attribute("Version")?.Value ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;

    private static void SetVersion(XElement item, string version)
    {
        if (item.Elements().FirstOrDefault(e => e.Name.LocalName == "Version") is { } element)
        {
            element.Value = version;
        }
        else
        {
            item.SetAttributeValue("Version", version);
        }
    }

    private static bool SamePath(string projectFilePath, string? include, string otherPath) =>
        include is not null && string.Equals(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectFilePath))!, include.Replace('\\', Path.DirectorySeparatorChar))),
            Path.GetFullPath(otherPath),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>A path relative to the project's folder with backslashes, as Visual Studio writes it.</summary>
    private static string RelativeInclude(string projectFilePath, string referencedProjectPath) =>
        Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(projectFilePath))!, Path.GetFullPath(referencedProjectPath)).Replace('/', '\\');

    /// <summary>The indentation of the root's children, two spaces when there are none to look at.</summary>
    private static string DetectIndent(XElement root)
    {
        if (root.Elements().FirstOrDefault()?.PreviousNode is XText text)
        {
            var value = text.Value;
            var lastNewline = value.LastIndexOf('\n');
            if (lastNewline >= 0 && lastNewline < value.Length - 1)
            {
                return value[(lastNewline + 1)..];
            }
        }

        return "  ";
    }
}
