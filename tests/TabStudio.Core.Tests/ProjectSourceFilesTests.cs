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
    public void A_compile_item_s_properties_are_expanded_as_msbuild_expands_them()
    {
        // Spectre.Console keeps its generators' output in Generated/ and removes it from compilation by a property.
        using var temp = new TempDirectory();
        temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
                <SharedFolder>$(MSBuildProjectDirectory)/../Shared</SharedFolder>
              </PropertyGroup>
              <ItemGroup>
                <Compile Remove="$(CompilerGeneratedFilesOutputPath)/**/*.cs" />
                <Compile Include="$(SharedFolder)/Helpers.cs" />
                <Compile Remove="$(NotSetAnywhere)/**/*.cs" />
              </ItemGroup>
            </Project>
            """);
        temp.Write("App/Program.cs");
        temp.Write("App/Generated/Gen/Color.g.cs");
        temp.Write("Shared/Helpers.cs");
        var project = ProjectParser.Load(temp.Resolve("App/App.csproj"));

        var files = ProjectSourceFiles.Enumerate(project);

        // A property nothing sets removes nothing, rather than every file.
        Assert.Equal(new[] { temp.Resolve("App/Program.cs"), temp.Resolve("Shared/Helpers.cs") }, files);
    }

    [Fact]
    public void An_explicit_include_into_another_project_s_folder_gets_its_nested_files_less_its_Exclude()
    {
        // Dock.Model.ReactiveUI.v11 compiles the sources of Dock.Model.ReactiveUI, a project of the same workspace.
        using var temp = new TempDirectory();
        temp.Write("src/Model/Model.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        temp.Write("src/Model/Factory.cs");
        temp.Write("src/Model/Controls/Tool.cs");
        temp.Write("src/Model/Core/Deep/DockBase.cs");
        temp.Write("src/Model/obj/Debug/Model.AssemblyInfo.cs");
        temp.Write("src/Model/bin/Generated.cs");
        temp.Write("src/Model/Legacy/Old.cs");
        temp.Write("src/Model.v11/Model.v11.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Compile Include="..\Model\**\*.cs" Exclude="..\Model\Legacy\**">
                  <Link>%(RecursiveDir)%(Filename)%(Extension)</Link>
                </Compile>
              </ItemGroup>
            </Project>
            """);
        temp.Write("src/Model.v11/Extra.cs");
        var project = ProjectParser.Load(temp.Resolve("src/Model.v11/Model.v11.csproj"));

        var files = ProjectSourceFiles.Enumerate(project, [temp.Resolve("src/Model"), temp.Resolve("src/Model.v11")]);

        Assert.Equal(
            new[]
            {
                temp.Resolve("src/Model.v11/Extra.cs"),
                temp.Resolve("src/Model/Controls/Tool.cs"),
                temp.Resolve("src/Model/Core/Deep/DockBase.cs"),
                temp.Resolve("src/Model/Factory.cs"),
            },
            files);
    }

    [Fact]
    public void The_default_items_still_leave_out_a_nested_project_s_folder()
    {
        using var temp = new TempDirectory();
        temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><ItemGroup><Compile Include="..\Shared\*.cs" Exclude="..\Shared\Skip.cs" /></ItemGroup></Project>
            """);
        temp.Write("App/Program.cs");
        temp.Write("App/Nested/Nested.csproj");
        temp.Write("App/Nested/Inner.cs");
        temp.Write("Shared/Helpers.cs");
        temp.Write("Shared/Skip.cs");
        var project = ProjectParser.Load(temp.Resolve("App/App.csproj"));

        var files = ProjectSourceFiles.Enumerate(project, [temp.Resolve("App/Nested")]);

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
