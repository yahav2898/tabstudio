using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

/// <summary>
/// End-to-end tests of web projects against the real nuget.org: restore ASP.NET Core, build a minimal API with the
/// workspace host, serve it in-process and call it over HTTP.
/// </summary>
public sealed class AspNetCoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };

    [Fact]
    public async Task Restores_builds_serves_and_stops_a_minimal_API()
    {
        var projectPath = _temp.Write("HelloWeb/HelloWeb.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("HelloWeb/Program.cs", """
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.MapGet("/", () => $"Hello from {app.Environment.ApplicationName} in {app.Environment.EnvironmentName}");
            app.MapGet("/sum/{a:int}/{b:int}", (int a, int b) => new { Sum = a + b });
            app.MapGet("/greeting", (IConfiguration configuration) => configuration["Greeting"]);

            app.Run();
            """);
        _temp.Write("HelloWeb/appsettings.json", """{ "Greeting": "Shalom" }""");

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        Assert.True(project.IsExecutable);

        var restore = await CreateRestorer().RestoreAsync(project);

        NuGetOrgTests.AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));
        var framework = Assert.Single(restore.Frameworks);
        Assert.Equal(ProjectParser.AspNetCoreFramework, framework.Name);
        Assert.StartsWith("10.", framework.Version, StringComparison.Ordinal);
        Assert.Contains(restore.CompileAssemblies, path => Path.GetFileName(path) == "Microsoft.AspNetCore.dll");
        Assert.Contains(restore.RuntimeAssemblies, path => Path.GetFileName(path) == "Microsoft.AspNetCore.Server.Kestrel.Core.dll");

        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        var build = await new ProjectBuilder(host).BuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        var console = new CapturingConsole();
        await using var web = await WebAppHost.StartAsync(build.EntryAssembly!, build.Dependencies, restore.RuntimeAssemblies, project.DirectoryPath, console);
        Assert.True(web.IsRunning, console.Output + console.Error);

        Assert.Equal("Hello from HelloWeb in Development", await _http.GetStringAsync(web.Address));
        Assert.Equal("""{"sum":5}""", await _http.GetStringAsync(new Uri(web.Address, "sum/2/3")));
        Assert.Equal("Shalom", await _http.GetStringAsync(new Uri(web.Address, "greeting")));

        var run = await web.StopAsync();

        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.False(web.IsRunning);
        Assert.Contains($"Now listening on: {web.Address.GetLeftPart(UriPartial.Authority)}", console.Output, StringComparison.Ordinal);
        Assert.Contains("Application is shutting down", console.Output, StringComparison.Ordinal);
        await Assert.ThrowsAsync<HttpRequestException>(() => _http.GetStringAsync(web.Address));
    }

    [Fact]
    public async Task A_web_app_that_crashes_at_startup_reports_the_crash()
    {
        var projectPath = _temp.Write("Broken/Broken.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("Broken/Program.cs", """
            var app = WebApplication.CreateBuilder(args).Build();
            throw new InvalidOperationException("The database is missing.");
            """);

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        var restore = await CreateRestorer().RestoreAsync(project);
        NuGetOrgTests.AssertNuGetOrgReachable(restore);

        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        var build = await new ProjectBuilder(host).BuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        var console = new CapturingConsole();
        await using var web = await WebAppHost.StartAsync(build.EntryAssembly!, build.Dependencies, restore.RuntimeAssemblies, project.DirectoryPath, console);
        var run = await web.Completion;

        Assert.False(web.IsRunning);
        Assert.Equal(RunOutcome.Crashed, run.Outcome);
        Assert.Contains("The database is missing.", console.Error, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _http.Dispose();
        _temp.Dispose();
    }

    private PackageRestorer CreateRestorer() => new(
        new PackageRestoreOptions
        {
            GlobalPackagesFolder = _temp.Resolve("packages"),
            FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
        },
        _http);
}
