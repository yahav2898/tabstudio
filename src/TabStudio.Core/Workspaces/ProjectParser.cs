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
///   <item>The nearest <c>Directory.Build.targets</c>, whose properties override the project's, as MSBuild imports it last.</item>
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

    /// <summary>The target framework TabStudio compiles every project against, whatever the project targets.</summary>
    public const string CompiledTargetFramework = "net10.0";

    /// <summary>The version the .NET SDK gives a project that sets neither <c>Version</c> nor <c>VersionPrefix</c>.</summary>
    public const string DefaultVersion = "1.0.0";

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

        var directory = Path.GetDirectoryName(fullPath);
        var projectXml = File.ReadAllText(fullPath);

        // MSBuild's evaluation order: Directory.Build.props and what it imports, Directory.Packages.props, the
        // project with what it imports, then Directory.Build.targets and what it imports. Later files win. MSBuild's
        // reserved properties of the project are known from the start, as imports may use them.
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MSBuildProjectName"] = Path.GetFileNameWithoutExtension(fullPath),
            ["MSBuildProjectDirectory"] = directory ?? string.Empty,
        };
        var before = new List<string>();
        if (FindDirectoryBuildProps(directory) is { } directoryBuildProps)
        {
            CollectImports(directoryBuildProps, known, before, []);
        }

        if (FindNearest(directory, "Directory.Packages.props") is { } packagesProps)
        {
            CollectImports(packagesProps, known, before, []);
        }

        var inherited = Merge(before);
        var after = new List<string>();
        Dictionary<string, string> projectProperties;
        XElement? projectRoot = null;
        try
        {
            projectRoot = LoadXml(fullPath, projectXml);
            projectProperties = WithThisFile(ReadProperties(projectRoot), fullPath);
        }
        catch (ProjectLoadException)
        {
            // Parse reports the broken project itself.
            projectProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var (key, value) in projectProperties)
        {
            known[key] = value;
        }

        var visited = new HashSet<string>(before, StringComparer.Ordinal) { fullPath };
        if (projectRoot is not null)
        {
            foreach (var import in ImportsOf(projectRoot, fullPath, known))
            {
                CollectImports(import, known, after, visited);
            }
        }

        if (FindDirectoryBuildTargets(directory) is { } directoryBuildTargets)
        {
            CollectImports(directoryBuildTargets, known, after, visited);
        }

        var overriding = Merge(after);
        return WithPlatformInvokeFiles(WithRazorComponents(Parse(fullPath, projectXml, inherited, overriding, [.. before, .. after])));
    }

    /// <summary>
    /// Adds a props or targets file and the files it imports, imported files first, to a list. An import is followed
    /// when its path, after <c>$(MSBuildThisFileDirectory)</c>, the properties known so far and the
    /// <c>GetPathOfFileAbove</c> and <c>GetDirectoryNameOfFileAbove</c> property functions (see
    /// <see cref="MsBuildImports"/>), names a file that exists, as in
    /// <c>&lt;Import Project="$(MSBuildThisFileDirectory)eng/$(ProjectType).targets" /&gt;</c> or Refit's
    /// <c>&lt;Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" /&gt;</c>;
    /// SDK imports, other property functions and wildcards are not. An import of a file that does not exist is skipped,
    /// as is one whose path comes out empty, as when <c>GetPathOfFileAbove</c> finds nothing.
    /// </summary>
    private static void CollectImports(string file, Dictionary<string, string> known, List<string> files, HashSet<string> visited, int depth = 0)
    {
        if (depth > 8 || !visited.Add(file) || !File.Exists(file))
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
            // A broken import should not stop the project from opening.
            return;
        }

        foreach (var (key, value) in WithThisFile(ReadProperties(root), file))
        {
            known[key] = value;
        }

        foreach (var import in ImportsOf(root, file, known))
        {
            CollectImports(import, known, files, visited, depth + 1);
        }

        files.Add(file);
    }

    /// <summary>The files a project or props file imports whose paths can be worked out without evaluating MSBuild.</summary>
    private static IEnumerable<string> ImportsOf(XElement root, string file, Dictionary<string, string> known)
    {
        var imports = root.Elements()
            .SelectMany(e => e.Name.LocalName == "ImportGroup" ? e.Elements() : [e])
            .Where(e => e.Name.LocalName == "Import" && e.Attribute("Sdk") is null);
        foreach (var import in imports)
        {
            if (import.Attribute("Project")?.Value is not { Length: > 0 } project)
            {
                continue;
            }

            if (MsBuildImports.ResolvePath(project, file, known) is { } path && File.Exists(path))
            {
                yield return path;
            }
        }
    }

    /// <summary>The <c>Project</c> of each <c>&lt;Import&gt;</c> a project file makes itself, as written; SDK imports are left out.</summary>
    private static List<string> ReadImportPaths(XElement root) =>
        root.Elements()
            .SelectMany(e => e.Name.LocalName == "ImportGroup" ? e.Elements() : [e])
            .Where(e => e.Name.LocalName == "Import" && e.Attribute("Sdk") is null)
            .Select(e => e.Attribute("Project")?.Value.Trim())
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .ToList();

    /// <summary>The properties of files in evaluation order, later ones winning; <see langword="null"/> when there are none.</summary>
    private static Dictionary<string, string>? Merge(List<string> files)
    {
        if (files.Count == 0)
        {
            return null;
        }

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            foreach (var (key, value) in ReadPropertiesSafely(file) ?? new Dictionary<string, string>())
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    /// <summary>Parses project XML that has already been read.</summary>
    /// <param name="filePath">The path the XML came from. Used for the project name and to resolve relative paths.</param>
    /// <param name="projectXml">The contents of the project file.</param>
    /// <param name="inheritedProperties">Properties from <c>Directory.Build.props</c>, which the project may override.</param>
    /// <param name="overridingProperties">
    /// Properties from <c>Directory.Build.targets</c>, which MSBuild imports after the project, so they override its own.
    /// </param>
    /// <param name="importedFiles">
    /// <c>Directory.Build.props</c> and <c>.targets</c>, whose <c>InternalsVisibleTo</c> and <c>AssemblyAttribute</c>
    /// items the project gets too.
    /// </param>
    /// <exception cref="ProjectLoadException">The text is not valid XML.</exception>
    public static ProjectInfo Parse(
        string filePath,
        string projectXml,
        IReadOnlyDictionary<string, string>? inheritedProperties = null,
        IReadOnlyDictionary<string, string>? overridingProperties = null,
        IReadOnlyList<string>? importedFiles = null)
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

        foreach (var (key, value) in WithThisFile(ReadProperties(root), filePath))
        {
            properties[key] = value;
        }

        // MSBuild's reserved properties, which repositories use for paths such as a key file next to the project.
        properties.TryAdd("MSBuildProjectDirectory", directory);
        properties.TryAdd("MSBuildProjectName", Path.GetFileNameWithoutExtension(filePath));
        properties.TryAdd("MSBuildProjectFile", Path.GetFileName(filePath));
        properties.TryAdd("MSBuildProjectFullPath", filePath);

        if (overridingProperties is not null)
        {
            foreach (var (key, value) in overridingProperties)
            {
                properties[key] = value;
            }
        }

        ExpandPropertyReferences(properties);

        var sdk = ReadSdk(root);
        var assemblyReferences = ReadAssemblyReferences(root);
        var isLegacy = sdk is null
            && ReadSdks(root).Count == 0
            && LanguageFromExtension(Path.GetExtension(filePath)) is ProjectLanguage.CSharp or ProjectLanguage.VisualBasic or ProjectLanguage.FSharp
            && IsFrameworkVersion(properties.GetValueOrDefault("TargetFrameworkVersion"));
        if (isLegacy)
        {
            AddLegacyProperties(properties, root, assemblyReferences);
        }
        else if (AddVersionFromPrefix(properties))
        {
            // Properties such as <InformationalVersion>$(Version)</InformationalVersion> get it too.
            ExpandPropertyReferences(properties);
        }

        // Items, in the project and in imported files, keep only the groups their conditions allow, evaluated for the
        // target framework TabStudio builds the project for (see EvaluationTargetFramework) and, as MSBuild defaults
        // them, the Debug configuration on any CPU. Humanizer's benchmarks reference Humanizer's project, or its NuGet
        // package when UseBaselinePackage is true: both made every type exist twice.
        var evaluation = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        if (EvaluationTargetFramework(ReadTargetFrameworks(properties)) is { } evaluationFramework)
        {
            evaluation["TargetFramework"] = evaluationFramework;
            AddFrameworkProperties(evaluation, evaluationFramework);
        }

        evaluation.TryAdd("Configuration", "Debug");
        evaluation.TryAdd("Platform", "AnyCPU");
        MsBuildConditions.RemoveFalse(root, evaluation, directory);
        List<XElement> allRoots = [root, .. ImportedRoots(importedFiles, evaluation)];
        var packages = ReadPackageReferences(
            allRoots,
            centralVersions: string.Equals(properties.GetValueOrDefault("ManagePackageVersionsCentrally")?.Trim(), "true", StringComparison.OrdinalIgnoreCase));
        if (isLegacy)
        {
            packages.AddRange(ReadPackagesConfig(directory).Where(p => !packages.Any(existing => string.Equals(existing.Name, p.Name, StringComparison.OrdinalIgnoreCase))));
        }

        var projectReferenceItems = ReadProjectReferenceItems(root, directory);
        var projectReferences = projectReferenceItems.Select(r => r.Path).ToList();
        var targetFrameworks = ReadTargetFrameworks(properties);
        var language = LanguageFromExtension(Path.GetExtension(filePath));
        var generatorInputs = ReadGeneratorInputs(allRoots, properties, directory);

        var info = new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            Language = language,
            Sdk = sdk,
            TargetFrameworks = targetFrameworks,
            OutputType = properties.GetValueOrDefault("OutputType") ?? DefaultOutputType(sdk),
            Properties = properties,
            PackageReferences = packages,
            ProjectReferences = projectReferences,
            ProjectReferenceItems = projectReferenceItems,
            Sdks = ReadSdks(root),
            AssemblyAttributes = AssemblyAttributeItems.Read(
                allRoots,
                properties,
                Path.GetFileNameWithoutExtension(filePath)),
            FrameworkReferences = ReadFrameworkReferences(root, sdk, properties),
            CompileIncludes = ReadItemPatterns(root, "Compile", "Include", properties),
            CompileIncludeExcludes = ReadIncludeExcludes(root, "Compile", properties),
            CompileRemoves = ReadItemPatterns(root, "Compile", "Remove", properties),
            EmbeddedResourceIncludes = ReadItemPatterns(root, "EmbeddedResource", "Include", properties),
            EmbeddedResourceRemoves = ReadItemPatterns(root, "EmbeddedResource", "Remove", properties),
            AvaloniaResourceIncludes = ReadItemPatterns(root, "AvaloniaResource", "Include", properties),
            AvaloniaResourceRemoves = ReadItemPatterns(root, "AvaloniaResource", "Remove", properties),
            AssemblyReferences = assemblyReferences,
            ComReferences = ReadComReferences(root),
            ImportPaths = ReadImportPaths(root),
            Usings = [.. allRoots.SelectMany(r => ReadUsings(r)).Distinct()],
            RemovedUsings = [.. allRoots.SelectMany(r => ReadItemPatterns(r, "Using", "Remove", properties)).Distinct(StringComparer.Ordinal)],
            Imports = [.. allRoots.SelectMany(r => ReadItemPatterns(r, "Import", "Include", properties)).Distinct(StringComparer.Ordinal)],
            RemovedImports = [.. allRoots.SelectMany(r => ReadItemPatterns(r, "Import", "Remove", properties)).Distinct(StringComparer.Ordinal)],
            CompilerVisibleProperties = [.. generatorInputs.Properties.Distinct(StringComparer.OrdinalIgnoreCase)],
            CompilerVisibleItemMetadata = [.. generatorInputs.Metadata.Distinct()],
            AdditionalFiles = generatorInputs.Files,
        };

        var (kind, isTest) = ProjectClassifier.Classify(info);
        return info with { Kind = kind, IsTestProject = isTest };
    }

    /// <summary>
    /// The target framework conditions in a project's imported files and in its <c>Directory.Packages.props</c> are
    /// evaluated for: the one TabStudio builds the project for, which compiles every project against
    /// <see cref="CompiledTargetFramework"/>. In order:
    /// </summary>
    /// <remarks>
    /// <list type="number">
    ///   <item><c>net10.0</c> itself, when the project lists it.</item>
    ///   <item>
    ///     Otherwise the .NET (Core) framework (<c>netcoreappX.Y</c>, or <c>netX.Y</c> from .NET 5 on, with or without
    ///     a platform such as <c>-android</c>) of the highest version up to 10.0, whose assets .NET 10 can use; when
    ///     there is none, the one of the lowest version above it. Of several with the same version, the first listed.
    ///   </item>
    ///   <item>Otherwise, for a project only for .NET Framework or .NET Standard, the first one listed.</item>
    /// </list>
    /// <para>
    /// Refit's <c>net462;net470;…;net10.0;net11.0</c> is evaluated for <c>net10.0</c>, so a <c>&lt;When&gt;</c> for
    /// <c>net11.0</c> or an item group for <c>net462</c> does not apply, as it does not to the code TabStudio compiles.
    /// </para>
    /// </remarks>
    /// <param name="targetFrameworks">The project's target frameworks, in the order its file lists them.</param>
    /// <returns>The framework, or <see langword="null"/> when the project lists none.</returns>
    public static string? EvaluationTargetFramework(IReadOnlyList<string> targetFrameworks)
    {
        ArgumentNullException.ThrowIfNull(targetFrameworks);
        if (targetFrameworks.Count == 0)
        {
            return null;
        }

        var exact = targetFrameworks.FirstOrDefault(f => string.Equals(f, CompiledTargetFramework, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var compiled = new Version(10, 0);
        var modern = targetFrameworks
            .Select(f => (Framework: f, Version: DotNetVersionOf(f)))
            .Where(f => f.Version is not null)
            .Select(f => (f.Framework, Version: f.Version!))
            .ToList();
        var usable = modern.Where(f => f.Version <= compiled).ToList();
        if (usable.Count > 0)
        {
            // MaxBy keeps the first of equal versions.
            return usable.MaxBy(f => f.Version).Framework;
        }

        return modern.Count > 0 ? modern.MinBy(f => f.Version).Framework : targetFrameworks[0];
    }

    /// <summary>
    /// The version of a .NET (Core) target framework: <c>8.0</c> for <c>net8.0</c> or <c>net8.0-android</c>, <c>3.1</c>
    /// for <c>netcoreapp3.1</c>; <see langword="null"/> for .NET Framework (<c>net48</c>), .NET Standard and others.
    /// </summary>
    private static Version? DotNetVersionOf(string framework)
    {
        var name = framework.Trim();
        var dash = name.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            name = name[..dash];
        }

        string digits;
        var isCoreApp = name.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase);
        if (isCoreApp)
        {
            digits = name["netcoreapp".Length..];
        }
        else if (name.StartsWith("net", StringComparison.OrdinalIgnoreCase) && name.Length > 3 && char.IsAsciiDigit(name[3]))
        {
            digits = name[3..];
        }
        else
        {
            return null;
        }

        // net48 and net472 have no dot: they are .NET Framework.
        if (!digits.Contains('.', StringComparison.Ordinal) || !Version.TryParse(digits, out var version))
        {
            return null;
        }

        return isCoreApp || version.Major >= 5 ? version : null;
    }

    /// <summary>
    /// Sets <c>Version</c> as the .NET SDK does when a project gives only its parts: <c>VersionPrefix</c> (1.0.0 when it
    /// is not set) followed by <c>-</c> and <c>VersionSuffix</c> when that is set, as StronglyTypedId's
    /// <c>version.props</c> gives <c>1.0.0</c> and <c>beta08</c>. A project that sets neither keeps no <c>Version</c>,
    /// as the parser leaves out the SDK's other defaults (and a version from git tags, as MinVer's, is not known);
    /// <see cref="DefaultVersion"/> is the SDK's value then.
    /// </summary>
    /// <returns>Whether <c>Version</c> was set.</returns>
    private static bool AddVersionFromPrefix(Dictionary<string, string> properties)
    {
        if (properties.ContainsKey("Version"))
        {
            return false;
        }

        var prefix = properties.GetValueOrDefault("VersionPrefix")?.Trim();
        var suffix = properties.GetValueOrDefault("VersionSuffix")?.Trim();
        if (prefix is null && suffix is null)
        {
            return false;
        }

        var version = string.IsNullOrEmpty(prefix) ? DefaultVersion : prefix;
        properties["Version"] = string.IsNullOrEmpty(suffix) ? version : version + "-" + suffix;
        return true;
    }

    /// <summary>
    /// Finds the <c>Directory.Build.props</c> that MSBuild would import for a project in <paramref name="startDirectory"/>:
    /// the nearest one in that directory or any parent.
    /// </summary>
    /// <param name="startDirectory">The directory of the project file.</param>
    /// <returns>The full path, or <see langword="null"/> when there is none.</returns>
    public static string? FindDirectoryBuildProps(string? startDirectory) => FindNearest(startDirectory, "Directory.Build.props");

    /// <summary>
    /// Finds the <c>Directory.Build.targets</c> that MSBuild would import for a project in <paramref name="startDirectory"/>:
    /// the nearest one in that directory or any parent.
    /// </summary>
    /// <param name="startDirectory">The directory of the project file.</param>
    /// <returns>The full path, or <see langword="null"/> when there is none.</returns>
    public static string? FindDirectoryBuildTargets(string? startDirectory) => FindNearest(startDirectory, "Directory.Build.targets");

    private static string? FindNearest(string? startDirectory, string fileName)
    {
        var current = string.IsNullOrEmpty(startDirectory) ? null : new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, fileName);
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
            return WithThisFile(ReadProperties(LoadXml(propsPath, File.ReadAllText(propsPath))), propsPath);
        }
        catch (ProjectLoadException)
        {
            // A broken Directory.Build.props or .targets should not stop the project from opening.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves <c>$(MSBuildThisFileDirectory)</c>, <c>$(MSBuildThisFileFullPath)</c> and <c>$(MSBuildThisFile)</c> in
    /// one file's properties, which mean the file that sets them, such as
    /// <c>$(MSBuildThisFileDirectory)assets/Serilog.snk</c> in a <c>Directory.Build.props</c>.
    /// </summary>
    private static Dictionary<string, string> WithThisFile(Dictionary<string, string> properties, string file)
    {
        var folder = (Path.GetDirectoryName(file) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var key in properties.Keys.ToList())
        {
            properties[key] = properties[key]
                .Replace("$(MSBuildThisFileDirectory)", folder, StringComparison.OrdinalIgnoreCase)
                .Replace("$(MSBuildThisFileFullPath)", file, StringComparison.OrdinalIgnoreCase)
                .Replace("$(MSBuildThisFile)", Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
        }

        return properties;
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

                // Compilation symbols decide which code compiles: a value whose condition is not known to hold, such
                // as Serilog's FEATURE_REMOTING for net48, compiled code for another framework into errors.
                if (isConditional && name.Equals("DefineConstants", StringComparison.OrdinalIgnoreCase))
                {
                    if (Holds(group.Attribute("Condition")?.Value, unconditional) && Holds(property.Attribute("Condition")?.Value, unconditional))
                    {
                        unconditional[name] = value;
                    }

                    continue;
                }

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

    /// <summary>Whether a condition is known to hold with the properties read so far; no condition always holds.</summary>
    private static bool Holds(string? condition, IReadOnlyDictionary<string, string> properties) =>
        condition is null || MsBuildConditions.Evaluate(condition, properties, string.Empty) == true;

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

    /// <summary>
    /// The package references of a project, then of the files it imports: the first reference to a package wins. With
    /// central package management on, a reference's version is its <c>VersionOverride</c>, which wins over the version
    /// in <c>Directory.Packages.props</c>, as Refit's <c>VersionOverride="$(MsCACSharpVersion)"</c> does, or else its
    /// <c>Version</c>; with it off, NuGet ignores <c>VersionOverride</c>, which then counts only for a reference without
    /// a <c>Version</c>.
    /// </summary>
    private static List<PackageReferenceInfo> ReadPackageReferences(IEnumerable<XElement> roots, bool centralVersions) =>
        roots.SelectMany((root, index) => index == 0 ? root.Descendants() : root.Descendants().Where(e => !e.Ancestors().Any(a => a.Name.LocalName == "Target")))
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (Name: e.Attribute("Include")?.Value, Element: e))
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new PackageReferenceInfo(
                p.Name!.Trim(),
                ReferenceVersion(p.Element, centralVersions),
                Metadata(p.Element, "PrivateAssets")?.Trim())
            {
                IncludeAssets = Metadata(p.Element, "IncludeAssets")?.Trim(),
                ExcludeAssets = Metadata(p.Element, "ExcludeAssets")?.Trim(),
            })
            .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>A package reference's version (see <see cref="ReadPackageReferences"/>); <see langword="null"/> when it states none.</summary>
    private static string? ReferenceVersion(XElement reference, bool centralVersions)
    {
        var version = Metadata(reference, "Version");
        var versionOverride = Metadata(reference, "VersionOverride");
        if (string.IsNullOrWhiteSpace(versionOverride) || (!centralVersions && !string.IsNullOrWhiteSpace(version)))
        {
            return version;
        }

        return versionOverride;
    }

    /// <summary>
    /// The shared frameworks a project uses: <c>FrameworkReference</c> items, plus ASP.NET Core for projects on the
    /// Web SDK, which adds it implicitly.
    /// </summary>
    /// <summary>
    /// The project's shared frameworks: its own <c>FrameworkReference</c> items, plus ASP.NET Core where the SDK adds it
    /// implicitly: the Web SDK always, and the Razor SDK when <c>AddRazorSupportForMvc</c> is on (a Razor class library
    /// with MVC views or Razor Pages).
    /// </summary>
    private static List<string> ReadFrameworkReferences(XElement root, string? sdk, Dictionary<string, string> properties)
    {
        var frameworks = ReadItemPatterns(root, "FrameworkReference", "Include", properties);
        var addsAspNetCore =
            string.Equals(sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(sdk, "Microsoft.NET.Sdk.Razor", StringComparison.OrdinalIgnoreCase)
                && string.Equals(properties.GetValueOrDefault("AddRazorSupportForMvc"), "true", StringComparison.OrdinalIgnoreCase));
        if (addsAspNetCore
            && !frameworks.Contains(AspNetCoreFramework, StringComparer.OrdinalIgnoreCase))
        {
            frameworks.Insert(0, AspNetCoreFramework);
        }

        return frameworks;
    }

    /// <summary>
    /// The output type an SDK sets when the project does not: the Web, Worker and Blazor WebAssembly SDKs build apps,
    /// the others libraries.
    /// </summary>
    private static string? DefaultOutputType(string? sdk) =>
        string.Equals(sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sdk, "Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sdk, "Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase)
            ? "Exe"
            : null;

    private static List<ProjectReferenceInfo> ReadProjectReferenceItems(XElement root, string projectDirectory) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (Include: e.Attribute("Include")?.Value, Item: e))
            .Where(r => !string.IsNullOrWhiteSpace(r.Include))
            .Select(r => new ProjectReferenceInfo(
                PathUtilities.ResolveRelative(projectDirectory, r.Include!),
                ReferenceOutputAssembly: !string.Equals(Metadata(r.Item, "ReferenceOutputAssembly")?.Trim(), "false", StringComparison.OrdinalIgnoreCase),
                IsPrivate: AssemblyAttributeItems.IncludesAll(Metadata(r.Item, "PrivateAssets")),
                OutputItemType: Metadata(r.Item, "OutputItemType")?.Trim() is { Length: > 0 } type ? type : null))
            .DistinctBy(r => r.Path, StringComparer.Ordinal)
            .ToList();

    /// <summary>The roots of <c>Directory.Build.props</c> and <c>.targets</c>; files that cannot be read are skipped.</summary>
    private static IEnumerable<XElement> ImportedRoots(IReadOnlyList<string>? files, Dictionary<string, string> properties)
    {
        foreach (var file in files ?? [])
        {
            XElement? root = null;
            try
            {
                root = LoadXml(file, File.ReadAllText(file));
            }
            catch (Exception ex) when (ex is ProjectLoadException or IOException or UnauthorizedAccessException)
            {
                // A broken Directory.Build.props or .targets should not stop the project from opening.
            }

            if (root is not null)
            {
                MsBuildConditions.RemoveFalse(root, properties, Path.GetDirectoryName(file) ?? string.Empty);
                yield return root;
            }
        }
    }

    /// <summary>
    /// Every SDK a project names: in the <c>Sdk</c> attribute (which may list several), in <c>&lt;Sdk Name="..."&gt;</c>
    /// elements and in SDK imports, without versions.
    /// </summary>
    private static List<string> ReadSdks(XElement root)
    {
        var names = new List<string>();
        void Add(string? value)
        {
            foreach (var sdk in (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                names.Add(NormalizeSdkName(sdk));
            }
        }

        Add(root.Attribute("Sdk")?.Value);
        foreach (var element in root.Elements().Where(e => e.Name.LocalName == "Sdk"))
        {
            Add(element.Attribute("Name")?.Value);
        }

        foreach (var import in root.Elements().Where(e => e.Name.LocalName == "Import"))
        {
            Add(import.Attribute("Sdk")?.Value);
        }

        return [.. names.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

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
    /// <summary>
    /// The patterns of an item type's <paramref name="attribute"/>, with the project's properties expanded, as MSBuild
    /// expands them: Spectre.Console's <c>&lt;Compile Remove="$(CompilerGeneratedFilesOutputPath)/**/*.cs"/&gt;</c> is
    /// <c>Generated/**/*.cs</c>. A pattern that names a property the project does not set is left out rather than
    /// expanded to nothing, which would turn <c>$(Unset)/**/*.cs</c> into every file of the project. A pattern that
    /// becomes an absolute path is made relative to the project's folder, as the others are.
    /// </summary>
    private static List<string> ReadItemPatterns(XElement root, string itemType, string attribute, Dictionary<string, string> properties)
    {
        var directory = properties.GetValueOrDefault("MSBuildProjectDirectory");
        return root.Descendants()
            .Where(e => e.Name.LocalName == itemType)
            .Select(e => e.Attribute(attribute)?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(pattern => ExpandPattern(pattern, properties, directory))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The <c>Exclude</c> patterns of an item type's items, keyed by each of their <c>Include</c> patterns, both
    /// expanded as <see cref="ReadItemPatterns"/> expands them. An item without <c>Exclude</c> gives its includes none,
    /// which are then left out; the first item with an include counts.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>> ReadIncludeExcludes(XElement root, string itemType, Dictionary<string, string> properties)
    {
        var directory = properties.GetValueOrDefault("MSBuildProjectDirectory");
        List<string> Patterns(string? value) =>
            [.. (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(pattern => ExpandPattern(pattern, properties, directory))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)];

        var excludes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var item in root.Descendants().Where(e => e.Name.LocalName == itemType && e.Attribute("Include") is not null))
        {
            var exclude = Patterns(item.Attribute("Exclude")?.Value);
            foreach (var include in Patterns(item.Attribute("Include")?.Value))
            {
                excludes.TryAdd(include, exclude);
            }
        }

        foreach (var include in excludes.Where(e => e.Value.Count == 0).Select(e => e.Key).ToList())
        {
            excludes.Remove(include);
        }

        return excludes;
    }

    private static string? ExpandPattern(string pattern, Dictionary<string, string> properties, string? directory)
    {
        if (!pattern.Contains("$(", StringComparison.Ordinal))
        {
            return pattern;
        }

        var expanded = PropertyReference().Replace(pattern, match =>
            properties.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Value);
        if (expanded.Contains("$(", StringComparison.Ordinal) || expanded.Length == 0)
        {
            return null;
        }

        return !string.IsNullOrEmpty(directory) && Path.IsPathRooted(expanded) ? Path.GetRelativePath(directory, expanded) : expanded;
    }

    /// <summary>
    /// The properties the SDK derives from a target framework, which conditions test as often as the framework itself:
    /// <c>TargetFrameworkIdentifier</c> (<c>.NETCoreApp</c>, <c>.NETStandard</c>, <c>.NETFramework</c>),
    /// <c>TargetFrameworkVersion</c> (<c>v10.0</c>) and, for a platform framework such as <c>net10.0-android</c>,
    /// <c>TargetPlatformIdentifier</c>. A project that sets one keeps its own.
    /// </summary>
    private static void AddFrameworkProperties(Dictionary<string, string> evaluation, string targetFramework)
    {
        var dash = targetFramework.IndexOf('-', StringComparison.Ordinal);
        var framework = (dash < 0 ? targetFramework : targetFramework[..dash]).ToLowerInvariant();
        (string? identifier, string? version) =
            framework.StartsWith("netstandard", StringComparison.Ordinal) ? (".NETStandard", framework["netstandard".Length..])
            : framework.StartsWith("netcoreapp", StringComparison.Ordinal) ? (".NETCoreApp", framework["netcoreapp".Length..])
            : framework.StartsWith("net", StringComparison.Ordinal) && framework.Contains('.', StringComparison.Ordinal) ? (".NETCoreApp", framework[3..])
            : framework.StartsWith("net", StringComparison.Ordinal) && framework.Length > 3 && framework[3..].All(char.IsAsciiDigit)
                ? (".NETFramework", string.Join('.', framework[3..].ToCharArray()))
            : (null, null);
        if (identifier is null)
        {
            return;
        }

        evaluation.TryAdd("TargetFrameworkIdentifier", identifier);
        evaluation.TryAdd("TargetFrameworkVersion", "v" + version);
        if (dash >= 0)
        {
            var platform = targetFramework[(dash + 1)..];
            var digits = platform.AsSpan().IndexOfAnyInRange('0', '9');
            evaluation.TryAdd("TargetPlatformIdentifier", digits < 0 ? platform : platform[..digits]);
        }
    }

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
