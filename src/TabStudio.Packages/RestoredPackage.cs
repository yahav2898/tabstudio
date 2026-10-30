namespace TabStudio.Packages;

/// <summary>
/// One package in the dependency graph of a restored project.
/// </summary>
/// <param name="Id">The package id, spelled as the package spells it.</param>
/// <param name="Version">The selected version, normalized, for example <c>13.0.3</c>.</param>
/// <param name="DirectoryPath">Where the package is extracted in the global packages folder.</param>
/// <param name="IsDirect">Whether the project references the package itself, rather than through another package.</param>
/// <param name="CompileAssemblies">Full paths of the assemblies code compiles against.</param>
/// <param name="RuntimeAssemblies">
/// Full paths of the assemblies a running program loads: the device's RID-specific ones
/// (<c>runtimes/{rid}/lib/{tfm}/</c>) when the package has them, otherwise <c>lib/{tfm}/</c>.
/// </param>
/// <param name="ProvidedByFramework">
/// Whether every assembly in the package is already part of the target framework, so the package contributes none.
/// </param>
public sealed record RestoredPackage(
    string Id,
    string Version,
    string DirectoryPath,
    bool IsDirect,
    IReadOnlyList<string> CompileAssemblies,
    IReadOnlyList<string> RuntimeAssemblies,
    bool ProvidedByFramework = false)
{
    /// <summary>Full paths of the package's C# analyzers and source generators.</summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];

    /// <summary>
    /// Full paths of the package's native libraries for the device: its <c>runtimes/{rid}/native/</c> files for the
    /// nearest RID, such as SQLite's <c>libe_sqlite3.so</c>.
    /// </summary>
    public IReadOnlyList<string> NativeLibraries { get; init; } = [];

    /// <summary>
    /// Whether the project uses the package's analyzers: it reaches the package through packages only. A package that
    /// comes from a referenced project keeps its analyzers to that project, as NuGet's default <c>PrivateAssets</c> does.
    /// </summary>
    public bool FlowsAnalyzers { get; init; } = true;

    /// <summary>
    /// Full paths of the package's props and targets files the project imports, as NuGet chooses them from
    /// <c>build/</c> and <c>buildTransitive/</c>: props first. They hold properties and items, such as
    /// <c>CompilerVisibleProperty</c>, that the package's source generators read.
    /// </summary>
    public IReadOnlyList<string> BuildFiles { get; init; } = [];

    /// <summary>
    /// Full paths of the C# sources of the package's <c>contentFiles/</c> that the project compiles, as a source-only
    /// package such as Wcwidth.Sources gives them (see <c>AssetSelector.SelectContentSources</c>).
    /// </summary>
    public IReadOnlyList<string> ContentSources { get; init; } = [];

    /// <summary>How far the package is from the project along its shortest path: 1 for a direct reference.</summary>
    public int Depth { get; init; } = 1;
}
