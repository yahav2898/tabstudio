namespace TabStudio.Core.Workspaces;

/// <summary>
/// A NuGet package referenced by a project.
/// </summary>
/// <param name="Name">The package id, for example <c>Newtonsoft.Json</c>.</param>
/// <param name="Version">The requested version, when the project states one.</param>
/// <param name="PrivateAssets">
/// The reference's <c>PrivateAssets</c> metadata, such as <c>all</c>, which keeps the package from flowing to projects
/// that reference this one; <see langword="null"/> when it is not set.
/// </param>
public sealed record PackageReferenceInfo(string Name, string? Version, string? PrivateAssets = null)
{
    /// <summary>Whether <c>PrivateAssets</c> is <c>all</c>: the package stays with this project.</summary>
    public bool IsPrivate => AssemblyAttributeItems.IncludesAll(PrivateAssets);
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
    /// <summary>Whether the referencing project compiles against this project's assembly, as a reference.</summary>
    public bool IsCompiledAgainst =>
        ReferenceOutputAssembly && !string.Equals(OutputItemType, "Analyzer", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>Patterns from <c>&lt;Compile Include="..." /&gt;</c> items, relative to the project directory.</summary>
    public IReadOnlyList<string> CompileIncludes { get; init; } = [];

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
