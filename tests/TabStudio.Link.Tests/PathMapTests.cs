using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Link.Language;
using TabStudio.Testing;

namespace TabStudio.Link.Tests;

public sealed class PathMapTests
{
    private const string AndroidRoot = "/data/user/0/com.tabstudio.ide/files/TabStudio/Projects/HelloTablet";

    [Fact]
    public void Android_paths_map_into_the_mirror_and_back()
    {
        using var mirror = new TempDirectory();
        var map = new LinkPathMap(AndroidRoot, '/', mirror.Path);

        var host = map.ToHost(AndroidRoot + "/src/HelloTablet/Program.cs");

        Assert.Equal(Path.Combine(mirror.Path, "src", "HelloTablet", "Program.cs"), host);
        Assert.Equal(AndroidRoot + "/src/HelloTablet/Program.cs", map.ToClient(host!));
        Assert.Equal(mirror.Path, map.ToHost(AndroidRoot));
        Assert.Equal(AndroidRoot, map.ToClient(mirror.Path));
    }

    [Fact]
    public void Windows_paths_map_into_the_mirror_and_back()
    {
        using var mirror = new TempDirectory();
        var map = new LinkPathMap(@"C:\Users\yahav\source\HelloTablet", '\\', mirror.Path);

        var host = map.ToHost(@"C:\Users\yahav\source\HelloTablet\App\Program.cs");

        Assert.Equal(Path.Combine(mirror.Path, "App", "Program.cs"), host);
        Assert.Equal(@"C:\Users\yahav\source\HelloTablet\App\Program.cs", map.ToClient(host!));
    }

    [Theory]
    [InlineData(AndroidRoot + "/../../etc/passwd")]
    [InlineData(AndroidRoot + "/src/./Program.cs")]
    [InlineData(AndroidRoot + "//Program.cs")]
    [InlineData("/data/user/0/somewhere-else/Program.cs")]
    [InlineData(AndroidRoot + "Sibling/Program.cs")]
    public void Paths_outside_the_workspace_or_that_would_leave_the_mirror_are_refused(string path)
    {
        using var mirror = new TempDirectory();
        var map = new LinkPathMap(AndroidRoot, '/', mirror.Path);

        Assert.Null(map.ToHost(path));
    }

    [Theory]
    [InlineData("../outside.cs")]
    [InlineData("a/../../outside.cs")]
    [InlineData("")]
    [InlineData("a//b.cs")]
    public void Relative_paths_that_would_leave_the_mirror_are_refused(string path)
    {
        using var mirror = new TempDirectory();

        Assert.Null(new LinkPathMap(AndroidRoot, '/', mirror.Path).RelativeToHost(path));
    }

    [Fact]
    public void Paths_outside_the_mirror_come_back_as_they_are()
    {
        using var mirror = new TempDirectory();
        var map = new LinkPathMap(AndroidRoot, '/', mirror.Path);

        Assert.Equal("/usr/share/dotnet/System.Runtime.dll", map.ToClient("/usr/share/dotnet/System.Runtime.dll"));
    }

    [Fact]
    public void Results_carry_the_tablets_paths()
    {
        using var mirror = new TempDirectory();
        var map = new LinkPathMap(AndroidRoot, '/', mirror.Path);
        var hostFile = map.ToHost(AndroidRoot + "/Program.cs")!;
        var diagnostic = new BuildDiagnostic("CS2001", DiagnosticSeverityLevel.Error, $"Source file '{hostFile}' could not be found.", new DiagnosticLocation(hostFile, 0, 1, 1, 1), "HelloTablet");
        var reference = new ReferenceItem(hostFile, 0, 4, 1, 1, "var x = 1;", IsDefinition: true);

        var mapped = map.ToClient(diagnostic);

        Assert.Equal(AndroidRoot + "/Program.cs", mapped.Location!.FilePath);
        Assert.Equal($"Source file '{AndroidRoot}/Program.cs' could not be found.", mapped.Message);
        Assert.Equal("Program.cs", mapped.FileName);
        Assert.Equal(AndroidRoot + "/Program.cs", map.ToClient(reference).FilePath);
    }

    [Fact]
    public void The_compiler_maps_the_mirror_to_the_tablet()
    {
        using var mirror = new TempDirectory();
        var map = new LinkPathMap(AndroidRoot, '/', mirror.Path);

        var (from, to) = map.CompilerPathMap;

        Assert.Equal(mirror.Path + Path.DirectorySeparatorChar, from);
        Assert.Equal(AndroidRoot + "/", to);
    }
}
