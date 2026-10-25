using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class ProjectTemplatesTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ProjectTemplates _templates;

    public ProjectTemplatesTests()
    {
        _templates = new ProjectTemplates(new AppPaths(_temp.Path));
    }

    [Theory]
    [InlineData("MyApp")]
    [InlineData("Contoso.Tools")]
    [InlineData("App_2")]
    public void Accepts_names_that_are_valid_namespaces(string name) => Assert.Null(ProjectTemplates.Validate(name));

    [Theory]
    [InlineData("")]
    [InlineData("2Fast")]
    [InlineData("My App")]
    [InlineData("App.")]
    [InlineData("a-b")]
    public void Rejects_names_that_are_not_namespaces(string name) => Assert.NotNull(ProjectTemplates.Validate(name));

    [Theory]
    [InlineData(ProjectTemplates.ConsoleId, 1)]
    [InlineData(ProjectTemplates.LibraryId, 1)]
    [InlineData(ProjectTemplates.ConsoleWithLibraryId, 2)]
    public async Task Every_template_creates_a_solution_that_builds(string template, int projects)
    {
        var solution = _templates.Create(template, "Hello");
        var workspace = WorkspaceLoader.Load(solution);
        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);

        Assert.Equal(projects, workspace.Projects.Count);
        foreach (var project in workspace.Projects)
        {
            var build = await new ProjectBuilder(host).BuildAsync(project);
            Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        }
    }

    [Fact]
    public void A_taken_name_gets_a_new_folder()
    {
        var first = _templates.Create(ProjectTemplates.ConsoleId, "Twice");
        var second = _templates.Create(ProjectTemplates.ConsoleId, "Twice");

        Assert.NotEqual(Path.GetDirectoryName(first), Path.GetDirectoryName(second));
    }

    public void Dispose() => _temp.Dispose();
}
