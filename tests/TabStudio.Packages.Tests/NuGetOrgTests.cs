using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

/// <summary>
/// End-to-end tests against the real nuget.org: restore, build with the workspace host and run in-process.
/// They need the internet, which CI has; without it they fail and say so.
/// </summary>
public sealed class NuGetOrgTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };

    [Fact]
    public async Task Restores_builds_and_runs_a_project_that_uses_Humanizer_and_Newtonsoft_Json()
    {
        var projectPath = _temp.Write("Hello/Hello.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Humanizer.Core" Version="2.14.1" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Hello/Program.cs", """
            using Humanizer;
            using Newtonsoft.Json;

            Console.WriteLine("hello world".Humanize(LetterCasing.Title));
            Console.WriteLine(JsonConvert.SerializeObject(new { Name = "tablet", Cores = 8 }));
            """);

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        var restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = _temp.Resolve("packages"),
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            _http);

        var restore = await restorer.RestoreAsync(project);

        AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));
        Assert.Contains(restore.Packages, p => p.Id == "Humanizer.Core" && p.Version == "2.14.1" && p.IsDirect);
        Assert.Contains(restore.Packages, p => p.Id == "Newtonsoft.Json" && p.Version == "13.0.3" && p.IsDirect);
        Assert.Contains(restore.CompileAssemblies, path => Path.GetFileName(path) == "Humanizer.dll");
        Assert.Contains(restore.RuntimeAssemblies, path => Path.GetFileName(path) == "Newtonsoft.Json.dll");

        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        var build = await new ProjectBuilder(host).BuildAsync(project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, restore.RuntimeAssemblies, [], console);

        Assert.Empty(console.Error);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("Hello World\n{\"Name\":\"tablet\",\"Cores\":8}\n", console.Output);

        var again = await restorer.RestoreAsync(project);
        Assert.True(again.FromCache);
        Assert.Equal(restore.CompileAssemblies, again.CompileAssemblies);
    }

    [Fact]
    public async Task Restores_a_package_with_transitive_dependencies_and_prunes_the_framework_ones()
    {
        // Microsoft.Extensions.Logging.Abstractions 8.0.0 depends on Microsoft.Extensions.DependencyInjection.Abstractions,
        // and, for older frameworks, on System.* packages that .NET 10 provides itself.
        var projectPath = _temp.Write("Logging/Logging.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);
        var restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = _temp.Resolve("packages"),
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            _http);

        var restore = await restorer.RestoreAsync(ProjectParser.Load(projectPath));

        AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));
        Assert.Contains(restore.Packages, p => p.Id == "Microsoft.Extensions.DependencyInjection.Abstractions" && !p.IsDirect);
        Assert.DoesNotContain(restore.Packages, p => p.Id.StartsWith("System.", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _http.Dispose();
        _temp.Dispose();
    }

    internal static void AssertNuGetOrgReachable(RestoreResult restore)
    {
        var unreachable = restore.Diagnostics.FirstOrDefault(d => d.Id == PackageDiagnosticIds.NetworkUnreachable);
        if (unreachable is not null)
        {
            Assert.Fail($"These tests restore from nuget.org, which this machine cannot reach. Run them with internet access (CI has it). {unreachable.Message}");
        }
    }
}
