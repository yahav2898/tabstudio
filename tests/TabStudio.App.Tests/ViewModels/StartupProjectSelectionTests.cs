using Avalonia.Headless.XUnit;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// The startup project a repository opens with, as in the IDE: the web app first, then a GUI app, then a console app,
/// and never the repository's build script. gothinkster's Conduit opened with its Bullseye <c>build</c> project as the
/// startup project, so F5 ran the build script.
/// </summary>
public sealed class StartupProjectSelectionTests
{
    [AvaloniaFact]
    public async Task A_repository_with_a_build_script_starts_its_web_app_and_the_Doctor_names_the_script()
    {
        using var temp = new TempDirectory();
        temp.Write("Conduit/build/build.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        temp.Write("Conduit/build/Program.cs", "System.Console.WriteLine(\"dotnet build, dotnet test, dotnet publish\");\n");
        temp.Write("Conduit/src/Tools/Tools.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        temp.Write("Conduit/src/Tools/Program.cs", "System.Console.WriteLine(\"tools\");\n");
        temp.Write("Conduit/src/Conduit/Conduit.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        temp.Write("Conduit/src/Conduit/Program.cs", "var app = WebApplication.Create(args);\napp.MapGet(\"/api/articles\", () => new { articlesCount = 0 });\napp.Run();\n");
        // The build script comes first in the solution, as in Conduit.slnx.
        var solution = temp.Write("Conduit/Conduit.slnx", """
            <Solution>
              <Project Path="build/build.csproj" />
              <Project Path="src/Tools/Tools.csproj" />
              <Project Path="src/Conduit/Conduit.csproj" />
            </Solution>
            """);

        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.LoadAsync(solution);
        var workspace = fixture.Workspace;

        Assert.Equal("Conduit", workspace.StartupProject?.Name);
        Assert.DoesNotContain(workspace.StartupProjects, option => option.Project.Name == "build");
        Assert.Contains(workspace.StartupProjects, option => option.Project.Name == "Tools");
        var script = Assert.Single(workspace.Compatibility, row => row.Project == "build");
        Assert.Equal("Build script", script.Type);
        Assert.False(script.IsReady);
    }
}
