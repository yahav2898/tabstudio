namespace TabStudio.Core.Workspaces;

/// <summary>
/// A NuGet package referenced by a project.
/// </summary>
/// <param name="Name">The package id, for example <c>Newtonsoft.Json</c>.</param>
/// <param name="Version">
/// The requested version, when the project states one: with central package management, the reference's
/// <c>VersionOverride</c>, which wins over the central version, or else its <c>Version</c>. <see langword="null"/> for
/// a reference whose version comes from <c>Directory.Packages.props</c>.
/// </param>
/// <param name="PrivateAssets">
/// The reference's <c>PrivateAssets</c> metadata, such as <c>all</c>, which keeps the package from flowing to projects
/// that reference this one; <see langword="null"/> when it is not set.
/// </param>
public sealed record PackageReferenceInfo(string Name, string? Version, string? PrivateAssets = null)
{
    /// <summary>Whether <c>PrivateAssets</c> is <c>all</c>: the package stays with this project.</summary>
    public bool IsPrivate => AssemblyAttributeItems.IncludesAll(PrivateAssets);

    /// <summary>The reference's <c>IncludeAssets</c> metadata, such as <c>runtime; build; analyzers</c>; <see langword="null"/> when it is not set.</summary>
    public string? IncludeAssets { get; init; }

    /// <summary>The reference's <c>ExcludeAssets</c> metadata, such as <c>analyzers</c>; <see langword="null"/> when it is not set.</summary>
    public string? ExcludeAssets { get; init; }
}

/// <summary>A <c>&lt;ProjectReference&gt;</c> item with the metadata that decides how it is used.</summary>
/// <param name="Path">Absolute path of the referenced project file.</param>
/// <param name="ReferenceOutputAssembly">
/// Whether the referencing project compiles against the referenced project's assembly. <c>false</c> makes it a
/// build-order dependency only.
/// </param>
/// <param name="IsPrivate">
/// Whether <c>PrivateAssets</c> is <c>all</c>, which stops the reference from flowing on to projects that reference
/// the referencing project.
/// </param>
/// <param name="OutputItemType">The <c>OutputItemType</c> metadata, such as <c>Analyzer</c> for an in-repo source generator.</param>
public sealed record ProjectReferenceInfo(string Path, bool ReferenceOutputAssembly = true, bool IsPrivate = false, string? OutputItemType = null)
{
    /// <summary>
    /// Whether the referencing project compiles against this project's assembly, as a reference: what
    /// <c>ReferenceOutputAssembly</c> says, whatever the <c>OutputItemType</c>. As in the SDK, a reference with
    /// <c>OutputItemType="Analyzer"</c> and <c>ReferenceOutputAssembly</c> left at <c>true</c> gives the project both
    /// ways, as an analyzer and as a reference (StronglyTypedId's attributes project).
    /// </summary>
    public bool IsCompiledAgainst => ReferenceOutputAssembly;

    /// <summary>
    /// Whether <c>OutputItemType</c> is <c>Analyzer</c>: the referenced project is a source generator or analyzer that
    /// runs inside the referencing project's compilation.
    /// </summary>
    public bool IsAnalyzer => string.Equals(OutputItemType, "Analyzer", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One argument of an assembly-level attribute the SDK generates.</summary>
/// <param name="Value">The value: the text of a string, or C# code when <paramref name="IsLiteral"/>.</param>
/// <param name="IsLiteral">Whether <paramref name="Value"/> is written as code (<c>_Parameter1_IsLiteral</c>) rather than as a string.</param>
/// <param name="Name">The property name of a named argument, or <see langword="null"/> for a positional one.</param>
public sealed record AssemblyAttributeArgument(string Value, bool IsLiteral = false, string? Name = null);

/// <summary>
/// An assembly-level attribute the .NET SDK generates from the project: an <c>&lt;AssemblyAttribute&gt;</c> item, or
/// <c>InternalsVisibleTo</c> from an <c>&lt;InternalsVisibleTo&gt;</c> item.
/// </summary>
/// <param name="TypeName">The attribute type, such as <c>System.Runtime.CompilerServices.InternalsVisibleToAttribute</c>.</param>
/// <param name="Arguments">Its positional arguments in order, then its named ones.</param>
public sealed record AssemblyAttributeInfo(string TypeName, IReadOnlyList<AssemblyAttributeArgument> Arguments);

/// <summary>
/// A namespace imported into every file of a project by a <c>&lt;Using Include="..." /&gt;</c> item, as the xUnit,
/// NUnit and MSTest project templates do for their framework.
/// </summary>
/// <param name="Name">The namespace, or the type of a static using.</param>
/// <param name="Alias">The alias, from the item's <c>Alias</c> metadata, or <see langword="null"/>.</param>
/// <param name="IsStatic">Whether the item's <c>Static</c> metadata makes it a <c>using static</c>.</param>
public sealed record UsingInfo(string Name, string? Alias = null, bool IsStatic = false);

/// <summary>
/// A <c>&lt;Reference Include="..." /&gt;</c> item: an assembly the project compiles against by name, as .NET Framework
/// projects reference <c>PresentationFramework</c> or <c>System.Windows.Forms</c>, or by file, with a <c>HintPath</c>.
/// </summary>
/// <param name="Name">The assembly's simple name, without the version and culture an <c>Include</c> may carry.</param>
/// <param name="HintPath">The <c>HintPath</c> metadata, as written in the project file, or <see langword="null"/>.</param>
public sealed record AssemblyReferenceInfo(string Name, string? HintPath = null);

/// <summary>
/// An item source generators see: an <c>&lt;AdditionalFiles&gt;</c> item, with its metadata, from the project, its
/// <c>Directory.Build</c> files or its packages' props and targets.
/// </summary>
/// <param name="Include">The file, or a pattern of files, relative to <paramref name="BaseDirectory"/>.</param>
/// <param name="BaseDirectory">The folder the pattern is relative to: the project's, as MSBuild evaluates items.</param>
/// <param name="Metadata">The item's metadata, such as Avalonia's <c>SourceItemGroup</c>. Keys are case-insensitive.</param>
public sealed record AdditionalFileItem(string Include, string BaseDirectory, IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// For an item a target added before the compiler ran (see <see cref="TargetItems"/>), every item type it was on the
    /// way, such as <c>ResxCode</c> then <c>AdditionalFiles</c> for ThisAssembly.Strings, whose generators read the
    /// metadata of each (<c>build_metadata.ResxCode.ManifestResourceName</c>); <see langword="null"/> for an
    /// <c>AdditionalFiles</c> item of a project file.
    /// </summary>
    public IReadOnlyList<string>? ItemTypes { get; init; }
}

/// <summary>
/// The metadata an item of a project file gives the files it includes or updates, such as MudBlazor's
/// <c>&lt;EmbeddedResource Update="Resources\LanguageResource.resx"&gt;&lt;AsConstants&gt;true&lt;/AsConstants&gt;</c>.
/// </summary>
/// <param name="Include">The pattern of its <c>Include</c> or <c>Update</c>, relative to the project's folder.</param>
/// <param name="Metadata">The metadata. Keys are case-insensitive.</param>
public sealed record ItemMetadataInfo(string Include, IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// A <c>&lt;CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="..." /&gt;</c> item: metadata of an item
/// type that the SDK hands generators as <c>build_metadata.{ItemType}.{MetadataName}</c>.
/// </summary>
/// <param name="ItemType">The item type, such as <c>AdditionalFiles</c>.</param>
/// <param name="MetadataName">The metadata, such as <c>SourceItemGroup</c>.</param>
public sealed record CompilerVisibleItemMetadataInfo(string ItemType, string MetadataName);

/// <summary>
/// Everything TabStudio knows about one project file, read straight from the file without running MSBuild.
/// </summary>
/// <remarks>
/// Reading project files directly keeps opening a solution instant on a tablet. The trade-off is that
/// MSBuild logic such as imported targets or complex conditions is not evaluated; see
/// <see cref="ProjectParser"/> for the exact rules.
/// </remarks>
public sealed record ProjectInfo
{
    /// <summary>The project name: the file name without its extension.</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path of the project file.</summary>
    public required string FilePath { get; init; }

    /// <summary>The language, inferred from the file extension.</summary>
    public ProjectLanguage Language { get; init; }

    /// <summary>The MSBuild SDK, for example <c>Microsoft.NET.Sdk.Web</c>, or <see langword="null"/> for legacy projects.</summary>
    public string? Sdk { get; init; }

    /// <summary>Target frameworks, from <c>TargetFramework</c> or <c>TargetFrameworks</c>.</summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>The <c>OutputType</c> property (<c>Exe</c>, <c>WinExe</c>, <c>Library</c>), if set.</summary>
    public string? OutputType { get; init; }

    /// <summary>All properties declared in the project file. Keys are case-insensitive.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>NuGet packages the project references.</summary>
    public IReadOnlyList<PackageReferenceInfo> PackageReferences { get; init; } = [];

    /// <summary>
    /// The shared frameworks the project uses besides .NET itself, such as <c>Microsoft.AspNetCore.App</c>: its
    /// <c>FrameworkReference</c> items and the framework its SDK brings in.
    /// </summary>
    public IReadOnlyList<string> FrameworkReferences { get; init; } = [];

    /// <summary>Absolute paths of the projects this project references.</summary>
    public IReadOnlyList<string> ProjectReferences { get; init; } = [];

    /// <summary>
    /// The project references with their metadata, in the same order as <see cref="ProjectReferences"/>. A reference
    /// missing here (a project built in code) counts as an ordinary one.
    /// </summary>
    public IReadOnlyList<ProjectReferenceInfo> ProjectReferenceItems { get; init; } = [];

    /// <summary>Every MSBuild SDK the project uses, such as <c>Microsoft.NET.Sdk</c> and <c>Aspire.AppHost.Sdk</c>.</summary>
    public IReadOnlyList<string> Sdks { get; init; } = [];

    /// <summary>
    /// The assembly attributes the SDK generates for the project: its <c>AssemblyAttribute</c> and
    /// <c>InternalsVisibleTo</c> items, including those of <c>Directory.Build.props</c> and <c>.targets</c>.
    /// </summary>
    public IReadOnlyList<AssemblyAttributeInfo> AssemblyAttributes { get; init; } = [];

    /// <summary>The global usings the project's <c>&lt;Using Include="..." /&gt;</c> items add, in order.</summary>
    public IReadOnlyList<UsingInfo> Usings { get; init; } = [];

    /// <summary>The namespaces <c>&lt;Using Remove="..." /&gt;</c> items take away from the implicit global usings.</summary>
    public IReadOnlyList<string> RemovedUsings { get; init; } = [];

    /// <summary>
    /// The namespaces a Visual Basic project imports into every file, from its <c>&lt;Import Include="..." /&gt;</c>
    /// items, as Visual Studio writes them (<c>System.Data</c>, <c>System.Drawing</c>), in order.
    /// </summary>
    public IReadOnlyList<string> Imports { get; init; } = [];

    /// <summary>The namespaces <c>&lt;Import Remove="..." /&gt;</c> items take away from the Visual Basic SDK's default imports.</summary>
    public IReadOnlyList<string> RemovedImports { get; init; } = [];

    /// <summary>Whether the project is written in Visual Basic.</summary>
    public bool IsVisualBasic => Language == ProjectLanguage.VisualBasic;

    /// <summary>Whether TabStudio compiles the project with Roslyn in the app: a C# or a Visual Basic project.</summary>
    public bool IsRoslynLanguage => Language is ProjectLanguage.CSharp or ProjectLanguage.VisualBasic;

    /// <summary>Patterns from <c>&lt;Compile Include="..." /&gt;</c> items, relative to the project directory.</summary>
    public IReadOnlyList<string> CompileIncludes { get; init; } = [];

    /// <summary>
    /// The <c>Exclude</c> patterns of <c>&lt;Compile Include="..." Exclude="..." /&gt;</c> items, keyed by their
    /// <c>Include</c> pattern as it is in <see cref="CompileIncludes"/>: files the include leaves out, as Dock's
    /// <c>Exclude="..\Dock.Model.ReactiveUI\bin\**;..\Dock.Model.ReactiveUI\obj\**"</c>. Unlike
    /// <see cref="CompileRemoves"/>, they apply to that include only. An include without one is not here; of several
    /// items with the same <c>Include</c>, the first counts.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CompileIncludeExcludes { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>Patterns from <c>&lt;Compile Remove="..." /&gt;</c> items, relative to the project directory.</summary>
    public IReadOnlyList<string> CompileRemoves { get; init; } = [];

    /// <summary>Patterns from <c>&lt;EmbeddedResource Include="..." /&gt;</c> items: files compiled into the assembly.</summary>
    public IReadOnlyList<string> EmbeddedResourceIncludes { get; init; } = [];

    /// <summary>Patterns from <c>&lt;EmbeddedResource Remove="..." /&gt;</c> items.</summary>
    public IReadOnlyList<string> EmbeddedResourceRemoves { get; init; } = [];

    /// <summary>
    /// Patterns from <c>&lt;AvaloniaResource Include="..." /&gt;</c> items: files an Avalonia app opens through
    /// <c>avares://</c> addresses, such as its images and fonts.
    /// </summary>
    public IReadOnlyList<string> AvaloniaResourceIncludes { get; init; } = [];

    /// <summary>Patterns from <c>&lt;AvaloniaResource Remove="..." /&gt;</c> items.</summary>
    public IReadOnlyList<string> AvaloniaResourceRemoves { get; init; } = [];

    /// <summary>
    /// Whether the project folder has Razor components (<c>.razor</c> files), which makes a web app a Blazor Web App and
    /// brings in Blazor's script. Set by <see cref="ProjectParser.Load"/>, which looks at the files.
    /// </summary>
    public bool HasRazorComponents { get; init; }

    /// <summary>
    /// Whether the project folder of an app on the Web SDK has Razor views or pages (<c>.cshtml</c> files), as an MVC or
    /// Razor Pages app does, rather than only serving an API. Set by <see cref="ProjectParser.Load"/>, which looks at the
    /// files, and looked for only in an app without Razor components (<see cref="HasRazorComponents"/>), which has a UI
    /// either way.
    /// </summary>
    public bool HasRazorViews { get; init; }

    /// <summary>
    /// The <c>Project</c> of each <c>&lt;Import&gt;</c> in the project file itself, as written, such as
    /// <c>$(MSBuildExtensionsPath)\Xamarin\iOS\Xamarin.iOS.CSharp.targets</c>; SDK imports are not included.
    /// </summary>
    public IReadOnlyList<string> ImportPaths { get; init; } = [];

    /// <summary>The project's <c>&lt;Reference&gt;</c> items, in order: assemblies it compiles against by name or by file.</summary>
    public IReadOnlyList<AssemblyReferenceInfo> AssemblyReferences { get; init; } = [];

    /// <summary>The <c>Include</c> of each <c>&lt;COMReference&gt;</c> item: the COM type libraries the project uses.</summary>
    public IReadOnlyList<string> ComReferences { get; init; } = [];

    /// <summary>
    /// The source files of a .NET Framework project, relative to its folder, that call Windows through P/Invoke
    /// (<c>DllImport</c>, <c>LibraryImport</c> or <c>static extern</c>). Set by <see cref="ProjectParser.Load"/>, which
    /// reads the files of legacy projects only; empty for others.
    /// </summary>
    public IReadOnlyList<string> PlatformInvokeFiles { get; init; } = [];

    /// <summary>
    /// The properties generators read, from <c>&lt;CompilerVisibleProperty&gt;</c> items of the project and of its
    /// packages' props and targets: the SDK writes each one, even when it is empty, as <c>build_property.{Name}</c>.
    /// </summary>
    public IReadOnlyList<string> CompilerVisibleProperties { get; init; } = [];

    /// <summary>The item metadata generators read, from <c>&lt;CompilerVisibleItemMetadata&gt;</c> items.</summary>
    public IReadOnlyList<CompilerVisibleItemMetadataInfo> CompilerVisibleItemMetadata { get; init; } = [];

    /// <summary>The <c>&lt;AdditionalFiles&gt;</c> items generators read, such as a generator's settings file.</summary>
    public IReadOnlyList<AdditionalFileItem> AdditionalFiles { get; init; } = [];

    /// <summary>
    /// The targets of the project's files and its packages' that run before the compiler, whose items
    /// <see cref="TargetItems"/> carries out, in the order they run.
    /// </summary>
    public IReadOnlyList<System.Xml.Linq.XElement> CompilerTargets { get; init; } = [];

    /// <summary>The <c>.proto</c> files of the project's <c>&lt;Protobuf&gt;</c> items, which Grpc.Tools' <c>protoc</c> compiles.</summary>
    public IReadOnlyList<string> ProtobufIncludes { get; init; } = [];

    /// <summary>The metadata the project's <c>EmbeddedResource</c> items give the files they include or update, in order.</summary>
    public IReadOnlyList<ItemMetadataInfo> EmbeddedResourceItems { get; init; } = [];

    /// <summary>
    /// The props and targets of the project's packages that have been read into it (see
    /// <see cref="ProjectParser.WithPackageBuildFiles"/>), in MSBuild's order; empty before its packages are restored.
    /// </summary>
    public IReadOnlyList<string> PackageBuildFiles { get; init; } = [];

    /// <summary>What the project produces.</summary>
    public ProjectKind Kind { get; init; }

    /// <summary>Whether the project contains tests, regardless of <see cref="Kind"/>.</summary>
    public bool IsTestProject { get; init; }

    /// <summary>The directory that contains the project file.</summary>
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>Whether the project produces something that can be launched, rather than a library.</summary>
    public bool IsExecutable =>
        string.Equals(OutputType, "Exe", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(OutputType, "WinExe", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether any target framework is Windows-only, such as <c>net10.0-windows</c>.</summary>
    public bool TargetsWindowsOnly =>
        TargetFrameworks.Count > 0 &&
        TargetFrameworks.All(tf => tf.Contains("-windows", StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether <c>ImplicitUsings</c> is turned on.</summary>
    public bool ImplicitUsings => IsEnabled(GetProperty("ImplicitUsings"));

    /// <summary>Whether nullable reference types are turned on.</summary>
    public bool NullableEnabled => IsEnabled(GetProperty("Nullable"));

    /// <summary>Returns a property value, or <see langword="null"/> if the project does not declare it.</summary>
    /// <param name="name">The property name. Case-insensitive.</param>
    public string? GetProperty(string name) =>
        Properties.TryGetValue(name, out var value) ? value : null;

    /// <summary>The metadata of a project reference; an ordinary reference when the project has none recorded.</summary>
    /// <param name="path">Absolute path of the referenced project file.</param>
    public ProjectReferenceInfo GetProjectReference(string path) =>
        ProjectReferenceItems.FirstOrDefault(r => string.Equals(r.Path, path, StringComparison.Ordinal)) ?? new ProjectReferenceInfo(path);

    /// <summary>Whether the project uses an MSBuild SDK, as its main SDK or an additional one. Case-insensitive.</summary>
    /// <param name="sdk">The SDK name, such as <c>Aspire.AppHost.Sdk</c>.</param>
    public bool UsesSdk(string sdk) =>
        string.Equals(Sdk, sdk, StringComparison.OrdinalIgnoreCase) || Sdks.Contains(sdk, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the project references the given NuGet package. Case-insensitive.</summary>
    /// <param name="packageName">The package id.</param>
    public bool HasPackage(string packageName) =>
        PackageReferences.Any(p => string.Equals(p.Name, packageName, StringComparison.OrdinalIgnoreCase));

    private static bool IsEnabled(string? value) =>
        string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
