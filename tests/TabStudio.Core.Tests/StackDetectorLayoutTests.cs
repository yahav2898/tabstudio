using TabStudio.Core.Detection;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>
/// What the GitHub preview's "Detected" row and the Compatibility Doctor see in real repository layouts: a monorepo,
/// a Python package in <c>src</c>, a solution with its tests beside it, MSBuild property files, and static sites.
/// </summary>
public sealed class StackDetectorLayoutTests : IDisposable
{
    private readonly TempDirectory _repo = new();

    [Fact]
    public void A_monorepo_with_several_package_json_files_is_Node_with_its_frameworks_and_no_static_site()
    {
        _repo.Write("package.json", """{ "name": "mono", "private": true, "workspaces": [ "packages/*" ], "devDependencies": { "typescript": "^5.9.3" } }""");
        _repo.Write("packages/web/package.json", """{ "name": "web", "scripts": { "dev": "vite" }, "dependencies": { "react": "^19.1.0" }, "devDependencies": { "vite": "^7.0.0" } }""");
        _repo.Write("packages/web/index.html", "<!DOCTYPE html><div id=\"root\"></div>");
        _repo.Write("packages/api/package.json", """{ "name": "api", "main": "index.js" }""");
        _repo.Write("docs/index.html", "<!DOCTYPE html><h1>Docs</h1>");

        var stack = StackDetector.Detect(_repo.Path);

        Assert.Equal("package.json", stack.Get(StackKind.Node)?.Evidence);
        Assert.Equal("19.1.0", stack.Get(StackKind.React)?.Version);
        Assert.NotNull(stack.Get(StackKind.Vite));
        Assert.Equal("5.9.3", stack.Get(StackKind.TypeScript)?.Version);
        Assert.Null(stack.Get(StackKind.StaticSite));
        Assert.Null(stack.Get(StackKind.DotNet));
    }

    [Fact]
    public void A_Python_package_in_src_is_Python_from_its_pyproject()
    {
        _repo.Write("pyproject.toml", "[project]\nname = \"greeter\"\n");
        _repo.Write("src/greeter/__init__.py");
        _repo.Write("src/greeter/__main__.py");
        _repo.Write("tests/test_greeter.py");

        var stack = StackDetector.Detect(_repo.Path);

        Assert.Equal("pyproject.toml", stack.Get(StackKind.Python)?.Evidence);
        Assert.Equal([StackKind.Python], stack.Components.Select(c => c.Kind));
    }

    [Fact]
    public void A_solution_at_the_root_with_tests_beside_it_is_dotnet_from_the_solution_and_its_docs_are_no_site()
    {
        _repo.Write("Weather.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        _repo.Write("src/Weather/Weather.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        _repo.Write("tests/Weather.Tests/Weather.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        _repo.Write("Directory.Build.props", "<Project />");
        _repo.Write("docs/index.html", "<!DOCTYPE html><h1>Weather</h1>");

        var stack = StackDetector.Detect(_repo.Path);

        Assert.Equal("Weather.sln", stack.Get(StackKind.DotNet)?.Evidence);
        Assert.Null(stack.Get(StackKind.StaticSite));
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Build.targets")]
    public void MSBuild_property_files_are_dotnet_when_the_projects_are_deeper_than_the_scan(string file)
    {
        _repo.Write(file, "<Project />");
        _repo.Write("src/apps/desktop/shell/Shell.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var dotnet = StackDetector.Detect(_repo.Path).Get(StackKind.DotNet);

        Assert.Equal(file, dotnet?.Evidence);
    }

    [Fact]
    public void An_index_html_with_no_project_is_a_static_site()
    {
        _repo.Write("index.html", "<!DOCTYPE html><h1>Hi</h1>");
        _repo.Write("styles.css", "h1 { color: teal; }");
        _repo.Write("script.js", "console.log('hi');");

        var stack = StackDetector.Detect(_repo.Path);

        Assert.Equal("index.html", stack.Get(StackKind.StaticSite)?.Evidence);
        Assert.Equal("Static web site", stack.Get(StackKind.StaticSite)?.DisplayName);
        Assert.Equal([StackKind.StaticSite], stack.Components.Select(c => c.Kind));
    }

    [Fact]
    public void A_site_in_docs_with_a_package_json_for_tools_only_is_still_a_static_site()
    {
        _repo.Write("docs/index.html", "<!DOCTYPE html><h1>Docs</h1>");
        _repo.Write("package.json", """{ "name": "site", "devDependencies": { "prettier": "^3.6.0" }, "scripts": { "format": "prettier -w ." } }""");

        var stack = StackDetector.Detect(_repo.Path);

        Assert.Equal(Path.Combine("docs", "index.html"), stack.Get(StackKind.StaticSite)?.Evidence);
        Assert.NotNull(stack.Get(StackKind.Node));
        Assert.Equal(StaticSiteDetector.FindIndex(_repo.Path), _repo.Resolve("docs/index.html"));
    }

    [Theory]
    [InlineData("""{ "name": "app", "main": "server.js" }""")]
    [InlineData("""{ "name": "app", "scripts": { "start": "node server.js" } }""")]
    [InlineData("""{ "name": "app", "scripts": { "dev": "vite" } }""")]
    public void An_index_html_that_a_Node_package_runs_or_builds_is_no_static_site(string packageJson)
    {
        _repo.Write("index.html", "<!DOCTYPE html><div id=\"app\"></div>");
        _repo.Write("package.json", packageJson);

        var stack = StackDetector.Detect(_repo.Path);

        Assert.Null(stack.Get(StackKind.StaticSite));
        Assert.True(StaticSiteDetector.IsRunByNode(_repo.Resolve("package.json")));
    }

    [Fact]
    public void A_build_output_folder_is_not_the_site()
    {
        _repo.Write("dist/index.html", "<!DOCTYPE html>");
        _repo.Write("build/index.html", "<!DOCTYPE html>");

        Assert.Null(StaticSiteDetector.FindIndex(_repo.Path));
        Assert.Null(StackDetector.Detect(_repo.Path).Get(StackKind.StaticSite));
    }

    public void Dispose() => _repo.Dispose();
}
