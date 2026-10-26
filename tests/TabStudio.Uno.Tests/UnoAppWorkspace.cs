using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;
using TabStudio.Testing;
using TabStudio.Uno.Hosting;
using TabStudio.Uno.Templates;

namespace TabStudio.Uno.Tests;

/// <summary>
/// An app created from one of the templates as the IDE has it once it is open: restored from nuget.org and loaded
/// into a workspace host with its packages and Uno's XAML generator, ready to build and run.
/// </summary>
internal sealed class UnoAppWorkspace : IDisposable
{
    private UnoAppWorkspace(Workspace workspace, RoslynWorkspaceHost host, ProjectInfo project, RestoreResult restore)
    {
        Workspace = workspace;
        Host = host;
        Project = project;
        Restore = restore;
    }

    public Workspace Workspace { get; }

    public RoslynWorkspaceHost Host { get; }

    public ProjectInfo Project { get; }

    public RestoreResult Restore { get; }

    public UnoRuntimeAssets Assets => UnoRuntimeAssets.From(Restore.RuntimeAssemblies);

    /// <summary>Creates an app from a template in a solution folder, restores and loads it.</summary>
    public static async Task<UnoAppWorkspace> CreateAsync(NuGetFixture nuget, TempDirectory folder, string templateId, string name)
    {
        var projectFile = UnoTemplates.Create(templateId, folder.Path, name);
        var solution = folder.Write(name + ".slnx", $"<Solution>\n  <Project Path=\"{Path.GetRelativePath(folder.Path, projectFile).Replace('\\', '/')}\" />\n</Solution>\n");
        return await LoadAsync(nuget, solution, name);
    }

    /// <summary>Loads and restores a solution, as opening it in the IDE does.</summary>
    public static async Task<UnoAppWorkspace> LoadAsync(NuGetFixture nuget, string solution, string name)
    {
        var workspace = WorkspaceLoader.Load(solution);
        var project = workspace.Projects.Single(p => p.Name == name);
        var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        var restore = await nuget.Restorer.RestoreAsync(project);
        Assert.True(restore.Success, $"{project.Name} did not restore:\n{string.Join('\n', restore.Diagnostics)}");
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        host.SetAnalyzers(project.FilePath, restore.Analyzers);
        return new UnoAppWorkspace(workspace, host, project, restore);
    }

    public string PathOf(string relativePath) => Path.Combine(Project.DirectoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Builds the app, failing the test on an error.</summary>
    public async Task<BuildResult> BuildAsync()
    {
        var build = await new ProjectBuilder(Host).BuildAsync(Project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error)));
        return build;
    }

    /// <summary>Changes a file on disk and in the workspace, as saving it in the editor does.</summary>
    public void Save(string relativePath, string text)
    {
        var path = PathOf(relativePath);
        File.WriteAllText(path, text);
        Host.UpdateText(path, text);
    }

    public string Read(string relativePath) => File.ReadAllText(PathOf(relativePath));

    public void Dispose() => Host.Dispose();
}
