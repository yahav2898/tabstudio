using TabStudio.Compilation;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Wpf.Tests;

/// <summary>What TabStudio gives a project with <c>UseWPF</c>, as the Windows Desktop SDK does.</summary>
public sealed class WpfProjectTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void App_xaml_is_the_application_definition_and_every_other_xaml_file_is_a_page()
    {
        var project = Write("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <UseWPF>true</UseWPF>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("App/App.xaml", "<Application />");
        _temp.Write("App/MainWindow.xaml", "<Window />");
        _temp.Write("App/Views/CustomerView.xaml", "<UserControl />");
        _temp.Write("App/obj/Debug/Generated.xaml", "<Page />");

        var items = WpfProject.XamlItems(project);

        Assert.Equal(
            ["App.xaml ApplicationDefinition", "MainWindow.xaml Page", "Views/CustomerView.xaml Page"],
            items.Select(i => $"{i.RelativePath} {i.Kind}"));
        Assert.True(WpfProject.UsesWpf(project));
        Assert.Equal(ProjectKind.Wpf, project.Kind);
    }

    [Fact]
    public void The_project_file_adds_and_removes_xaml_items()
    {
        var project = Write("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <UseWPF>true</UseWPF>
              </PropertyGroup>
              <ItemGroup>
                <ApplicationDefinition Remove="App.xaml" />
                <Page Include="App.xaml" />
                <Page Remove="Drafts\**\*.xaml" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("App/App.xaml", "<Application />");
        _temp.Write("App/Shell.xaml", "<Window />");
        _temp.Write("App/Drafts/Old/Sketch.xaml", "<Window />");

        var items = WpfProject.XamlItems(project);

        Assert.Equal(["App.xaml Page", "Shell.xaml Page"], items.Select(i => $"{i.RelativePath} {i.Kind}"));
    }

    [Fact]
    public void WPF_takes_System_IO_and_System_Net_Http_out_of_the_implicit_usings()
    {
        var project = Write("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0-windows</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <UseWPF>true</UseWPF>
              </PropertyGroup>
            </Project>
            """);

        var usings = CSharpCompiler.CreateGlobalUsingsSource(project)!;

        Assert.Contains("global using global::System.Linq;", usings, StringComparison.Ordinal);
        Assert.DoesNotContain("global::System.IO;", usings, StringComparison.Ordinal);
        Assert.DoesNotContain("global::System.Net.Http;", usings, StringComparison.Ordinal);
    }

    [Fact]
    public void WPF_projects_get_the_symbol_OpenSilver_defines()
    {
        var project = Write("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0-windows</TargetFramework>
                <UseWPF>true</UseWPF>
              </PropertyGroup>
            </Project>
            """);

        var options = WpfProject.ParseOptions(project, CSharpCompiler.CreateParseOptions());

        Assert.Contains(WpfProject.OpenSilverSymbol, options.PreprocessorSymbolNames);
        Assert.Contains("DEBUG", options.PreprocessorSymbolNames);
    }

    [Fact]
    public void Second_pass_files_lead_back_to_their_xaml()
    {
        var project = Write("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var generated = Path.Combine(WpfProject.SecondPassDirectory(project), "Views", "CustomerView.xaml.g.cs");

        Assert.Equal(Path.Combine(project.DirectoryPath, "Views", "CustomerView.xaml"), WpfProject.SecondPassSource(project, generated));
        Assert.Null(WpfProject.SecondPassSource(project, Path.Combine(project.DirectoryPath, "MainWindow.xaml.cs")));
    }

    public void Dispose() => _temp.Dispose();

    private ProjectInfo Write(string projectFile) => ProjectParser.Load(_temp.Write("App/App.csproj", projectFile));
}
