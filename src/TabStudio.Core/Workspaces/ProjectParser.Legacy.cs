using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Legacy .NET Framework projects, the format Visual Studio wrote before SDK-style projects: no SDK, a
/// <c>TargetFrameworkVersion</c> such as <c>v4.7.2</c>, and every file listed. MSBuild's targets for that format build
/// exactly the items the project lists, so TabStudio reads them as MSBuild does: its <c>Compile</c>,
/// <c>ApplicationDefinition</c>, <c>Page</c> and <c>EmbeddedResource</c> items with no default items around them,
/// its <c>Reference</c> items, and the packages of its <c>packages.config</c>.
/// </summary>
/// <remarks>
/// What the SDK learns from a switch, a legacy project says with its references: one that references
/// <c>PresentationFramework</c> (or has WPF's project type, or XAML items) is a WPF project, as if it set
/// <c>UseWPF</c>, and one that references <c>System.Windows.Forms</c> is a Windows Forms project, as if it set
/// <c>UseWindowsForms</c>, which is what .NET Upgrade Assistant writes for them. The project then builds on the same
/// WPF and Windows Forms paths as an SDK-style one.
/// </remarks>
public static partial class ProjectParser
{
    /// <summary>The project type GUID Visual Studio writes into <c>ProjectTypeGuids</c> for WPF projects.</summary>
    private const string WpfProjectTypeGuid = "60dc8134-eba5-43b8-bcc9-bb4bc16c2548";

    /// <summary>The default items that the legacy format does not have, all turned off for a legacy project.</summary>
    private static readonly string[] LegacyDefaultItemSwitches =
    [
        "EnableDefaultItems", "EnableDefaultCompileItems", "EnableDefaultEmbeddedResourceItems", "EnableDefaultNoneItems",
        "EnableDefaultPageItems", "EnableDefaultApplicationDefinition",
    ];

    /// <summary>
    /// Whether a project is in the legacy .NET Framework format: it names no MSBuild SDK, and it sets a
    /// <c>TargetFrameworkVersion</c> such as <c>v4.8</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool IsLegacyFrameworkProject(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Sdk is null
            && project.Sdks.Count == 0
            && project.Language is ProjectLanguage.CSharp or ProjectLanguage.VisualBasic or ProjectLanguage.FSharp
            && IsFrameworkVersion(project.GetProperty("TargetFrameworkVersion"));
    }

    /// <summary>Whether a <c>TargetFrameworkVersion</c> names a .NET Framework, such as <c>v4.7.2</c>.</summary>
    private static bool IsFrameworkVersion(string? version) =>
        version?.Trim() is { Length: > 1 } text && text[0] is 'v' or 'V' && char.IsDigit(text[1]);

    /// <summary>
    /// What a legacy project's items say that an SDK-style project would say with properties: <c>UseWPF</c> or
    /// <c>UseWindowsForms</c> from its references and items, and no default items. Values the project sets itself win.
    /// </summary>
    private static void AddLegacyProperties(Dictionary<string, string> properties, XElement root, IReadOnlyList<AssemblyReferenceInfo> references)
    {
        foreach (var name in LegacyDefaultItemSwitches)
        {
            properties.TryAdd(name, "false");
        }

        var usesWpf = references.Any(r => IsAssembly(r, "PresentationFramework"))
            || (properties.GetValueOrDefault("ProjectTypeGuids") ?? string.Empty).Contains(WpfProjectTypeGuid, StringComparison.OrdinalIgnoreCase)
            || root.Descendants().Any(e => e.Name.LocalName is "ApplicationDefinition" or "Page" && e.Parent?.Name.LocalName == "ItemGroup");
        if (usesWpf)
        {
            properties.TryAdd("UseWPF", "true");
        }
        else if (references.Any(r => IsAssembly(r, "System.Windows.Forms")))
        {
            properties.TryAdd("UseWindowsForms", "true");
        }
    }

    private static bool IsAssembly(AssemblyReferenceInfo reference, string name) =>
        string.Equals(reference.Name, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The project's <c>&lt;Reference&gt;</c> items, by simple name: <c>Newtonsoft.Json, Version=13.0.0.0, Culture=neutral</c>
    /// is <c>Newtonsoft.Json</c>.
    /// </summary>
    private static List<AssemblyReferenceInfo> ReadAssemblyReferences(XElement root) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == "Reference" && e.Parent?.Name.LocalName == "ItemGroup")
            .Select(e => (Include: e.Attribute("Include")?.Value, Item: e))
            .Where(r => !string.IsNullOrWhiteSpace(r.Include) && !r.Include!.Contains("$(", StringComparison.Ordinal))
            .Select(r => new AssemblyReferenceInfo(
                r.Include!.Split(',')[0].Trim(),
                Metadata(r.Item, "HintPath")?.Trim() is { Length: > 0 } hint ? hint : null))
            .DistinctBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The <c>Include</c> of each <c>&lt;COMReference&gt;</c> item.</summary>
    private static List<string> ReadComReferences(XElement root) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == "COMReference")
            .Select(e => e.Attribute("Include")?.Value?.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The packages of the <c>packages.config</c> beside a project, as NuGet restores them for the legacy format:
    /// <c>&lt;package id="Newtonsoft.Json" version="13.0.3" /&gt;</c>. A development dependency stays with the project.
    /// </summary>
    /// <param name="projectDirectory">The project's folder.</param>
    /// <returns>The packages, or none when there is no readable <c>packages.config</c>.</returns>
    public static IReadOnlyList<PackageReferenceInfo> ReadPackagesConfig(string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        var path = Path.Combine(projectDirectory, "packages.config");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var document = XDocument.Parse(File.ReadAllText(path));
            return document.Descendants()
                .Where(e => e.Name.LocalName == "package")
                .Select(e => (Id: e.Attribute("id")?.Value?.Trim(), Version: e.Attribute("version")?.Value?.Trim(), Item: e))
                .Where(p => !string.IsNullOrEmpty(p.Id))
                .Select(p => new PackageReferenceInfo(
                    p.Id!,
                    string.IsNullOrEmpty(p.Version) ? null : p.Version,
                    string.Equals(p.Item.Attribute("developmentDependency")?.Value, "true", StringComparison.OrdinalIgnoreCase) ? "all" : null))
                .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            // A broken packages.config should not stop the project from opening; its packages are then missing.
            return [];
        }
    }

    /// <summary>
    /// For a legacy project, the source files that call Windows through P/Invoke, which only .NET Framework on Windows
    /// runs; other projects are returned as they are.
    /// </summary>
    private static ProjectInfo WithPlatformInvokeFiles(ProjectInfo project)
    {
        if (!IsLegacyFrameworkProject(project))
        {
            return project;
        }

        var files = new List<string>();
        foreach (var file in ProjectSourceFiles.Enumerate(project, extension: project.Language == ProjectLanguage.VisualBasic ? ".vb" : ".cs"))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (PlatformInvoke().IsMatch(text))
            {
                files.Add(Path.GetRelativePath(project.DirectoryPath, file).Replace('\\', '/'));
            }
        }

        return files.Count == 0 ? project : project with { PlatformInvokeFiles = files };
    }

    /// <summary><c>[DllImport(</c>, <c>[LibraryImport(</c>, VB's <c>Declare Function</c>, or a <c>static extern</c> method.</summary>
    [GeneratedRegex(@"\[\s*(?:assembly\s*:\s*)?(?:global::)?(?:System\.Runtime\.InteropServices\.)?(?:DllImport|LibraryImport)(?:Attribute)?\s*\(|\bstatic\s+extern\b|\bextern\s+static\b|<\s*DllImport\s*\(|^\s*(?:Public\s+|Private\s+|Friend\s+)?Declare\s+(?:Auto\s+|Ansi\s+|Unicode\s+)?(?:Function|Sub)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PlatformInvoke();
}
