using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;

namespace TabStudio.Packages.Tests;

/// <summary>.NET Framework projects, which compile with .NET: the packages .NET forwards their framework's types to.</summary>
public sealed class NetFrameworkProjectTests : IDisposable
{
    private readonly RestoreFixture _fixture = new();

    [Fact]
    public async Task A_NET_Framework_library_that_uses_SqlClient_gets_the_package_NET_forwards_it_to()
    {
        // Dapper.EntityFramework (net461) tests for System.Data.SqlClient.SqlParameter, which .NET Framework's System.Data
        // has and .NET's System.Data forwards to the System.Data.SqlClient package: without it, CS1069.
        _fixture.Add(new TestPackage("System.Data.SqlClient", "4.9.0").WithFile(
            "lib/net8.0/System.Data.SqlClient.dll",
            TestPackage.CompileLibrary("System.Data.SqlClient", "namespace System.Data.SqlClient { public sealed class SqlParameter { public string UdtTypeName { get; set; } = \"\"; } }")));
        var path = _fixture.Write("Handlers/Handlers.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net461</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);
        _fixture.Write("Handlers/GeographyHandler.cs", """
            using System.Data.SqlClient;

            namespace Handlers
            {
                public static class GeographyHandler
                {
                    public static void SetValue(object parameter)
                    {
                        if (parameter is SqlParameter sqlParameter)
                        {
                            sqlParameter.UdtTypeName = "geography";
                        }
                    }
                }
            }
            """);
        var project = ProjectParser.Load(path);

        var result = await _fixture.CreateRestorer().RestoreAsync(project);
        var build = await BuildAsync(project, result);

        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        Assert.Equal("System.Data.SqlClient", Assert.Single(result.Packages).Id);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
    }

    [Fact]
    public async Task A_NET_Framework_library_that_does_not_use_SqlClient_restores_nothing_more()
    {
        // Nothing to download, so a project without packages still builds offline.
        var path = _fixture.Write("Plain/Plain.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net48</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        _fixture.Write("Plain/Code.cs", "namespace Plain { public static class Code { public static int Answer => 42; } }");

        var result = await _fixture.CreateRestorer().RestoreAsync(ProjectParser.Load(path));

        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        Assert.Empty(result.Packages);
    }

    public void Dispose() => _fixture.Dispose();

    /// <summary>Builds a project against what a restore gave it, as the IDE's language service does.</summary>
    private static async Task<BuildResult> BuildAsync(ProjectInfo project, RestoreResult restore)
    {
        using var host = new RoslynWorkspaceHost();
        host.Load(WorkspaceLoader.Load(project.FilePath), FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies, restore.UnrestoredReferences);
        return await new ProjectBuilder(host).BuildAsync(host.FindProject(project.FilePath)!);
    }
}
