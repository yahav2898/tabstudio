using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Reads MSBuild project files (<c>.csproj</c>, <c>.vbproj</c>, <c>.fsproj</c>, <c>.vcxproj</c>) into a <see cref="ProjectInfo"/>.
/// </summary>
/// <remarks>
/// <para>
/// The parser evaluates the XML itself instead of running MSBuild, so that opening a large solution takes milliseconds
/// on a tablet, and it evaluates it as MSBuild does for Visual Studio's Debug configuration on any CPU:
/// </para>
/// <list type="bullet">
///   <item>
///     Properties in order, through <c>Directory.Build.props</c>, the packages' props, <c>Directory.Packages.props</c>,
///     the project and its imports, the SDK's framework properties, the packages' targets and <c>Directory.Build.targets</c>,
///     each with its condition evaluated when it is reached and its value expanded then, property functions included
///     (see <see cref="MsBuildExpressions"/>).
///   </item>
///   <item>
///     A project with several target frameworks evaluated again for the one TabStudio compiles, with
///     <c>TargetFramework</c> as a global property, as the SDK's inner build is.
///   </item>
///   <item>Items from every one of those files, after all properties, where their conditions hold.</item>
///   <item>The SDK, from the <c>Sdk</c> attribute, an <c>&lt;Sdk&gt;</c> element or an SDK import.</item>
/// </list>
/// <para>
/// It does not run targets. What a target would add is read only where TabStudio knows the target, such as the friends
/// a target adds before compiling.
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
    /// <remarks>
    /// The project is evaluated as MSBuild evaluates it (see <see cref="Evaluate"/>): <c>Directory.Build.props</c> and
    /// what it imports, <c>Directory.Packages.props</c>, the project with what it imports, then
    /// <c>Directory.Build.targets</c>, each property set in order with its condition evaluated when it is reached, and
    /// the items of every file after all properties.
    /// </remarks>
    public static ProjectInfo Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new ProjectLoadException(fullPath, "the file does not exist.", null);
        }

        var root = LoadXml(fullPath, File.ReadAllText(fullPath));
        return WithPlatformInvokeFiles(WithRazorComponents(Build(fullPath, root, new EvaluationOptions(FromDisk: true, [], []))));
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

    /// <summary>Parses project XML that has already been read, without the files MSBuild finds beside it.</summary>
    /// <param name="filePath">The path the XML came from. Used for the project name and to resolve relative paths.</param>
    /// <param name="projectXml">The contents of the project file.</param>
    /// <param name="inheritedProperties">Properties as a <c>Directory.Build.props</c> would set them, which the project may override.</param>
    /// <param name="overridingProperties">
    /// Properties as a <c>Directory.Build.targets</c> would set them: MSBuild imports it after the project, so they override its own.
    /// </param>
    /// <param name="importedFiles">Files imported after the project, whose properties and items the project gets too.</param>
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
        return Build(filePath, root, new EvaluationOptions(FromDisk: false, [], [], inheritedProperties, overridingProperties, importedFiles));
    }

    /// <summary>Evaluates a project and reads what TabStudio needs from it.</summary>
    private static ProjectInfo Build(string filePath, XElement root, EvaluationOptions options)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var evaluation = Evaluate(filePath, root, options);

        // The project's properties are those its files set; the SDK's defaults and the global properties were there for
        // the conditions only.
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in evaluation.Defined)
        {
            if (evaluation.Properties.TryGetValue(name, out var value))
            {
                properties[name] = value;
            }
        }

        // MSBuild's reserved properties, which repositories use for paths such as a key file next to the project.
        properties.TryAdd("MSBuildProjectDirectory", directory);
        properties.TryAdd("MSBuildProjectName", Path.GetFileNameWithoutExtension(filePath));
        properties.TryAdd("MSBuildProjectFile", Path.GetFileName(filePath));
        properties.TryAdd("MSBuildProjectFullPath", filePath);

        // A value with a function that cannot be evaluated here keeps its text; plain references in it still resolve.
        ExpandPropertyReferences(properties);

        var items = evaluation.Items;
        var sdk = ReadSdk(root);
        var assemblyReferences = ReadAssemblyReferences(items);
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

        // Items come from every file the project imports, in MSBuild's order, kept only where their conditions hold for
        // the inner build TabStudio compiles (see Evaluate): Humanizer's benchmarks reference Humanizer's project, or its
        // NuGet package when UseBaselinePackage is true, and its tests get their project references from an imported props.
        List<XElement> allRoots = [items, evaluation.Targets];
        var packages = ReadPackageReferences(
            [items],
            centralVersions: string.Equals(evaluation.Properties.GetValueOrDefault("ManagePackageVersionsCentrally")?.Trim(), "true", StringComparison.OrdinalIgnoreCase));
        if (isLegacy)
        {
            packages.AddRange(ReadPackagesConfig(directory).Where(p => !packages.Any(existing => string.Equals(existing.Name, p.Name, StringComparison.OrdinalIgnoreCase))));
        }

        var projectReferenceItems = ReadProjectReferenceItems(items, directory);
        var projectReferences = projectReferenceItems.Select(r => r.Path).ToList();
        var targetFrameworks = ReadTargetFrameworks(properties);
        var language = LanguageFromExtension(Path.GetExtension(filePath));
        var generatorInputs = ReadGeneratorInputs([items], properties, directory);

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
            FrameworkReferences = ReadFrameworkReferences(items, sdk, properties),
            CompileIncludes = ReadItemPatterns(items, "Compile", "Include", properties),
            CompileIncludeExcludes = ReadIncludeExcludes(items, "Compile", properties),
            CompileRemoves = ReadItemPatterns(items, "Compile", "Remove", properties),
            EmbeddedResourceIncludes = [.. DefaultEmbeddedResources(sdk, evaluation.Properties), .. ReadItemPatterns(items, "EmbeddedResource", "Include", properties)],
            EmbeddedResourceRemoves = ReadItemPatterns(items, "EmbeddedResource", "Remove", properties),
            AvaloniaResourceIncludes = ReadItemPatterns(items, "AvaloniaResource", "Include", properties),
            AvaloniaResourceRemoves = ReadItemPatterns(items, "AvaloniaResource", "Remove", properties),
            AssemblyReferences = assemblyReferences,
            ComReferences = ReadComReferences(items),
            ImportPaths = ReadImportPaths(root),
            Usings = [.. ReadUsings(items).Distinct()],
            RemovedUsings = [.. ReadItemPatterns(items, "Using", "Remove", properties).Distinct(StringComparer.Ordinal)],
            Imports = [.. ReadItemPatterns(items, "Import", "Include", properties).Distinct(StringComparer.Ordinal)],
            RemovedImports = [.. ReadItemPatterns(items, "Import", "Remove", properties).Distinct(StringComparer.Ordinal)],
            CompilerVisibleProperties = [.. generatorInputs.Properties.Distinct(StringComparer.OrdinalIgnoreCase)],
            CompilerVisibleItemMetadata = [.. generatorInputs.Metadata.Distinct()],
            AdditionalFiles = generatorInputs.Files,
        };

        info = info with
        {
            CompilerTargets = TargetItems.CompilerTargetsOf([evaluation.Targets]),
            EmbeddedResourceItems = ReadItemMetadata(items, "EmbeddedResource"),
            ProtobufIncludes = ReadItemPatterns(items, "Protobuf", "Include", properties),
        };
        if (info.CompilerTargets.Count > 0)
        {
            info = info with { AdditionalFiles = [.. info.AdditionalFiles, .. TargetItems.AdditionalFiles(info, evaluation.Properties)] };
        }

        var (kind, isTest) = ProjectClassifier.Classify(info);
        return info with { Kind = kind, IsTestProject = isTest };
    }

    /// <summary>
    /// The <c>EmbeddedResource</c> items the SDK includes by default: every <c>.resx</c> file of an SDK-style project,
    /// unless <c>EnableDefaultItems</c> or <c>EnableDefaultEmbeddedResourceItems</c> is <c>false</c>.
    /// </summary>
    private static string[] DefaultEmbeddedResources(string? sdk, Dictionary<string, string> properties) =>
        sdk is not null
        && !string.Equals(properties.GetValueOrDefault("EnableDefaultItems")?.Trim(), "false", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(properties.GetValueOrDefault("EnableDefaultEmbeddedResourceItems")?.Trim(), "false", StringComparison.OrdinalIgnoreCase)
            ? ["**/*.resx"]
            : [];

    /// <summary>The metadata of an item type's items that include or update files, in order.</summary>
    private static List<ItemMetadataInfo> ReadItemMetadata(XElement items, string itemType)
    {
        var result = new List<ItemMetadataInfo>();
        foreach (var item in items.Descendants().Where(e => e.Name.LocalName == itemType))
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in item.Attributes().Where(a => !ItemAttributes.Contains(a.Name.LocalName) && !a.IsNamespaceDeclaration))
            {
                metadata[attribute.Name.LocalName] = attribute.Value.Trim();
            }

            foreach (var child in item.Elements())
            {
                metadata[child.Name.LocalName] = child.Value.Trim();
            }

            var patterns = item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value;
            if (metadata.Count == 0 || string.IsNullOrWhiteSpace(patterns))
            {
                continue;
            }

            foreach (var pattern in patterns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(p => !p.Contains("$(", StringComparison.Ordinal) && !p.Contains("@(", StringComparison.Ordinal)))
            {
                result.Add(new ItemMetadataInfo(pattern, metadata));
            }
        }

        return result;
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
    private static List<PackageReferenceInfo> ReadPackageReferences(IEnumerable<XElement> roots, bool centralVersions)
    {
        var items = roots.SelectMany((root, index) => index == 0 ? root.Descendants() : root.Descendants().Where(e => !e.Ancestors().Any(a => a.Name.LocalName == "Target"))).ToList();
        var references = items
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
            });

        // With central package management, NuGet's targets add each GlobalPackageReference of Directory.Packages.props to
        // every project, with only the assets a development dependency has, all private (DOT-04).
        var global = centralVersions
            ? items.Where(e => e.Name.LocalName == "GlobalPackageReference" && !string.IsNullOrWhiteSpace(e.Attribute("Include")?.Value))
                .Select(e => new PackageReferenceInfo(e.Attribute("Include")!.Value.Trim(), Metadata(e, "Version")?.Trim(), "All")
                {
                    IncludeAssets = "Runtime;Build;Native;contentFiles;Analyzers",
                })
            : [];
        return [.. references.Concat(global).DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }

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
    /// The output type an SDK sets when the project does not: the Web, Worker, Blazor WebAssembly and WebAssembly SDKs
    /// build apps (the last one the browser head of a cross-platform app), the others libraries.
    /// </summary>
    private static string? DefaultOutputType(string? sdk) =>
        string.Equals(sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sdk, "Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sdk, "Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sdk, "Microsoft.NET.Sdk.WebAssembly", StringComparison.OrdinalIgnoreCase)
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
        var expanded = pattern;
        if (pattern.Contains("$(", StringComparison.Ordinal))
        {
            expanded = PropertyReference().Replace(pattern, match =>
                properties.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Value);
            if (expanded.Contains("$(", StringComparison.Ordinal) || expanded.Length == 0)
            {
                return null;
            }
        }

        // A path from another file, such as $(MSBuildThisFileDirectory)shared/*.cs, is relative to the project as the others are.
        return !string.IsNullOrEmpty(directory) && Path.IsPathRooted(expanded) ? Path.GetRelativePath(directory, expanded) : expanded;
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
