using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
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

    /// <summary>
    /// eShop: the MAUI app is not built and not started, and the tests of the AppHost are skipped with one line because
    /// they reference it, rather than failing with its types missing. The Run menu lists programs in the order F5 picks
    /// them, a smoke test under tests last.
    /// </summary>
    [AvaloniaFact]
    public async Task Projects_that_need_a_workload_or_reference_a_skipped_project_are_skipped_with_one_line_each()
    {
        using var temp = new TempDirectory();
        temp.Write("Shop/src/AppHost/AppHost.csproj", """
            <Project Sdk="Aspire.AppHost.Sdk/13.0.0">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        temp.Write("Shop/src/AppHost/AppHost.cs", "var builder = DistributedApplication.CreateBuilder(args);\n");
        temp.Write("Shop/src/ClientApp/ClientApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
            </Project>
            """);
        temp.Write("Shop/src/ClientApp/Platforms/Android/MainActivity.cs", "public class MainActivity : MauiAppCompatActivity { }\n");
        temp.Write("Shop/src/Tool/Tool.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        temp.Write("Shop/src/Tool/Program.cs", "System.Console.WriteLine(\"tool\");\n");
        temp.Write("Shop/tests/Smoke/Smoke.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        temp.Write("Shop/tests/Smoke/Program.cs", "System.Console.WriteLine(\"smoke\");\n");
        temp.Write("Shop/tests/AppHost.Tests/AppHost.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject></PropertyGroup>
              <ItemGroup><ProjectReference Include="..\..\src\AppHost\AppHost.csproj" /></ItemGroup>
            </Project>
            """);
        temp.Write("Shop/tests/AppHost.Tests/AppHostTests.cs", "public sealed class AppHostTests { }\n");
        var solution = temp.Write("Shop/Shop.slnx", """
            <Solution>
              <Project Path="src/AppHost/AppHost.csproj" />
              <Project Path="tests/Smoke/Smoke.csproj" />
              <Project Path="src/ClientApp/ClientApp.csproj" />
              <Project Path="src/Tool/Tool.csproj" />
              <Project Path="tests/AppHost.Tests/AppHost.Tests.csproj" />
            </Solution>
            """);

        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.LoadAsync(solution);
        var workspace = fixture.Workspace;

        Assert.Equal("Tool", workspace.StartupProject?.Name);
        Assert.Equal(new[] { "Tool", "Smoke" }, workspace.StartupProjects.Select(option => option.Project.Name));
        Assert.False(Assert.Single(workspace.Compatibility, row => row.Project == "ClientApp").IsReady);

        await workspace.BuildCommand.ExecuteAsync(null);

        var output = workspace.Output.Text;
        Assert.Contains("Skipped: Project: ClientApp: it is a .NET MAUI project, which only .NET's MAUI workload builds, on a computer", output, StringComparison.Ordinal);
        Assert.Contains("Skipped: Project: AppHost.Tests: it references AppHost, which is not built on this device", output, StringComparison.Ordinal);
        Assert.DoesNotContain(workspace.ErrorList.Items, item => item.IsError);
    }

    /// <summary>StronglyTypedId: only libraries and tests, so there is no startup project, and the status bar says why.</summary>
    [AvaloniaFact]
    public async Task A_solution_of_libraries_and_tests_says_why_nothing_starts()
    {
        using var temp = new TempDirectory();
        temp.Write("Ids/src/Ids/Ids.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        temp.Write("Ids/src/Ids/Id.cs", "namespace Ids; public readonly record struct Id(int Value);\n");
        temp.Write("Ids/test/Ids.Tests/Ids.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject></PropertyGroup>
              <ItemGroup><ProjectReference Include="..\..\src\Ids\Ids.csproj" /></ItemGroup>
            </Project>
            """);
        var solution = temp.Write("Ids/Ids.slnx", """
            <Solution>
              <Project Path="src/Ids/Ids.csproj" />
              <Project Path="test/Ids.Tests/Ids.Tests.csproj" />
            </Solution>
            """);

        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.LoadAsync(solution);

        Assert.Null(fixture.Workspace.StartupProject);
        Assert.Equal("This solution has only libraries and tests: run the tests in Test Explorer.", fixture.Workspace.StatusText);
    }

    /// <summary>
    /// eShop kept starting WebhookClient after updates: the session saved TabStudio's automatic pick of an older version
    /// as if the person had chosen it. Only a chosen startup project is restored now, and a reload makes the pick again.
    /// </summary>
    [AvaloniaFact]
    public async Task An_automatic_startup_project_saved_by_an_older_version_is_picked_again_and_a_chosen_one_is_kept()
    {
        using var temp = new TempDirectory();
        temp.Write("Shop/src/Hooks/Hooks.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        temp.Write("Shop/src/Hooks/Program.cs", "System.Console.WriteLine(\"hooks\");\n");
        temp.Write("Shop/src/WebApp/WebApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        temp.Write("Shop/src/WebApp/Program.cs", "var app = WebApplication.Create(args);\napp.MapGet(\"/\", () => \"shop\");\napp.Run();\n");
        var solution = temp.Write("Shop/Shop.slnx", """
            <Solution>
              <Project Path="src/Hooks/Hooks.csproj" />
              <Project Path="src/WebApp/WebApp.csproj" />
            </Solution>
            """);
        var hooks = temp.Resolve("Shop/src/Hooks/Hooks.csproj");

        using (var fixture = new WorkspaceFixture())
        {
            await fixture.Workspace.LoadAsync(solution, new SessionState(solution, [], null, hooks));
            Assert.Equal("WebApp", fixture.Workspace.StartupProject?.Name);
        }

        using (var fixture = new WorkspaceFixture())
        {
            await fixture.Workspace.LoadAsync(solution, new SessionState(solution, [], null, hooks) { IsStartupProjectChosen = true });
            Assert.Equal("Hooks", fixture.Workspace.StartupProject?.Name);
            await fixture.Workspace.ReloadWorkspaceAsync();
            Assert.Equal("Hooks", fixture.Workspace.StartupProject?.Name);
        }
    }
}
