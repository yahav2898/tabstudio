using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

/// <summary>Shared frameworks flow from referenced projects, as in the .NET SDK.</summary>
public sealed class FrameworkReferenceFlowTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void A_console_tool_that_references_a_web_project_gets_aspnet_core()
    {
        // As in a SaaS repository: a console tool referencing the API project, which uses the Web SDK.
        _temp.Write("Api/Api.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="../Data/Data.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("Data/Data.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var tool = ProjectParser.Load(_temp.Write("Tool/Tool.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="../Api/Api.csproj" /></ItemGroup>
            </Project>
            """));

        Assert.Empty(tool.FrameworkReferences);
        Assert.Equal([ProjectParser.AspNetCoreFramework], ProjectPackageReferences.FrameworkReferencesWithReferencedProjects(tool));
    }

    [Fact]
    public void A_project_without_references_keeps_its_own_frameworks_only()
    {
        var library = ProjectParser.Load(_temp.Write("Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """));

        Assert.Empty(ProjectPackageReferences.FrameworkReferencesWithReferencedProjects(library));
    }
}
