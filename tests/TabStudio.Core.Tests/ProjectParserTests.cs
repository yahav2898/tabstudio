using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class ProjectParserTests
{
    private static readonly string ProjectPath = Path.Combine(Path.GetTempPath(), "Sample", "App", "App.csproj");

    [Fact]
    public void Reads_an_sdk_style_console_project()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Equal("App", project.Name);
        Assert.Equal(ProjectLanguage.CSharp, project.Language);
        Assert.Equal("Microsoft.NET.Sdk", project.Sdk);
        Assert.Equal(new[] { "net10.0" }, project.TargetFrameworks);
        Assert.Equal(ProjectKind.ConsoleApp, project.Kind);
        Assert.True(project.IsExecutable);
        Assert.True(project.ImplicitUsings);
        Assert.True(project.NullableEnabled);
        Assert.False(project.IsTestProject);
    }

    [Theory]
    [InlineData("Microsoft.NET.Sdk.Web", ProjectKind.AspNetCore)]
    [InlineData("Microsoft.NET.Sdk.Web/9.0.100", ProjectKind.AspNetCore)]
    [InlineData("Microsoft.NET.Sdk.BlazorWebAssembly", ProjectKind.BlazorWebAssembly)]
    [InlineData("Microsoft.NET.Sdk.Worker", ProjectKind.Worker)]
    [InlineData("Uno.Sdk", ProjectKind.Uno)]
    public void Recognizes_the_project_kind_from_the_sdk(string sdk, ProjectKind expected)
    {
        var xml = $"""<Project Sdk="{sdk}"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""";

        Assert.Equal(expected, ProjectParser.Parse(ProjectPath, xml).Kind);
    }

    [Theory]
    [InlineData("UseWPF", ProjectKind.Wpf)]
    [InlineData("UseWindowsForms", ProjectKind.WinForms)]
    [InlineData("UseWinUI", ProjectKind.WinUI)]
    [InlineData("UseMaui", ProjectKind.Maui)]
    public void Recognizes_ui_frameworks_from_their_switches(string property, ProjectKind expected)
    {
        var xml = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <{property}>true</{property}>
              </PropertyGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Equal(expected, project.Kind);
        Assert.True(project.TargetsWindowsOnly);
    }

    [Fact]
    public void A_test_project_is_a_test_project_even_when_it_uses_a_ui_framework()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWPF>true</UseWPF></PropertyGroup>
              <ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" /></ItemGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Equal(ProjectKind.Test, project.Kind);
        Assert.True(project.IsTestProject);
    }

    [Fact]
    public void Reads_package_and_project_references_written_with_backslashes()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                <PackageReference Include="Serilog"><Version>4.0.0</Version></PackageReference>
                <ProjectReference Include="..\Lib\Lib.csproj" />
              </ItemGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Equal(
            new[] { new PackageReferenceInfo("Newtonsoft.Json", "13.0.3"), new PackageReferenceInfo("Serilog", "4.0.0") },
            project.PackageReferences);
        var expectedReference = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Sample", "Lib", "Lib.csproj"));
        Assert.Equal(new[] { expectedReference }, project.ProjectReferences);
    }

    [Fact]
    public void An_unconditional_property_wins_over_a_conditional_one()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup Condition="'$(Configuration)' == 'Release'">
                <OutputType>WinExe</OutputType>
                <Optimize>true</Optimize>
              </PropertyGroup>
              <PropertyGroup>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Equal("Exe", project.OutputType);
        Assert.Equal("true", project.GetProperty("Optimize"));
    }

    [Fact]
    public void Inherits_directory_build_props_and_expands_property_references()
    {
        using var temp = new TempDirectory();
        temp.Write("Directory.Build.props", """
            <Project>
              <PropertyGroup>
                <AppTargetFramework>net10.0</AppTargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        var path = temp.Write("src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>$(AppTargetFramework)</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.Equal(new[] { "net10.0" }, project.TargetFrameworks);
        Assert.True(project.NullableEnabled);
    }

    [Fact]
    public void Reads_a_legacy_project_with_the_msbuild_namespace()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
              </PropertyGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Null(project.Sdk);
        Assert.Equal("Exe", project.OutputType);
        Assert.Equal(ProjectKind.ConsoleApp, project.Kind);
    }

    [Fact]
    public void Invalid_xml_throws_a_load_exception_that_names_the_file()
    {
        var exception = Assert.Throws<ProjectLoadException>(() => ProjectParser.Parse(ProjectPath, "<Project>"));

        Assert.Equal(ProjectPath, exception.FilePath);
        Assert.Contains("App.csproj", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("App.csproj", true)]
    [InlineData("App.fsproj", true)]
    [InlineData("Native.vcxproj", true)]
    [InlineData("App.sln", false)]
    [InlineData("Program.cs", false)]
    public void Recognizes_project_file_extensions(string fileName, bool expected) =>
        Assert.Equal(expected, ProjectParser.IsProjectFile(fileName));
}
