using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class ProjectSourceFilesTests
{
    [Theory]
    [InlineData("**/*.cs", "Models/Order.cs", true)]
    [InlineData("**/*.cs", "Program.cs", true)]
    [InlineData("*.cs", "Models/Order.cs", false)]
    [InlineData("Generated/**", "Generated/Deep/File.g.cs", true)]
    [InlineData("Generated\\*.g.cs", "Generated/File.g.cs", true)]
    [InlineData("File?.cs", "File1.cs", true)]
    [InlineData("File?.cs", "File10.cs", false)]
    public void Glob_patterns_match_like_msbuild(string pattern, string file, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "proj");
        var glob = new GlobPattern(root, pattern);

        Assert.Equal(expected, glob.IsMatch(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Glob_base_directory_stops_before_the_first_wildcard()
    {
        var root = Path.Combine(Path.GetTempPath(), "proj");

        Assert.Equal(Path.Combine(root, "Gen"), new GlobPattern(root, "Gen/**/*.cs").BaseDirectory);
        Assert.Equal(root, new GlobPattern(root, "Gen*/x.cs").BaseDirectory);
    }

    [Fact]
    public void Includes_every_source_file_except_build_output_and_other_projects()
    {
        using var temp = new TempDirectory();
        temp.Write("App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        temp.Write("App/Program.cs");
        temp.Write("App/Models/Order.cs");
        temp.Write("App/obj/Debug/App.AssemblyInfo.cs");
        temp.Write("App/Nested/Nested.csproj");
        temp.Write("App/Nested/Inner.cs");
        temp.Write("App/notes.txt");
        var project = ProjectParser.Load(temp.Resolve("App/App.csproj"));

        var files = ProjectSourceFiles.Enumerate(project, [temp.Resolve("App/Nested")]);

        Assert.Equal(new[] { temp.Resolve("App/Models/Order.cs"), temp.Resolve("App/Program.cs") }, files);
    }

    [Fact]
    public void Honors_compile_include_and_remove_items()
    {
        using var temp = new TempDirectory();
        temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Compile Remove="Templates/**" />
                <Compile Include="..\Shared\Helpers.cs" />
              </ItemGroup>
            </Project>
            """);
        temp.Write("App/Program.cs");
        temp.Write("App/Templates/Template.cs");
        temp.Write("Shared/Helpers.cs");
        var project = ProjectParser.Load(temp.Resolve("App/App.csproj"));

        var files = ProjectSourceFiles.Enumerate(project);

        Assert.Equal(new[] { temp.Resolve("App/Program.cs"), temp.Resolve("Shared/Helpers.cs") }, files);
    }

    [Fact]
    public void Disabling_default_items_keeps_only_explicit_includes()
    {
        using var temp = new TempDirectory();
        temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
              <ItemGroup><Compile Include="Program.cs" /></ItemGroup>
            </Project>
            """);
        temp.Write("App/Program.cs");
        temp.Write("App/Unused.cs");
        var project = ProjectParser.Load(temp.Resolve("App/App.csproj"));

        Assert.Equal(new[] { temp.Resolve("App/Program.cs") }, ProjectSourceFiles.Enumerate(project));
    }
}
