using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

/// <summary>Central package management as MSBuild evaluates Directory.Packages.props, from Refit's and Terminal.Gui's.</summary>
public sealed class CentralPackageVersionsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Choose_When_and_conditions_are_evaluated_for_the_framework_TabStudio_compiles()
    {
        _temp.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Plain" Version="1.0.0" />
                <PackageVersion Include="Pkcs" Version="9.0.0" Condition="'$(MSBuildProjectName)' == 'Other'" />
              </ItemGroup>
              <ItemGroup Condition="'$(Configuration)|$(Platform)'=='Release|AnyCPU'">
                <PackageVersion Include="ReleaseOnly" Version="2.0.0" />
              </ItemGroup>
              <Choose>
                <When Condition="'$(TargetFramework)' == 'net11.0'">
                  <ItemGroup><PackageVersion Include="Extensions" Version="11.0.0-rc.2" /></ItemGroup>
                </When>
                <Otherwise>
                  <ItemGroup>
                    <PackageVersion Include="Extensions" Version="10.0.13" />
                    <PackageVersion Include="Pkcs" Version="10.0.13" Condition="'$(MSBuildProjectName)' == 'Lib'" />
                  </ItemGroup>
                </Otherwise>
              </Choose>
              <Choose>
                <When Condition="'$(TargetFramework)' == 'net11.0'">
                  <ItemGroup><PackageVersion Include="WebAssembly" Version="11.0.0-rc.2" /></ItemGroup>
                </When>
                <When Condition="'$(TargetFramework)' == 'net10.0'">
                  <ItemGroup><PackageVersion Include="WebAssembly" Version="10.0.13" /></ItemGroup>
                </When>
              </Choose>
            </Project>
            """);
        var project = Load("Lib", "<TargetFrameworks>net462;net8.0;net10.0;net11.0</TargetFrameworks>");

        var versions = CentralPackageVersions.Load(project);

        Assert.Equal("1.0.0", versions["Plain"]);
        Assert.Equal("10.0.13", versions["Extensions"]);
        Assert.Equal("10.0.13", versions["WebAssembly"]);
        Assert.Equal("10.0.13", versions["Pkcs"]);
        Assert.False(versions.ContainsKey("ReleaseOnly"));
    }

    [Fact]
    public void Update_changes_an_earlier_version_unless_its_condition_cannot_be_evaluated()
    {
        // Refit pins Roslyn for benchmarks only, by a property function no condition here can evaluate.
        _temp.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><Pinned>3.0.0</Pinned></PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Workspaces" Version="4.14.0" />
                <PackageVersion Include="Json" Version="1.0.0" />
                <PackageVersion Include="Gone" Version="1.0.0" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Update="Workspaces" Version="5.6.0" Condition="$(MSBuildProjectDirectory.Contains('benchmarks'))" />
                <PackageVersion Update="Json" Version="$(Pinned)" />
                <PackageVersion Update="NeverIncluded" Version="9.9.9" />
                <PackageVersion Remove="Gone" />
              </ItemGroup>
            </Project>
            """);
        var project = Load("Lib", "<TargetFramework>net10.0</TargetFramework>");

        var versions = CentralPackageVersions.Load(project);

        Assert.Equal("4.14.0", versions["Workspaces"]);
        Assert.Equal("3.0.0", versions["Json"]);
        Assert.False(versions.ContainsKey("NeverIncluded"));
        Assert.False(versions.ContainsKey("Gone"));
    }

    [Fact]
    public void A_nested_Directory_Packages_props_that_imports_the_root_one_gets_both_files_versions()
    {
        // Refit's TestingFrameworks/XUnit/Directory.Packages.props.
        _temp.Write("src/Directory.Packages.props", """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup><PackageVersion Include="Shared" Version="1.2.3" /></ItemGroup>
            </Project>
            """);
        _temp.Write("src/examples/XUnit/Directory.Packages.props", """
            <Project>
              <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Packages.props', '$(MSBuildThisFileDirectory)../'))" />
              <ItemGroup><PackageVersion Include="xunit.v3" Version="4.0.2" /></ItemGroup>
            </Project>
            """);
        var path = _temp.Write("src/examples/XUnit/XUnitTests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="xunit.v3" /><PackageReference Include="Shared" /></ItemGroup>
            </Project>
            """);

        var versions = CentralPackageVersions.Load(ProjectParser.Load(path));

        Assert.Equal("1.2.3", versions["Shared"]);
        Assert.Equal("4.0.2", versions["xunit.v3"]);
    }

    [Fact]
    public void VersionOverride_wins_over_the_central_version_in_a_restore()
    {
        _temp.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup><PackageVersion Include="Roslyn" Version="4.14.0" /><PackageVersion Include="Json" Version="13.0.4" /></ItemGroup>
            </Project>
            """);
        var project = Load(
            "Generator",
            "<TargetFramework>netstandard2.0</TargetFramework><RoslynVersion>4.8.0</RoslynVersion>",
            """<PackageReference Include="Roslyn" VersionOverride="$(RoslynVersion)" /><PackageReference Include="Json" />""");
        var diagnostics = new List<BuildDiagnostic>();

        var dependencies = ProjectPackageReferences.Read(project, diagnostics);

        Assert.Empty(diagnostics);
        Assert.Equal("4.8.0", dependencies.Single(d => d.Id == "Roslyn").Range.MinVersion?.ToNormalizedString());
        Assert.Equal("13.0.4", dependencies.Single(d => d.Id == "Json").Range.MinVersion?.ToNormalizedString());
    }

    [Theory]
    [InlineData("<VersionPrefix>1.0.0</VersionPrefix><VersionSuffix>beta08</VersionSuffix>", "1.0.0-beta08")]
    [InlineData("", "1.0.0")]
    public void A_reference_to_the_project_s_own_Version_gets_the_SDK_s_version(string properties, string expected)
    {
        // StronglyTypedId's NuGet integration tests: <PackageReference Include="StronglyTypedId" Version="$(Version)" />.
        var project = Load(
            "IntegrationTests",
            "<TargetFramework>net10.0</TargetFramework>" + properties,
            """<PackageReference Include="StronglyTypedId" Version="$(Version)" />""");
        var diagnostics = new List<BuildDiagnostic>();

        var dependency = Assert.Single(ProjectPackageReferences.Read(project, diagnostics));

        Assert.Empty(diagnostics);
        Assert.Equal(expected, dependency.Range.MinVersion?.ToNormalizedString());
    }

    /// <summary>Writes and loads a project named <paramref name="name"/> with these properties and items.</summary>
    private ProjectInfo Load(string name, string properties, string items = "") =>
        ProjectParser.Load(_temp.Write($"{name}/{name}.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>{properties}</PropertyGroup>
              <ItemGroup>{items}</ItemGroup>
            </Project>
            """));
}
