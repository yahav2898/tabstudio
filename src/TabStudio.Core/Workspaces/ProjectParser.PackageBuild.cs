using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// What a project gets from its packages' props and targets files, and the generator inputs of its own file: the
/// properties packages set, the <c>CompilerVisibleProperty</c> and <c>CompilerVisibleItemMetadata</c> items through
/// which the SDK hands properties and item metadata to source generators, <c>AdditionalFiles</c> and <c>Using</c> items.
/// </summary>
public static partial class ProjectParser
{
    private const int MaxBuildFileImportDepth = 8;

    private static readonly HashSet<string> ItemAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Include", "Exclude", "Remove", "Update", "Condition", "KeepMetadata", "RemoveMetadata", "KeepDuplicates", "MatchOnMetadata", "MatchOnMetadataOptions",
    };

    /// <summary>
    /// Reads the props and targets files a project imports from its packages into it, as MSBuild evaluates them around
    /// the project file: a package's props come before the project, so the project's own properties win over theirs,
    /// and its targets after, so theirs win. Properties, conditions on them, imports, and the
    /// <c>CompilerVisibleProperty</c>, <c>CompilerVisibleItemMetadata</c>, <c>AdditionalFiles</c> and <c>Using</c>
    /// items outside targets are read; targets and property functions are not run.
    /// </summary>
    /// <param name="project">The project as read from its file, without packages read into it.</param>
    /// <param name="buildFiles">Full paths of the packages' props and targets, in MSBuild's order, as a restore gives them.</param>
    /// <returns>The project with what the packages add; the same project when there are no files.</returns>
    public static ProjectInfo WithPackageBuildFiles(ProjectInfo project, IReadOnlyList<string> buildFiles)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(buildFiles);
        if (buildFiles.Count == 0)
        {
            return project;
        }

        var state = new BuildFileState(project);
        foreach (var file in buildFiles)
        {
            var isProps = file.EndsWith(".props", StringComparison.OrdinalIgnoreCase);
            ReadBuildFile(file, state, isProps, depth: 0, new HashSet<string>(StringComparer.Ordinal));
        }

        // The SDK's own properties were there for conditions; unless a package changed one, the project keeps none.
        foreach (var (name, value) in state.SdkDefaults)
        {
            if (state.Properties.TryGetValue(name, out var now) && string.Equals(now, value, StringComparison.Ordinal))
            {
                state.Properties.Remove(name);
            }
        }

        return project with
        {
            Properties = state.Properties,
            Usings = [.. project.Usings.Concat(state.Usings).Distinct()],
            RemovedUsings = [.. project.RemovedUsings.Concat(state.RemovedUsings).Distinct(StringComparer.Ordinal)],
            CompilerVisibleProperties = [.. project.CompilerVisibleProperties.Concat(state.VisibleProperties).Distinct(StringComparer.OrdinalIgnoreCase)],
            CompilerVisibleItemMetadata = [.. project.CompilerVisibleItemMetadata.Concat(state.VisibleMetadata).Distinct()],
            AdditionalFiles = [.. project.AdditionalFiles, .. state.AdditionalFiles],
            AssemblyReferences = [.. project.AssemblyReferences.Concat(state.References).DistinctBy(r => r.Name, StringComparer.OrdinalIgnoreCase)],
            PackageBuildFiles = buildFiles,
        };
    }

    /// <summary>The properties and items read so far, which later files and conditions see.</summary>
    private sealed class BuildFileState
    {
        public BuildFileState(ProjectInfo project)
        {
            ProjectDefined = new(project.Properties.Keys, StringComparer.OrdinalIgnoreCase);
            Properties = new(project.Properties, StringComparer.OrdinalIgnoreCase);
            ProjectDirectory = project.DirectoryPath;
            foreach (var (name, value) in SdkPropertiesOf(project))
            {
                if (Properties.TryAdd(name, value))
                {
                    SdkDefaults[name] = value;
                }
            }
        }

        /// <summary>The properties the SDK sets that packages' files test, such as <c>Language</c>, added for conditions only.</summary>
        public Dictionary<string, string> SdkDefaults { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The properties the project file and its <c>Directory.Build</c> files set, which package props do not override.</summary>
        public HashSet<string> ProjectDefined { get; }

        public Dictionary<string, string> Properties { get; }

        public string ProjectDirectory { get; }

        public List<UsingInfo> Usings { get; } = [];

        public List<string> RemovedUsings { get; } = [];

        public List<string> VisibleProperties { get; } = [];

        public List<CompilerVisibleItemMetadataInfo> VisibleMetadata { get; } = [];

        public List<AdditionalFileItem> AdditionalFiles { get; } = [];

        /// <summary>The assemblies the files reference by <c>HintPath</c>, such as MSTest's TestFramework.Extensions.</summary>
        public List<AssemblyReferenceInfo> References { get; } = [];
    }

    /// <summary>
    /// The properties the .NET SDK sets for every project that packages' props and targets test, for the target
    /// framework TabStudio builds the project for (see <see cref="EvaluationTargetFramework"/>).
    /// </summary>
    private static IEnumerable<(string Name, string Value)> SdkPropertiesOf(ProjectInfo project)
    {
        yield return ("Language", project.Language switch
        {
            ProjectLanguage.VisualBasic => "VB",
            ProjectLanguage.FSharp => "F#",
            ProjectLanguage.Cpp => "C++",
            _ => "C#",
        });
        yield return ("UsingMicrosoftNETSdk", project.Sdk is null ? string.Empty : "true");
        yield return ("TargetFrameworkIdentifier", ".NETCoreApp");
        if (EvaluationTargetFramework(project.TargetFrameworks) is { } framework)
        {
            yield return ("TargetFramework", framework);
            var digits = framework.StartsWith("net", StringComparison.OrdinalIgnoreCase) ? new string([.. framework[3..].TakeWhile(c => char.IsAsciiDigit(c) || c == '.')]) : string.Empty;
            if (digits.Contains('.', StringComparison.Ordinal))
            {
                yield return ("TargetFrameworkVersion", "v" + digits);
            }
        }

        yield return ("MSBuildProjectExtension", Path.GetExtension(project.FilePath));
        yield return ("MSBuildProjectFullPath", project.FilePath);
        yield return ("DesignTimeBuild", string.Empty);
    }

    private static void ReadBuildFile(string file, BuildFileState state, bool isProps, int depth, HashSet<string> visited)
    {
        if (depth > MaxBuildFileImportDepth || !visited.Add(file) || !File.Exists(file))
        {
            return;
        }

        XElement root;
        try
        {
            root = LoadXml(file, File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is ProjectLoadException or IOException or UnauthorizedAccessException)
        {
            // A package's broken file should not stop the project from building.
            return;
        }

        var folder = Path.GetDirectoryName(file) ?? string.Empty;
        foreach (var element in root.Elements())
        {
            if (!IsTrueHere(element, state, file))
            {
                continue;
            }

            switch (element.Name.LocalName)
            {
                case "PropertyGroup":
                    // A value under a condition this cannot evaluate, such as one calling a property function, is left
                    // out: the value before it, usually the package's default, is the likelier one.
                    foreach (var property in element.Elements().Where(p => IsTrueHere(p, state, file, unknown: false)))
                    {
                        // A package's props come before the project, whose own value is the one that stays.
                        if (isProps && state.ProjectDefined.Contains(property.Name.LocalName))
                        {
                            continue;
                        }

                        state.Properties[property.Name.LocalName] = Expand(property.Value.Trim(), state, file);
                    }

                    break;
                case "ItemGroup":
                    foreach (var item in element.Elements())
                    {
                        // Declaring what generators may see is harmless; adding files or usings under a doubtful
                        // condition is not.
                        var unknown = item.Name.LocalName is "CompilerVisibleProperty" or "CompilerVisibleItemMetadata";
                        if (IsTrueHere(item, state, file, unknown))
                        {
                            ReadGeneratorItem(item, state, file);
                        }
                    }

                    break;
                case "Import":
                    ImportBuildFile(element, state, file, folder, isProps, depth, visited);
                    break;
                case "Target":
                    ReadInterceptorNamespaces(element, state, file);
                    break;
                case "ImportGroup":
                    foreach (var import in element.Elements().Where(i => i.Name.LocalName == "Import" && IsTrueHere(i, state, file)))
                    {
                        ImportBuildFile(import, state, file, folder, isProps, depth, visited);
                    }

                    break;
            }
        }
    }

    /// <summary>The properties the compiler reads the namespaces generators may put interceptors in from.</summary>
    private static readonly string[] InterceptorNamespaceProperties = ["InterceptorsNamespaces", "InterceptorsPreviewNamespaces"];

    /// <summary>
    /// Reads the interceptor namespaces a package's target adds before the compiler runs, as ReactiveUI.Binding's
    /// does for its generator only once it knows the compiler's version. Targets are not run, so their property groups
    /// are read on a copy of the properties, a condition that cannot be evaluated counting as true, and only the
    /// namespaces they add are kept: allowing interceptors in a namespace changes nothing until a generator puts one
    /// there.
    /// </summary>
    private static void ReadInterceptorNamespaces(XElement target, BuildFileState state, string file)
    {
        var groups = target.Elements().Where(e => e.Name.LocalName == "PropertyGroup").ToList();
        if (!groups.SelectMany(g => g.Elements()).Any(p => InterceptorNamespaceProperties.Contains(p.Name.LocalName, StringComparer.OrdinalIgnoreCase)))
        {
            return;
        }

        var properties = new Dictionary<string, string>(state.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.Where(g => IsTrueHere(g, properties, file, unknown: true)))
        {
            foreach (var property in group.Elements().Where(p => IsTrueHere(p, properties, file, unknown: true)))
            {
                properties[property.Name.LocalName] = Expand(property.Value.Trim(), properties, file);
            }
        }

        foreach (var name in InterceptorNamespaceProperties)
        {
            var added = Split(properties.GetValueOrDefault(name)).Where(n => !n.Contains("$(", StringComparison.Ordinal) && !n.Contains('(', StringComparison.Ordinal));
            var known = Split(state.Properties.GetValueOrDefault(name));
            var merged = known.Concat(added).Distinct(StringComparer.Ordinal).ToList();
            if (merged.Count > known.Length)
            {
                state.Properties[name] = string.Join(';', merged);
            }
        }
    }

    private static void ImportBuildFile(XElement import, BuildFileState state, string file, string folder, bool isProps, int depth, HashSet<string> visited)
    {
        if (import.Attribute("Sdk") is not null || import.Attribute("Project")?.Value is not { Length: > 0 } project)
        {
            return;
        }

        var path = Expand(project, state, file);
        if (path.Length == 0 || path.Contains("$(", StringComparison.Ordinal) || path.Contains('*', StringComparison.Ordinal))
        {
            return;
        }

        var full = Path.GetFullPath(Path.Combine(folder, path.Replace('\\', Path.DirectorySeparatorChar)));
        ReadBuildFile(full, state, isProps, depth + 1, visited);
    }

    /// <summary>
    /// Reads one item a generator or the compiler sees: a compiler-visible property or item metadata, an additional file,
    /// a global using, or an assembly referenced by its path.
    /// </summary>
    private static void ReadGeneratorItem(XElement item, BuildFileState state, string file)
    {
        var include = item.Attribute("Include")?.Value is { } text ? Expand(text, state, file) : null;
        var remove = item.Attribute("Remove")?.Value is { } removed ? Expand(removed, state, file) : null;
        switch (item.Name.LocalName)
        {
            case "CompilerVisibleProperty":
                state.VisibleProperties.AddRange(Split(include));
                state.VisibleProperties.RemoveAll(name => Split(remove).Contains(name, StringComparer.OrdinalIgnoreCase));
                break;
            case "CompilerVisibleItemMetadata" when Metadata(item, "MetadataName") is { Length: > 0 } metadata:
                state.VisibleMetadata.AddRange(Split(include).Select(type => new CompilerVisibleItemMetadataInfo(type, Expand(metadata.Trim(), state, file))));
                break;
            case "AdditionalFiles":
                foreach (var path in Split(include).Where(p => !p.Contains("@(", StringComparison.Ordinal) && !p.Contains("%(", StringComparison.Ordinal)))
                {
                    state.AdditionalFiles.Add(new AdditionalFileItem(path, state.ProjectDirectory, ItemMetadata(item, value => Expand(value, state, file))));
                }

                break;
            case "Reference" when Metadata(item, "HintPath") is { Length: > 0 } hint:
                // A package's targets can reference an assembly of its own by path, as MSTest.TestFramework does for
                // TestFramework.Extensions, where TestContext is: the SDK compiles against it.
                foreach (var name in Split(include).Where(n => !n.Contains("$(", StringComparison.Ordinal)))
                {
                    state.References.Add(new AssemblyReferenceInfo(name.Split(',')[0].Trim(), Expand(hint.Trim(), state, file)));
                }

                break;
            case "Using":
                foreach (var name in Split(include).Where(n => !n.Contains("$(", StringComparison.Ordinal)))
                {
                    state.Usings.Add(new UsingInfo(
                        name,
                        Metadata(item, "Alias") is { Length: > 0 } alias ? alias.Trim() : null,
                        string.Equals(Metadata(item, "Static")?.Trim(), "true", StringComparison.OrdinalIgnoreCase)));
                }

                state.RemovedUsings.AddRange(Split(remove));
                break;
        }
    }

    /// <summary>
    /// The generator inputs of a project file and its <c>Directory.Build</c> files: their <c>CompilerVisibleProperty</c>,
    /// <c>CompilerVisibleItemMetadata</c> and <c>AdditionalFiles</c> items, outside targets.
    /// </summary>
    private static (List<string> Properties, List<CompilerVisibleItemMetadataInfo> Metadata, List<AdditionalFileItem> Files) ReadGeneratorInputs(
        IEnumerable<XElement> roots,
        Dictionary<string, string> properties,
        string projectDirectory)
    {
        var visibleProperties = new List<string>();
        var visibleMetadata = new List<CompilerVisibleItemMetadataInfo>();
        var files = new List<AdditionalFileItem>();
        string Expanded(string text) => PropertyReference().Replace(text, m => properties.TryGetValue(m.Groups["name"].Value, out var v) ? v : string.Empty);
        foreach (var item in roots.SelectMany(r => r.Elements().Where(e => e.Name.LocalName == "ItemGroup")).SelectMany(g => g.Elements()))
        {
            var include = item.Attribute("Include")?.Value is { } text ? Expanded(text) : null;
            switch (item.Name.LocalName)
            {
                case "CompilerVisibleProperty":
                    visibleProperties.AddRange(Split(include));
                    break;
                case "CompilerVisibleItemMetadata" when Metadata(item, "MetadataName") is { Length: > 0 } metadata:
                    visibleMetadata.AddRange(Split(include).Select(type => new CompilerVisibleItemMetadataInfo(type, metadata.Trim())));
                    break;
                case "AdditionalFiles":
                    files.AddRange(Split(include)
                        .Where(p => !p.Contains("@(", StringComparison.Ordinal) && !p.Contains("%(", StringComparison.Ordinal))
                        .Select(p => new AdditionalFileItem(p, projectDirectory, ItemMetadata(item, Expanded))));
                    break;
            }
        }

        return (visibleProperties, visibleMetadata, files);
    }

    /// <summary>An item's metadata, from its attributes and child elements.</summary>
    private static Dictionary<string, string> ItemMetadata(XElement item, Func<string, string> expand)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in item.Attributes().Where(a => !a.IsNamespaceDeclaration && !ItemAttributes.Contains(a.Name.LocalName)))
        {
            metadata[attribute.Name.LocalName] = expand(attribute.Value.Trim());
        }

        foreach (var child in item.Elements())
        {
            metadata[child.Name.LocalName] = expand(child.Value.Trim());
        }

        return metadata;
    }

    /// <summary>Whether an element's own condition holds; <paramref name="unknown"/> for one that cannot be evaluated.</summary>
    private static bool IsTrueHere(XElement element, BuildFileState state, string file, bool unknown = true) =>
        IsTrueHere(element, state.Properties, file, unknown);

    private static bool IsTrueHere(XElement element, Dictionary<string, string> properties, string file, bool unknown) =>
        element.Attribute("Condition")?.Value is not { } condition
        || (MsBuildConditions.Evaluate(ThisFile(condition, file), properties, Path.GetDirectoryName(file) ?? string.Empty) ?? unknown);

    /// <summary>
    /// Expands <c>$(MSBuildThisFile…)</c> to the file being read and <c>$(Name)</c> to the properties known so far; an
    /// unknown property is empty, as in MSBuild. Property functions are left as they are.
    /// </summary>
    private static string Expand(string text, BuildFileState state, string file) => Expand(text, state.Properties, file);

    private static string Expand(string text, Dictionary<string, string> properties, string file) =>
        PropertyReference().Replace(ThisFile(text, file), match =>
            properties.TryGetValue(match.Groups["name"].Value, out var value) ? value : string.Empty);

    /// <summary>Resolves the <c>$(MSBuildThisFile…)</c> properties, which mean the file that uses them.</summary>
    private static string ThisFile(string text, string file) => MsBuildImports.ExpandThisFile(text, file);

    private static string[] Split(string? value) =>
        (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
