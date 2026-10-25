using NuGet.Frameworks;

namespace TabStudio.Packages.Assets;

/// <summary>
/// The assemblies of one package that a project uses, as paths relative to the package folder with forward slashes.
/// </summary>
/// <param name="CompileAssemblies">What code compiles against: <c>ref/{tfm}/</c>, or <c>lib/{tfm}/</c> when the package has no reference assemblies.</param>
/// <param name="RuntimeAssemblies">What a program loads when it runs: <c>lib/{tfm}/</c>.</param>
/// <param name="IsCompatible">
/// Whether the package can be used: it has assets for the target framework (or a fallback), or no framework-specific
/// assemblies at all, like a metapackage.
/// </param>
/// <param name="FallbackFramework">The <c>AssetTargetFallback</c> framework whose assets were used, if any.</param>
/// <param name="SupportedFrameworks">Every framework the package has <c>lib/</c> or <c>ref/</c> folders for.</param>
internal sealed record PackageAssets(
    IReadOnlyList<string> CompileAssemblies,
    IReadOnlyList<string> RuntimeAssemblies,
    bool IsCompatible,
    NuGetFramework? FallbackFramework,
    IReadOnlyList<NuGetFramework> SupportedFrameworks)
{
    /// <summary>The C# analyzers and source generators the compiler loads: <c>analyzers/dotnet/...</c>.</summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];
}
