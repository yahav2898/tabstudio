using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>Imports written with property functions, and the Version the SDK makes of VersionPrefix and VersionSuffix.</summary>
public sealed class MsBuildImportsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void A_nested_Directory_Build_props_imports_the_one_above_it_with_GetPathOfFileAbove()
    {
        // Refit's examples, Terminal.Gui's Tests and StronglyTypedId's test folder chain their Directory.Build.props
        // this way; without it the repository's TargetFramework, ImplicitUsings and version.props are lost.
        _temp.Write("Directory.Build.props", """
            <Project>
              <Import Project="version.props" />
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
            </Project>
            """);
        _temp.Write("version.props", """
            <Project><PropertyGroup><VersionPrefix>1.0.0</VersionPrefix><VersionSuffix>beta08</VersionSuffix></PropertyGroup></Project>
            """);
        _temp.Write("test/Directory.Build.props", """
            <Project>
              <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
              <PropertyGroup><IsPackable>false</IsPackable></PropertyGroup>
            </Project>
            """);
        var path = _temp.Write("test/Tests/Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup><PackageReference Include="StronglyTypedId" Version="$(Version)" /></ItemGroup>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.Equal(new[] { "net10.0" }, project.TargetFrameworks);
        Assert.True(project.ImplicitUsings);
        Assert.Equal("false", project.GetProperty("IsPackable"));
        Assert.Equal("1.0.0-beta08", project.GetProperty("Version"));
    }

    [Fact]
    public void GetDirectoryNameOfFileAbove_and_a_one_argument_GetPathOfFileAbove_are_followed_too()
    {
        _temp.Write("Directory.Build.props", """
            <Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>
            """);
        _temp.Write("src/Directory.Build.props", """
            <Project>
              <Import Project="$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory).., Directory.Build.props))/Directory.Build.props" />
            </Project>
            """);
        _temp.Write("src/Shared.props", """
            <Project><PropertyGroup><LangVersion>preview</LangVersion></PropertyGroup></Project>
            """);
        var path = _temp.Write("src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <Import Project="$([MSBuild]::GetPathOfFileAbove(Shared.props))" />
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.True(project.NullableEnabled);
        Assert.Equal("preview", project.GetProperty("LangVersion"));
    }

    [Fact]
    public void An_import_whose_file_is_not_found_above_is_skipped()
    {
        _temp.Write("src/Directory.Build.props", """
            <Project>
              <Import Project="$([MSBuild]::GetPathOfFileAbove('Missing.props', '$(MSBuildThisFileDirectory)../'))" />
              <PropertyGroup><Nullable>enable</Nullable></PropertyGroup>
            </Project>
            """);
        var path = _temp.Write("src/App/App.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");

        var project = ProjectParser.Load(path);

        Assert.True(project.NullableEnabled);
    }

    [Fact]
    public void Import_paths_are_resolved_from_properties_and_the_two_functions_and_nothing_else()
    {
        var root = _temp.Path;
        var props = _temp.Write("Directory.Build.props", "<Project />");
        var file = _temp.Write("src/Lib/Lib.props", "<Project />");
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["RepoRoot"] = root + Path.DirectorySeparatorChar };

        Assert.Equal(props, MsBuildImports.ResolvePath("$(RepoRoot)Directory.Build.props", file, properties));
        Assert.Equal(props, MsBuildImports.ResolvePath("$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)'))", file, properties));
        Assert.Equal(props, MsBuildImports.ResolvePath("$([MSBuild]::GetDirectoryNameOfFileAbove('$(MSBuildThisFileDirectory)', 'Directory.Build.props'))\\Directory.Build.props", file, properties));
        Assert.Null(MsBuildImports.ResolvePath("$(NotSet)Directory.Build.props", file, properties));
        Assert.Null(MsBuildImports.ResolvePath("$([System.IO.Path]::Combine('$(RepoRoot)', 'Directory.Build.props'))", file, properties));
        Assert.Null(MsBuildImports.ResolvePath("$([MSBuild]::GetPathOfFileAbove('Missing.props', '$(MSBuildThisFileDirectory)'))", file, properties));
        Assert.Null(MsBuildImports.ResolvePath("$(RepoRoot)eng/*.props", file, properties));
    }

    [Theory]
    [InlineData("<VersionPrefix>2.1.0</VersionPrefix>", "2.1.0")]
    [InlineData("<VersionPrefix>2.1.0</VersionPrefix><VersionSuffix>rc.1</VersionSuffix>", "2.1.0-rc.1")]
    [InlineData("<VersionSuffix>preview</VersionSuffix>", "1.0.0-preview")]
    [InlineData("<Version>3.0.0</Version><VersionPrefix>2.1.0</VersionPrefix>", "3.0.0")]
    [InlineData("", null)]
    public void Version_is_VersionPrefix_and_VersionSuffix_when_the_project_does_not_set_it(string properties, string? expected)
    {
        var project = ProjectParser.Parse("/src/Lib/Lib.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>{properties}</PropertyGroup></Project>
            """);

        Assert.Equal(expected, project.GetProperty("Version"));
    }
}
