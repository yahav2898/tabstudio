using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class ProjectParserTests
{
    private static readonly string ProjectPath = Path.Combine(Path.GetTempPath(), "Sample", "App", "App.csproj");

    [Fact]
    public void The_project_s_own_item_groups_keep_only_what_their_conditions_allow()
    {
        // Humanizer's benchmarks: the repository's own project, or its NuGet package when UseBaselinePackage is true.
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworks>net11.0;net10.0;net8.0</TargetFrameworks>
                <UseBaselinePackage Condition="'$(UseBaselinePackage)' == ''">false</UseBaselinePackage>
              </PropertyGroup>
              <ItemGroup Condition="'$(UseBaselinePackage)' == 'true'">
                <PackageReference Include="Humanizer" Version="3.0.10" />
              </ItemGroup>
              <ItemGroup Condition="'$(UseBaselinePackage)' != 'true'">
                <ProjectReference Include="..\Humanizer\Humanizer.csproj" />
              </ItemGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageReference Include="Net8Only" Version="1.0.0" />
              </ItemGroup>
              <ItemGroup Condition="'$(Configuration)' == 'Debug'">
                <PackageReference Include="DebugOnly" Version="1.0.0" />
              </ItemGroup>
              <ItemGroup Condition=" '$(TargetFrameworkIdentifier)' == '.NETCoreApp' ">
                <PackageReference Include="NetCoreOnly" Version="1.0.0" />
              </ItemGroup>
              <ItemGroup Condition=" '$(TargetFrameworkIdentifier)' == '.NETFramework' ">
                <PackageReference Include="NetFrameworkOnly" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Equal(["DebugOnly", "NetCoreOnly"], project.PackageReferences.Select(p => p.Name));
        Assert.Single(project.ProjectReferences);
    }

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
    public void Reads_using_items_with_their_alias_and_static_metadata()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Using Include="Xunit" />
                <Using Include="System.Console" Static="true" />
                <Using Include="System.Text.StringBuilder"><Alias>Builder</Alias></Using>
                <Using Remove="System.Net.Http" />
              </ItemGroup>
            </Project>
            """;

        var project = ProjectParser.Parse(ProjectPath, xml);

        Assert.Equal(
            new[] { new UsingInfo("Xunit"), new UsingInfo("System.Console", IsStatic: true), new UsingInfo("System.Text.StringBuilder", Alias: "Builder") },
            project.Usings);
        Assert.Equal(new[] { "System.Net.Http" }, project.RemovedUsings);
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
    public void Directory_build_targets_overrides_the_project_as_msbuild_imports_it_last()
    {
        // As Piranha CMS does: TargetFrameworks in Directory.Build.props and ImplicitUsings in Directory.Build.targets.
        using var temp = new TempDirectory();
        temp.Write("Directory.Build.props", """
            <Project>
              <PropertyGroup>
                <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);
        temp.Write("Directory.Build.targets", """
            <Project>
              <PropertyGroup>
                <ImplicitUsings>true</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        var path = temp.Write("core/Hosting/Hosting.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <Nullable>disable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.Equal(new[] { "net8.0", "net9.0" }, project.TargetFrameworks);
        Assert.True(project.ImplicitUsings);
        Assert.True(project.NullableEnabled);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void A_razor_class_library_with_mvc_support_gets_aspnet_core(string addRazorSupportForMvc, bool expected)
    {
        // As Piranha.Manager is: Razor Pages in a Razor class library, with no FrameworkReference of its own.
        var project = ProjectParser.Parse("/src/Manager/Manager.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AddRazorSupportForMvc>{addRazorSupportForMvc}</AddRazorSupportForMvc>
              </PropertyGroup>
            </Project>
            """);

        Assert.Equal(expected, project.FrameworkReferences.Contains(ProjectParser.AspNetCoreFramework));
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

    [Fact]
    public void Web_projects_use_the_ASP_NET_Core_framework_and_framework_references_are_read()
    {
        var web = ProjectParser.Parse(ProjectPath, """<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        var library = ProjectParser.Parse(ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            </Project>
            """);
        var console = ProjectParser.Parse(ProjectPath, """<Project Sdk="Microsoft.NET.Sdk" />""");

        Assert.Equal(new[] { ProjectParser.AspNetCoreFramework }, web.FrameworkReferences);
        Assert.Equal(new[] { ProjectParser.AspNetCoreFramework }, library.FrameworkReferences);
        Assert.Empty(console.FrameworkReferences);
    }

    [Theory]
    [InlineData("Microsoft.NET.Sdk.Web", "", true)]
    [InlineData("Microsoft.NET.Sdk.Worker", "", true)]
    [InlineData("Microsoft.NET.Sdk.Web", "<OutputType>Library</OutputType>", false)]
    [InlineData("Microsoft.NET.Sdk", "", false)]
    public void Web_and_worker_projects_are_executable_unless_they_say_otherwise(string sdk, string properties, bool executable)
    {
        var project = ProjectParser.Parse(ProjectPath, $"""<Project Sdk="{sdk}"><PropertyGroup>{properties}</PropertyGroup></Project>""");

        Assert.Equal(executable, project.IsExecutable);
    }

    [Theory]
    [InlineData("net462;net470;net48;net8.0;net9.0;net10.0;net11.0", "net10.0")]
    [InlineData("net8.0;net9.0", "net9.0")]
    [InlineData("net9.0;net8.0", "net9.0")]
    [InlineData("net11.0;net12.0", "net11.0")]
    [InlineData("netstandard2.0;net8.0-android;net8.0-ios", "net8.0-android")]
    [InlineData("net48;netcoreapp3.1", "netcoreapp3.1")]
    [InlineData("net48;netstandard2.0", "net48")]
    [InlineData("netstandard2.0", "netstandard2.0")]
    public void Conditions_are_evaluated_for_the_framework_TabStudio_compiles(string targetFrameworks, string expected) =>
        Assert.Equal(expected, ProjectParser.EvaluationTargetFramework(targetFrameworks.Split(';')));

    [Fact]
    public void A_project_without_target_frameworks_has_no_framework_for_conditions() =>
        Assert.Null(ProjectParser.EvaluationTargetFramework([]));

    [Fact]
    public void Item_groups_of_imported_files_are_kept_for_net10_rather_than_the_first_framework()
    {
        // Refit lists net462 first; its Directory.Build.props adds .NET Framework polyfills only for net4*.
        using var temp = new TempDirectory();
        temp.Write("Directory.Build.props", """
            <Project>
              <ItemGroup Condition="'$(TargetFramework)' == 'net462'"><Using Include="Legacy.Polyfills" /></ItemGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'"><Using Include="Modern.Helpers" /></ItemGroup>
            </Project>
            """);
        var path = temp.Write("Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFrameworks>net462;net8.0;net10.0</TargetFrameworks></PropertyGroup>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.Contains(project.Usings, u => u.Name == "Modern.Helpers");
        Assert.DoesNotContain(project.Usings, u => u.Name == "Legacy.Polyfills");
    }

    [Theory]
    [InlineData("true", "2.0.0")]
    [InlineData("false", "1.0.0")]
    public void VersionOverride_wins_over_Version_with_central_package_management(string central, string expected)
    {
        // Refit: <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="$(MsCACSharpVersion)" />.
        var project = ProjectParser.Parse(ProjectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><ManagePackageVersionsCentrally>{central}</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Roslyn" VersionOverride="4.8.0" />
                <PackageReference Include="Both" Version="1.0.0"><VersionOverride>2.0.0</VersionOverride></PackageReference>
                <PackageReference Include="Central" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal("4.8.0", project.PackageReferences.Single(p => p.Name == "Roslyn").Version);
        Assert.Equal(expected, project.PackageReferences.Single(p => p.Name == "Both").Version);
        Assert.Null(project.PackageReferences.Single(p => p.Name == "Central").Version);
    }
}
