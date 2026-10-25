using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;
using TabStudio.Web.RazorApps;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// A workspace of web projects as the IDE has it once it is open: every project restored from nuget.org and loaded
/// into a workspace host with its packages and analyzers, ready to build, prepare and run.
/// </summary>
internal sealed class WebAppWorkspace : IDisposable
{
    private readonly NuGetFixture _nuget;
    private readonly Dictionary<string, RestoreResult> _restores;

    private WebAppWorkspace(NuGetFixture nuget, Workspace workspace, RoslynWorkspaceHost host, Dictionary<string, RestoreResult> restores)
    {
        _nuget = nuget;
        Workspace = workspace;
        Host = host;
        _restores = restores;
    }

    public Workspace Workspace { get; }

    public RoslynWorkspaceHost Host { get; }

    public static async Task<WebAppWorkspace> LoadAsync(NuGetFixture nuget, string path)
    {
        var workspace = WorkspaceLoader.Load(path);
        var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        var restores = new Dictionary<string, RestoreResult>(StringComparer.Ordinal);
        foreach (var project in workspace.Projects)
        {
            var restore = await nuget.Restorer.RestoreAsync(project);
            Assert.True(restore.Success, $"{project.Name} did not restore:\n{string.Join('\n', restore.Diagnostics)}");
            host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
            host.SetAnalyzers(project.FilePath, restore.Analyzers);
            restores[project.FilePath] = restore;
        }

        return new WebAppWorkspace(nuget, workspace, host, restores);
    }

    public ProjectInfo Project(string name) => Workspace.Projects.Single(p => p.Name == name);

    public IReadOnlyList<string> RuntimeAssemblies(string projectFilePath) =>
        _restores.TryGetValue(projectFilePath, out var restore) ? restore.RuntimeAssemblies : [];

    /// <summary>Builds a project and the projects it references, failing the test on an error.</summary>
    public async Task<BuildResult> BuildAsync(string name)
    {
        var build = await new ProjectBuilder(Host).BuildAsync(Project(name));
        Assert.True(build.Success, string.Join('\n', build.Diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error)));
        return build;
    }

    public RazorAppBuild AppBuild(string name, BuildResult build) => new()
    {
        Project = Project(name),
        Projects = Workspace.Projects,
        Build = build,
        PackageAssemblies = RuntimeAssemblies,
    };

    /// <summary>Builds an ASP.NET Core app, gets it ready as the IDE does and starts it, failing the test if it does not listen.</summary>
    public async Task<RunningWebApp> StartServerAppAsync(string name)
    {
        var project = Project(name);
        var build = await BuildAsync(name);
        var prepared = await new RazorAppPreparer(_nuget.Restorer).PrepareServerAppAsync(AppBuild(name, build));
        Assert.DoesNotContain(prepared.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);

        var console = new CapturingConsole();
        var web = await WebAppHost.StartAsync(
            build.EntryAssembly!,
            build.Dependencies,
            RuntimeAssemblies(project.FilePath),
            project.DirectoryPath,
            console,
            new WebAppOptions { Arguments = prepared.Arguments });
        Assert.True(web.IsRunning, $"{name} did not start:\n{console.Output}\n{console.Error}");
        return new RunningWebApp(web, prepared, console);
    }

    /// <summary>Builds a Blazor WebAssembly app, gets it ready as the IDE does and serves it.</summary>
    public async Task<WebAssemblyAppHost> StartWebAssemblyAppAsync(string name)
    {
        var build = await BuildAsync(name);
        var prepared = await new RazorAppPreparer(_nuget.Restorer).PrepareWebAssemblyAppAsync(AppBuild(name, build));
        Assert.True(prepared.App is not null, string.Join('\n', prepared.Diagnostics));
        return await WebAssemblyAppHost.StartAsync(prepared.App!);
    }

    public void Dispose() => Host.Dispose();
}

/// <summary>An ASP.NET Core app running in the test's process.</summary>
internal sealed class RunningWebApp(WebAppHost host, PreparedServerApp prepared, CapturingConsole console) : IAsyncDisposable
{
    public Uri Address => host.Address;

    public PreparedServerApp Prepared => prepared;

    public CapturingConsole Console => console;

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        prepared.Dispose();
    }
}
