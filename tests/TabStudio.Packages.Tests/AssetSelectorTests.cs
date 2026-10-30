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
    public void The_CSharp_sources_of_contentFiles_come_from_the_nearest_framework_folder()
    {
        // Spectre.Console's Wcwidth.Sources is a source-only package: NuGet compiles its contentFiles into the project.
        string[] files =
        [
            "contentFiles/cs/netstandard2.0/Wcwidth/UnicodeCalculator.cs",
            "contentFiles/cs/net8.0/Wcwidth/UnicodeCalculator.cs",
            "contentFiles/cs/net8.0/Wcwidth/readme.txt",
            "contentFiles/vb/net8.0/Wcwidth/UnicodeCalculator.vb",
            "contentFiles/any/any/Shared.cs",
            "lib/net8.0/_._",
        ];

        Assert.Equal(new[] { "contentFiles/cs/net8.0/Wcwidth/UnicodeCalculator.cs" }, AssetSelector.SelectContentSources(files, Net10, Fallback, AssetFlags.ContentFiles));
        Assert.Equal(new[] { "contentFiles/any/any/Shared.cs" }, AssetSelector.SelectContentSources(["contentFiles/any/any/Shared.cs"], Net10, Fallback, AssetFlags.ContentFiles));
        Assert.Empty(AssetSelector.SelectContentSources(files, Net10, Fallback, AssetFlags.Compile | AssetFlags.Runtime));
    }

    [Fact]
    public void Content_files_whose_nuspec_build_action_is_not_Compile_are_not_compiled()
    {
        var nuspec = """
            <package><metadata><id>Source</id><version>1.0.0</version>
              <contentFiles>
                <files include="cs/**/*.cs" buildAction="Compile" />
                <files include="cs/net8.0/Tools/*.cs" exclude="cs/net8.0/Tools/Kept.cs" buildAction="None" />
              </contentFiles>
            </metadata></package>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(nuspec));
        var entries = Manifests.NuspecReader.ReadContentFileEntries(stream);
        string[] files = ["contentFiles/cs/net8.0/Main.cs", "contentFiles/cs/net8.0/Tools/Kept.cs", "contentFiles/cs/net8.0/Tools/Left.cs"];

        Assert.Equal(
            new[] { "contentFiles/cs/net8.0/Main.cs", "contentFiles/cs/net8.0/Tools/Kept.cs" },
            AssetSelector.SelectContentSources(files, Net10, Fallback, AssetFlags.ContentFiles, entries));
    }

    [Fact]
    public void A_package_s_framework_references_are_read_for_the_nearest_framework()
    {
        // Microsoft.AspNetCore.Mvc.NewtonsoftJson brings ASP.NET Core to Ocelot's library, which has no FrameworkReference.
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>Microsoft.AspNetCore.Mvc.NewtonsoftJson</id>
                <version>10.0.0</version>
                <frameworkReferences>
                  <group targetFramework="net10.0"><frameworkReference name="Microsoft.AspNetCore.App" /></group>
                  <group targetFramework="netstandard2.0" />
                </frameworkReferences>
              </metadata>
            </package>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(nuspec));

        Assert.Equal(new[] { "Microsoft.AspNetCore.App" }, TabStudio.Packages.Manifests.NuspecReader.ReadFrameworkReferences(stream, Net10, Fallback));
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

    [Fact]
    public void Analyzers_come_from_the_highest_roslyn_folder_the_compiler_supports_plus_unversioned_ones()
    {
        var analyzers = AssetSelector.SelectAnalyzers(
            [
                "analyzers/dotnet/cs/Shared.dll",
                "analyzers/dotnet/Agnostic.dll",
                "analyzers/dotnet/vb/VisualBasic.dll",
                "analyzers/dotnet/roslyn4.0/cs/Generator.dll",
                "analyzers/dotnet/roslyn4.12/cs/Generator.dll",
                "analyzers/dotnet/roslyn4.12/cs/Generator.CodeFixes.dll",
                "analyzers/dotnet/roslyn5.0/cs/Generator.dll",
                "analyzers/dotnet/cs/readme.txt",
                "lib/net8.0/Lib.dll",
            ],
            new Version(4, 14));

        Assert.Equal(
            new[]
            {
                "analyzers/dotnet/Agnostic.dll",
                "analyzers/dotnet/cs/Shared.dll",
                "analyzers/dotnet/roslyn4.12/cs/Generator.CodeFixes.dll",
                "analyzers/dotnet/roslyn4.12/cs/Generator.dll",
            },
            analyzers);
    }

    /// <summary>
    /// What the .NET 10 SDK's ResolvePackageAssets picked from a package with these files (measured with SDK 10.0.113
    /// and a local feed): a roslyn folder may come before or after <c>dotnet</c>, and the highest version wins across
    /// the whole package.
    /// </summary>
    [Fact]
    public void Analyzers_are_chosen_as_the_SDK_chooses_them_whatever_the_folder_layout()
    {
        var analyzers = AssetSelector.SelectAnalyzers(
            [
                "analyzers/roslyn4.0/dotnet/cs/A.dll",
                "analyzers/roslyn5.0/dotnet/cs/A.dll",
                "analyzers/roslyn9.0/dotnet/cs/A.dll",
                "analyzers/dotnet/roslyn4.4/cs/B.dll",
                "analyzers/dotnet/cs/C.dll",
                "analyzers/dotnet/vb/D.dll",
                "analyzers/cs/E.dll",
                "analyzers/F.dll",
                "analyzers/dotnet/cs/G.resources.dll",
                "analyzers/dotnet/cs/de/G.resources.dll",
            ],
            new Version(5, 0));

        Assert.Equal(
            new[]
            {
                "analyzers/F.dll",
                "analyzers/cs/E.dll",
                "analyzers/dotnet/cs/C.dll",
                "analyzers/roslyn5.0/dotnet/cs/A.dll",
            },
            analyzers);
    }

    [Fact]
    public void Riok_Mapperly_s_layout_gives_its_generator_and_abstractions()
    {
        var analyzers = AssetSelector.SelectAnalyzers(
            [
                "analyzers/roslyn4.0/dotnet/cs/Riok.Mapperly.dll",
                "analyzers/roslyn4.0/dotnet/cs/Riok.Mapperly.Abstractions.dll",
                "analyzers/roslyn4.11/dotnet/cs/Riok.Mapperly.dll",
                "analyzers/roslyn4.11/dotnet/cs/Riok.Mapperly.Abstractions.dll",
                "analyzers/roslyn5.0/dotnet/cs/Riok.Mapperly.dll",
                "analyzers/roslyn5.0/dotnet/cs/Riok.Mapperly.Abstractions.dll",
                "build/Riok.Mapperly.targets",
                "lib/netstandard2.0/Riok.Mapperly.Abstractions.dll",
            ],
            new Version(5, 0));

        Assert.Equal(
            new[] { "analyzers/roslyn5.0/dotnet/cs/Riok.Mapperly.Abstractions.dll", "analyzers/roslyn5.0/dotnet/cs/Riok.Mapperly.dll" },
            analyzers);
    }

    [Fact]
    public void A_package_whose_analyzers_all_need_a_newer_compiler_says_which_version()
    {
        var analyzers = AssetSelector.SelectAnalyzers(
            ["analyzers/dotnet/roslyn5.3/cs/Generator.dll", "analyzers/dotnet/roslyn6.0/cs/Generator.dll"],
            new Version(5, 0),
            out var onlyNewer);

        Assert.Empty(analyzers);
        Assert.Equal(new Version(5, 3), onlyNewer);
        AssetSelector.SelectAnalyzers(["analyzers/dotnet/roslyn4.0/cs/G.dll", "analyzers/dotnet/roslyn6.0/cs/G.dll"], new Version(5, 0), out var fits);
        Assert.Null(fits);
    }

    [Fact]
    public void Packages_without_a_compiler_version_bring_no_analyzers()
    {
        var assets = Select("lib/net8.0/Lib.dll", "analyzers/dotnet/cs/Generator.dll");

        Assert.Empty(assets.Analyzers);
        Assert.Equal(
            new[] { "analyzers/dotnet/cs/Generator.dll" },
            AssetSelector.Select(["lib/net8.0/Lib.dll", "analyzers/dotnet/cs/Generator.dll"], Net10, Fallback, new Version(4, 14)).Analyzers);
    }
}
