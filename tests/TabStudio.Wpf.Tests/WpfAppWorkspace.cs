using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;
using TabStudio.Web.WebAssembly;
using TabStudio.Wpf.Browser;
using TabStudio.Wpf.Building;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// A workspace of WPF projects as the IDE has it once it is open: every project restored from nuget.org, a WPF project
/// with OpenSilver, loaded into a workspace host that builds WPF projects with OpenSilver's XAML compiler.
/// </summary>
internal sealed class WpfAppWorkspace : IDisposable
{
    private readonly NuGetFixture _nuget;

    private WpfAppWorkspace(NuGetFixture nuget, Workspace workspace, RoslynWorkspaceHost host)
    {
        _nuget = nuget;
        Workspace = workspace;
        Host = host;
    }

    public Workspace Workspace { get; }

    public RoslynWorkspaceHost Host { get; }

    public static async Task<WpfAppWorkspace> LoadAsync(NuGetFixture nuget, string path)
    {
        var workspace = WorkspaceLoader.Load(path);
        var host = new RoslynWorkspaceHost();
        host.SetWpfXamlCompiler(new OpenSilverWpfCompiler(nuget.FrameworkReferenceDirectory));
        host.Load(workspace, FileSystemTextProvider.Instance);
        foreach (var project in workspace.Projects)
        {
            var restore = await nuget.Restorer.RestoreAsync(WpfProject.UsesWpf(project) ? OpenSilverPackages.WithOpenSilver(project) : project);
            Assert.True(restore.Success, $"{project.Name} did not restore:\n{string.Join('\n', restore.Diagnostics)}");
            host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
            host.SetAnalyzers(project.FilePath, restore.Analyzers);
        }

        return new WpfAppWorkspace(nuget, workspace, host);
    }

    public ProjectInfo Project(string name) => Workspace.Projects.Single(p => p.Name == name);

    /// <summary>Builds a project, failing the test on an error.</summary>
    public async Task<BuildResult> BuildAsync(string name)
    {
        var build = await TryBuildAsync(name);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error)));
        return build;
    }

    /// <summary>Builds a project, errors or not.</summary>
    public Task<BuildResult> TryBuildAsync(string name) => new ProjectBuilder(Host).BuildAsync(Project(name));

    /// <summary>Builds a WPF app, gets it ready as the IDE does and serves it.</summary>
    public async Task<WebAssemblyAppHost> StartAsync(string name)
    {
        var build = await BuildAsync(name);
        var prepared = await new WpfAppPreparer(_nuget.Restorer).PrepareAsync(Project(name), build, ReferenceAssemblies.Net100);
        Assert.True(prepared.App is not null, string.Join('\n', prepared.Diagnostics));
        return await WebAssemblyAppHost.StartAsync(prepared.App!);
    }

    public void Dispose() => Host.Dispose();
}
