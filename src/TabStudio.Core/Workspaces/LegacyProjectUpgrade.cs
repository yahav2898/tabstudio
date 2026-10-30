using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// What upgrading a legacy .NET Framework project to an SDK-style one changes, before it is written: the project
/// file's text before and after, the files it removes (<c>packages.config</c>), and notes for the user.
/// </summary>
/// <param name="ProjectFilePath">The project file.</param>
/// <param name="OriginalXml">Its text now.</param>
/// <param name="UpgradedXml">Its SDK-style text.</param>
/// <param name="RemovedFiles">Full paths of the files the upgrade removes, such as <c>packages.config</c>.</param>
/// <param name="Notes">What the user should know, one sentence each.</param>
public sealed record LegacyProjectUpgrade(
    string ProjectFilePath,
    string OriginalXml,
    string UpgradedXml,
    IReadOnlyList<string> RemovedFiles,
    IReadOnlyList<string> Notes)
{
    /// <summary>The new target framework, such as <c>net10.0-windows</c>.</summary>
    public required string TargetFramework { get; init; }

    /// <summary>Writes the upgraded project file and removes the files it replaces.</summary>
    public void Apply()
    {
        File.WriteAllText(ProjectFilePath, UpgradedXml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var file in RemovedFiles)
        {
            File.Delete(file);
        }
    }
}

/// <summary>
/// Converts a legacy .NET Framework project into an SDK-style .NET project, as .NET Upgrade Assistant does: a
/// <c>net10.0-windows</c> target, <c>UseWPF</c> or <c>UseWindowsForms</c> for what its references say, the packages
/// of <c>packages.config</c> as <c>PackageReference</c> items, and no items the SDK's defaults already include. The
/// result opens in TabStudio and in Visual Studio 2022 and later alike.
/// </summary>
/// <remarks>
/// <para>
/// The SDK includes every <c>.cs</c> file in the project's folder, every <c>.resx</c> and, for WPF, every
/// <c>.xaml</c> file. Items the project listed only to include them are dropped; files in the folder the project did not
/// list get a <c>Remove</c> item, so the project builds the same files as before. Item metadata that tools read, such as a
/// <c>.resx</c> file's <c>Generator</c>, stays on an <c>Update</c> item.
/// </para>
/// <para>
/// Framework references that .NET has under the target framework are dropped; a few that are NuGet packages in .NET,
/// such as <c>System.Speech</c>, become package references; references to DLLs by file stay. The build settings
/// MSBuild's legacy targets needed (<c>ProjectGuid</c>, <c>OutputPath</c>, <c>DebugType</c> and the like) and the
/// imports of those targets go.
/// </para>
/// </remarks>
public static class LegacyProjectUpgrader
{
    /// <summary>The .NET the upgraded project targets.</summary>
    public const string DotNetVersion = "net10.0";

    /// <summary>The version of the NuGet packages that replace .NET Framework assemblies, such as <c>System.Speech</c>.</summary>
    private const string WindowsCompatibilityPackageVersion = "10.0.0";

    /// <summary>Properties of the legacy format that SDK-style projects set themselves or do not need.</summary>
    private static readonly HashSet<string> DroppedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Configuration", "Platform", "ProjectGuid", "ProjectTypeGuids", "TargetFrameworkVersion", "TargetFrameworkProfile",
        "TargetFrameworkIdentifier", "FileAlignment", "AutoGenerateBindingRedirects", "Deterministic", "WarningLevel",
        "DebugType", "DebugSymbols", "Optimize", "OutputPath", "ErrorReport", "Prefer32Bit", "AppDesignerFolder",
        "ProductVersion", "SchemaVersion", "NuGetPackageImportStamp", "MinimumVisualStudioVersion", "ProjectTypeGuid",
        "IsWebBootstrapper", "PublishUrl", "Install", "InstallFrom", "UpdateEnabled", "UpdateMode", "UpdateInterval",
        "UpdateIntervalUnits", "UpdatePeriodically", "UpdateRequired", "MapFileExtensions", "ApplicationRevision",
        "ApplicationVersion", "UseApplicationTrust", "BootstrapperEnabled", "OldToolsVersion", "UpgradeBackupLocation",
        "FileUpgradeFlags", "TargetZone", "GenerateManifests", "SignManifests", "ManifestCertificateThumbprint",
        "ManifestKeyFile", "ExpressionBlendVersion", "WarningsAsErrors", "CodeAnalysisRuleSet", "RestorePackages",
        "SolutionDir", "VisualStudioVersion", "VSToolsPath", "WcfConfigValidationEnabled", "UseIISExpress",
        "OutputType", "RootNamespace", "AssemblyName", "UseWPF", "UseWindowsForms",
    };

    /// <summary>The .NET Framework assemblies that are part of .NET itself.</summary>
    private static readonly HashSet<string> DotNetAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib", "netstandard", "System", "System.Core", "System.Data", "System.Data.DataSetExtensions", "System.Xml",
        "System.Xml.Linq", "Microsoft.CSharp", "Microsoft.VisualBasic", "System.Net", "System.Net.Http", "System.Numerics",
        "System.Runtime", "System.Runtime.Serialization", "System.ComponentModel.DataAnnotations", "System.Transactions",
        "System.IO.Compression", "System.IO.Compression.FileSystem", "System.Deployment", "System.ValueTuple",
        "System.Web.ApplicationServices", "System.ComponentModel.Composition.Registration",
    };

    /// <summary>What the Windows desktop's shared framework has for WPF.</summary>
    private static readonly HashSet<string> WpfAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", "ReachFramework", "System.Printing",
        "UIAutomationClient", "UIAutomationClientSideProviders", "UIAutomationProvider", "UIAutomationTypes", "PresentationUI",
        "PresentationFramework.Aero", "PresentationFramework.Aero2", "PresentationFramework.AeroLite", "PresentationFramework.Classic",
        "PresentationFramework.Luna", "PresentationFramework.Royale", "System.Windows.Presentation", "System.Windows.Controls.Ribbon",
        "System.Configuration", "System.Drawing",
    };

    /// <summary>What the Windows desktop's shared framework has for Windows Forms.</summary>
    private static readonly HashSet<string> WindowsFormsAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Windows.Forms", "System.Drawing", "System.Design", "System.Drawing.Design", "Accessibility", "WindowsFormsIntegration",
        "System.Configuration", "Microsoft.VisualBasic.Forms",
    };

    /// <summary>.NET Framework assemblies that are NuGet packages in .NET.</summary>
    private static readonly Dictionary<string, string> PackagedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Configuration"] = "System.Configuration.ConfigurationManager",
        ["System.Drawing"] = "System.Drawing.Common",
        ["System.Speech"] = "System.Speech",
        ["System.Management"] = "System.Management",
        ["System.ServiceProcess"] = "System.ServiceProcess.ServiceController",
        ["System.Runtime.Caching"] = "System.Runtime.Caching",
        ["System.ComponentModel.Composition"] = "System.ComponentModel.Composition",
        ["System.IO.Ports"] = "System.IO.Ports",
        ["System.Data.Odbc"] = "System.Data.Odbc",
        ["System.Data.OleDb"] = "System.Data.OleDb",
        ["System.DirectoryServices"] = "System.DirectoryServices",
        ["System.Security"] = "System.Security.Cryptography.Pkcs",
    };

    /// <summary>Item types that do not exist in SDK-style projects, or that only Visual Studio's old project system read.</summary>
    private static readonly HashSet<string> DroppedItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "BootstrapperPackage", "Folder", "Service", "WCFMetadata", "CodeAnalysisDictionary",
    };

    /// <summary>Metadata Visual Studio's old project system wrote that the SDK works out itself.</summary>
    private static readonly HashSet<string> DroppedMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        "SubType",
    };

    /// <summary>Works out the upgrade of a legacy project, without writing anything.</summary>
    /// <param name="projectFilePath">The project file.</param>
    /// <exception cref="InvalidOperationException">The project is not a legacy .NET Framework project.</exception>
    /// <exception cref="ProjectLoadException">The project file cannot be read.</exception>
    public static LegacyProjectUpgrade Plan(string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);
        var path = Path.GetFullPath(projectFilePath);
        var project = ProjectParser.Load(path);
        if (!ProjectParser.IsLegacyFrameworkProject(project))
        {
            throw new InvalidOperationException($"{project.Name} is not a .NET Framework project in the legacy format.");
        }

        string xml;
        XElement root;
        try
        {
            xml = File.ReadAllText(path);
            root = XDocument.Parse(xml).Root ?? throw new ProjectLoadException(path, "the file is empty.", null);
        }
        catch (XmlException ex)
        {
            throw new ProjectLoadException(path, $"the XML is not valid ({ex.Message}).", ex);
        }

        return new Converter(project, root, xml).Convert();
    }

    /// <summary>One conversion.</summary>
    private sealed class Converter(ProjectInfo project, XElement root, string originalXml)
    {
        private readonly string _directory = project.DirectoryPath;
        private readonly List<string> _notes = [];
        private readonly List<XElement> _packages = [];
        private readonly List<XElement> _items = [];
        private readonly List<XElement> _conditionalGroups = [];
        private readonly HashSet<string> _listed = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool _usesWpf = IsTrue(project.GetProperty("UseWPF"));

        /// <summary>The project's source files: <c>.vb</c> in a <c>.vbproj</c>, <c>.cs</c> otherwise.</summary>
        private readonly string _sourceExtension = project.FilePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ? ".vb" : ".cs";
        private readonly bool _usesWindowsForms = IsTrue(project.GetProperty("UseWindowsForms"))
            || project.AssemblyReferences.Any(r => string.Equals(r.Name, "System.Windows.Forms", StringComparison.OrdinalIgnoreCase));

        private bool _customApplicationDefinition;

        private bool IsDesktop => _usesWpf || _usesWindowsForms;

        public LegacyProjectUpgrade Convert()
        {
            var removed = new List<string>();
            var packagesConfig = Path.Combine(_directory, "packages.config");
            var packagesFromConfig = ProjectParser.ReadPackagesConfig(_directory);
            if (File.Exists(packagesConfig))
            {
                removed.Add(packagesConfig);
                foreach (var package in packagesFromConfig)
                {
                    AddPackage(package.Name, package.Version, package.PrivateAssets);
                }

                if (packagesFromConfig.Count > 0)
                {
                    _notes.Add($"The {Count(packagesFromConfig.Count, "package")} of packages.config {(packagesFromConfig.Count == 1 ? "is a PackageReference" : "are PackageReference items")} now, and packages.config is removed.");
                }
            }

            foreach (var group in root.Elements().Where(e => e.Name.LocalName == "ItemGroup"))
            {
                ConvertItems(group, packagesFromConfig);
            }

            var keptProperties = new List<XElement>();
            foreach (var group in root.Elements().Where(e => e.Name.LocalName == "PropertyGroup"))
            {
                if (group.Attribute("Condition") is { } condition)
                {
                    var kept = group.Elements().Where(p => KeepsConditionalProperty(p)).Select(Clean).ToList();
                    if (kept.Count > 0)
                    {
                        _conditionalGroups.Add(new XElement("PropertyGroup", new XAttribute("Condition", condition.Value), kept));
                    }
                }
                else
                {
                    keptProperties.AddRange(group.Elements().Where(p => !DroppedProperties.Contains(p.Name.LocalName)).Select(Clean));
                }
            }

            var targetFramework = IsDesktop || project.AssemblyReferences.Any(r => PackagedAssemblies.ContainsKey(r.Name) && !DotNetAssemblies.Contains(r.Name))
                ? DotNetVersion + "-windows"
                : DotNetVersion;
            var properties = new XElement("PropertyGroup");
            if (project.OutputType is { } outputType && !string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase))
            {
                properties.Add(new XElement("OutputType", outputType));
            }

            properties.Add(new XElement("TargetFramework", targetFramework));
            if (_usesWpf)
            {
                properties.Add(new XElement("UseWPF", "true"));
            }

            if (_usesWindowsForms)
            {
                properties.Add(new XElement("UseWindowsForms", "true"));
            }

            if (project.GetProperty("RootNamespace") is { Length: > 0 } rootNamespace && rootNamespace != project.Name)
            {
                properties.Add(new XElement("RootNamespace", rootNamespace));
            }

            if (project.GetProperty("AssemblyName") is { Length: > 0 } assemblyName && assemblyName != project.Name)
            {
                properties.Add(new XElement("AssemblyName", assemblyName));
            }

            if (HasAssemblyInfo())
            {
                properties.Add(new XElement("GenerateAssemblyInfo", "false"));
                _notes.Add((_sourceExtension == ".vb" ? "My Project/AssemblyInfo.vb" : "Properties/AssemblyInfo.cs") + " stays, so the SDK does not generate the assembly attributes it already has (GenerateAssemblyInfo is false).");
            }

            if (_customApplicationDefinition)
            {
                properties.Add(new XElement("EnableDefaultApplicationDefinition", "false"));
            }

            properties.Add(keptProperties);

            var upgraded = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), properties, _conditionalGroups);
            AddRemovesForUnlistedFiles();
            if (_items.Count > 0)
            {
                upgraded.Add(new XElement("ItemGroup", _items));
            }

            if (_packages.Count > 0)
            {
                upgraded.Add(new XElement("ItemGroup", _packages));
            }

            foreach (var element in root.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Import" when KeepsImport(element):
                    case "Target" when KeepsTarget(element):
                    case "Choose":
                    case "UsingTask":
                        upgraded.Add(Clean(element));
                        break;
                }
            }

            var text = Format(upgraded);
            return new LegacyProjectUpgrade(project.FilePath, originalXml, text, removed, _notes) { TargetFramework = targetFramework };
        }

        /// <summary>Converts the items of one item group into <see cref="_items"/> and <see cref="_packages"/>.</summary>
        private void ConvertItems(XElement group, IReadOnlyList<PackageReferenceInfo> packagesFromConfig)
        {
            var condition = group.Attribute("Condition")?.Value;
            foreach (var item in group.Elements())
            {
                var type = item.Name.LocalName;
                var include = item.Attribute("Include")?.Value;
                if (DroppedItems.Contains(type))
                {
                    continue;
                }

                if (condition is not null)
                {
                    // An item for one configuration is kept as written; the SDK's defaults do not cover it.
                    _items.Add(WithCondition(Clean(item), condition));
                    continue;
                }

                switch (type)
                {
                    case "Reference" when include is not null:
                        ConvertReference(item, include, packagesFromConfig);
                        break;
                    case "ProjectReference":
                        var reference = Clean(item);
                        reference.Elements().Where(m => m.Name.LocalName is "Project" or "Name").Remove();
                        _items.Add(reference);
                        break;
                    case "Compile" or "EmbeddedResource" or "Page" or "ApplicationDefinition" or "None" or "Content" or "Resource" when include is not null:
                        ConvertFileItem(item, type, include);
                        break;
                    default:
                        _items.Add(Clean(item));
                        break;
                }
            }
        }

        private void ConvertReference(XElement item, string include, IReadOnlyList<PackageReferenceInfo> packagesFromConfig)
        {
            var name = include.Split(',')[0].Trim();
            var hintPath = Metadata(item, "HintPath");
            if (hintPath is not null)
            {
                // A package's DLL, which its PackageReference now brings.
                var folder = hintPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                var index = Array.FindIndex(folder, p => string.Equals(p, "packages", StringComparison.OrdinalIgnoreCase));
                if (index >= 0 && index + 1 < folder.Length
                    && packagesFromConfig.Any(p => folder[index + 1].StartsWith(p.Name + ".", StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                _items.Add(Clean(item));
                return;
            }

            if (DotNetAssemblies.Contains(name) || (_usesWpf && WpfAssemblies.Contains(name)) || (_usesWindowsForms && WindowsFormsAssemblies.Contains(name)))
            {
                return;
            }

            if (PackagedAssemblies.TryGetValue(name, out var package))
            {
                AddPackage(package, WindowsCompatibilityPackageVersion, privateAssets: null);
                _notes.Add($"{name} is the {package} package in .NET.");
                return;
            }

            _items.Add(Clean(item));
            _notes.Add($"The reference to {name} stays as it was: .NET has no assembly of that name, so check that the project still builds.");
        }

        /// <summary>
        /// A file item: dropped when the SDK includes the file by default and nothing on it matters, made an
        /// <c>Update</c> item when its metadata matters, and kept as it is otherwise.
        /// </summary>
        private void ConvertFileItem(XElement item, string type, string include)
        {
            var relative = include.Replace('\\', '/');
            var full = Path.GetFullPath(Path.Combine(_directory, relative));
            var inFolder = full.StartsWith(_directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            var hasLink = Metadata(item, "Link") is not null;
            var isWildcard = include.Contains('*', StringComparison.Ordinal);
            var extension = Path.GetExtension(full);
            var isDefault = inFolder && !hasLink && !isWildcard && type switch
            {
                "Compile" => string.Equals(extension, _sourceExtension, StringComparison.OrdinalIgnoreCase),
                "EmbeddedResource" => string.Equals(extension, ".resx", StringComparison.OrdinalIgnoreCase),
                "Page" => _usesWpf && string.Equals(extension, ".xaml", StringComparison.OrdinalIgnoreCase),
                "ApplicationDefinition" => _usesWpf && string.Equals(Path.GetFileName(full), "App.xaml", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetDirectoryName(full), _directory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal),
                "None" => true,
                _ => false,
            };

            if (type == "ApplicationDefinition" && !isDefault)
            {
                _customApplicationDefinition = true;
            }

            if (inFolder && !isWildcard)
            {
                _listed.Add(type + "|" + full);
            }

            if (!isDefault)
            {
                _items.Add(Clean(item));
                return;
            }

            var metadata = item.Elements()
                .Where(m => !DroppedMetadata.Contains(m.Name.LocalName))
                .Where(m => !(m.Name.LocalName == "DependentUpon" && IsConventionalParent(full, m.Value)))
                .Where(m => !(type is "Page" or "ApplicationDefinition" && m.Name.LocalName == "Generator" && m.Value.Trim() == "MSBuild:Compile"))
                .Select(Clean)
                .ToList();
            if (metadata.Count > 0)
            {
                _items.Add(new XElement(type, new XAttribute("Update", include), metadata));
            }
        }

        /// <summary>
        /// Files in the project folder that the SDK would include but the project did not list: each gets a
        /// <c>Remove</c> item, so the upgraded project builds what the legacy one built.
        /// </summary>
        private void AddRemovesForUnlistedFiles()
        {
            var otherProjects = Directory.Exists(_directory)
                ? Directory.EnumerateFiles(_directory, "*.*proj", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                    .Where(p => !string.Equals(p, project.FilePath, StringComparison.Ordinal) && ProjectParser.IsProjectFile(p))
                    .Select(p => Path.GetDirectoryName(p)! + Path.DirectorySeparatorChar)
                    .ToList()
                : [];
            var checks = new List<(string Type, string Extension)> { ("Compile", _sourceExtension), ("EmbeddedResource", ".resx") };
            if (_usesWpf)
            {
                checks.Add(("Page", ".xaml"));
            }

            foreach (var (type, extension) in checks)
            {
                foreach (var file in FilesUnder(_directory, extension).Where(f => !otherProjects.Any(o => f.StartsWith(o, StringComparison.Ordinal))))
                {
                    if (_listed.Contains(type + "|" + file) || (type == "Page" && _listed.Contains("ApplicationDefinition|" + file)))
                    {
                        continue;
                    }

                    var relative = Path.GetRelativePath(_directory, file).Replace('/', '\\');
                    _items.Add(new XElement(type, new XAttribute("Remove", relative)));
                }
            }
        }

        private void AddPackage(string name, string? version, string? privateAssets)
        {
            if (_packages.Any(p => string.Equals(p.Attribute("Include")?.Value, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var element = new XElement("PackageReference", new XAttribute("Include", name));
            if (version is not null)
            {
                element.Add(new XAttribute("Version", version));
            }

            if (privateAssets is not null)
            {
                element.Add(new XAttribute("PrivateAssets", privateAssets));
            }

            _packages.Add(element);
        }

        private bool HasAssemblyInfo() =>
            project.CompileIncludes.Any(c => string.Equals(Path.GetFileName(c.Replace('\\', '/')), "AssemblyInfo" + _sourceExtension, StringComparison.OrdinalIgnoreCase));

        /// <summary>Whether a property of a configuration's group is worth keeping: not one the SDK sets itself.</summary>
        private static bool KeepsConditionalProperty(XElement property)
        {
            var name = property.Name.LocalName;
            var value = property.Value.Trim();
            return !DroppedProperties.Contains(name)
                && !(name == "DefineConstants" && value is "DEBUG;TRACE" or "TRACE" or "DEBUG" or "TRACE;DEBUG")
                && !(name == "PlatformTarget" && string.Equals(value, "AnyCPU", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Whether an import is the project's own, rather than MSBuild's legacy targets or a package's build files.</summary>
        private static bool KeepsImport(XElement import)
        {
            var path = import.Attribute("Project")?.Value ?? string.Empty;
            return import.Attribute("Sdk") is null
                && !path.Contains("$(MSBuildToolsPath)", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("$(MSBuildBinPath)", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("$(MSBuildExtensionsPath", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("$(VSToolsPath)", StringComparison.OrdinalIgnoreCase)
                && !path.Replace('\\', '/').Contains("packages/", StringComparison.OrdinalIgnoreCase)
                && !path.Contains(".nuget", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Whether a target is the project's own: not NuGet's check of packages.config imports, and not an empty BeforeBuild or AfterBuild.</summary>
        private static bool KeepsTarget(XElement target)
        {
            var name = target.Attribute("Name")?.Value ?? string.Empty;
            if (name == "EnsureNuGetPackageBuildImports")
            {
                return false;
            }

            return !(name is "BeforeBuild" or "AfterBuild" && !target.Elements().Any());
        }

        /// <summary>
        /// Whether <c>DependentUpon</c> names the file an item is nested under anyway: <c>Form1.cs</c> for
        /// <c>Form1.Designer.cs</c> or <c>Form1.resx</c>, and <c>MainWindow.xaml</c> for <c>MainWindow.xaml.cs</c>.
        /// </summary>
        private static bool IsConventionalParent(string file, string dependentUpon)
        {
            var name = Path.GetFileName(file);
            var parent = Path.GetFileName(dependentUpon.Replace('\\', '/'));
            // Form1.Designer.vb under Form1.vb in a Visual Basic project, as Form1.Designer.cs under Form1.cs.
            var source = name.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) || parent.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) ? ".vb" : ".cs";
            var designer = ".Designer" + source;
            var stem = name.EndsWith(designer, StringComparison.OrdinalIgnoreCase) ? name[..^designer.Length] : Path.GetFileNameWithoutExtension(name);

            // MainWindow.xaml.cs under MainWindow.xaml, as Visual Studio nests code-behind by its name.
            return string.Equals(parent, stem + source, StringComparison.OrdinalIgnoreCase)
                || (name.EndsWith(source, StringComparison.OrdinalIgnoreCase) && string.Equals(parent, name[..^3], StringComparison.OrdinalIgnoreCase));
        }

        private static XElement WithCondition(XElement item, string condition)
        {
            item.SetAttributeValue("Condition", condition);
            return item;
        }

        private static string? Metadata(XElement item, string name) =>
            item.Attribute(name)?.Value ?? item.Elements().FirstOrDefault(m => m.Name.LocalName == name)?.Value;

        private static IEnumerable<string> FilesUnder(string directory, string extension)
        {
            if (!Directory.Exists(directory))
            {
                return [];
            }

            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
            return Directory.EnumerateFiles(directory, "*" + extension, options)
                .Where(f => string.Equals(Path.GetExtension(f), extension, StringComparison.OrdinalIgnoreCase))
                .Where(f => !Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar).SkipLast(1).Any(PathUtilities.IsIgnoredDirectory))
                .Order(StringComparer.Ordinal);
        }

        private static string Count(int count, string noun) => count == 1 ? $"one {noun}" : $"{count} {noun}s";

        private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An element without the MSBuild 2003 namespace, which SDK-style projects do not use.</summary>
    private static XElement Clean(XElement element) =>
        new(
            element.Name.LocalName,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => new XAttribute(a.Name.LocalName, a.Value)),
            element.Nodes().Select(node => node switch
            {
                XElement child => (object)Clean(child),
                XText text when string.IsNullOrWhiteSpace(text.Value) => null!,
                XText text => new XText(text.Value),
                XComment comment => new XComment(comment.Value),
                _ => null!,
            }).Where(n => n is not null));

    /// <summary>The project as Visual Studio writes SDK-style projects: two-space indents, a blank line between groups.</summary>
    private static string Format(XElement project)
    {
        var settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "  ", NewLineChars = "\n" };
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, settings))
        {
            project.WriteTo(writer);
        }

        // A blank line between the groups of the project, as in the templates.
        var lines = builder.ToString().Split('\n');
        var text = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (i > 1 && line.StartsWith("  <", StringComparison.Ordinal) && !line.StartsWith("  </", StringComparison.Ordinal))
            {
                text.Append('\n');
            }

            text.Append(line).Append('\n');
        }

        return text.ToString().Replace("\n\n</Project>", "\n</Project>", StringComparison.Ordinal).Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
    }
}
