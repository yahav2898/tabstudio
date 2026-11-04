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
/// <c>BeforeCompile</c>, or to another target that counts, or a property such as <c>CoreCompileDependsOn</c> lists it.
/// Targets hooked before another run first, and the targets a target depends on (<c>DependsOnTargets</c>) run just before
/// it. A target's property groups set properties for the targets after it, and the tasks that write a source file
/// (<c>Copy</c>, <c>WriteLinesToFile</c>) write it in memory (see <see cref="CompileFiles"/>).
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
        "ResolvePackageDependenciesForBuild", "ResolveNuGetPackageAssets", "ResolvePackageAssets", "ResolveTargetingPackAssets",
        "RemoveDuplicateAnalyzers", "BuildOnlySettings",
    };

    /// <summary>
    /// The SDK's targets in the order a build runs them, so that targets hooked to an earlier one run first; a target
    /// hooked to none of them runs last.
    /// </summary>
    private static readonly string[] SdkTargetOrder =
    [
        "BuildOnlySettings", "BeforeBuild", "PrepareForBuild", "ResolvePackageAssets", "ResolveTargetingPackAssets",
        "ResolvePackageDependenciesForBuild", "ResolveNuGetPackageAssets", "ResolveReferences", "AssignTargetPaths",
        "PrepareResourceNames", "PrepareResources", "_GenerateCompileInputs", "BeforeCompile", "CoreGenerateAssemblyInfo",
        "GenerateMSBuildEditorConfigFileShouldRun", "GenerateMSBuildEditorConfigFileCore", "RemoveDuplicateAnalyzers",
        "CoreCompile", "Compile", "CompileDesignTime",
    ];

    /// <summary>The item types whose items the compiler or its generators get, which are kept from what targets add.</summary>
    private static readonly HashSet<string> CompilerItemTypes = new(StringComparer.OrdinalIgnoreCase) { "AdditionalFiles" };

    /// <summary>The targets of some files that run before the compiler (see the remarks), in the order they run.</summary>
    /// <param name="roots">The files' roots, each with its <c>&lt;Target&gt;</c> elements.</param>
    public static IReadOnlyList<XElement> CompilerTargetsOf(IEnumerable<XElement> roots) => CompilerTargetsOf(roots, []);

    /// <summary>
    /// The targets of some files that run before the compiler (see the remarks), in the order they run, with the targets
    /// each depends on (<c>DependsOnTargets</c>) just before it, as MSBuild runs them.
    /// </summary>
    /// <param name="roots">The files' roots, each with its <c>&lt;Target&gt;</c> elements.</param>
    /// <param name="running">
    /// The names of targets that run before the compiler because a property of the SDK's lists them, as IFluentInterface
    /// adds its target to <c>CoreCompileDependsOn</c> (see <see cref="RunningTargets"/>).
    /// </param>
    public static IReadOnlyList<XElement> CompilerTargetsOf(IEnumerable<XElement> roots, IEnumerable<string> running)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(running);
        var targets = roots.SelectMany(r => r.Elements().Where(e => e.Name.LocalName == "Target")).ToList();
        var listed = new HashSet<string>(running, StringComparer.OrdinalIgnoreCase);
        var chosen = new List<XElement>();
        var names = new HashSet<string>(CompilerTargets, StringComparer.OrdinalIgnoreCase);
        names.UnionWith(listed);
        var definitions = targets.Where(t => NameOf(t).Length > 0).GroupBy(t => NameOf(t), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
        bool added;
        do
        {
            added = false;
            foreach (var target in targets.Where(t => !chosen.Contains(t)))
            {
                if (Hooks(target).Any(names.Contains) || listed.Contains(NameOf(target)))
                {
                    chosen.Add(target);
                    names.Add(NameOf(target));
                    added = true;
                }
            }

            // A target another one depends on runs too, and so do those hooked to it: ThisAssembly.Project's
            // PrepareProjectProperties runs before PrepareConstants, which ThisAssembly.Constants' target depends on.
            foreach (var dependency in chosen.ToList().SelectMany(t => Split(t.Attribute("DependsOnTargets")?.Value)))
            {
                if (dependency.Contains("$(", StringComparison.Ordinal))
                {
                    continue;
                }

                names.Add(dependency);
                if (definitions.TryGetValue(dependency, out var defined) && !chosen.Contains(defined))
                {
                    chosen.Add(defined);
                    added = true;
                }
            }
        }
        while (added);

        // The SDK's targets run in their order, and a target hooked to one runs with it: Moq's AddConstants, before
        // PrepareForBuild, adds the FileConstant items PrepareConstants, before GenerateMSBuildEditorConfigFileShouldRun,
        // reads, wherever their files put them.
        var stage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < SdkTargetOrder.Length; i++)
        {
            stage[SdkTargetOrder[i]] = i;
        }

        bool changed;
        var pass = 0;
        do
        {
            changed = false;
            foreach (var target in chosen)
            {
                var name = NameOf(target);
                var own = Hooks(target).Select(h => stage.GetValueOrDefault(h, int.MaxValue))
                    .Concat(chosen.Where(t => Split(t.Attribute("DependsOnTargets")?.Value).Contains(name, StringComparer.OrdinalIgnoreCase)).Select(t => stage.GetValueOrDefault(NameOf(t), int.MaxValue)))
                    .DefaultIfEmpty(int.MaxValue).Min();
                if (name.Length > 0 && own < stage.GetValueOrDefault(name, int.MaxValue) && !SdkTargetOrder.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    stage[name] = own;
                    changed = true;
                }
            }
        }
        while (changed && ++pass < 32);

        int StageOf(XElement target) => stage.GetValueOrDefault(NameOf(target), Hooks(target).Select(h => stage.GetValueOrDefault(h, int.MaxValue)).DefaultIfEmpty(int.MaxValue).Min());

        // A target hooked before another chosen one runs before it.
        var byName = chosen.Where(t => NameOf(t).Length > 0).GroupBy(t => NameOf(t), StringComparer.OrdinalIgnoreCase)
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

        var ordered = chosen.Select((t, i) => (Target: t, Index: i, Depth: Depth(t, 0), Stage: StageOf(t)))
            .OrderBy(t => t.Stage).ThenByDescending(t => t.Depth).ThenBy(t => t.Index).Select(t => t.Target);

        // Each target's dependencies run first, once, as MSBuild runs them: IFluentInterface's target depends on the one
        // that writes its source, and Microsoft.Testing.Platform's on the one that generates SelfRegisteredExtensions. A
        // target defined twice is the last definition, as in MSBuild.
        var all = definitions;
        var result = new List<XElement>();
        var visited = new HashSet<XElement>();
        void Visit(XElement target, int guard)
        {
            if (guard > 32 || !visited.Add(target))
            {
                return;
            }

            foreach (var dependency in Split(target.Attribute("DependsOnTargets")?.Value).Where(n => !n.Contains("$(", StringComparison.Ordinal)))
            {
                if (all.TryGetValue(dependency, out var found))
                {
                    Visit(found, guard + 1);
                }
            }

            result.Add(target);
        }

        foreach (var target in ordered)
        {
            Visit(target, 0);
        }

        return result;
    }

    /// <summary>
    /// The targets the SDK's properties list to run before the compiler: <c>CoreCompileDependsOn</c>,
    /// <c>PrepareForBuildDependsOn</c>, <c>ResolveReferencesDependsOn</c> and <c>PrepareResourcesDependsOn</c>, without the
    /// parts that still name a property.
    /// </summary>
    /// <param name="properties">The project's properties, with those its packages' files set.</param>
    public static IReadOnlyList<string> RunningTargets(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return [.. DependsOnProperties
            .SelectMany(name => Split(properties.GetValueOrDefault(name)))
            .Where(n => !n.Contains("$(", StringComparison.Ordinal) && !n.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The SDK's properties whose targets run before the compiler (see <see cref="RunningTargets"/>).</summary>
    private static readonly string[] DependsOnProperties = ["CoreCompileDependsOn", "PrepareForBuildDependsOn", "ResolveReferencesDependsOn", "PrepareResourcesDependsOn"];

    private static string NameOf(XElement target) => target.Attribute("Name")?.Value.Trim() ?? string.Empty;

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

        var items = Seeds(project, properties);
        var run = Run(project, properties, items);

        // A file two targets add, as ThisAssembly.Resources and ThisAssembly.Strings both add a .resx file (one from
        // @(EmbeddedResource), the other from @(ResxCode)), is one file to the compiler, whose generators see the metadata
        // of every item type it was on the way: the SDK writes build_metadata.{ItemType}.{Name} from each type's items.
        var files = new List<Item>();
        var byPath = new Dictionary<string, Item>(StringComparer.Ordinal);
        foreach (var item in run.Added.Where(i => CompilerItemTypes.Contains(i.Type) && items.GetValueOrDefault(i.Type)?.Contains(i) == true))
        {
            if (byPath.TryGetValue(item.FullPath, out var first))
            {
                foreach (var (name, value) in item.Metadata)
                {
                    first.Metadata[name] = value;
                }

                first.Types.AddRange([.. item.Types.Where(t => !first.Types.Contains(t, StringComparer.OrdinalIgnoreCase))]);
                continue;
            }

            var copy = new Item(item.Type, item.FullPath, new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase), item.Types, item.IsFile);
            byPath[item.FullPath] = copy;
            files.Add(copy);
        }

        return [.. files.Select(i => new AdditionalFileItem(Path.GetRelativePath(project.DirectoryPath, i.FullPath), project.DirectoryPath, i.Metadata)
        {
            ItemTypes = [.. i.Types],
            IsItemOnly = !i.IsFile,
        })];
    }

    /// <summary>
    /// The items the targets start from: the project's <c>EmbeddedResource</c> items, its <c>Compile</c> files when a
    /// target reads them (Moq's tests make a ThisAssembly constant of each scenario file), and its other items outside
    /// targets (<see cref="ProjectInfo.TargetInputItems"/>), such as ThisAssembly.Project's <c>ProjectProperty</c>.
    /// </summary>
    private static Dictionary<string, List<Item>> Seeds(ProjectInfo project, IReadOnlyDictionary<string, string> properties)
    {
        var items = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase)
        {
            ["EmbeddedResource"] = EmbeddedResources(project, properties),
        };
        if (project.CompilerTargets.Any(t => CompileReference().IsMatch(t.ToString())))
        {
            items["Compile"] = [.. ProjectSourceFiles.Enumerate(project, extension: project.Language == ProjectLanguage.VisualBasic ? ".vb" : ".cs")
                .Select(path => new Item("Compile", path, WellKnownMetadata(project, path), []))];
        }

        foreach (var input in project.TargetInputItems)
        {
            if (!items.TryGetValue(input.ItemType, out var list))
            {
                items[input.ItemType] = list = [];
            }

            if (NamedItem(project, input.Include) is { } item)
            {
                foreach (var (name, value) in input.Metadata)
                {
                    item.Metadata[name] = MsBuildExpressions.Expand(value, properties, null, project.DirectoryPath) ?? value;
                }

                list.Add(new Item(input.ItemType, item.FullPath, item.Metadata, [], item.IsFile));
            }
        }

        return items;
    }

    /// <summary>
    /// An item MSBuild makes of a name, which is a file's path or not (<c>Project.MSBuildProjectDirectory</c>): its full
    /// path under the project's folder and its well-known metadata either way; <see langword="null"/> for no path.
    /// </summary>
    private static Item? NamedItem(ProjectInfo project, string name)
    {
        if (FullPath(project, name) is not { } path)
        {
            return null;
        }

        var metadata = WellKnownMetadata(project, path);
        if (!Path.IsPathRooted(name.Trim()))
        {
            metadata["Identity"] = name.Trim();
        }

        return new Item("File", path, metadata, [], File.Exists(path));
    }

    /// <summary>
    /// The sources a project's targets write before the compiler runs and add to its <c>Compile</c> items, from inside a
    /// target or outside one (see the remarks of <see cref="TargetItems"/>), with the text they would write: with
    /// <c>Copy</c>, as TypeNameFormatter.Sources copies its source into <c>obj/</c>; with <c>WriteLinesToFile</c>, as
    /// IFluentInterface writes its interface there; or with Microsoft.Testing.Platform's
    /// <c>TestingPlatformSelfRegisteredExtensions</c> task. TabStudio runs no task, so without them the code the project
    /// uses from those files is missing. A file no target writes is left out: what decides whether a package's own file
    /// is compiled, such as <c>$(MSBuildVersion)</c> for Stunts', is not always known here.
    /// </summary>
    /// <param name="project">The project, with its <see cref="ProjectInfo.CompilerTargets"/>.</param>
    /// <param name="properties">The properties the targets see.</param>
    /// <param name="includes">The <c>Include</c> of the <c>Compile</c> items outside targets, expanded, relative to the project's folder or full.</param>
    public static IReadOnlyList<CompileFileItem> CompileFiles(ProjectInfo project, IReadOnlyDictionary<string, string> properties, IReadOnlyList<string> includes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(includes);
        if (project.CompilerTargets.Count == 0 && includes.Count == 0)
        {
            return [];
        }

        var items = Seeds(project, properties);
        var run = project.CompilerTargets.Count == 0 ? new TargetRun(properties) : Run(project, properties, items);
        var paths = new List<string>();
        foreach (var include in includes)
        {
            if (new GlobPattern(project.DirectoryPath, include).HasWildcards)
            {
                paths.AddRange(ProjectSourceFiles.EnumerateItems(project, [include], []));
            }
            else if (FullPath(project, include) is { } path)
            {
                paths.Add(path);
            }
        }

        paths.AddRange(run.Added.Where(i => string.Equals(i.Type, "Compile", StringComparison.OrdinalIgnoreCase) && items.GetValueOrDefault("Compile")?.Contains(i) == true).Select(i => i.FullPath));
        return [.. paths.Distinct(StringComparer.Ordinal)
            .Where(run.Files.ContainsKey)
            .Select(path => new CompileFileItem(path, run.Files[path]))];
    }

    /// <summary>
    /// The analyzer assemblies a project's targets remove before the compiler runs, as a package that replaces another's
    /// generator does: Microsoft.Extensions.Telemetry.Abstractions sets <c>DisableMicrosoftExtensionsLoggingSourceGenerator</c>
    /// and removes the <c>Analyzer</c> items of Microsoft.Extensions.Logging's, whose <c>[LoggerMessage]</c> code its own
    /// generator writes instead; with both, every logging method would be implemented twice (CS0757).
    /// </summary>
    /// <param name="project">The project, with its <see cref="ProjectInfo.CompilerTargets"/> and its properties.</param>
    /// <param name="analyzers">The analyzers it gets from .NET and its packages, with the package each comes from.</param>
    /// <returns>The full paths of the analyzers that are removed.</returns>
    public static IReadOnlyList<string> RemovedAnalyzers(ProjectInfo project, IReadOnlyList<AnalyzerItem> analyzers)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analyzers);
        if (analyzers.Count == 0 || !project.CompilerTargets.Any(t => t.ToString().Contains("Analyzer", StringComparison.Ordinal)))
        {
            return [];
        }

        // The SDK picks a package's analyzers for the compiler's Roslyn version itself, which packages' targets check.
        var properties = new Dictionary<string, string>(project.Properties, StringComparer.OrdinalIgnoreCase);
        properties.TryAdd("SupportsRoslynComponentVersioning", "true");

        // The SDK's C# targets say where the compiler is; packages' targets take an empty value to mean there is none and
        // remove their generators, as CommunityToolkit.Mvvm's MVVMToolkitRemoveAnalyzersForRosynNotFound does. Since its
        // DependsOnTargets run here too, that removed [ObservableProperty]'s generator from every C# project.
        if (project.Language == ProjectLanguage.CSharp)
        {
            properties.TryAdd("CSharpCoreTargetsPath", Path.Combine(AppContext.BaseDirectory, "Roslyn", "Microsoft.CSharp.Core.targets"));
        }
        var seeds = analyzers.Select(a =>
        {
            var metadata = WellKnownMetadata(project, a.FullPath);
            metadata["Identity"] = a.FullPath;
            metadata["AssemblyName"] = Path.GetFileNameWithoutExtension(a.FullPath);
            metadata["NuGetPackageId"] = a.PackageId ?? string.Empty;
            return new Item("Analyzer", a.FullPath, metadata, []);
        }).ToList();
        var items = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase) { ["Analyzer"] = [.. seeds] };
        if (project.CompilerTargets.Any(t => t.ToString().Contains("EmbeddedResource", StringComparison.Ordinal)))
        {
            items["EmbeddedResource"] = EmbeddedResources(project, properties);
        }

        Run(project, properties, items);
        var remaining = items["Analyzer"].Select(i => i.FullPath).ToHashSet(StringComparer.Ordinal);
        return [.. seeds.Select(s => s.FullPath).Where(path => !remaining.Contains(path)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Runs a project's compiler targets, in order, over some items: their property groups, item groups and the tasks that
    /// write files (see <see cref="CompileFiles"/>).
    /// </summary>
    private static TargetRun Run(ProjectInfo project, IReadOnlyDictionary<string, string> properties, Dictionary<string, List<Item>> items)
    {
        var run = new TargetRun(properties);
        foreach (var target in project.CompilerTargets)
        {
            if (Condition(target.Attribute("Condition")?.Value, run.Properties, null, project) == false)
            {
                continue;
            }

            foreach (var element in target.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "ItemGroup":
                        if (Condition(element.Attribute("Condition")?.Value, run.Properties, null, project) != false)
                        {
                            foreach (var child in element.Elements())
                            {
                                Execute(child, items, run, project);
                            }
                        }

                        break;
                    case "PropertyGroup":
                        SetProperties(element, run, project);
                        break;
                    default:
                        if (Condition(element.Attribute("Condition")?.Value, run.Properties, null, project) != false)
                        {
                            RunTask(element, run, project);
                        }

                        break;
                }
            }
        }

        return run;
    }

    /// <summary>What running a project's targets has done so far: the properties they set and the files they wrote.</summary>
    private sealed class TargetRun(IReadOnlyDictionary<string, string> properties)
    {
        public Dictionary<string, string> Properties { get; } = new(properties, StringComparer.OrdinalIgnoreCase);

        /// <summary>The items the targets added, of every type.</summary>
        public List<Item> Added { get; } = [];

        /// <summary>The text of each file the targets wrote, by full path.</summary>
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// A property group of a target: each property whose condition holds gets its value, with property functions such as
    /// <c>$([System.IO.File]::ReadAllText(...))</c> evaluated. A value or condition that cannot be evaluated here, such
    /// as one with items, leaves the property as it was.
    /// </summary>
    private static void SetProperties(XElement group, TargetRun run, ProjectInfo project)
    {
        if (Condition(group.Attribute("Condition")?.Value, run.Properties, null, project) != true)
        {
            return;
        }

        foreach (var property in group.Elements())
        {
            if (Condition(property.Attribute("Condition")?.Value, run.Properties, null, project) == true
                && ExpandValue(property.Value.Trim(), run, project) is { } value)
            {
                run.Properties[property.Name.LocalName] = value;
            }
        }
    }

    /// <summary>
    /// The tasks that write a file the compiler may get, carried out in memory: <c>Copy</c>, <c>WriteLinesToFile</c>, and
    /// Microsoft.Testing.Platform's <c>TestingPlatformSelfRegisteredExtensions</c>. Other tasks are left out.
    /// </summary>
    private static void RunTask(XElement task, TargetRun run, ProjectInfo project)
    {
        string? Parameter(string name) => task.Attribute(name)?.Value is { } text ? ExpandValue(text, run, project) : null;
        switch (task.Name.LocalName)
        {
            case "Copy":
                var sources = Split(Parameter("SourceFiles"));
                var destinationFiles = Split(Parameter("DestinationFiles"));
                var folder = Parameter("DestinationFolder");
                for (var i = 0; i < sources.Length; i++)
                {
                    var destination = destinationFiles.Length == sources.Length
                        ? destinationFiles[i]
                        : folder is { Length: > 0 } ? Path.Combine(folder.Replace('\\', Path.DirectorySeparatorChar), Path.GetFileName(sources[i].Replace('\\', Path.DirectorySeparatorChar))) : null;
                    if (destination is not null && FullPath(project, sources[i]) is { } source && FullPath(project, destination) is { } target
                        && (run.Files.TryGetValue(source, out var text) || TryRead(source, out text)))
                    {
                        run.Files[target] = text;
                    }
                }

                break;
            case "WriteLinesToFile" when task.Attribute("Lines")?.Value is { } lines && !lines.Contains("@(", StringComparison.Ordinal):
                // Each item of Lines is a line. A property is one item even with semicolons in it when a property
                // function made its value, as IFluentInterface's file is read with File.ReadAllText: MSBuild escapes
                // what property functions return. So Lines is split where its own text has a semicolon.
                if (Parameter("File") is { Length: > 0 } file && FullPath(project, file) is { } path)
                {
                    var written = new StringBuilder();
                    foreach (var part in SplitOutside(lines))
                    {
                        if (ExpandValue(part, run, project) is not { } line)
                        {
                            return;
                        }

                        // MSBuild hands a task its parameters unescaped: FastEndpoints.Swagger writes its module
                        // initializer from a property whose semicolons are %3b, which reached the compiler as written
                        // (14 errors in CleanArchitecture's web project).
                        line = Unescape(line);
                        if (line.Length > 0)
                        {
                            written.Append(line).Append('\n');
                        }
                    }

                    var overwrite = string.Equals(Parameter("Overwrite")?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
                    run.Files[path] = (overwrite ? string.Empty : run.Files.GetValueOrDefault(path) ?? string.Empty) + written.ToString();
                }

                break;
            case "TestingPlatformSelfRegisteredExtensions":
                if (Parameter("SelfRegisteredExtensionsSourcePath") is { Length: > 0 } extensions && FullPath(project, extensions) is { } extensionsPath
                    && string.Equals(Parameter("Language")?.Trim(), "C#", StringComparison.OrdinalIgnoreCase))
                {
                    run.Files[extensionsPath] = SelfRegisteredExtensionsSource(Parameter("RootNamespace"));
                }

                break;
        }
    }

    /// <summary>
    /// The <c>SelfRegisteredExtensions</c> class Microsoft.Testing.Platform's MSBuild task generates for a test project, in
    /// its root namespace, made safe as the task makes it: xUnit.net v3 and MSTest projects get it, and code may use its
    /// namespace (<c>using UnitTests.Parallelizable;</c> in Terminal.Gui's tests). The extensions it registers are those of
    /// <c>TestingPlatformBuilderHook</c> items, which matter only to Microsoft.Testing.Platform's own runner; TabStudio's
    /// test runner does not use it, so the method registers none.
    /// </summary>
    private static string SelfRegisteredExtensionsSource(string? rootNamespace)
    {
        var name = SafeNamespace(rootNamespace ?? string.Empty);
        var type = """
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                internal static class SelfRegisteredExtensions
                {
                    public static void AddSelfRegisteredExtensions(this global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder, string[] args)
                    {
                    }
                }
            """;
        return name.Length == 0
            ? "// <auto-generated/>\n" + type.Replace("\n    ", "\n", StringComparison.Ordinal).TrimStart() + "\n"
            : $"// <auto-generated/>\nnamespace {name}\n{{\n{type}\n}}\n";
    }

    /// <summary>
    /// A namespace made of valid identifiers, as the .NET templates' safe namespace form (and the testing platform's task)
    /// makes it: an invalid character becomes <c>_</c>, and a part that cannot start with its first character gets one in front.
    /// </summary>
    private static string SafeNamespace(string value)
    {
        var text = new StringBuilder();
        foreach (var c in value.Trim())
        {
            var first = text.Length == 0 || text[^1] == '.';
            if (c == '.' && !first)
            {
                text.Append(c);
            }
            else if (first && !IsIdentifierStart(c) && IsIdentifierPart(c))
            {
                text.Append('_').Append(c);
            }
            else if (first ? IsIdentifierStart(c) : IsIdentifierPart(c))
            {
                text.Append(c);
            }
            else
            {
                text.Append('_');
            }
        }

        return text.ToString().TrimEnd('.');
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>A value with the properties the targets set so far; <see langword="null"/> when it uses items or metadata, or cannot be evaluated.</summary>
    private static string? ExpandValue(string text, TargetRun run, ProjectInfo project) =>
        text.Contains("@(", StringComparison.Ordinal) || text.Contains("%(", StringComparison.Ordinal)
            ? null
            : MsBuildExpressions.Expand(text, run.Properties, null, project.DirectoryPath);

    /// <summary>A path relative to the project's folder, or full, as a full path; <see langword="null"/> when it is not one.</summary>
    private static string? FullPath(ProjectInfo project, string path)
    {
        var normalized = path.Trim().Replace('\\', Path.DirectorySeparatorChar);
        if (normalized.Length == 0 || normalized.Contains("$(", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized : Path.Combine(project.DirectoryPath, normalized));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool TryRead(string path, out string text)
    {
        try
        {
            if (File.Exists(path))
            {
                text = File.ReadAllText(path);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable file is not copied.
        }

        text = string.Empty;
        return false;
    }

    /// <summary>
    /// A value as a task gets it: MSBuild's escapes of its own special characters (<c>%3B</c> for <c>;</c>, <c>%24</c> for
    /// <c>$</c>, <c>%40</c>, <c>%25</c>, <c>%27</c>, <c>%3F</c>, <c>%2A</c>) turned back into the characters. Other
    /// <c>%</c> sequences are left as they are, as text a property function read from a file may have them.
    /// </summary>
    private static string Unescape(string text) =>
        text.Contains('%', StringComparison.Ordinal) ? MsBuildEscape().Replace(text, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString()) : text;

    [GeneratedRegex("%(3[Bb]|24|40|25|27|3[Ff]|2[Aa])", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MsBuildEscape();

    /// <summary>The parts of a text between its semicolons, outside <c>$( … )</c>.</summary>
    private static List<string> SplitOutside(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth = Math.Max(0, depth - 1);
                    break;
                case ';' when depth == 0:
                    parts.Add(text[start..i]);
                    start = i + 1;
                    break;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>One item: its file, its metadata, and the item types it was created as, the last one its own.</summary>
    private sealed class Item(string type, string fullPath, Dictionary<string, string> metadata, IEnumerable<string> types, bool isFile = true)
    {
        public string Type { get; } = type;

        /// <summary>Whether the item is a file, rather than a name such as a ThisAssembly constant's.</summary>
        public bool IsFile { get; } = isFile;

        /// <summary>
        /// For an item a transform made (<c>@(ProjectProperty -&gt; 'Project.%(Identity)')</c>), the item it was made from,
        /// whose metadata the element's own metadata and condition read, as MSBuild batches over it.
        /// </summary>
        public Item? Origin { get; init; }

        public string FullPath { get; } = fullPath;

        public Dictionary<string, string> Metadata { get; } = metadata;

        public List<string> Types { get; } = [.. types.Append(type).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void Execute(XElement element, Dictionary<string, List<Item>> items, TargetRun run, ProjectInfo project)
    {
        var properties = run.Properties;
        var type = element.Name.LocalName;
        var include = element.Attribute("Include")?.Value;
        var update = element.Attribute("Update")?.Value;
        var remove = element.Attribute("Remove")?.Value;
        var condition = element.Attribute("Condition")?.Value;
        if (!items.TryGetValue(type, out var own))
        {
            own = [];
            items[type] = own;
        }

        if (remove is not null)
        {
            // A condition with metadata is checked for each item removed.
            var removed = Sources(remove, items, project, run).Where(r => Condition(condition, properties, r, project) != false).ToList();
            own.RemoveAll(i => removed.Any(r => string.Equals(r.FullPath, i.FullPath, StringComparison.Ordinal)));
            return;
        }

        if (include is null)
        {
            // <EmbeddedResource><FileExtension Condition="...">%(Extension)</FileExtension></EmbeddedResource> sets
            // metadata on every item of its type, one item at a time.
            // With Update, only the items it names, as ThisAssembly.Constants' <Constant Update="@(Constant)">.
            var updated = update is null || ItemReference().Match(update.Trim()) is { Success: true } self && string.Equals(self.Groups["type"].Value, type, StringComparison.OrdinalIgnoreCase)
                ? null
                : Sources(update, items, project, run).Select(i => i.FullPath).ToHashSet(StringComparer.Ordinal);
            foreach (var item in own.Where(i => (updated is null || updated.Contains(i.FullPath)) && Condition(condition, properties, i, project) != false))
            {
                SetMetadata(element, item, item.Metadata, properties, project);
            }

            return;
        }

        foreach (var source in Sources(include, items, project, run))
        {
            var batch = source.Origin ?? source;
            if (Condition(condition, properties, batch, project) == false)
            {
                continue;
            }

            var metadata = new Dictionary<string, string>(source.Metadata, StringComparer.OrdinalIgnoreCase);
            SetMetadata(element, batch, metadata, properties, project);
            var item = new Item(type, source.FullPath, metadata, source.Types, source.IsFile);
            own.Add(item);
            run.Added.Add(item);
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

    /// <summary>
    /// The items an <c>Include</c> or <c>Remove</c> names: those of each <c>@(Type)</c>, and files by path, with the files
    /// the targets wrote so far.
    /// </summary>
    private static List<Item> Sources(string text, Dictionary<string, List<Item>> items, ProjectInfo project, TargetRun run)
    {
        var properties = run.Properties;
        var sources = new List<Item>();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ItemReference().Match(part) is { Success: true } reference)
            {
                sources.AddRange(items.GetValueOrDefault(reference.Groups["type"].Value) ?? []);
                continue;
            }

            if (ItemTransform().Match(part) is { Success: true } transform)
            {
                sources.AddRange(Transform(items.GetValueOrDefault(transform.Groups["type"].Value) ?? [], transform.Groups["body"].Value.Trim(), project, properties));
                continue;
            }

            if (part.Contains("@(", StringComparison.Ordinal) || part.Contains("%(", StringComparison.Ordinal)
                || MsBuildExpressions.Expand(part, properties, null, project.DirectoryPath, unknownProperties: false) is not { } path)
            {
                continue;
            }

            if (!new GlobPattern(project.DirectoryPath, path).HasWildcards && FullPath(project, path) is { } written && run.Files.ContainsKey(written))
            {
                sources.Add(new Item("File", written, WellKnownMetadata(project, written), []));
                continue;
            }

            if (!new GlobPattern(project.DirectoryPath, path).HasWildcards && ProjectSourceFiles.EnumerateItems(project, [path], []).Count == 0)
            {
                // A name is an item whether or not a file has it, as MSBuild includes it.
                if (NamedItem(project, path) is { } named)
                {
                    sources.Add(named);
                }

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
    /// The items of a transform, <c>@(Type -&gt; 'expression')</c>, each made from one item, with its metadata and its
    /// well-known metadata of the new name; or of <c>@(Type -&gt; Distinct())</c>, the first item of each name.
    /// Another item function gives none.
    /// </summary>
    private static List<Item> Transform(List<Item> inputs, string body, ProjectInfo project, Dictionary<string, string> properties)
    {
        if (string.Equals(body, "Distinct()", StringComparison.OrdinalIgnoreCase))
        {
            return [.. inputs.DistinctBy(i => i.Metadata.GetValueOrDefault("Identity") ?? i.FullPath, StringComparer.OrdinalIgnoreCase)];
        }

        if (body.Length < 2 || body[0] != '\'' || body[^1] != '\'')
        {
            return [];
        }

        var result = new List<Item>();
        foreach (var item in inputs)
        {
            if (Expand(body[1..^1], properties, item, project) is not { Length: > 0 } name
                || NamedItem(project, name) is not { } named)
            {
                continue;
            }

            var metadata = new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in named.Metadata)
            {
                metadata[key] = value;
            }

            result.Add(new Item("File", named.FullPath, metadata, [], named.IsFile) { Origin = item });
        }

        return result;
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

    [GeneratedRegex(@"^@\(\s*(?<type>[A-Za-z_][A-Za-z0-9_.\-]*)\s*->\s*(?<body>.*)\)$", RegexOptions.CultureInvariant | RegexOptions.Singleline, 100)]
    private static partial Regex ItemTransform();

    [GeneratedRegex(@"@\(\s*Compile\s*[)\-]", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CompileReference();

    [GeneratedRegex(@"%\(\s*(?:[A-Za-z_][A-Za-z0-9_\-]*\s*\.\s*)?(?<name>[A-Za-z_][A-Za-z0-9_\-]*)\s*\)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MetadataReference();

    [GeneratedRegex("[^A-Za-z0-9_]", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Identifier();

    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,2}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CultureName();
}

/// <summary>An analyzer assembly a project gets, as an <c>Analyzer</c> item that packages' targets can remove.</summary>
/// <param name="FullPath">The assembly's full path.</param>
/// <param name="PackageId">The package it comes from (its <c>NuGetPackageId</c> metadata), or <see langword="null"/>.</param>
public sealed record AnalyzerItem(string FullPath, string? PackageId);
