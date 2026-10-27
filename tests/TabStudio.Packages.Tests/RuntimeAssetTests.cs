using NuGet.Frameworks;
using TabStudio.Packages.Assets;

namespace TabStudio.Packages.Tests;

/// <summary>
/// RID-specific and native assets, with the file lists of the real packages (taken from nuget.org's packages, without
/// their satellite resources, documentation and signature files).
/// </summary>
public sealed class RuntimeAssetTests
{
    private static readonly NuGetFramework Net10 = NuGetFramework.Parse("net10.0");
    private static readonly IReadOnlyList<NuGetFramework> Fallback =
        PackageRestoreOptions.DefaultAssetTargetFallback.Select(f => NuGetFramework.Parse(f)).ToList();

    /// <summary>Microsoft.Data.SqlClient 6.0.2, which EF Core's SQL Server provider 9 uses.</summary>
    private static readonly string[] SqlClient6 =
    [
        "Microsoft.Data.SqlClient.nuspec",
        "ref/net462/Microsoft.Data.SqlClient.dll",
        "ref/net8.0/Microsoft.Data.SqlClient.dll",
        "ref/net9.0/Microsoft.Data.SqlClient.dll",
        "lib/net462/Microsoft.Data.SqlClient.dll",
        "lib/net8.0/Microsoft.Data.SqlClient.dll",
        "lib/net9.0/Microsoft.Data.SqlClient.dll",
        "lib/net9.0/de/Microsoft.Data.SqlClient.resources.dll",
        "runtimes/win/lib/net462/Microsoft.Data.SqlClient.dll",
        "runtimes/win/lib/net8.0/Microsoft.Data.SqlClient.dll",
        "runtimes/unix/lib/net8.0/Microsoft.Data.SqlClient.dll",
        "runtimes/win/lib/net9.0/Microsoft.Data.SqlClient.dll",
        "runtimes/unix/lib/net9.0/Microsoft.Data.SqlClient.dll",
    ];

    /// <summary>Microsoft.Data.SqlClient 5.1.6, which EF Core's SQL Server provider 7 and 8 use.</summary>
    private static readonly string[] SqlClient5 =
    [
        "ref/net6.0/Microsoft.Data.SqlClient.dll",
        "ref/netstandard2.0/Microsoft.Data.SqlClient.dll",
        "ref/netstandard2.1/Microsoft.Data.SqlClient.dll",
        "lib/net462/Microsoft.Data.SqlClient.dll",
        "lib/net6.0/Microsoft.Data.SqlClient.dll",
        "lib/netstandard2.0/Microsoft.Data.SqlClient.dll",
        "lib/netstandard2.1/Microsoft.Data.SqlClient.dll",
        "runtimes/win/lib/net462/Microsoft.Data.SqlClient.dll",
        "runtimes/win/lib/net6.0/Microsoft.Data.SqlClient.dll",
        "runtimes/unix/lib/net6.0/Microsoft.Data.SqlClient.dll",
        "runtimes/win/lib/netstandard2.0/Microsoft.Data.SqlClient.dll",
        "runtimes/unix/lib/netstandard2.0/Microsoft.Data.SqlClient.dll",
        "runtimes/win/lib/netstandard2.1/Microsoft.Data.SqlClient.dll",
        "runtimes/unix/lib/netstandard2.1/Microsoft.Data.SqlClient.dll",
    ];

    /// <summary>System.Data.SqlClient 4.8.6.</summary>
    private static readonly string[] SystemSqlClient =
    [
        "lib/MonoAndroid10/_._",
        "lib/net461/System.Data.SqlClient.dll",
        "lib/netcoreapp2.1/System.Data.SqlClient.dll",
        "lib/netstandard2.0/System.Data.SqlClient.dll",
        "ref/netcoreapp2.1/System.Data.SqlClient.dll",
        "ref/netstandard2.0/System.Data.SqlClient.dll",
        "runtimes/unix/lib/netcoreapp2.1/System.Data.SqlClient.dll",
        "runtimes/unix/lib/netstandard2.0/System.Data.SqlClient.dll",
        "runtimes/win/lib/netcoreapp2.1/System.Data.SqlClient.dll",
        "runtimes/win/lib/netstandard2.0/System.Data.SqlClient.dll",
        "runtimes/win/lib/uap10.0.16299/System.Data.SqlClient.dll",
    ];

    /// <summary>SQLitePCLRaw.lib.e_sqlite3 2.1.11, the native SQLite that Microsoft.Data.Sqlite 9 and 10 bring.</summary>
    private static readonly string[] Sqlite =
    [
        "runtimes/win-x86/native/e_sqlite3.dll",
        "runtimes/win-x64/native/e_sqlite3.dll",
        "runtimes/win-arm64/native/e_sqlite3.dll",
        "runtimes/win10-arm64/nativeassets/uap10.0/e_sqlite3.dll",
        "runtimes/osx-x64/native/libe_sqlite3.dylib",
        "runtimes/osx-arm64/native/libe_sqlite3.dylib",
        "runtimes/linux-x64/native/libe_sqlite3.so",
        "runtimes/linux-x86/native/libe_sqlite3.so",
        "runtimes/linux-arm/native/libe_sqlite3.so",
        "runtimes/linux-armel/native/libe_sqlite3.so",
        "runtimes/linux-arm64/native/libe_sqlite3.so",
        "runtimes/linux-musl-x64/native/libe_sqlite3.so",
        "runtimes/linux-musl-arm64/native/libe_sqlite3.so",
        "runtimes/browser-wasm/nativeassets/net9.0/e_sqlite3.a",
        "buildTransitive/net9.0/SQLitePCLRaw.lib.e_sqlite3.targets",
        "lib/net461/_._",
        "lib/netstandard2.0/_._",
    ];

    /// <summary>Microsoft.Data.Sqlite.Core 10.0.0: no RID-specific assets.</summary>
    private static readonly string[] SqliteCore =
    [
        "lib/net8.0/Microsoft.Data.Sqlite.dll",
        "lib/netstandard2.0/Microsoft.Data.Sqlite.dll",
    ];

    /// <summary>SkiaSharp.NativeAssets.Linux 3.119.0.</summary>
    private static readonly string[] SkiaLinux =
    [
        "lib/net8.0/_._",
        "runtimes/linux-arm/native/libSkiaSharp.so",
        "runtimes/linux-arm64/native/libSkiaSharp.so",
        "runtimes/linux-musl-arm64/native/libSkiaSharp.so",
        "runtimes/linux-x64/native/libSkiaSharp.so",
        "runtimes/linux-musl-x64/native/libSkiaSharp.so",
    ];

    [Theory]
    [InlineData("android-arm64")]
    [InlineData("linux-arm64")]
    [InlineData("linux-arm")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void Microsoft_Data_SqlClient_runs_with_its_unix_assembly_and_compiles_against_ref(string rid)
    {
        var assets = Select(SqlClient6, rid);

        Assert.Equal(["runtimes/unix/lib/net9.0/Microsoft.Data.SqlClient.dll"], assets.RuntimeAssemblies);
        Assert.Equal(["ref/net9.0/Microsoft.Data.SqlClient.dll"], assets.CompileAssemblies);
        Assert.Empty(assets.NativeLibraries);
    }

    [Fact]
    public void Microsoft_Data_SqlClient_5_uses_the_nearest_framework_before_the_nearest_rid()
    {
        Assert.Equal(["runtimes/unix/lib/net6.0/Microsoft.Data.SqlClient.dll"], Select(SqlClient5, "linux-arm64").RuntimeAssemblies);
        Assert.Equal(["runtimes/win/lib/net6.0/Microsoft.Data.SqlClient.dll"], Select(SqlClient5, "win-x64").RuntimeAssemblies);
    }

    [Fact]
    public void System_Data_SqlClient_runs_with_its_unix_assembly()
    {
        var assets = Select(SystemSqlClient, "android-arm64");

        Assert.Equal(["runtimes/unix/lib/netcoreapp2.1/System.Data.SqlClient.dll"], assets.RuntimeAssemblies);
        Assert.Equal(["ref/netcoreapp2.1/System.Data.SqlClient.dll"], assets.CompileAssemblies);
    }

    [Fact]
    public void Without_runtime_identifiers_the_lib_stub_is_used_as_before()
    {
        var assets = AssetSelector.Select(SqlClient6, Net10, Fallback);

        Assert.Equal(["lib/net9.0/Microsoft.Data.SqlClient.dll"], assets.RuntimeAssemblies);
        Assert.Empty(assets.NativeLibraries);
    }

    [Theory]
    [InlineData("linux-arm64", "runtimes/linux-arm64/native/libe_sqlite3.so")]
    [InlineData("android-arm64", "runtimes/linux-arm64/native/libe_sqlite3.so")]
    [InlineData("linux-arm", "runtimes/linux-arm/native/libe_sqlite3.so")]
    [InlineData("linux-x64", "runtimes/linux-x64/native/libe_sqlite3.so")]
    [InlineData("linux-musl-x64", "runtimes/linux-musl-x64/native/libe_sqlite3.so")]
    [InlineData("win-x64", "runtimes/win-x64/native/e_sqlite3.dll")]
    [InlineData("osx-arm64", "runtimes/osx-arm64/native/libe_sqlite3.dylib")]
    public void SQLite_native_library_comes_from_the_nearest_rid(string rid, string expected)
    {
        var assets = Select(Sqlite, rid);

        Assert.Equal([expected], assets.NativeLibraries);
        Assert.Empty(assets.RuntimeAssemblies);
        Assert.True(assets.IsCompatible);
    }

    [Fact]
    public void A_rid_the_package_has_no_native_folder_for_gets_none()
    {
        Assert.Empty(Select(Sqlite, "linux-riscv64").NativeLibraries);
        Assert.Empty(Select(SkiaLinux, "win-x64").NativeLibraries);
    }

    [Fact]
    public void Packages_without_runtime_folders_are_unchanged()
    {
        var assets = Select(SqliteCore, "android-arm64");

        Assert.Equal(["lib/net8.0/Microsoft.Data.Sqlite.dll"], assets.RuntimeAssemblies);
        Assert.Empty(assets.NativeLibraries);
    }

    [Fact]
    public void SkiaSharp_native_assets_follow_the_device()
    {
        Assert.Equal(["runtimes/linux-arm64/native/libSkiaSharp.so"], Select(SkiaLinux, "android-arm64").NativeLibraries);
        Assert.Equal(["runtimes/linux-musl-arm64/native/libSkiaSharp.so"], Select(SkiaLinux, "linux-musl-arm64").NativeLibraries);
    }

    [Fact]
    public void A_placeholder_in_the_nearest_rid_folder_means_nothing_for_that_rid()
    {
        var assets = Select(
            [
                "lib/net8.0/Lib.dll",
                "runtimes/linux/lib/net8.0/_._",
                "runtimes/unix/lib/net8.0/Lib.dll",
                "runtimes/linux-arm64/native/_._",
                "runtimes/unix/native/libLib.so",
            ],
            "linux-arm64");

        Assert.Empty(assets.RuntimeAssemblies);
        Assert.Empty(assets.NativeLibraries);
    }

    [Fact]
    public void The_device_graph_walks_android_through_linux_and_unix_to_any()
    {
        Assert.Equal(
            ["android-arm64", "android", "linux-bionic-arm64", "linux-bionic", "linux-arm64", "linux", "unix-arm64", "unix", "any", "base"],
            RuntimeGraph.Fallback.Expand("android-arm64"));
        Assert.Equal(["linux-x64", "linux", "unix-x64", "unix", "any", "base"], RuntimeGraph.Fallback.Expand("linux-x64"));
        Assert.Equal(["win-arm64", "win", "any", "base"], RuntimeGraph.Fallback.Expand("win-arm64"));
        Assert.Equal(["made-up", "any", "base"], RuntimeGraph.Fallback.Expand("made-up"));
    }

    [Fact]
    public void A_packages_runtime_json_extends_the_graph()
    {
        var graph = RuntimeGraph.Fallback.Merge("""
            { "runtimes": { "tablet-arm64": { "#import": [ "linux-arm64" ] }, "linux": { "#import": [ "unix", "posix" ] } } }
            """);

        Assert.Equal(["tablet-arm64", "linux-arm64", "linux", "unix-arm64", "unix", "posix", "any", "base"], graph.Expand("tablet-arm64"));
        Assert.Same(RuntimeGraph.Fallback, RuntimeGraph.Fallback.Merge("not json"));
    }

    [Fact]
    public void The_device_rid_is_a_portable_rid()
    {
        Assert.Contains(RuntimeGraph.DeviceRuntimeIdentifier, RuntimeGraph.Fallback.Expand(RuntimeGraph.DeviceRuntimeIdentifier));
        Assert.Contains("any", RuntimeGraph.Fallback.Expand(RuntimeGraph.DeviceRuntimeIdentifier));
        Assert.Matches("^(android|linux|linux-musl|win|osx)-(arm64|arm|x64|x86)$", RuntimeGraph.DeviceRuntimeIdentifier);
    }

    private static PackageAssets Select(IEnumerable<string> files, string rid) =>
        AssetSelector.Select(files, Net10, Fallback, compilerVersion: null, RuntimeGraph.Fallback.Expand(rid));
}
