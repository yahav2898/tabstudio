using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>
/// Projects that only a platform workload builds, on a computer: .NET MAUI apps (maui-samples' TodoSQLite, eShop's
/// ClientApp and HybridApp), the mobile heads of cross-platform apps, Xamarin projects (CommunityToolkit's
/// MvvmSampleXF.iOS, which TabStudio 0.19 took for a console app and chose to start) and UWP apps. They are never
/// started, never compiled, and the Compatibility Doctor says so in one line.
/// </summary>
public sealed class PlatformWorkloadTests : IDisposable
{
    private static readonly string ProjectPath = Path.Combine(Path.GetTempPath(), "Sample", "App", "App.csproj");

    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData("<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks><OutputType>Exe</OutputType><UseMaui>true</UseMaui>")]
    [InlineData("<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0</TargetFrameworks><OutputType Condition=\"'$(TargetFramework)' != 'net10.0'\">Exe</OutputType><UseMaui>true</UseMaui>")]
    [InlineData("<TargetFramework>net10.0</TargetFramework><UseMaui>true</UseMaui>")]
    public void A_MAUI_project_needs_the_MAUI_workload_and_is_never_started(string properties)
    {
        var project = ProjectParser.Parse(ProjectPath, $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>{properties}</PropertyGroup></Project>""");

        Assert.Equal(ProjectKind.Maui, project.Kind);
        Assert.Equal(PlatformWorkload.Maui, ProjectClassifier.RequiredWorkload(project));
        Assert.True(ProjectClassifier.NeedsPlatformWorkload(project));
        Assert.Null(ProjectClassifier.StartupRank(project));
        Assert.Contains("MAUI workload", ProjectClassifier.PlatformWorkloadReason(project), StringComparison.Ordinal);
    }

    /// <summary>
    /// The Doctor's row says what is true today, one line: not supported, and why. Before, it said the app would be packaged
    /// and installed on the tablet in an update, which the roadmap does not commit to.
    /// </summary>
    [Fact]
    public void The_Doctor_says_a_MAUI_app_is_built_on_a_computer_and_left_out_of_builds()
    {
        var project = ProjectParser.Parse(ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
            </Project>
            """);

        var report = CompatibilityDoctor.Diagnose(project);

        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.Equal(RunHost.None, report.Host);
        Assert.False(report.CanRunToday);
        Assert.Equal("Not supported: a .NET MAUI project is built by .NET's MAUI workload, on a computer. TabStudio leaves it out of builds.", report.Explanation);
        Assert.DoesNotContain("will", report.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("net10.0-android", "Android")]
    [InlineData("net10.0-ios", "iOS")]
    [InlineData("net10.0-tizen", "Tizen")]
    [InlineData("net10.0-android;net10.0-windows10.0.19041.0", "Android")]
    public void A_mobile_platform_head_needs_its_workload(string frameworks, string platform)
    {
        var project = ProjectParser.Parse(ProjectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFrameworks>{frameworks}</TargetFrameworks></PropertyGroup>
              <ItemGroup><PackageReference Include="Avalonia.Android" Version="11.3.0" /></ItemGroup>
            </Project>
            """);

        var report = CompatibilityDoctor.Diagnose(project);

        Assert.Equal(PlatformWorkload.MobilePlatform, ProjectClassifier.RequiredWorkload(project));
        Assert.Null(ProjectClassifier.StartupRank(project));
        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.Equal($"{platform} head, on a computer", report.RunsVia);
    }

    /// <summary>WPF, Windows Forms and WinUI projects for Windows alone keep running as they do: they need no workload.</summary>
    [Theory]
    [InlineData("<UseWPF>true</UseWPF><TargetFramework>net10.0-windows</TargetFramework>", ProjectKind.Wpf)]
    [InlineData("<UseWindowsForms>true</UseWindowsForms><TargetFramework>net10.0-windows</TargetFramework>", ProjectKind.WinForms)]
    [InlineData("<UseWinUI>true</UseWinUI><TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>", ProjectKind.WinUI)]
    public void A_Windows_desktop_app_needs_no_workload(string properties, ProjectKind kind)
    {
        var project = ProjectParser.Parse(ProjectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>WinExe</OutputType>{properties}</PropertyGroup></Project>
            """);

        Assert.Equal(kind, project.Kind);
        Assert.Equal(PlatformWorkload.None, ProjectClassifier.RequiredWorkload(project));
        Assert.NotNull(ProjectClassifier.StartupRank(project));
        Assert.Equal(CompatibilityStatus.Ready, CompatibilityDoctor.Diagnose(project).Status);
    }

    /// <summary>An Uno Platform app lists Android and iOS too, but TabStudio builds it for Uno's desktop target.</summary>
    [Fact]
    public void An_Uno_app_with_mobile_targets_needs_no_workload()
    {
        var project = ProjectParser.Parse(ProjectPath, """
            <Project Sdk="Uno.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-browserwasm;net10.0-desktop</TargetFrameworks>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);

        Assert.Equal(ProjectKind.Uno, project.Kind);
        Assert.Equal(PlatformWorkload.None, ProjectClassifier.RequiredWorkload(project));
    }

    /// <summary>CommunityToolkit/MVVM-Samples' MvvmSampleXF.iOS: an old-style project with OutputType Exe and no target framework.</summary>
    [Fact]
    public void A_Xamarin_iOS_project_is_not_a_console_app_to_start()
    {
        var project = ProjectParser.Parse(ProjectPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <ProjectTypeGuids>{FEACFBD2-3405-455C-9665-78FE426C6842};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup><Reference Include="Xamarin.iOS" /></ItemGroup>
              <Import Project="$(MSBuildExtensionsPath)\Xamarin\iOS\Xamarin.iOS.CSharp.targets" />
            </Project>
            """);

        var report = CompatibilityDoctor.Diagnose(project);

        Assert.Equal(PlatformWorkload.Xamarin, ProjectClassifier.RequiredWorkload(project));
        Assert.Null(ProjectClassifier.StartupRank(project));
        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.Equal("Not supported: a Xamarin project is built by Xamarin's tools in Visual Studio, on a computer. TabStudio leaves it out of builds.", report.Explanation);
    }

    [Theory]
    [InlineData("<ProjectTypeGuids>{EFBA0AD7-5A72-4C68-AF49-83D382785DCF};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>", "")]
    [InlineData("<AndroidApplication>True</AndroidApplication>", "")]
    [InlineData("", """<Import Project="$(MSBuildExtensionsPath)\Xamarin\Android\Xamarin.Android.CSharp.targets" />""")]
    public void A_Xamarin_Android_project_is_recognized_by_its_type_its_switch_or_its_targets(string properties, string import)
    {
        // As MvvmSampleXF.Android: a library to MSBuild, with Xamarin.Android's TargetFrameworkVersion.
        var project = ProjectParser.Parse(ProjectPath, $"""
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup><OutputType>Library</OutputType><TargetFrameworkVersion>v13.0</TargetFrameworkVersion>{properties}</PropertyGroup>
              {import}
            </Project>
            """);

        var report = CompatibilityDoctor.Diagnose(project);

        Assert.Equal(PlatformWorkload.Xamarin, ProjectClassifier.RequiredWorkload(project));
        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.False(report.CanUpgradeProject);
    }

    [Theory]
    [InlineData("<OutputType>AppContainerExe</OutputType>")]
    [InlineData("<OutputType>Exe</OutputType><TargetPlatformIdentifier>UAP</TargetPlatformIdentifier>")]
    public void A_UWP_app_needs_Visual_Studios_UWP_tools(string properties)
    {
        var project = ProjectParser.Parse(ProjectPath, $"""
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>{properties}<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion></PropertyGroup>
            </Project>
            """);

        var report = CompatibilityDoctor.Diagnose(project);

        Assert.Equal(PlatformWorkload.Uwp, ProjectClassifier.RequiredWorkload(project));
        Assert.Null(ProjectClassifier.StartupRank(project));
        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.Equal("Not supported: a UWP project is built by Visual Studio's Universal Windows Platform tools, on Windows. TabStudio leaves it out of builds.", report.Explanation);
    }

    [Fact]
    public void A_console_app_or_a_library_needs_no_workload()
    {
        var console = ProjectParser.Parse(ProjectPath, """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        var library = ProjectParser.Parse(ProjectPath, """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks></PropertyGroup></Project>""");

        Assert.False(ProjectClassifier.NeedsPlatformWorkload(console));
        Assert.False(ProjectClassifier.NeedsPlatformWorkload(library));
        Assert.Null(ProjectClassifier.PlatformWorkloadReason(console));
    }

    /// <summary>
    /// CommunityToolkit/MVVM-Samples: the Android and iOS heads are Xamarin projects and the shared one a library, so
    /// nothing is started and the IDE says which app needs a computer.
    /// </summary>
    [Fact]
    public void A_Xamarin_solution_has_no_startup_project_and_says_why()
    {
        _temp.Write("MvvmSampleXF.iOS/MvvmSampleXF.iOS.csproj", """
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <ProjectTypeGuids>{FEACFBD2-3405-455C-9665-78FE426C6842};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("MvvmSampleXF/MvvmSampleXF.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup></Project>
            """);
        var solution = _temp.Write("MvvmSampleXF.slnx", """
            <Solution>
              <Project Path="MvvmSampleXF.iOS/MvvmSampleXF.iOS.csproj" />
              <Project Path="MvvmSampleXF/MvvmSampleXF.csproj" />
            </Solution>
            """);

        var workspace = WorkspaceLoader.Load(solution);

        Assert.Null(workspace.DefaultStartupProject);
        Assert.Equal(
            "Nothing in this solution starts on this tablet: MvvmSampleXF.iOS is a Xamarin project, which only Xamarin's tools in Visual Studio build, on a computer.",
            workspace.NoStartupProjectReason);
    }

    public void Dispose() => _temp.Dispose();
}
