namespace TabStudio.Core.Workspaces;

/// <summary>
/// A NuGet package referenced by a project.
/// </summary>
/// <param name="Name">The package id, for example <c>Newtonsoft.Json</c>.</param>
/// <param name="Version">The requested version, when the project states one.</param>
public sealed record PackageReferenceInfo(string Name, string? Version);

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

    /// <summary>The global usings the project's <c>&lt;Using Include="..." /&gt;</c> items add, in order.</summary>
    public IReadOnlyList<UsingInfo> Usings { get; init; } = [];

    /// <summary>The namespaces <c>&lt;Using Remove="..." /&gt;</c> items take away from the implicit global usings.</summary>
    public IReadOnlyList<string> RemovedUsings { get; init; } = [];

    /// <summary>Patterns from <c>&lt;Compile Include="..." /&gt;</c> items, relative to the project directory.</summary>
    public IReadOnlyList<string> CompileIncludes { get; init; } = [];

    /// <summary>Patterns from <c>&lt;Compile Remove="..." /&gt;</c> items, relative to the project directory.</summary>
    public IReadOnlyList<string> CompileRemoves { get; init; } = [];

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

    /// <summary>Whether the project references the given NuGet package. Case-insensitive.</summary>
    /// <param name="packageName">The package id.</param>
    public bool HasPackage(string packageName) =>
        PackageReferences.Any(p => string.Equals(p.Name, packageName, StringComparison.OrdinalIgnoreCase));

    private static bool IsEnabled(string? value) =>
        string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
