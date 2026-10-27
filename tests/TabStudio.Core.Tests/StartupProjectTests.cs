using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>
/// Which project F5 starts when nobody has picked one, and how build scripts are told apart from apps: in
/// gothinkster/aspnetcore-realworld-example-app, TabStudio 0.19 picked the Bullseye build script over the web API.
/// </summary>
public sealed class StartupProjectTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    /// <summary>Conduit's layout: the web API in src, a Bullseye build script in build, tests in tests.</summary>
    [Fact]
    public void A_web_app_is_the_startup_project_and_a_Bullseye_build_script_is_never_chosen()
    {
        _temp.Write("build/build.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Bullseye" Version="6.3.0" />
                <PackageReference Include="SimpleExec" Version="13.1.0" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("src/Conduit/Conduit.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("tests/Conduit.IntegrationTests/Conduit.IntegrationTests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="xunit" Version="2.9.3" /></ItemGroup>
            </Project>
            """);
        var solution = _temp.Write("Conduit.slnx", """
            <Solution>
              <Project Path="build/build.csproj" />
              <Project Path="src/Conduit/Conduit.csproj" />
              <Project Path="tests/Conduit.IntegrationTests/Conduit.IntegrationTests.csproj" />
            </Solution>
            """);

        var workspace = WorkspaceLoader.Load(solution);

        var build = workspace.FindProject("build")!;
        Assert.Equal(ProjectKind.BuildScript, build.Kind);
        Assert.True(ProjectClassifier.IsBuildScript(build));
        Assert.Null(ProjectClassifier.StartupRank(build));
        Assert.Equal("Conduit", workspace.DefaultStartupProject?.Name);
    }

    [Fact]
    public void The_Compatibility_Doctor_says_a_build_script_runs_on_a_computer_not_in_process()
    {
        var path = _temp.Write("build/build.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Bullseye" Version="6.3.0" /></ItemGroup>
            </Project>
            """);

        var report = CompatibilityDoctor.Diagnose(ProjectParser.Load(path));

        Assert.Equal(ProjectKind.BuildScript, report.Kind);
        Assert.NotEqual(CompatibilityStatus.Ready, report.Status);
        Assert.False(report.CanRunToday);
        Assert.NotEqual("In-process", report.RunsVia);
        Assert.NotEqual(RunHost.InProcess, report.Host);
        Assert.Contains("Bullseye", report.Explanation, StringComparison.Ordinal);
        Assert.Contains("computer", report.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Nuke.Common")]
    [InlineData("Cake.Frosting")]
    [InlineData("SimpleExec")]
    [InlineData("Bullseye")]
    public void Programs_on_a_build_script_library_are_build_scripts(string package)
    {
        var path = _temp.Write("tools/Pipeline/Pipeline.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="{package}" Version="1.0.0" /></ItemGroup>
            </Project>
            """);

        Assert.Equal(ProjectKind.BuildScript, ProjectParser.Load(path).Kind);
    }

    [Theory]
    [InlineData("build/_build.csproj")]
    [InlineData("build/Build.csproj")]
    [InlineData("build/build.csproj")]
    public void A_program_named_build_in_a_build_folder_is_a_build_script(string file)
    {
        var path = _temp.Write(file, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        Assert.Equal(ProjectKind.BuildScript, ProjectParser.Load(path).Kind);
    }

    [Fact]
    public void A_console_app_named_build_elsewhere_and_a_library_using_Cake_stay_what_they_are()
    {
        var console = _temp.Write("src/Tools/build.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var addin = _temp.Write("src/Cake.MyAddin/Cake.MyAddin.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Cake.Core" Version="5.0.0" /></ItemGroup>
            </Project>
            """);

        Assert.Equal(ProjectKind.ConsoleApp, ProjectParser.Load(console).Kind);
        Assert.Equal(ProjectKind.ClassLibrary, ProjectParser.Load(addin).Kind);
    }

    /// <summary>Web apps come first, then apps with a UI, then console apps, whatever the solution's order.</summary>
    [Fact]
    public void Web_apps_come_before_apps_with_a_UI_which_come_before_console_apps()
    {
        _temp.Write("Tool/Tool.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("Desktop/Desktop.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Avalonia" Version="11.3.0" /></ItemGroup>
            </Project>
            """);
        _temp.Write("Web/Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var all = _temp.Write("All.slnx", """
            <Solution>
              <Project Path="Tool/Tool.csproj" />
              <Project Path="Desktop/Desktop.csproj" />
              <Project Path="Web/Web.csproj" />
            </Solution>
            """);
        var noWeb = _temp.Write("NoWeb.slnx", """
            <Solution>
              <Project Path="Tool/Tool.csproj" />
              <Project Path="Desktop/Desktop.csproj" />
            </Solution>
            """);
        var consoleOnly = _temp.Write("ConsoleOnly.slnx", """
            <Solution>
              <Project Path="Tool/Tool.csproj" />
            </Solution>
            """);

        Assert.Equal("Web", WorkspaceLoader.Load(all).DefaultStartupProject?.Name);
        Assert.Equal("Desktop", WorkspaceLoader.Load(noWeb).DefaultStartupProject?.Name);
        Assert.Equal("Tool", WorkspaceLoader.Load(consoleOnly).DefaultStartupProject?.Name);
    }

    public void Dispose() => _temp.Dispose();
}
