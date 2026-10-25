using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Finding Python and Node.js projects in a workspace.</summary>
public sealed class ToolchainProjectDetectorTests : IDisposable
{
    private readonly TempDirectory _workspace = new();

    private static ToolchainCatalog Catalog => ToolchainCatalog.Default;

    [Fact]
    public void A_folder_of_Python_files_is_a_Python_project()
    {
        _workspace.Write("hello.py", "print('hi')\n");

        var project = Assert.Single(ToolchainProjectDetector.Detect(_workspace.Path, Catalog));

        Assert.Equal("python", project.Toolchain.Id);
        Assert.Equal(_workspace.Path, project.Directory);
        Assert.Null(project.Marker);
    }

    [Theory]
    [InlineData("requirements.txt")]
    [InlineData("pyproject.toml")]
    public void A_Python_project_file_makes_its_folder_a_project(string marker)
    {
        _workspace.Write($"tool/{marker}");
        _workspace.Write("tool/src/tool/__init__.py");

        var project = Assert.Single(ToolchainProjectDetector.Detect(_workspace.Path, Catalog));

        Assert.Equal("python", project.Toolchain.Id);
        Assert.Equal(_workspace.Resolve("tool"), project.Directory);
        Assert.Equal(marker, project.Marker);
        Assert.Equal(_workspace.Resolve($"tool/{marker}"), project.MarkerPath);
    }

    [Fact]
    public void Finds_every_project_of_a_workspace_and_not_their_dependencies()
    {
        _workspace.Write("web/package.json", """{ "name": "web" }""");
        _workspace.Write("web/node_modules/left-pad/package.json", """{ "name": "left-pad" }""");
        _workspace.Write("web/lib/util/package.json", """{ "name": "util" }""");
        _workspace.Write("scripts/clean.py");
        _workspace.Write("scripts/.venv/lib/site.py");
        _workspace.Write("notes/readme.md");

        var projects = ToolchainProjectDetector.Detect(_workspace.Path, Catalog);

        Assert.Equal(
            [("node", _workspace.Resolve("web")), ("node", _workspace.Resolve("web/lib/util")), ("python", _workspace.Resolve("scripts"))],
            projects.Select(p => (p.Toolchain.Id, p.Directory)).OrderBy(p => p.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void Lists_only_toolchains_the_store_offers()
    {
        _workspace.Write("Cargo.toml", "[package]\nname = \"demo\"\n");
        _workspace.Write("src/main.rs", "fn main() {}\n");

        Assert.Empty(ToolchainProjectDetector.Detect(_workspace.Path, Catalog));
    }

    [Fact]
    public void A_file_belongs_to_the_nearest_project_that_runs_it()
    {
        _workspace.Write("package.json", "{}");
        _workspace.Write("tools/gen.py");
        var projects = ToolchainProjectDetector.Detect(_workspace.Path, Catalog);

        Assert.Equal("python", ToolchainProjectDetector.ForFile(projects, _workspace.Resolve("tools/gen.py"))?.Toolchain.Id);
        Assert.Equal("node", ToolchainProjectDetector.ForFile(projects, _workspace.Resolve("tools/build.mjs"))?.Toolchain.Id);
        Assert.Null(ToolchainProjectDetector.ForFile(projects, _workspace.Resolve("tools/readme.md")));
    }

    public void Dispose() => _workspace.Dispose();
}
