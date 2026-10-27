using TabStudio.Compilation.Running;

namespace TabStudio.Compilation.Tests;

public sealed class PackageNativeLibrariesTests
{
    private const string Packages = "/data/user/0/app/files/TabStudio/Packages";

    private static readonly string[] Files =
    [
        $"{Packages}/microsoft.data.sqlite.core/10.0.0/lib/net8.0/Microsoft.Data.Sqlite.dll",
        $"{Packages}/microsoft.data.sqlclient/6.0.2/runtimes/unix/lib/net9.0/Microsoft.Data.SqlClient.dll",
        $"{Packages}/sqlitepclraw.lib.e_sqlite3/2.1.11/runtimes/linux-arm64/native/libe_sqlite3.so",
        $"{Packages}/skiasharp.nativeassets.linux/3.119.0/runtimes/linux-arm64/native/libSkiaSharp.so",
        @"C:\Users\dev\.nuget\packages\sqlitepclraw.lib.e_sqlite3\2.1.11\runtimes\win-x64\native\e_sqlite3.dll",
    ];

    [Fact]
    public void Native_libraries_are_the_files_in_a_runtimes_native_folder()
    {
        Assert.Equal(
            [Files[0], Files[1]],
            PackageNativeLibraries.Assemblies(Files));
        Assert.True(PackageNativeLibraries.IsNativeLibrary(Files[2]));
        Assert.True(PackageNativeLibraries.IsNativeLibrary(Files[4]));
        Assert.False(PackageNativeLibraries.IsNativeLibrary(Files[1]));
        Assert.False(PackageNativeLibraries.IsNativeLibrary("/home/me/native/runtimes/libfoo.so"));
    }

    [Theory]
    [InlineData("e_sqlite3", 2)]
    [InlineData("libe_sqlite3", 2)]
    [InlineData("libe_sqlite3.so", 2)]
    [InlineData("e_sqlite3.so", 2)]
    [InlineData("libSkiaSharp", 3)]
    [InlineData("SkiaSharp", 3)]
    [InlineData("e_sqlite3.dll", 4)]
    public void Library_names_are_probed_with_the_lib_prefix_and_platform_suffixes(string name, int expected)
    {
        var libraries = new PackageNativeLibraries(Files.Take(4));
        var windows = new PackageNativeLibraries([Files[4]]);

        var found = expected == 4 ? windows.TryFind(name, out var path) : libraries.TryFind(name, out path);

        Assert.True(found);
        Assert.Equal(Files[expected], path);
    }

    [Fact]
    public void Names_no_package_brings_are_left_to_the_runtime()
    {
        var libraries = new PackageNativeLibraries(Files);

        Assert.False(libraries.TryFind("libc", out _));
        Assert.False(libraries.TryFind("/usr/lib/libe_sqlite3.so", out _));
        Assert.Equal(0, libraries.Load("libgit2"));
        Assert.True(new PackageNativeLibraries([Files[0]]).IsEmpty);
    }
}
