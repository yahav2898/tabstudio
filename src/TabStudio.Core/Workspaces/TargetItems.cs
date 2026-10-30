using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// The items that targets add just before the compiler runs, which source generators read: TabStudio does not run
/// targets, so the item groups of those targets are carried out here, over the project's <c>EmbeddedResource</c> items,
/// as MSBuild would. ThisAssembly.Strings and Microsoft.CodeAnalysis.ResxSourceGenerator, which MudBlazor uses for its
/// <c>LanguageResource</c>, give their generators the project's <c>.resx</c> files this way:
/// <c>&lt;AdditionalFiles Include="@(EmbeddedResource)" Condition="'%(EmbeddedResource.Type)' == 'Resx'" /&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// A target counts when it runs before the compiler: it is hooked (<c>BeforeTargets</c> or <c>AfterTargets</c>) to one of
/// the SDK's targets that prepare the compilation, such as <c>GenerateMSBuildEditorConfigFileShouldRun</c> or
/// <c>BeforeCompile</c>, or to another target that counts. Targets hooked before another run first.
/// </para>
/// <para>
/// Within a target, an item group adds items from <c>@(Type)</c>, copying each source item's metadata, with
/// <c>%(Type.Name)</c> and <c>%(Name)</c> in its condition and metadata read per item (MSBuild's batching); an item
/// without <c>Include</c> sets metadata on every item of its type. <c>EmbeddedResource</c> items start with the metadata
/// the SDK's resource targets give them before then: <c>Type</c> (<c>Resx</c> or <c>Non-Resx</c>), <c>WithCulture</c>,
/// <c>Culture</c> and <c>ManifestResourceName</c>, besides the item's own and MSBuild's well-known metadata. Transforms
/// and other item functions are not carried out; an item that uses one is left out.
/// </para>
/// </remarks>
public static partial class TargetItems
{
    /// <summary>The SDK's targets that run before the compiler and after the project's items are known.</summary>
    private static readonly HashSet<string> CompilerTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "GenerateMSBuildEditorConfigFileShouldRun", "GenerateMSBuildEditorConfigFileCore", "BeforeCompile", "CoreCompile",
        "Compile", "PrepareForBuild", "BeforeBuild", "PrepareResources", "PrepareResourceNames", "AssignTargetPaths",
        "ResolveReferences", "CompileDesignTime", "_GenerateCompileInputs", "CoreGenerateAssemblyInfo",
    };

    /// <summary>The item types whose items the compiler or its generators get, which are kept from what targets add.</summary>
    private static readonly HashSet<string> CompilerItemTypes = new(StringComparer.OrdinalIgnoreCase) { "AdditionalFiles" };

    /// <summary>The targets of some files that run before the compiler (see the remarks), in the order they run.</summary>
    /// <param name="roots">The files' roots, each with its <c>&lt;Target&gt;</c> elements.</param>
    public static IReadOnlyList<XElement> CompilerTargetsOf(IEnumerable<XElement> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var targets = roots.SelectMany(r => r.Elements().Where(e => e.Name.LocalName == "Target")).ToList();
        var chosen = new List<XElement>();
        var names = new HashSet<string>(CompilerTargets, StringComparer.OrdinalIgnoreCase);
        bool added;
        do
        {
            added = false;
            foreach (var target in targets.Where(t => !chosen.Contains(t)))
            {
                if (Hooks(target).Any(names.Contains))
                {
                    chosen.Add(target);
                    names.Add(target.Attribute("Name")?.Value.Trim() ?? string.Empty);
                    added = true;
                }
            }
        }
        while (added);

        // A target hooked before another chosen one runs before it.
        var byName = chosen.Where(t => t.Attribute("Name") is not null).GroupBy(t => t.Attribute("Name")!.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        int Depth(XElement target, int guard)
        {
            if (guard > 16)
            {
                return 0;
            }

            var before = Split(target.Attribute("BeforeTargets")?.Value).Select(n => byName.GetValueOrDefault(n)).OfType<XElement>().ToList();
            return before.Count == 0 ? 0 : before.Max(b => Depth(b, guard + 1)) + 1;
        }

        return [.. chosen.Select((t, i) => (Target: t, Index: i, Depth: Depth(t, 0))).OrderByDescending(t => t.Depth).ThenBy(t => t.Index).Select(t => t.Target)];
    }

    /// <summary>
    /// The <c>AdditionalFiles</c> items a project's targets add before the compiler runs (see the remarks of
    /// <see cref="TargetItems"/>), each with its metadata and every item type it was on the way.
    /// </summary>
    /// <param name="project">The project, with its <see cref="ProjectInfo.CompilerTargets"/> and <see cref="ProjectInfo.EmbeddedResourceItems"/>.</param>
    /// <param name="properties">The properties the targets' conditions see.</param>
    public static IReadOnlyList<AdditionalFileItem> AdditionalFiles(ProjectInfo project, IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(properties);
        if (project.CompilerTargets.Count == 0)
        {
            return [];
        }

        var items = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase)
        {
            ["EmbeddedResource"] = EmbeddedResources(project, properties),
        };
        var added = new List<Item>();
        foreach (var target in project.CompilerTargets)
        {
            if (Condition(target.Attribute("Condition")?.Value, properties, null, project) == false)
            {
                continue;
            }

            foreach (var group in target.Elements().Where(e => e.Name.LocalName == "ItemGroup"))
            {
                if (Condition(group.Attribute("Condition")?.Value, properties, null, project) == false)
                {
                    continue;
                }

                foreach (var element in group.Elements())
                {
                    Execute(element, items, added, properties, project);
                }
            }
        }

        return [.. added
            .Where(i => CompilerItemTypes.Contains(i.Type) && items.GetValueOrDefault(i.Type)?.Contains(i) == true)
            .DistinctBy(i => i.FullPath, StringComparer.Ordinal)
            .Select(i => new AdditionalFileItem(Path.GetRelativePath(project.DirectoryPath, i.FullPath), project.DirectoryPath, i.Metadata)
            {
                ItemTypes = [.. i.Types],
            })];
    }

    /// <summary>One item: its file, its metadata, and the item types it was created as, the last one its own.</summary>
    private sealed class Item(string type, string fullPath, Dictionary<string, string> metadata, IEnumerable<string> types)
    {
        public string Type { get; } = type;

        public string FullPath { get; } = fullPath;

        public Dictionary<string, string> Metadata { get; } = metadata;

        public List<string> Types { get; } = [.. types.Append(type).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void Execute(XElement element, Dictionary<string, List<Item>> items, List<Item> added, IReadOnlyDictionary<string, string> properties, ProjectInfo project)
    {
        var type = element.Name.LocalName;
        var include = element.Attribute("Include")?.Value;
        var remove = element.Attribute("Remove")?.Value;
        var condition = element.Attribute("Condition")?.Value;
        if (!items.TryGetValue(type, out var own))
        {
            own = [];
            items[type] = own;
        }

        if (remove is not null)
        {
            var removed = Sources(remove, items, project, properties);
            own.RemoveAll(i => removed.Any(r => string.Equals(r.FullPath, i.FullPath, StringComparison.Ordinal)));
            return;
        }

        if (include is null)
        {
            // <EmbeddedResource><FileExtension Condition="...">%(Extension)</FileExtension></EmbeddedResource> sets
            // metadata on every item of its type, one item at a time.
            foreach (var item in own.Where(i => Condition(condition, properties, i, project) != false))
            {
                SetMetadata(element, item, item.Metadata, properties, project);
            }

            return;
        }

        foreach (var source in Sources(include, items, project, properties))
        {
            if (Condition(condition, properties, source, project) == false)
            {
                continue;
            }

            var metadata = new Dictionary<string, string>(source.Metadata, StringComparer.OrdinalIgnoreCase);
            SetMetadata(element, source, metadata, properties, project);
            var item = new Item(type, source.FullPath, metadata, source.Types);
            own.Add(item);
            added.Add(item);
        }
    }

    /// <summary>The metadata an item element sets, as attributes or child elements, each with its own condition.</summary>
    private static void SetMetadata(XElement element, Item source, Dictionary<string, string> metadata, IReadOnlyDictionary<string, string> properties, ProjectInfo project)
    {
        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && !ItemAttributes.Contains(a.Name.LocalName)))
        {
            if (Expand(attribute.Value, properties, source, project) is { } value)
            {
                metadata[attribute.Name.LocalName] = value;
            }
        }

        foreach (var child in element.Elements())
        {
            if (Condition(child.Attribute("Condition")?.Value, properties, source, project) != false
                && Expand(child.Value.Trim(), properties, source, project) is { } value)
            {
                metadata[child.Name.LocalName] = value;
            }
        }
    }

    private static readonly HashSet<string> ItemAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Include", "Exclude", "Remove", "Update", "Condition", "KeepMetadata", "RemoveMetadata", "KeepDuplicates", "MatchOnMetadata", "MatchOnMetadataOptions",
    };

    /// <summary>The items an <c>Include</c> or <c>Remove</c> names: those of each <c>@(Type)</c>, and files by path.</summary>
    private static List<Item> Sources(string text, Dictionary<string, List<Item>> items, ProjectInfo project, IReadOnlyDictionary<string, string> properties)
    {
        var sources = new List<Item>();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ItemReference().Match(part) is { Success: true } reference)
            {
                sources.AddRange(items.GetValueOrDefault(reference.Groups["type"].Value) ?? []);
                continue;
            }

            if (part.Contains("@(", StringComparison.Ordinal) || part.Contains("%(", StringComparison.Ordinal)
                || MsBuildExpressions.Expand(part, properties, null, project.DirectoryPath, unknownProperties: false) is not { } path)
            {
                continue;
            }

            foreach (var file in ProjectSourceFiles.EnumerateItems(project, [path], []))
            {
                sources.Add(new Item("File", file, WellKnownMetadata(project, file), []));
            }
        }

        return sources;
    }

    /// <summary>
    /// The project's <c>EmbeddedResource</c> items, each with its own metadata (from the items that include or update it,
    /// in order), MSBuild's well-known metadata, and what the SDK's resource targets add before the compiler.
    /// </summary>
    private static List<Item> EmbeddedResources(ProjectInfo project, IReadOnlyDictionary<string, string> properties)
    {
        var rootNamespace = properties.GetValueOrDefault("RootNamespace") is { Length: > 0 } root ? root : project.Name;
        var resources = new List<Item>();
        foreach (var path in ProjectSourceFiles.EnumerateItems(project, project.EmbeddedResourceIncludes, project.EmbeddedResourceRemoves))
        {
            var metadata = WellKnownMetadata(project, path);
            var resx = string.Equals(Path.GetExtension(path), ".resx", StringComparison.OrdinalIgnoreCase);
            var culture = CultureOf(path);
            metadata["Type"] = resx ? "Resx" : "Non-Resx";
            metadata["WithCulture"] = culture is null ? "false" : "true";
            if (culture is not null)
            {
                metadata["Culture"] = culture;
            }

            foreach (var item in project.EmbeddedResourceItems.Where(i => new GlobPattern(project.DirectoryPath, i.Include).IsMatch(path)))
            {
                foreach (var (name, value) in item.Metadata)
                {
                    metadata[name] = value;
                }
            }

            metadata.TryAdd("ManifestResourceName", ManifestResourceName(rootNamespace, Path.GetRelativePath(project.DirectoryPath, path), resx, culture));
            resources.Add(new Item("EmbeddedResource", path, metadata, []));
        }

        return resources;
    }

    /// <summary>The name <c>CreateCSharpManifestResourceName</c> gives a file: the root namespace, then its path with dots.</summary>
    private static string ManifestResourceName(string rootNamespace, string relativePath, bool resx, string? culture)
    {
        var parts = relativePath.Replace('\\', '/').Split('/');
        var name = new StringBuilder(rootNamespace);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (i == parts.Length - 1)
            {
                if (resx)
                {
                    part = Path.GetFileNameWithoutExtension(part);
                    if (culture is not null && part.EndsWith("." + culture, StringComparison.OrdinalIgnoreCase))
                    {
                        part = part[..^(culture.Length + 1)];
                    }
                }
            }
            else
            {
                part = Identifier().Replace(part, "_");
            }

            if (name.Length > 0)
            {
                name.Append('.');
            }

            name.Append(part);
        }

        return name.ToString();
    }

    /// <summary>The culture of a file such as <c>Strings.de.resx</c>; <see langword="null"/> when its name has none.</summary>
    private static string? CultureOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            return null;
        }

        var suffix = name[(dot + 1)..];
        if (!CultureName().IsMatch(suffix))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(suffix, predefinedOnly: true).Name.Length > 0 ? suffix : null;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>MSBuild's well-known metadata of a file item.</summary>
    private static Dictionary<string, string> WellKnownMetadata(ProjectInfo project, string fullPath)
    {
        var relative = Path.GetRelativePath(project.DirectoryPath, fullPath);
        var relativeDirectory = Path.GetDirectoryName(relative) is { Length: > 0 } folder ? folder + Path.DirectorySeparatorChar : string.Empty;
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identity"] = relative,
            ["FullPath"] = fullPath,
            ["RootDir"] = Path.GetPathRoot(fullPath) ?? string.Empty,
            ["Filename"] = Path.GetFileNameWithoutExtension(fullPath),
            ["Extension"] = Path.GetExtension(fullPath),
            ["RelativeDir"] = relativeDirectory,
            ["Directory"] = directory[(Path.GetPathRoot(directory)?.Length ?? 0)..] + Path.DirectorySeparatorChar,
            ["RecursiveDir"] = string.Empty,
        };
    }

    /// <summary>A condition with <c>%(Type.Name)</c> and <c>%(Name)</c> read from one item; <see langword="null"/> when it cannot be evaluated.</summary>
    private static bool? Condition(string? condition, IReadOnlyDictionary<string, string> properties, Item? item, ProjectInfo project)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var text = WithMetadata(condition, item, quote: true);
        return text is null || text.Contains("@(", StringComparison.Ordinal)
            ? null
            : MsBuildExpressions.Condition(text, properties, null, project.DirectoryPath);
    }

    private static string? Expand(string text, IReadOnlyDictionary<string, string> properties, Item item, ProjectInfo project)
    {
        var withMetadata = WithMetadata(text, item, quote: false);
        return withMetadata is null || withMetadata.Contains("@(", StringComparison.Ordinal)
            ? null
            : MsBuildExpressions.Expand(withMetadata, properties, null, project.DirectoryPath);
    }

    /// <summary>
    /// Replaces <c>%(Type.Name)</c> and <c>%(Name)</c> with one item's metadata (empty when it has none); without an item,
    /// a text with metadata cannot be evaluated. Inside a condition the values are escaped for its quotes.
    /// </summary>
    private static string? WithMetadata(string text, Item? item, bool quote)
    {
        if (!text.Contains("%(", StringComparison.Ordinal))
        {
            return text;
        }

        if (item is null)
        {
            return null;
        }

        return MetadataReference().Replace(text, match =>
        {
            var value = item.Metadata.GetValueOrDefault(match.Groups["name"].Value) ?? string.Empty;
            return quote ? value.Replace("'", "%27", StringComparison.Ordinal) : value;
        });
    }

    private static string[] Split(string? value) =>
        (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> Hooks(XElement target) =>
        Split(target.Attribute("BeforeTargets")?.Value).Concat(Split(target.Attribute("AfterTargets")?.Value));

    [GeneratedRegex(@"^@\(\s*(?<type>[A-Za-z_][A-Za-z0-9_.\-]*)\s*\)$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ItemReference();

    [GeneratedRegex(@"%\(\s*(?:[A-Za-z_][A-Za-z0-9_\-]*\s*\.\s*)?(?<name>[A-Za-z_][A-Za-z0-9_\-]*)\s*\)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MetadataReference();

    [GeneratedRegex("[^A-Za-z0-9_]", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Identifier();

    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,2}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CultureName();
}
