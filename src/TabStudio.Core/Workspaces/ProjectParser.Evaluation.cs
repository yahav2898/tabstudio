using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// MSBuild's evaluation of a project, as the .NET SDK and Visual Studio do it, without running targets.
/// </summary>
public static partial class ProjectParser
{
    /// <summary>The MSBuild version Visual Studio 2026 and the .NET 10 SDK report, which some files compare against.</summary>
    private const string MsBuildVersion = "18.0.0";

    /// <summary>The .NET SDK version conditions see (<c>NETCoreSdkVersion</c>): TabStudio compiles as the .NET 10 SDK does.</summary>
    private const string NetCoreSdkVersion = "10.0.100";

    /// <summary>
    /// The result of evaluating a project: its properties after every file, the items that apply, and the targets.
    /// </summary>
    /// <param name="Properties">Every property at the end of the evaluation, including the SDK's and the global ones.</param>
    /// <param name="Defined">The properties a file (the project or one it imports) set, rather than only the SDK or a global property.</param>
    /// <param name="Items">
    /// A <c>&lt;Project&gt;</c> with one <c>&lt;ItemGroup&gt;</c> of every item outside targets whose conditions hold, from
    /// every file in evaluation order, with properties and <c>$(MSBuildThisFile…)</c> expanded as MSBuild expands them.
    /// </param>
    /// <param name="Targets">A <c>&lt;Project&gt;</c> with the <c>&lt;Target&gt;</c> elements of every file, with <c>$(MSBuildThisFile…)</c> expanded.</param>
    /// <param name="ImportedFiles">The files imported, in evaluation order, without the project itself.</param>
    private sealed record Evaluation(
        Dictionary<string, string> Properties,
        HashSet<string> Defined,
        XElement Items,
        XElement Targets,
        List<string> ImportedFiles);

    /// <summary>How a project is evaluated.</summary>
    /// <param name="FromDisk">
    /// Whether the files MSBuild finds by itself are imported: <c>Directory.Build.props</c>, <c>Directory.Packages.props</c>
    /// and <c>Directory.Build.targets</c>. <see cref="Parse"/> reads a project's own text without them.
    /// </param>
    /// <param name="PackageProps">The packages' props files, which the SDK imports after <c>Directory.Build.props</c>.</param>
    /// <param name="PackageTargets">The packages' targets files, which the SDK imports before <c>Directory.Build.targets</c>.</param>
    /// <param name="Inherited">Properties to start from, as if a file before the project set them.</param>
    /// <param name="Overriding">Properties to set after the project, as if a file after it set them.</param>
    /// <param name="ExtraImports">Files whose items the project gets after its own, as if it imported them at its end.</param>
    private sealed record EvaluationOptions(
        bool FromDisk,
        IReadOnlyList<string> PackageProps,
        IReadOnlyList<string> PackageTargets,
        IReadOnlyDictionary<string, string>? Inherited = null,
        IReadOnlyDictionary<string, string>? Overriding = null,
        IReadOnlyList<string>? ExtraImports = null);

    /// <summary>
    /// Evaluates a project as Visual Studio does for its Debug configuration on any CPU: the outer evaluation reads its
    /// target frameworks, and a project with several is evaluated again as the inner build for the one TabStudio compiles
    /// (<see cref="EvaluationTargetFramework"/>), with <c>TargetFramework</c> as a global property, so that every
    /// condition on the framework, in the project and in the files it imports, sees it as the SDK's build does.
    /// </summary>
    private static Evaluation Evaluate(string projectPath, XElement root, EvaluationOptions options)
    {
        var globals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = "Debug",
            ["Platform"] = "AnyCPU",
        };
        var outer = new Evaluator(projectPath, root, globals, options).Run();
        if (outer.Properties.GetValueOrDefault("TargetFramework") is { Length: > 0 }
            || EvaluationTargetFramework(ReadTargetFrameworks(outer.Properties)) is not { } framework)
        {
            return outer;
        }

        globals["TargetFramework"] = framework;
        var inner = new Evaluator(projectPath, root, globals, options).Run();

        // The inner build's TargetFramework is global: unless the project set it, it is not one of the project's own.
        if (!outer.Defined.Contains("TargetFramework"))
        {
            inner.Defined.Remove("TargetFramework");
        }

        return inner;
    }

    /// <summary>One pass of MSBuild's evaluation over a project and the files it imports.</summary>
    private sealed class Evaluator
    {
        private const int MaxImportDepth = 32;

        /// <summary>The common targets a project made before the SDK imports by their place in Visual Studio.</summary>
        private static readonly HashSet<string> CommonTargetsFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.CSharp.targets", "Microsoft.VisualBasic.targets", "Microsoft.FSharp.targets", "Microsoft.Common.targets",
        };

        private readonly string _projectPath;
        private readonly string _projectDirectory;
        private readonly XElement _root;
        private readonly Dictionary<string, string> _globals;
        private readonly EvaluationOptions _options;
        private readonly Dictionary<string, string> _properties = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _defined = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(XElement Group, string? File)> _itemGroups = [];
        private readonly List<(XElement Target, string? File)> _targets = [];
        private readonly HashSet<string> _imported = new(StringComparer.Ordinal);
        private readonly List<string> _importedFiles = [];
        private bool _commonPropsDone;
        private bool _commonTargetsDone;

        public Evaluator(string projectPath, XElement root, Dictionary<string, string> globals, EvaluationOptions options)
        {
            _projectPath = projectPath;
            _projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
            _root = root;
            _globals = globals;
            _options = options;
        }

        public Evaluation Run()
        {
            // MSBuild's reserved properties and the environment Visual Studio evaluates in.
            Reserved("MSBuildProjectDirectory", _projectDirectory);
            Reserved("MSBuildProjectDirectoryNoRoot", _projectDirectory[(Path.GetPathRoot(_projectDirectory)?.Length ?? 0)..]);
            Reserved("MSBuildProjectName", Path.GetFileNameWithoutExtension(_projectPath));
            Reserved("MSBuildProjectFile", Path.GetFileName(_projectPath));
            Reserved("MSBuildProjectFullPath", _projectPath);
            Reserved("MSBuildProjectExtension", Path.GetExtension(_projectPath));
            Reserved("MSBuildToolsVersion", "Current");
            Reserved("MSBuildVersion", MsBuildVersion);
            Reserved("MSBuildRuntimeType", "Core");
            Reserved("VisualStudioVersion", "18.0");
            Reserved("OS", "Windows_NT");
            Reserved("NETCoreSdkVersion", NetCoreSdkVersion);
            Reserved("BundledNETCoreAppTargetFrameworkVersion", "10.0");
            Reserved("BundledNETCoreAppPackageVersion", "10.0.0");
            Reserved("MSBuildProjectExtensionsPath", Path.Combine(_projectDirectory, "obj") + Path.DirectorySeparatorChar);
            foreach (var (name, value) in _globals)
            {
                _properties[name] = value;
            }

            foreach (var (name, value) in _options.Inherited ?? new Dictionary<string, string>())
            {
                Set(name, value, defined: true);
            }

            var sdks = ReadSdks(_root);
            foreach (var sdk in sdks)
            {
                SdkProps(sdk);
            }

            Walk(_root, _projectPath, depth: 0);
            if (sdks.Count > 0)
            {
                SdkTargets();
            }

            foreach (var (name, value) in _options.Overriding ?? new Dictionary<string, string>())
            {
                Set(name, value, defined: true);
            }

            foreach (var file in _options.ExtraImports ?? [])
            {
                Import(file, depth: 0);
            }

            return new Evaluation(_properties, _defined, EvaluateItems(), CollectTargets(), _importedFiles);
        }

        private void Reserved(string name, string value) => _properties[name] = value;

        /// <summary>Sets a property, unless a global property of that name holds, as in MSBuild.</summary>
        private void Set(string name, string value, bool defined)
        {
            if (_globals.ContainsKey(name))
            {
                return;
            }

            _properties[name] = value;
            if (defined)
            {
                _defined.Add(name);
            }
        }

        /// <summary>Sets a property the SDK gives a default to, when nothing has set it yet.</summary>
        private void Default(string name, string value)
        {
            if (!_properties.TryGetValue(name, out var current) || current.Length == 0)
            {
                Set(name, value, defined: false);
            }
        }

        private string DirectoryOf(string? file) => file is null ? _projectDirectory : Path.GetDirectoryName(file) ?? _projectDirectory;

        private bool? Condition(XElement element, string? file, string directory) =>
            element.Attribute("Condition")?.Value is { } condition
                ? MsBuildExpressions.Condition(condition, _properties, file, directory)
                : true;

        /// <summary>The first pass over one file: properties, imports and <c>&lt;Choose&gt;</c>, in order; item groups are kept for later.</summary>
        private void Walk(XElement container, string? file, int depth)
        {
            foreach (var element in container.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "PropertyGroup":
                        // Groups, imports and import groups are evaluated in their own file's folder; the rest in the project's.
                        if (Condition(element, file, DirectoryOf(file)) != false)
                        {
                            foreach (var property in element.Elements())
                            {
                                SetFromFile(property, file);
                            }
                        }

                        break;
                    case "ItemGroup":
                        _itemGroups.Add((element, file));
                        break;
                    case "Import":
                        ImportElement(element, file, depth);
                        break;
                    case "ImportGroup":
                        if (Condition(element, file, DirectoryOf(file)) != false)
                        {
                            foreach (var import in element.Elements().Where(e => e.Name.LocalName == "Import"))
                            {
                                ImportElement(import, file, depth);
                            }
                        }

                        break;
                    case "Choose":
                        Choose(element, file, depth);
                        break;
                    case "Target":
                        _targets.Add((element, file));
                        break;
                }
            }
        }

        /// <summary>
        /// A property element: its condition is evaluated in the project's folder, and its value is expanded with the
        /// properties set so far, itself included, as in <c>&lt;DefineConstants&gt;$(DefineConstants);FEATURE&lt;/DefineConstants&gt;</c>.
        /// A condition that cannot be evaluated here sets the property only when nothing has.
        /// </summary>
        private void SetFromFile(XElement property, string? file)
        {
            var name = property.Name.LocalName;
            var holds = Condition(property, file, _projectDirectory);
            if (holds == false || (holds is null && _properties.TryGetValue(name, out var existing) && existing.Length > 0))
            {
                return;
            }

            var raw = property.Value.Trim();
            var value = MsBuildExpressions.Expand(raw, _properties, file, _projectDirectory) ?? raw;
            Set(name, value, defined: true);
        }

        /// <summary>
        /// A <c>&lt;Choose&gt;</c>: the first <c>&lt;When&gt;</c> whose condition holds, otherwise <c>&lt;Otherwise&gt;</c>. A
        /// condition that cannot be evaluated counts as holding, as conditions did before TabStudio evaluated them.
        /// </summary>
        private void Choose(XElement choose, string? file, int depth)
        {
            foreach (var branch in choose.Elements())
            {
                if (branch.Name.LocalName == "When")
                {
                    if (Condition(branch, file, _projectDirectory) != false)
                    {
                        Walk(branch, file, depth);
                        return;
                    }
                }
                else if (branch.Name.LocalName == "Otherwise")
                {
                    Walk(branch, file, depth);
                    return;
                }
            }
        }

        private void ImportElement(XElement import, string? file, int depth)
        {
            if (Condition(import, file, DirectoryOf(file)) == false)
            {
                return;
            }

            var project = import.Attribute("Project")?.Value.Trim() ?? string.Empty;
            if (import.Attribute("Sdk")?.Value is { Length: > 0 } sdk)
            {
                // <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" /> imports the SDK's props or targets here.
                var name = NormalizeSdkName(sdk);
                if (project.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
                {
                    SdkTargets();
                }
                else
                {
                    SdkProps(name);
                }

                return;
            }

            if (project.Length == 0 || MsBuildExpressions.Expand(project, _properties, file, DirectoryOf(file)) is not { } expanded)
            {
                return;
            }

            foreach (var path in expanded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                foreach (var resolved in ResolveImport(path, DirectoryOf(file)))
                {
                    Import(resolved, depth + 1);
                }
            }
        }

        /// <summary>
        /// The files an import names: the path relative to the importing file's folder, or every file a wildcard in its
        /// name matches, in order. The common props and targets a project made before the SDK imports from
        /// <c>$(MSBuildExtensionsPath)</c> are the SDK's own, which <see cref="CommonProps"/> and
        /// <see cref="CommonTargets"/> stand for.
        /// </summary>
        private IEnumerable<string> ResolveImport(string path, string directory)
        {
            var native = path.Replace('\\', Path.DirectorySeparatorChar);
            var name = Path.GetFileName(native);
            string full;
            try
            {
                full = Path.GetFullPath(Path.IsPathRooted(native) ? native : Path.Combine(directory, native));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                yield break;
            }

            if (name.Contains('*', StringComparison.Ordinal) || name.Contains('?', StringComparison.Ordinal))
            {
                var folder = Path.GetDirectoryName(full);
                if (folder is null || folder.Contains('*', StringComparison.Ordinal) || !Directory.Exists(folder))
                {
                    yield break;
                }

                foreach (var match in Directory.EnumerateFiles(folder, name).Order(StringComparer.OrdinalIgnoreCase))
                {
                    yield return match;
                }

                yield break;
            }

            if (File.Exists(full))
            {
                yield return full;
                yield break;
            }

            if (name.Equals("Microsoft.Common.props", StringComparison.OrdinalIgnoreCase))
            {
                CommonProps();
            }
            else if (CommonTargetsFiles.Contains(name))
            {
                FrameworkProperties();
                CommonTargets();
            }
        }

        private void Import(string path, int depth)
        {
            // MSBuild imports a file once per evaluation, and skips a project importing itself.
            if (depth > MaxImportDepth || string.Equals(path, _projectPath, StringComparison.Ordinal) || !_imported.Add(path) || !File.Exists(path))
            {
                return;
            }

            XElement root;
            try
            {
                root = LoadXml(path, File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is ProjectLoadException or IOException or UnauthorizedAccessException)
            {
                // A broken import does not stop the project from opening.
                return;
            }

            _importedFiles.Add(path);
            Walk(root, path, depth);
        }

        /// <summary>What an SDK's <c>Sdk.props</c> does before the project: the common props, then the SDK's defaults.</summary>
        private void SdkProps(string sdk)
        {
            CommonProps();
            Default("UsingMicrosoftNETSdk", "true");
            Default("Language", LanguageName());
            Default("DefaultLanguageSourceExtension", LanguageName() switch { "VB" => ".vb", "F#" => ".fs", _ => ".cs" });
            Default("EnableDefaultItems", "true");
            Default("BaseIntermediateOutputPath", "obj" + Path.DirectorySeparatorChar);
            Default("BaseOutputPath", "bin" + Path.DirectorySeparatorChar);
            var flag = sdk.ToUpperInvariant() switch
            {
                "MICROSOFT.NET.SDK.WEB" => "UsingMicrosoftNETSdkWeb",
                "MICROSOFT.NET.SDK.RAZOR" => "UsingMicrosoftNETSdkRazor",
                "MICROSOFT.NET.SDK.BLAZORWEBASSEMBLY" => "UsingMicrosoftNETSdkBlazorWebAssembly",
                "MICROSOFT.NET.SDK.WEBASSEMBLY" => "UsingMicrosoftNETSdkWebAssembly",
                "MICROSOFT.NET.SDK.WORKER" => "UsingMicrosoftNETSdkWorker",
                "MICROSOFT.NET.SDK.WINDOWSDESKTOP" => "UsingMicrosoftNETSdkWindowsDesktop",
                _ => null,
            };
            if (flag is not null)
            {
                Default(flag, "true");
            }

            if (flag is "UsingMicrosoftNETSdkWeb" or "UsingMicrosoftNETSdkBlazorWebAssembly")
            {
                Default("UsingMicrosoftNETSdkRazor", "true");
            }
        }

        /// <summary>What an SDK's <c>Sdk.targets</c> does after the project: framework inference, then the common targets.</summary>
        private void SdkTargets()
        {
            FrameworkProperties();
            OutputPaths();
            CommonTargets();
        }

        /// <summary>The names and folders the SDK's targets give a project that sets none, which paths in items use.</summary>
        private void OutputPaths()
        {
            var separator = Path.DirectorySeparatorChar.ToString();
            var framework = Property("TargetFramework") is { Length: > 0 } tf ? tf + separator : string.Empty;
            var configuration = (Property("Configuration") ?? "Debug") + separator;
            Default("AssemblyName", Path.GetFileNameWithoutExtension(_projectPath));
            Default("TargetName", Property("AssemblyName") ?? Path.GetFileNameWithoutExtension(_projectPath));
            Default("RootNamespace", (Property("AssemblyName") ?? Path.GetFileNameWithoutExtension(_projectPath)).Replace(' ', '_'));
            Default("ProjectDir", _projectDirectory.TrimEnd(Path.DirectorySeparatorChar) + separator);
            Default("ProjectName", Path.GetFileNameWithoutExtension(_projectPath));
            Default("IntermediateOutputPath", Property("BaseIntermediateOutputPath") + configuration + framework);
            Default("OutputPath", Property("BaseOutputPath") + configuration + framework);
        }

        private string LanguageName() => Path.GetExtension(_projectPath).ToUpperInvariant() switch
        {
            ".VBPROJ" => "VB",
            ".FSPROJ" => "F#",
            ".VCXPROJ" => "C++",
            _ => "C#",
        };

        /// <summary>
        /// <c>Microsoft.Common.props</c>: <c>Directory.Build.props</c>, the packages' props (the restore's
        /// <c>nuget.g.props</c>), then NuGet's <c>Directory.Packages.props</c>, each unless the project turned it off.
        /// </summary>
        private void CommonProps()
        {
            if (_commonPropsDone)
            {
                return;
            }

            _commonPropsDone = true;
            if (_options.FromDisk)
            {
                Default("ImportDirectoryBuildProps", "true");
                if (IsTrue("ImportDirectoryBuildProps"))
                {
                    var path = Property("DirectoryBuildPropsPath") is { Length: > 0 } custom
                        ? custom
                        : MsBuildImports.GetPathOfFileAbove("Directory.Build.props", _projectDirectory);
                    if (path.Length > 0)
                    {
                        Default("DirectoryBuildPropsPath", path);
                        Import(Path.GetFullPath(path, _projectDirectory), 1);
                    }
                }
            }

            if (!IsFalse("ImportProjectExtensionProps"))
            {
                foreach (var file in _options.PackageProps)
                {
                    Import(file, 1);
                }
            }

            if (_options.FromDisk)
            {
                Default("ImportDirectoryPackagesProps", "true");
                if (IsTrue("ImportDirectoryPackagesProps"))
                {
                    var path = Property("DirectoryPackagesPropsPath") is { Length: > 0 } custom
                        ? custom
                        : MsBuildImports.GetPathOfFileAbove("Directory.Packages.props", _projectDirectory);
                    if (path.Length > 0)
                    {
                        Default("DirectoryPackagesPropsPath", path);
                        Import(Path.GetFullPath(path, _projectDirectory), 1);
                    }
                }
            }
        }

        /// <summary>
        /// The properties the SDK derives from <c>TargetFramework</c> after the project
        /// (<c>Microsoft.NET.TargetFrameworkInference.targets</c>), which <c>Directory.Build.targets</c> and the items see.
        /// </summary>
        private void FrameworkProperties()
        {
            if (Property("TargetFramework") is not { Length: > 0 } framework || MsBuildExpressions.TargetFrameworks.Parse(framework) is not { } parsed)
            {
                return;
            }

            Default("TargetFrameworkIdentifier", parsed.Identifier);
            Default("TargetFrameworkVersion", "v" + parsed.Version.ToString(2));
            if (parsed.Platform.Length > 0)
            {
                Default("TargetPlatformIdentifier", parsed.Platform);
                if (parsed.PlatformVersion is { } platformVersion)
                {
                    Default("TargetPlatformVersion", platformVersion.ToString());
                }
            }
        }

        /// <summary>
        /// <c>Microsoft.Common.targets</c>: the packages' targets (the restore's <c>nuget.g.targets</c>), then
        /// <c>Directory.Build.targets</c>, each unless the project turned it off.
        /// </summary>
        private void CommonTargets()
        {
            if (_commonTargetsDone)
            {
                return;
            }

            _commonTargetsDone = true;
            if (!IsFalse("ImportProjectExtensionTargets"))
            {
                foreach (var file in _options.PackageTargets)
                {
                    Import(file, 1);
                }
            }

            if (_options.FromDisk)
            {
                Default("ImportDirectoryBuildTargets", "true");
                if (IsTrue("ImportDirectoryBuildTargets"))
                {
                    var path = Property("DirectoryBuildTargetsPath") is { Length: > 0 } custom
                        ? custom
                        : MsBuildImports.GetPathOfFileAbove("Directory.Build.targets", _projectDirectory);
                    if (path.Length > 0)
                    {
                        Default("DirectoryBuildTargetsPath", path);
                        Import(Path.GetFullPath(path, _projectDirectory), 1);
                    }
                }
            }
        }

        private string? Property(string name) => _properties.TryGetValue(name, out var value) ? value.Trim() : null;

        private bool IsFalse(string name) => string.Equals(Property(name), "false", StringComparison.OrdinalIgnoreCase);

        private bool IsTrue(string name) => string.Equals(Property(name), "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// MSBuild's item pass: every item group outside targets, in order, whose condition (and whose
        /// <c>&lt;When&gt;</c>'s) holds with the final properties, keeping items whose own conditions hold; a condition that
        /// cannot be evaluated keeps its group or item, as TabStudio did before evaluating them.
        /// </summary>
        private XElement EvaluateItems()
        {
            var group = new XElement("ItemGroup");
            foreach (var (itemGroup, file) in _itemGroups)
            {
                if (Condition(itemGroup, file, _projectDirectory) == false)
                {
                    continue;
                }

                foreach (var item in itemGroup.Elements())
                {
                    if (Condition(item, file, _projectDirectory) == false)
                    {
                        continue;
                    }

                    group.Add(Expanded(item, file));
                }
            }

            return new XElement("Project", group);
        }

        /// <summary>
        /// A copy of an item with its attributes and metadata expanded with the final properties, as MSBuild expands
        /// them: <c>$(MSBuildThisFileDirectory)</c> is the folder of the file the item is in. A value with a function
        /// that cannot be evaluated here keeps its text. Its condition, already evaluated, is dropped.
        /// </summary>
        private XElement Expanded(XElement item, string? file)
        {
            var copy = new XElement(item.Name.LocalName);
            foreach (var attribute in item.Attributes())
            {
                if (attribute.IsNamespaceDeclaration || attribute.Name.LocalName == "Condition")
                {
                    continue;
                }

                copy.SetAttributeValue(attribute.Name.LocalName, ExpandText(attribute.Value, file));
            }

            foreach (var metadata in item.Elements())
            {
                if (Condition(metadata, file, _projectDirectory) == false)
                {
                    continue;
                }

                copy.Add(new XElement(metadata.Name.LocalName, ExpandText(metadata.Value, file)));
            }

            return copy;
        }

        /// <summary>
        /// An item's text with properties expanded; a property nothing sets keeps the text as it is, so that a path such
        /// as <c>$(SomeSdkPath)/**/*.cs</c> is left out rather than read as every file from the root.
        /// </summary>
        private string ExpandText(string text, string? file) =>
            text.Contains("$(", StringComparison.Ordinal)
                ? MsBuildExpressions.Expand(text, _properties, file, _projectDirectory, unknownProperties: false)
                    ?? (file is null ? text : MsBuildImports.ExpandThisFile(text, file))
                : text;

        /// <summary>Every file's targets, with <c>$(MSBuildThisFile…)</c> expanded, for what reads items a target adds.</summary>
        private XElement CollectTargets()
        {
            var targets = new XElement("Project");
            foreach (var (target, file) in _targets)
            {
                var copy = new XElement(target);
                if (file is not null)
                {
                    foreach (var attribute in copy.DescendantsAndSelf().SelectMany(e => e.Attributes()).Where(a => a.Value.Contains("$(MSBuildThisFile", StringComparison.OrdinalIgnoreCase)))
                    {
                        attribute.Value = MsBuildImports.ExpandThisFile(attribute.Value, file);
                    }
                }

                targets.Add(copy);
            }

            return targets;
        }
    }
}
