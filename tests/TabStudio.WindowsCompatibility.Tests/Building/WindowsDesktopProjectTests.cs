using TabStudio.Core.Workspaces;
using TabStudio.Testing;
using TabStudio.WindowsCompatibility.Building;

namespace TabStudio.WindowsCompatibility.Tests.Building;

/// <summary>Windows Forms and WPF projects as the Windows Desktop SDK sees them.</summary>
public sealed class WindowsDesktopProjectTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void A_wpf_project_has_its_app_its_pages_and_its_resources()
    {
        var file = _temp.Write("WpfApp1/WpfApp1.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <UseWPF>true</UseWPF>
              </PropertyGroup>
              <ItemGroup>
                <Resource Include="Images\logo.png" />
                <Page Remove="Draft.xaml" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("WpfApp1/App.xaml", "<Application />");
        _temp.Write("WpfApp1/MainWindow.xaml", "<Window />");
        _temp.Write("WpfApp1/Views/Settings.xaml", "<Page />");
        _temp.Write("WpfApp1/Draft.xaml", "<Page />");
        _temp.Write("WpfApp1/obj/Debug/net10.0-windows/Stale.xaml", "<Page />");
        _temp.Write("WpfApp1/Images/logo.png", "png");

        var project = WindowsDesktopProject.From(ProjectParser.Parse(file, File.ReadAllText(file)))!;
        var items = project.ReadWpfItems();

        Assert.True(project.UsesWpf);
        Assert.False(project.UsesWindowsForms);
        Assert.Equal("App.xaml", items.ApplicationDefinition);
        Assert.Equal(["MainWindow.xaml", "Views/Settings.xaml"], items.Pages);
        Assert.Equal(["Images/logo.png"], items.Resources);
        Assert.Equal("obj/Release/net10.0-windows/", project.IntermediateFolder("Release"));
        Assert.Equal(Path.Combine(_temp.Resolve("WpfApp1"), "bin", "Debug", "net10.0-windows"), project.OutputFolder("Debug"));
    }

    [Fact]
    public void A_windows_forms_project_has_no_xaml_and_references_its_profile_of_the_framework()
    {
        var file = _temp.Write("WinFormsApp1/WinFormsApp1.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """);
        var info = ProjectParser.Parse(file, File.ReadAllText(file));
        var project = WindowsDesktopProject.From(info)!;

        Assert.True(project.UsesWindowsForms);
        Assert.Null(project.ReadWpfItems().ApplicationDefinition);
        Assert.Equal([ProjectParser.WindowsDesktopWindowsFormsFramework], info.FrameworkReferences);
        Assert.Equal(ProjectParser.WindowsDesktopWindowsFormsFramework, NuGetWindowsReferencePack.Framework(project));
        Assert.True(project.IsApp);
        Assert.Equal("WinFormsApp1", project.AssemblyName);
    }

    [Fact]
    public void A_console_project_is_not_a_windows_desktop_project()
    {
        var file = _temp.Write("Console/Console.csproj", """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");

        Assert.Null(WindowsDesktopProject.From(ProjectParser.Parse(file, File.ReadAllText(file))));
    }
}
