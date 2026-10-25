using NuGet.Frameworks;
using TabStudio.Packages.Assets;

namespace TabStudio.Packages.Tests;

public sealed class AssetSelectorTests
{
    private static readonly NuGetFramework Net10 = NuGetFramework.Parse("net10.0");
    private static readonly IReadOnlyList<NuGetFramework> Fallback =
        PackageRestoreOptions.DefaultAssetTargetFallback.Select(f => NuGetFramework.Parse(f)).ToList();

    [Fact]
    public void Compiles_against_reference_assemblies_and_runs_with_lib()
    {
        var assets = Select(
            "ref/netstandard2.0/Lib.dll",
            "lib/netstandard2.0/Lib.dll",
            "lib/net8.0/Lib.dll");

        Assert.Equal(new[] { "ref/netstandard2.0/Lib.dll" }, assets.CompileAssemblies);
        Assert.Equal(new[] { "lib/net8.0/Lib.dll" }, assets.RuntimeAssemblies);
        Assert.True(assets.IsCompatible);
    }

    [Fact]
    public void Picks_the_nearest_compatible_framework_folder()
    {
        var assets = Select(
            "lib/netstandard2.0/Lib.dll",
            "lib/net6.0/Lib.dll",
            "lib/net8.0/Lib.dll",
            "lib/net11.0/Lib.dll",
            "lib/net48/Lib.dll");

        Assert.Equal(new[] { "lib/net8.0/Lib.dll" }, assets.CompileAssemblies);
        Assert.Equal(new[] { "lib/net8.0/Lib.dll" }, assets.RuntimeAssemblies);
        Assert.Null(assets.FallbackFramework);
    }

    [Fact]
    public void A_placeholder_folder_is_compatible_but_contributes_nothing()
    {
        var assets = Select("lib/net10.0/_._", "lib/netstandard2.0/Lib.dll");

        Assert.True(assets.IsCompatible);
        Assert.Empty(assets.CompileAssemblies);
        Assert.Empty(assets.RuntimeAssemblies);
    }

    [Fact]
    public void Ignores_satellite_assemblies_and_other_files()
    {
        var assets = Select(
            "lib/net6.0/Lib.dll",
            "lib/net6.0/Lib.xml",
            "lib/net6.0/Lib.pdb",
            "lib/net6.0/de/Lib.resources.dll",
            "runtimes/linux-arm64/lib/net6.0/Lib.dll",
            "analyzers/dotnet/cs/Lib.Analyzers.dll",
            "build/Lib.targets");

        Assert.Equal(new[] { "lib/net6.0/Lib.dll" }, assets.RuntimeAssemblies);
    }

    [Fact]
    public void Falls_back_to_net_framework_assets_when_nothing_else_fits()
    {
        var assets = Select("lib/net45/Old.dll", "lib/net20/Older.dll");

        Assert.True(assets.IsCompatible);
        Assert.Equal(new[] { "lib/net45/Old.dll" }, assets.RuntimeAssemblies);
        Assert.Equal("net461", assets.FallbackFramework?.GetShortFolderName());
    }

    [Fact]
    public void A_package_with_only_incompatible_assemblies_is_incompatible()
    {
        var assets = Select("lib/net11.0/Future.dll", "lib/monoandroid10/Droid.dll");

        Assert.False(assets.IsCompatible);
        Assert.Equal(2, assets.SupportedFrameworks.Count);
    }

    [Fact]
    public void A_package_without_lib_or_ref_folders_is_compatible()
    {
        var assets = Select("build/Tool.props", "tools/run.ps1", "README.md");

        Assert.True(assets.IsCompatible);
        Assert.Empty(assets.CompileAssemblies);
    }

    private static PackageAssets Select(params string[] files) => AssetSelector.Select(files, Net10, Fallback);
}
