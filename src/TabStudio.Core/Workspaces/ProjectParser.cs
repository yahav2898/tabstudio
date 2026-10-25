using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Reads MSBuild project files (<c>.csproj</c>, <c>.vbproj</c>, <c>.fsproj</c>, <c>.vcxproj</c>) into a <see cref="ProjectInfo"/>.
/// </summary>
/// <remarks>
/// <para>
/// The parser reads the XML directly instead of evaluating it with MSBuild, so that opening a large
/// solution takes milliseconds on a tablet. It understands what matters for an IDE:
/// </para>
/// <list type="bullet">
///   <item>Properties, where an unconditional value wins over a conditional one.</item>
///   <item>The nearest <c>Directory.Build.props</c>, whose properties the project can override.</item>
///   <item>Simple <c>$(Property)</c> references between those properties.</item>
///   <item>The SDK, from the <c>Sdk</c> attribute, an <c>&lt;Sdk&gt;</c> element or an SDK import.</item>
///   <item><c>PackageReference</c>, <c>ProjectReference</c> and <c>Using</c> items.</item>
/// </list>
/// <para>
/// It does not run targets, evaluate conditions or follow arbitrary imports. When an exact answer is
/// needed, the build engine evaluates the project properly.
/// </para>
/// </remarks>
public static partial class ProjectParser
{
    /// <summary>The shared framework of ASP.NET Core.</summary>
    public const string AspNetCoreFramework = "Microsoft.AspNetCore.App";

    private const int MaxPropertyExpansionDepth = 8;

    /// <summary>File extensions TabStudio treats as project files.</summary>
    public static IReadOnlyList<string> ProjectExtensions { get; } = [".csproj", ".vbproj", ".fsproj", ".vcxproj"];

    /// <summary>Whether a path looks like a project file TabStudio can read.</summary>
    /// <param name="path">A file path.</param>
    public static bool IsProjectFile(string path) =>
        ProjectExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads and parses a project file from disk.</summary>
    /// <param name="filePath">Path of the project file.</param>
    /// <exception cref="ProjectLoadException">The file does not exist or is not valid XML.</exception>
    public static ProjectInfo Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new ProjectLoadException(fullPath, "the file does not exist.", null);
        }

        var directoryBuildProps = FindDirectoryBuildProps(Path.GetDirectoryName(fullPath));
        var inherited = directoryBuildProps is null
            ? null
            : ReadPropertiesSafely(directoryBuildProps);

        return Parse(fullPath, File.ReadAllText(fullPath), inherited);
    }

    /// <summary>Parses project XML that has already been read.</summary>
    /// <param name="filePath">The path the XML came from. Used for the project name and to resolve relative paths.</param>
    /// <param name="projectXml">The contents of the project file.</param>
    /// <param name="inheritedProperties">Properties from <c>Directory.Build.props</c>, which the project may override.</param>
    /// <exception cref="ProjectLoadException">The text is not valid XML.</exception>
    public static ProjectInfo Parse(string filePath, string projectXml, IReadOnlyDictionary<string, string>? inheritedProperties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(projectXml);

        var root = LoadXml(filePath, projectXml);
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (inheritedProperties is not null)
        {
            foreach (var (key, value) in inheritedProperties)
            {
                properties[key] = value;
            }
        }

        foreach (var (key, value) in ReadProperties(root))
        {
            properties[key] = value;
        }

        ExpandPropertyReferences(properties);

        var sdk = ReadSdk(root);
        var packages = ReadPackageReferences(root);
        var projectReferences = ReadProjectReferences(root, directory);
        var targetFrameworks = ReadTargetFrameworks(properties);
        var language = LanguageFromExtension(Path.GetExtension(filePath));

        var info = new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            Language = language,
            Sdk = sdk,
            TargetFrameworks = targetFrameworks,
            OutputType = properties.GetValueOrDefault("OutputType"),
            Properties = properties,
            PackageReferences = packages,
            ProjectReferences = projectReferences,
            FrameworkReferences = ReadFrameworkReferences(root, sdk),
            CompileIncludes = ReadItemPatterns(root, "Compile", "Include"),
            CompileRemoves = ReadItemPatterns(root, "Compile", "Remove"),
            Usings = ReadUsings(root),
            RemovedUsings = ReadItemPatterns(root, "Using", "Remove"),
        };

        var (kind, isTest) = ProjectClassifier.Classify(info);
        return info with { Kind = kind, IsTestProject = isTest };
    }

    /// <summary>
    /// Finds the <c>Directory.Build.props</c> that MSBuild would import for a project in <paramref name="startDirectory"/>:
    /// the nearest one in that directory or any parent.
    /// </summary>
    /// <param name="startDirectory">The directory of the project file.</param>
    /// <returns>The full path, or <see langword="null"/> when there is none.</returns>
    public static string? FindDirectoryBuildProps(string? startDirectory)
    {
        var current = string.IsNullOrEmpty(startDirectory) ? null : new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Directory.Build.props");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static Dictionary<string, string>? ReadPropertiesSafely(string propsPath)
    {
        try
        {
            return ReadProperties(LoadXml(propsPath, File.ReadAllText(propsPath)));
        }
        catch (ProjectLoadException)
        {
            // A broken Directory.Build.props should not stop the project from opening.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static XElement LoadXml(string filePath, string xml)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);
            return document.Root ?? throw new ProjectLoadException(filePath, "the file is empty.", null);
        }
        catch (XmlException ex)
        {
            throw new ProjectLoadException(filePath, $"the XML is not valid ({ex.Message}).", ex);
        }
    }

    /// <summary>
    /// Reads all properties. Unconditional values take precedence over conditional ones,
    /// and a later unconditional value overrides an earlier one, as in MSBuild.
    /// </summary>
    private static Dictionary<string, string> ReadProperties(XElement root)
    {
        var unconditional = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var conditional = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in root.Elements().Where(e => e.Name.LocalName == "PropertyGroup"))
        {
            var groupIsConditional = group.Attribute("Condition") is not null;
            foreach (var property in group.Elements())
            {
                var name = property.Name.LocalName;
                var value = property.Value.Trim();
                var isConditional = groupIsConditional || property.Attribute("Condition") is not null;

                if (isConditional)
                {
                    conditional.TryAdd(name, value);
                }
                else
                {
                    unconditional[name] = value;
                }
            }
        }

        foreach (var (name, value) in conditional)
        {
            unconditional.TryAdd(name, value);
        }

        return unconditional;
    }

    /// <summary>Replaces <c>$(Name)</c> references with the values of other known properties.</summary>
    private static void ExpandPropertyReferences(Dictionary<string, string> properties)
    {
        for (var pass = 0; pass < MaxPropertyExpansionDepth; pass++)
        {
            var changed = false;
            foreach (var key in properties.Keys.ToList())
            {
                var value = properties[key];
                if (!value.Contains("$(", StringComparison.Ordinal))
                {
                    continue;
                }

                var expanded = PropertyReference().Replace(value, match =>
                {
                    var referenced = match.Groups["name"].Value;
                    return !string.Equals(referenced, key, StringComparison.OrdinalIgnoreCase) &&
                           properties.TryGetValue(referenced, out var replacement)
                        ? replacement
                        : match.Value;
                });

                if (!string.Equals(expanded, value, StringComparison.Ordinal))
                {
                    properties[key] = expanded;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }
        }
    }

    private static string? ReadSdk(XElement root)
    {
        var fromAttribute = root.Attribute("Sdk")?.Value;
        if (!string.IsNullOrWhiteSpace(fromAttribute))
        {
            return NormalizeSdkName(fromAttribute);
        }

        var fromElement = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Sdk")?
            .Attribute("Name")?.Value;
        if (!string.IsNullOrWhiteSpace(fromElement))
        {
            return NormalizeSdkName(fromElement);
        }

        var fromImport = root.Elements()
            .Where(e => e.Name.LocalName == "Import")
            .Select(e => e.Attribute("Sdk")?.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return fromImport is null ? null : NormalizeSdkName(fromImport);
    }

    /// <summary>Turns <c>Microsoft.NET.Sdk.Web/9.0.0;Other</c> into <c>Microsoft.NET.Sdk.Web</c>.</summary>
    private static string NormalizeSdkName(string sdk)
    {
        var first = sdk.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        var slash = first.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? first[..slash] : first;
    }

    private static List<PackageReferenceInfo> ReadPackageReferences(XElement root) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (Name: e.Attribute("Include")?.Value, Element: e))
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new PackageReferenceInfo(
                p.Name!.Trim(),
                p.Element.Attribute("Version")?.Value ??
                p.Element.Elements().FirstOrDefault(c => c.Name.LocalName == "Version")?.Value))
            .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The shared frameworks a project uses: <c>FrameworkReference</c> items, plus ASP.NET Core for projects on the
    /// Web SDK, which adds it implicitly.
    /// </summary>
    private static List<string> ReadFrameworkReferences(XElement root, string? sdk)
    {
        var frameworks = ReadItemPatterns(root, "FrameworkReference", "Include");
        if (string.Equals(sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)
            && !frameworks.Contains(AspNetCoreFramework, StringComparer.OrdinalIgnoreCase))
        {
            frameworks.Insert(0, AspNetCoreFramework);
        }

        return frameworks;
    }

    private static List<string> ReadProjectReferences(XElement root, string projectDirectory) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => PathUtilities.ResolveRelative(projectDirectory, include!))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Reads the <c>Using</c> items that add global usings, with their <c>Alias</c> and <c>Static</c> metadata.</summary>
    private static List<UsingInfo> ReadUsings(XElement root) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == "Using")
            .SelectMany(item => (item.Attribute("Include")?.Value ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(name => !name.Contains("$(", StringComparison.Ordinal))
                .Select(name => new UsingInfo(
                    name,
                    Metadata(item, "Alias") is { Length: > 0 } alias ? alias.Trim() : null,
                    string.Equals(Metadata(item, "Static")?.Trim(), "true", StringComparison.OrdinalIgnoreCase))))
            .Distinct()
            .ToList();

    /// <summary>An item's metadata, written as an attribute or as a child element.</summary>
    private static string? Metadata(XElement item, string name) =>
        item.Attribute(name)?.Value ?? item.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value;

    /// <summary>Reads the patterns of one attribute (such as <c>Include</c>) on one item type, split on semicolons.</summary>
    private static List<string> ReadItemPatterns(XElement root, string itemType, string attribute) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == itemType)
            .Select(e => e.Attribute(attribute)?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(pattern => !pattern.Contains("$(", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static List<string> ReadTargetFrameworks(Dictionary<string, string> properties)
    {
        var raw = properties.GetValueOrDefault("TargetFrameworks") ?? properties.GetValueOrDefault("TargetFramework");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tf => !tf.Contains("$(", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProjectLanguage LanguageFromExtension(string extension) => extension.ToUpperInvariant() switch
    {
        ".CSPROJ" => ProjectLanguage.CSharp,
        ".VBPROJ" => ProjectLanguage.VisualBasic,
        ".FSPROJ" => ProjectLanguage.FSharp,
        ".VCXPROJ" => ProjectLanguage.Cpp,
        _ => ProjectLanguage.Unknown,
    };

    [GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_.\-]*)\)")]
    private static partial Regex PropertyReference();
}
