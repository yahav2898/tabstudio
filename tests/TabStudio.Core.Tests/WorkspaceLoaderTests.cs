using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class WorkspaceLoaderTests
{
    private const string ConsoleProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="..\Lib\Lib.csproj" />
            <PackageReference Include="Spectre.Console" Version="0.50.0" />
          </ItemGroup>
        </Project>
        """;

    private const string LibraryProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
        </Project>
        """;

    [Fact]
    public void Opening_a_folder_opens_the_solution_inside_it()
    {
        using var temp = CreateShop();

        var workspace = WorkspaceLoader.Load(temp.Path);

        Assert.Equal("Shop", workspace.Name);
        Assert.NotNull(workspace.Solution);
        Assert.Equal(new[] { "App", "Lib" }, workspace.Projects.Select(p => p.Name));
        Assert.Empty(workspace.LoadErrors);

        var tree = workspace.Tree;
        Assert.Equal(WorkspaceNodeKind.Solution, tree.Kind);
        var src = Assert.Single(tree.Children);
        Assert.Equal(WorkspaceNodeKind.SolutionFolder, src.Kind);
        Assert.Equal("src", src.Name);

        var app = src.Children[0];
        Assert.Equal(WorkspaceNodeKind.Project, app.Kind);
        Assert.Equal(ProjectKind.ConsoleApp, app.Project?.Kind);
        Assert.Equal(new[] { "Dependencies", "Models", "Program.cs" }, app.Children.Select(c => c.Name));

        var dependencies = app.Children[0];
        Assert.Equal(new[] { "Lib", "Spectre.Console (0.50.0)" }, dependencies.Children.Select(c => c.Name));
    }

    [Fact]
    public void Build_output_and_tool_folders_are_left_out_of_the_tree()
    {
        using var temp = CreateShop();
        temp.Write("src/App/bin/Debug/App.dll");
        temp.Write("src/App/obj/project.assets.json");
        temp.Write("src/App/.vs/state");

        var workspace = WorkspaceLoader.Load(temp.Path);

        var names = workspace.Tree.DescendantsAndSelf().Select(n => n.Name).ToList();
        Assert.DoesNotContain("bin", names);
        Assert.DoesNotContain("obj", names);
        Assert.DoesNotContain(".vs", names);
    }

    [Fact]
    public void Opening_a_folder_without_a_solution_finds_its_projects()
    {
        using var temp = new TempDirectory();
        temp.Write("tools/Cli/Cli.csproj", ConsoleProject.Replace("""<ProjectReference Include="..\Lib\Lib.csproj" />""", string.Empty, StringComparison.Ordinal));
        temp.Write("tools/Cli/Program.cs", "Console.WriteLine();");

        var workspace = WorkspaceLoader.Load(temp.Path);

        Assert.Null(workspace.Solution);
        Assert.Equal(WorkspaceNodeKind.Root, workspace.Tree.Kind);
        Assert.Equal("Cli", Assert.Single(workspace.Projects).Name);
    }

    [Fact]
    public void A_plain_folder_opens_as_a_file_tree()
    {
        using var temp = new TempDirectory();
        temp.Write("notes/todo.md", "- ship it");
        temp.Write("README.md", "# Hello");

        var workspace = WorkspaceLoader.Load(temp.Path);

        Assert.Empty(workspace.Projects);
        Assert.Equal(new[] { "notes", "README.md" }, workspace.Tree.Children.Select(c => c.Name));
    }

    [Fact]
    public void A_broken_project_is_reported_without_stopping_the_rest()
    {
        using var temp = CreateShop();
        temp.Write("src/Lib/Lib.csproj", "<Project");

        var workspace = WorkspaceLoader.Load(temp.Path);

        Assert.Equal(2, workspace.Projects.Count);
        Assert.Equal(ProjectKind.Unknown, workspace.FindProject("Lib")?.Kind);
        Assert.Contains(workspace.LoadErrors, e => e.Contains("Lib.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public void Finds_the_project_that_owns_a_file()
    {
        using var temp = CreateShop();
        var workspace = WorkspaceLoader.Load(temp.Path);

        var owner = workspace.FindProjectForFile(temp.Resolve("src/App/Models/Order.cs"));

        Assert.Equal("App", owner?.Name);
        Assert.Null(workspace.FindProjectForFile(temp.Resolve("README.md")));
    }

    [Fact]
    public void The_default_startup_project_is_the_console_app()
    {
        using var temp = CreateShop();

        var workspace = WorkspaceLoader.Load(Path.Combine(temp.Path, "Shop.slnx"));

        Assert.Equal("App", workspace.DefaultStartupProject?.Name);
    }

    [Fact]
    public void Opening_a_missing_path_throws()
    {
        Assert.Throws<ProjectLoadException>(() => WorkspaceLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    private static TempDirectory CreateShop()
    {
        var temp = new TempDirectory();
        temp.Write("Shop.slnx", """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/App/App.csproj" />
                <Project Path="src/Lib/Lib.csproj" />
              </Folder>
            </Solution>
            """);
        temp.Write("src/App/App.csproj", ConsoleProject);
        temp.Write("src/App/Program.cs", "Console.WriteLine(\"Hello\");");
        temp.Write("src/App/Models/Order.cs", "public record Order(int Id);");
        temp.Write("src/Lib/Lib.csproj", LibraryProject);
        temp.Write("src/Lib/Greeter.cs", "public static class Greeter { }");
        temp.Write("README.md", "# Shop");
        return temp;
    }
}
