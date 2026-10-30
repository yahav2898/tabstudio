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

    /// <summary>
    /// Web apps with a UI come first, then apps with a UI, then web APIs, then console apps, whatever the solution's
    /// order.
    /// </summary>
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
        _temp.Write("Web/Pages/Index.cshtml", "@page\n<h1>Shop</h1>");
        _temp.Write("Api/Api.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var all = _temp.Write("All.slnx", """
            <Solution>
              <Project Path="Api/Api.csproj" />
              <Project Path="Tool/Tool.csproj" />
              <Project Path="Desktop/Desktop.csproj" />
              <Project Path="Web/Web.csproj" />
            </Solution>
            """);
        var noWebUi = _temp.Write("NoWebUi.slnx", """
            <Solution>
              <Project Path="Api/Api.csproj" />
              <Project Path="Tool/Tool.csproj" />
              <Project Path="Desktop/Desktop.csproj" />
            </Solution>
            """);
        var api = _temp.Write("Api.slnx", """
            <Solution>
              <Project Path="Tool/Tool.csproj" />
              <Project Path="Api/Api.csproj" />
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
        Assert.Equal("Desktop", WorkspaceLoader.Load(noWebUi).DefaultStartupProject?.Name);
        Assert.Equal("Api", WorkspaceLoader.Load(api).DefaultStartupProject?.Name);
        Assert.Equal("Desktop", WorkspaceLoader.Load(noWeb).DefaultStartupProject?.Name);
        Assert.Equal("Tool", WorkspaceLoader.Load(consoleOnly).DefaultStartupProject?.Name);
    }

    /// <summary>
    /// dotnet/eShop: the Blazor WebApp is the startup project, not Basket.API, the first of the APIs; the AppHost, the
    /// MAUI apps and the tests are never chosen.
    /// </summary>
    [Fact]
    public void A_web_app_with_a_UI_comes_before_the_APIs_and_the_AppHost_MAUI_apps_and_tests_are_never_chosen()
    {
        _temp.Write("src/eShop.AppHost/eShop.AppHost.csproj", """
            <Project Sdk="Aspire.AppHost.Sdk/13.0.0">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("src/Basket.API/Basket.API.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("src/Identity.API/Identity.API.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("src/Identity.API/Views/Account/Login.cshtml", "<h1>Sign in</h1>");
        _temp.Write("src/WebApp/WebApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("src/WebApp/Components/App.razor", "<Routes />");
        _temp.Write("src/ClientApp/ClientApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0</TargetFrameworks>
                <OutputType Condition="'$(TargetFramework)' != 'net10.0'">Exe</OutputType>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("src/HybridApp/HybridApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("tests/eShop.AppHost.UnitTests/eShop.AppHost.UnitTests.csproj", """
            <Project Sdk="MSTest.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="..\..\src\eShop.AppHost\eShop.AppHost.csproj" /></ItemGroup>
            </Project>
            """);
        var solution = _temp.Write("eShop.slnx", """
            <Solution>
              <Project Path="src/eShop.AppHost/eShop.AppHost.csproj" />
              <Project Path="src/Basket.API/Basket.API.csproj" />
              <Project Path="src/Identity.API/Identity.API.csproj" />
              <Project Path="src/WebApp/WebApp.csproj" />
              <Project Path="src/HybridApp/HybridApp.csproj" />
              <Project Path="src/ClientApp/ClientApp.csproj" />
              <Project Path="tests/eShop.AppHost.UnitTests/eShop.AppHost.UnitTests.csproj" />
            </Solution>
            """);

        var workspace = WorkspaceLoader.Load(solution);

        Assert.Equal(ProjectKind.BlazorWebApp, workspace.FindProject("WebApp")!.Kind);
        Assert.True(workspace.FindProject("Identity.API")!.HasRazorViews);
        Assert.False(workspace.FindProject("Basket.API")!.HasRazorViews);
        Assert.True(workspace.FindProject("eShop.AppHost.UnitTests")!.IsTestProject);
        Assert.Equal("WebApp", workspace.DefaultStartupProject?.Name);
        Assert.Equal(new[] { "WebApp", "Identity.API", "Basket.API" }, workspace.StartupCandidates.Select(p => p.Name));
    }

    /// <summary>spectre.console: BenchmarkDotNet's benchmarks are a console program, but never the startup project.</summary>
    [Fact]
    public void BenchmarkDotNet_benchmarks_are_never_the_startup_project_and_the_Doctor_says_they_run_on_a_computer()
    {
        var path = _temp.Write("src/Benchmarks/Benchmarks.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks></PropertyGroup>
              <ItemGroup><PackageReference Include="BenchmarkDotNet" /></ItemGroup>
            </Project>
            """);
        _temp.Write("src/Spectre.Console/Spectre.Console.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var solution = _temp.Write("Spectre.Console.slnx", """
            <Solution>
              <Project Path="src/Benchmarks/Benchmarks.csproj" />
              <Project Path="src/Spectre.Console/Spectre.Console.csproj" />
            </Solution>
            """);

        var benchmarks = ProjectParser.Load(path);
        var report = CompatibilityDoctor.Diagnose(benchmarks);
        var workspace = WorkspaceLoader.Load(solution);

        Assert.Equal(ProjectKind.Benchmark, benchmarks.Kind);
        Assert.Null(ProjectClassifier.StartupRank(benchmarks));
        Assert.Null(workspace.DefaultStartupProject);
        Assert.False(report.CanRunToday);
        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.Equal("BenchmarkDotNet benchmarks: BenchmarkDotNet builds and starts separate processes with the .NET SDK, on a computer.", report.Explanation);
        Assert.Equal("This solution has only libraries and benchmarks: nothing in it starts on its own.", workspace.NoStartupProjectReason);
    }

    /// <summary>
    /// Terminal.Gui: a program under docfx/scripts is picked only when nothing else can start, and UICatalog, which the
    /// ScenarioRunner beside it runs, comes before it.
    /// </summary>
    [Fact]
    public void Programs_in_docs_scripts_tools_build_benchmarks_or_test_folders_come_last()
    {
        const string ConsoleProject = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """;
        _temp.Write("docfx/scripts/OutputView/OutputView.csproj", ConsoleProject);
        _temp.Write("Examples/ScenarioRunner/ScenarioRunner.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="..\UICatalog\UICatalog.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("Examples/UICatalog/UICatalog.csproj", ConsoleProject);
        _temp.Write("Tests/NativeAotSmoke/NativeAotSmoke.csproj", ConsoleProject);
        var solution = _temp.Write("Terminal.slnx", """
            <Solution>
              <Project Path="docfx/scripts/OutputView/OutputView.csproj" />
              <Project Path="Examples/ScenarioRunner/ScenarioRunner.csproj" />
              <Project Path="Examples/UICatalog/UICatalog.csproj" />
              <Project Path="Tests/NativeAotSmoke/NativeAotSmoke.csproj" />
            </Solution>
            """);
        var onlyScripts = _temp.Write("Scripts.slnx", """
            <Solution>
              <Project Path="docfx/scripts/OutputView/OutputView.csproj" />
            </Solution>
            """);

        var workspace = WorkspaceLoader.Load(solution);

        Assert.True(ProjectClassifier.IsInAuxiliaryFolder(workspace.FindProject("OutputView")!, workspace.RootDirectory));
        Assert.True(ProjectClassifier.IsInAuxiliaryFolder(workspace.FindProject("NativeAotSmoke")!, workspace.RootDirectory));
        Assert.False(ProjectClassifier.IsInAuxiliaryFolder(workspace.FindProject("UICatalog")!, workspace.RootDirectory));
        Assert.Equal("UICatalog", workspace.DefaultStartupProject?.Name);
        Assert.Equal(new[] { "UICatalog", "ScenarioRunner", "OutputView", "NativeAotSmoke" }, workspace.StartupCandidates.Select(p => p.Name));
        Assert.Equal("OutputView", WorkspaceLoader.Load(onlyScripts).DefaultStartupProject?.Name);
    }

    /// <summary>
    /// Executable test projects, as xUnit v3, TUnit and MSTest.Sdk make them, are tests, never console apps to start.
    /// refit's TUnitTests was reported Ready to run in-process, as a console app.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.NET.Sdk", "<PackageReference Include=\"TUnit\" />")]
    [InlineData("Microsoft.NET.Sdk", "<PackageReference Include=\"xunit.v3\" />")]
    [InlineData("Microsoft.NET.Sdk", "<PackageReference Include=\"NUnit\" />")]
    [InlineData("Microsoft.NET.Sdk", "<PackageReference Include=\"MSTest\" />")]
    [InlineData("Microsoft.NET.Sdk", "<PackageReference Include=\"MSTest.TestFramework\" />")]
    [InlineData("MSTest.Sdk/3.8.0", "")]
    public void Executable_test_projects_are_tests_and_never_the_startup_project(string sdk, string items)
    {
        var path = _temp.Write("tests/Tests/Tests.csproj", $"""
            <Project Sdk="{sdk}">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>{items}</ItemGroup>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.True(project.IsTestProject);
        Assert.Equal(ProjectKind.Test, project.Kind);
        Assert.Null(ProjectClassifier.StartupRank(project));
    }

    /// <summary>Test Explorer runs xUnit, NUnit and MSTest; the Doctor does not say TUnit's tests run here.</summary>
    [Fact]
    public void The_Doctor_does_not_report_TUnit_tests_ready()
    {
        var path = _temp.Write("TUnit/TUnitTests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="TUnit" /></ItemGroup>
            </Project>
            """);

        var report = CompatibilityDoctor.Diagnose(ProjectParser.Load(path));

        Assert.Equal(ProjectKind.Test, report.Kind);
        Assert.False(report.CanRunToday);
        Assert.NotEqual(RunHost.InProcess, report.Host);
        Assert.Contains("TUnit", report.Explanation, StringComparison.Ordinal);
    }

    /// <summary>A folder named like an auxiliary one above the workspace does not count: only the folders inside it do.</summary>
    [Fact]
    public void Only_folders_inside_the_workspace_make_a_program_auxiliary()
    {
        var path = _temp.Write("tools/Repo/src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.False(ProjectClassifier.IsInAuxiliaryFolder(project, _temp.Resolve("tools/Repo")));
        Assert.True(ProjectClassifier.IsInAuxiliaryFolder(project, _temp.Path));
        Assert.False(ProjectClassifier.IsInAuxiliaryFolder(project, project.DirectoryPath));
    }

    /// <summary>StronglyTypedId: only libraries, tests and a NUKE build script, so there is no startup project, and the IDE says why.</summary>
    [Fact]
    public void A_solution_of_libraries_and_tests_has_no_startup_project_and_says_why()
    {
        _temp.Write("src/Ids/Ids.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("test/Ids.Tests/Ids.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="xunit.v3" Version="3.0.0" /></ItemGroup>
            </Project>
            """);
        var withTests = _temp.Write("Ids.slnx", """
            <Solution>
              <Project Path="src/Ids/Ids.csproj" />
              <Project Path="test/Ids.Tests/Ids.Tests.csproj" />
            </Solution>
            """);
        var library = _temp.Write("Library.slnx", """
            <Solution>
              <Project Path="src/Ids/Ids.csproj" />
            </Solution>
            """);

        var workspace = WorkspaceLoader.Load(withTests);

        Assert.Null(workspace.DefaultStartupProject);
        Assert.Empty(workspace.StartupCandidates);
        Assert.Equal("This solution has only libraries and tests: run the tests in Test Explorer.", workspace.NoStartupProjectReason);
        Assert.Equal("This solution has only libraries: nothing in it starts on its own.", WorkspaceLoader.Load(library).NoStartupProjectReason);
    }

    [Fact]
    public void A_solution_with_a_startup_project_gives_no_reason_for_having_none()
    {
        var path = _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        Assert.Null(WorkspaceLoader.Load(path).NoStartupProjectReason);
    }

    /// <summary>maui-samples' TodoSQLite: a lone .NET MAUI app is not started, and the IDE names it.</summary>
    [Fact]
    public void A_solution_whose_only_app_needs_a_platform_workload_names_it()
    {
        var path = _temp.Write("TodoSQLite/TodoSQLite.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
            </Project>
            """);

        var workspace = WorkspaceLoader.Load(path);

        Assert.Null(workspace.DefaultStartupProject);
        Assert.Equal(
            "Nothing in this solution starts on this tablet: TodoSQLite is a .NET MAUI project, which only .NET's MAUI workload builds, on a computer.",
            workspace.NoStartupProjectReason);
    }

    public void Dispose() => _temp.Dispose();
}
