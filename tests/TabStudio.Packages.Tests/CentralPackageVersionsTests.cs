using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

/// <summary>Central package management as MSBuild evaluates Directory.Packages.props, from Refit's and Terminal.Gui's.</summary>
public sealed class CentralPackageVersionsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

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
