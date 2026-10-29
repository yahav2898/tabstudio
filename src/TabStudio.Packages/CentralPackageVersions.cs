using System.Xml;
using System.Xml.Linq;
using TabStudio.Core.Workspaces;

namespace TabStudio.Packages;

/// <summary>
/// Reads central package management: the <c>&lt;PackageVersion&gt;</c> items of the nearest
/// <c>Directory.Packages.props</c> and of the files it imports, which give the versions of <c>&lt;PackageReference&gt;</c>
/// items that have none.
/// </summary>
/// <remarks>
/// <para>
/// The versions apply only when <c>ManagePackageVersionsCentrally</c> is <c>true</c>, in the props file or the
/// project, as in the .NET SDK.
/// </para>
/// <para>
/// The file is evaluated in MSBuild's two passes. The first reads its properties and follows its imports, in order,
/// such as Refit's nested
/// <c>&lt;Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Packages.props', '$(MSBuildThisFileDirectory)../'))" /&gt;</c>
/// (see <see cref="MsBuildImports"/>). The second reads the items, whose conditions see every property, the project's
/// winning over the file's. Conditions on groups, items, imports and the <c>&lt;When&gt;</c> of a <c>&lt;Choose&gt;</c>
/// are evaluated with <see cref="MsBuildConditions"/>, for the target framework TabStudio builds the project for
/// (<see cref="ProjectParser.EvaluationTargetFramework"/>). <c>Include</c> adds a version, the first for a package
/// winning; <c>Update</c> changes the version of the items before it and <c>Remove</c> removes them.
/// </para>
/// <para>
/// A condition that cannot be evaluated, such as one calling a property function, counts as true for a property, a
/// group, an import and a <c>&lt;When&gt;</c>. A <c>PackageVersion</c> under one is used only when no other gives the
/// package a version, and an <c>Update</c> or <c>Remove</c> under one is not applied: it is usually the exception, such
/// as Refit's <c>Condition="$(MSBuildProjectDirectory.Contains('benchmarks'))"</c>.
/// </para>
/// </remarks>
internal static partial class CentralPackageVersions
{
    /// <summary>The file name MSBuild looks for.</summary>
    public const string FileName = "Directory.Packages.props";

    private const string EnableProperty = "ManagePackageVersionsCentrally";

    private const int MaxImportDepth = 8;

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

        // The first pass: properties and imports, in order. The project's properties are known from the start, as
        // MSBuild's reserved ones and a project's target framework are when it evaluates the file.
        var framework = ProjectParser.EvaluationTargetFramework(project.TargetFrameworks);
        var properties = new Dictionary<string, string>(project.Properties, StringComparer.OrdinalIgnoreCase);
        SetFramework(properties, framework);
        var elements = new List<(XElement Element, string File)>();
        ReadFile(path, properties, elements, new HashSet<string>(StringComparer.Ordinal), depth: 0);

        var enabled = project.GetProperty(EnableProperty) ?? properties.GetValueOrDefault(EnableProperty)?.Trim();
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return versions;
        }

        // The second pass: items, after every property. Versions are often properties of the file itself, such as
        // <AspNetVersion>8.0.2</AspNetVersion>; the project's properties come later in MSBuild's evaluation, so they win.
        foreach (var (name, value) in project.Properties)
        {
            properties[name] = value;
        }

        SetFramework(properties, framework);
        var doubtful = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (element, file) in elements)
        {
            ReadItems(element, file, properties, versions, doubtful);
        }

        foreach (var (id, version) in doubtful)
        {
            versions.TryAdd(id, version);
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

    private static void SetFramework(Dictionary<string, string> properties, string? framework)
    {
        if (framework is not null)
        {
            properties["TargetFramework"] = framework;
        }
    }

    /// <summary>
    /// The first pass over one file: sets its properties, follows its imports, and adds its item groups and
    /// <c>&lt;Choose&gt;</c> elements, in evaluation order, to <paramref name="elements"/> for the second pass.
    /// </summary>
    private static void ReadFile(string file, Dictionary<string, string> properties, List<(XElement Element, string File)> elements, HashSet<string> visited, int depth)
    {
        if (depth > MaxImportDepth || !visited.Add(file))
        {
            return;
        }

        XElement? root;
        try
        {
            root = XDocument.Parse(File.ReadAllText(file)).Root;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            // A broken import should not stop the restore.
            return;
        }

        if (root is null)
        {
            return;
        }

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "PropertyGroup" when Condition(element, file, properties) != false:
                    ReadProperties(element, file, properties);
                    break;
                case "Choose":
                    ReadChooseProperties(element, file, properties);
                    elements.Add((element, file));
                    break;
                case "ItemGroup":
                    elements.Add((element, file));
                    break;
                case "Import" when Condition(element, file, properties) != false:
                    Import(element, file, properties, elements, visited, depth);
                    break;
                case "ImportGroup" when Condition(element, file, properties) != false:
                    foreach (var import in element.Elements().Where(e => e.Name.LocalName == "Import"))
                    {
                        if (Condition(import, file, properties) != false)
                        {
                            Import(import, file, properties, elements, visited, depth);
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Sets the properties of a group, in order, except those whose condition is false. One under a condition that
    /// cannot be evaluated counts, as every property of the file did before conditions were evaluated here.
    /// </summary>
    private static void ReadProperties(XElement group, string file, Dictionary<string, string> properties)
    {
        foreach (var property in group.Elements())
        {
            if (Condition(property, file, properties) != false)
            {
                properties[property.Name.LocalName] = ExpandProperties(MsBuildImports.ExpandThisFile(property.Value.Trim(), file), properties);
            }
        }
    }

    /// <summary>Sets the properties of the branch of a <c>&lt;Choose&gt;</c> that applies, and of the choices inside it.</summary>
    private static void ReadChooseProperties(XElement choose, string file, Dictionary<string, string> properties)
    {
        if (Branch(choose, file, properties) is not { } branch)
        {
            return;
        }

        foreach (var child in branch.Elements())
        {
            if (child.Name.LocalName == "PropertyGroup" && Condition(child, file, properties) != false)
            {
                ReadProperties(child, file, properties);
            }
            else if (child.Name.LocalName == "Choose")
            {
                ReadChooseProperties(child, file, properties);
            }
        }
    }

    private static void Import(XElement import, string file, Dictionary<string, string> properties, List<(XElement Element, string File)> elements, HashSet<string> visited, int depth)
    {
        if (import.Attribute("Sdk") is not null || import.Attribute("Project")?.Value is not { Length: > 0 } project)
        {
            return;
        }

        // An import of a file that does not exist, or whose path comes out empty, is skipped.
        if (MsBuildImports.ResolvePath(project, file, properties) is { } path && File.Exists(path))
        {
            ReadFile(path, properties, elements, visited, depth + 1);
        }
    }

    /// <summary>
    /// The branch of a <c>&lt;Choose&gt;</c> MSBuild takes: the first <c>&lt;When&gt;</c> whose condition holds (or cannot
    /// be evaluated), else the <c>&lt;Otherwise&gt;</c>; <see langword="null"/> when there is neither.
    /// </summary>
    private static XElement? Branch(XElement choose, string file, Dictionary<string, string> properties)
    {
        foreach (var option in choose.Elements())
        {
            if (option.Name.LocalName == "When" && Condition(option, file, properties) != false)
            {
                return option;
            }

            if (option.Name.LocalName == "Otherwise")
            {
                return option;
            }
        }

        return null;
    }

    /// <summary>The second pass over an item group or a <c>&lt;Choose&gt;</c>: applies its <c>PackageVersion</c> items.</summary>
    private static void ReadItems(XElement element, string file, Dictionary<string, string> properties, Dictionary<string, string> versions, Dictionary<string, string> doubtful)
    {
        if (element.Name.LocalName == "Choose")
        {
            if (Branch(element, file, properties) is { } branch)
            {
                foreach (var child in branch.Elements())
                {
                    ReadItems(child, file, properties, versions, doubtful);
                }
            }

            return;
        }

        var group = element.Name.LocalName == "ItemGroup" ? Condition(element, file, properties) : false;
        if (group == false)
        {
            return;
        }

        foreach (var item in element.Elements().Where(e => e.Name.LocalName == "PackageVersion"))
        {
            var holds = Condition(item, file, properties);
            if (holds != false)
            {
                ReadItem(item, file, properties, versions, doubtful, certain: group == true && holds == true);
            }
        }
    }

    /// <summary>
    /// Applies one <c>PackageVersion</c> item: its <c>Include</c> adds versions, its <c>Update</c> changes the versions
    /// of earlier items and its <c>Remove</c> removes them.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="file">The file it is in.</param>
    /// <param name="properties">Every property.</param>
    /// <param name="versions">The versions so far, under conditions that hold.</param>
    /// <param name="doubtful">The versions so far, under conditions that cannot be evaluated.</param>
    /// <param name="certain">Whether the item's conditions are known to hold.</param>
    private static void ReadItem(XElement item, string file, Dictionary<string, string> properties, Dictionary<string, string> versions, Dictionary<string, string> doubtful, bool certain)
    {
        var written = (item.Attribute("Version")?.Value ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value)?.Trim();
        var version = string.IsNullOrEmpty(written) ? null : ExpandProperties(MsBuildImports.ExpandThisFile(written, file), properties);

        foreach (var id in Ids(item, "Include"))
        {
            if (version is not null)
            {
                (certain ? versions : doubtful).TryAdd(id, version);
            }
        }

        if (!certain)
        {
            return;
        }

        // An update changes only the packages already there; the order of the versions does not matter.
        if (version is not null)
        {
            foreach (var id in Ids(item, "Update"))
            {
                if (versions.Remove(id))
                {
                    versions.Add(id, version);
                }

                if (doubtful.Remove(id))
                {
                    doubtful.Add(id, version);
                }
            }
        }

        foreach (var id in Ids(item, "Remove"))
        {
            versions.Remove(id);
            doubtful.Remove(id);
        }
    }

    /// <summary>The package ids of an item's attribute, such as <c>Include="A;B"</c>.</summary>
    private static string[] Ids(XElement item, string attribute) =>
        (item.Attribute(attribute)?.Value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether an element's condition holds: <see langword="true"/> without one, <see langword="null"/> when it cannot
    /// be evaluated.
    /// </summary>
    private static bool? Condition(XElement element, string file, Dictionary<string, string> properties) =>
        element.Attribute("Condition")?.Value is not { } condition
            ? true
            : MsBuildConditions.Evaluate(MsBuildImports.ExpandThisFile(condition, file), properties, Path.GetDirectoryName(file) ?? string.Empty);

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
