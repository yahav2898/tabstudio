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

    [Theory]
    [InlineData("docs")]
    [InlineData("doc")]
    [InlineData("Documentation")]
    public void Documentation_folders_are_not_projects_of_the_workspace(string folder)
    {
        _workspace.Write("app/package.json", """{ "name": "app", "scripts": { "start": "node index.js" } }""");
        // A Sphinx project: its Makefile, conf.py and a docs site's package.json build documentation.
        _workspace.Write($"{folder}/Makefile", "SPHINXBUILD ?= sphinx-build\nhtml:\n\t$(SPHINXBUILD) -M html . _build\n");
        _workspace.Write($"{folder}/conf.py", "project = 'app'\n");
        _workspace.Write($"{folder}/package.json", """{ "name": "docs", "scripts": { "start": "docusaurus start" } }""");
        _workspace.Write($"{folder}/guide/extension.c", "int main(void) { return 0; }\n");

        var projects = ToolchainProjectDetector.Detect(_workspace.Path, Catalog);

        var project = Assert.Single(projects);
        Assert.Equal(("node", _workspace.Resolve("app")), (project.Toolchain.Id, project.Directory));
    }

    [Fact]
    public void A_web_app_s_wwwroot_is_not_a_project()
    {
        _workspace.Write("Web/Web.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        _workspace.Write("Web/wwwroot/package.json", """{ "name": "assets", "scripts": { "dev": "vite" } }""");
        _workspace.Write("Web/wwwroot/js/site.js", "console.log('hi');\n");
        _workspace.Write("Web/wwwroot/lib/jquery/package.json", """{ "name": "jquery", "main": "dist/jquery.js" }""");

        Assert.Empty(ToolchainProjectDetector.Detect(_workspace.Path, Catalog));
    }

    [Fact]
    public void A_documentation_folder_opened_as_the_workspace_is_still_searched()
    {
        var docs = _workspace.Resolve("docs");
        _workspace.Write("docs/package.json", """{ "name": "docs", "scripts": { "start": "docusaurus start" } }""");

        var project = Assert.Single(ToolchainProjectDetector.Detect(docs, Catalog));

        Assert.Equal(("node", docs), (project.Toolchain.Id, project.Directory));
    }

    [Fact]
    public void Lists_only_toolchains_the_store_offers()
    {
        _workspace.Write("pom.xml", "<project><artifactId>demo</artifactId></project>\n");
        _workspace.Write("src/Main.java", "class Main {}\n");
        var java = Catalog.Find("java")!;
        var hiddenCatalog = Catalog with { Toolchains = [java with { Listed = false }] };

        Assert.Equal("java", Assert.Single(ToolchainProjectDetector.Detect(_workspace.Path, Catalog)).Toolchain.Id);
        Assert.Empty(ToolchainProjectDetector.Detect(_workspace.Path, hiddenCatalog));
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
