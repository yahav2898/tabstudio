namespace TabStudio.Packages;

/// <summary>
/// One package in the dependency graph of a restored project.
/// </summary>
/// <param name="Id">The package id, spelled as the package spells it.</param>
/// <param name="Version">The selected version, normalized, for example <c>13.0.3</c>.</param>
/// <param name="DirectoryPath">Where the package is extracted in the global packages folder.</param>
/// <param name="IsDirect">Whether the project references the package itself, rather than through another package.</param>
/// <param name="CompileAssemblies">Full paths of the assemblies code compiles against.</param>
/// <param name="RuntimeAssemblies">Full paths of the assemblies a running program loads.</param>
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
}
