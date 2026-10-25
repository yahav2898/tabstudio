using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>
/// What setting a repository up plans from real layouts: a Python package in <c>src</c>, a Node.js monorepo, lock
/// files, and the fingerprint that sets a project up again only when its manifest changes.
/// </summary>
public sealed class ProjectSetupPlanTests : IDisposable
{
    private readonly TempDirectory _repo = new();

    private static ToolchainDefinition Python => ToolchainCatalog.Default.Find("python")!;

    private static ToolchainDefinition Node => ToolchainCatalog.Default.Find("node")!;

    [Fact]
    public void A_Python_package_in_src_is_one_project_that_installs_editable_and_runs_as_a_module()
    {
        _repo.Write("pyproject.toml", "[project]\nname = \"greeter\"\nversion = \"1.0\"\n");
        _repo.Write("src/greeter/__init__.py");
        _repo.Write("src/greeter/__main__.py", "print('hi')\n");
        _repo.Write("tests/test_greeter.py", "def test_it():\n    pass\n");

        var project = Assert.Single(ToolchainProjectDetector.Detect(_repo.Path, ToolchainCatalog.Default));
        var plan = RunPlanner.PlanProject(project);
        var dependency = RunPlanner.DependenciesOf(project);

        Assert.Equal("python", project.Toolchain.Id);
        Assert.Equal(_repo.Path, project.Directory);
        Assert.NotNull(plan);
        Assert.Equal(["-u", "-m", "greeter"], plan.Run.Arguments);
        Assert.Equal("src", plan.Run.Environment["PYTHONPATH"]);
        Assert.Equal("src/greeter/__main__.py", plan.EntryPoint);
        Assert.Equal("pip install -e .", dependency?.Name);
        Assert.True(RunPlanner.IsPending(project, dependency!));

        // The virtual environment comes first, then the editable install into it.
        var steps = RunPlanner.PlanDependencies(project, dependency!);
        Assert.Equal(2, steps.Count);
        Assert.Contains("venv", steps[0].Arguments);
        Assert.EndsWith(Path.Combine(".venv", "bin", "python"), steps[1].Program, StringComparison.Ordinal);
        Assert.Equal(["-m", "pip", "install", "--disable-pip-version-check", "-e", "."], steps[1].Arguments);
    }

    [Fact]
    public void A_wildcard_entry_takes_the_first_folder_in_name_order_that_has_the_file()
    {
        _repo.Write("src/zeta/__main__.py");
        _repo.Write("src/alpha/__init__.py");
        _repo.Write("src/beta/__main__.py");

        var entry = RunPlanner.FindEntry(_repo.Path, "src/*/__main__.py");

        Assert.NotNull(entry);
        Assert.Equal("beta", entry.Match);
        Assert.Equal("src/beta/__main__.py", entry.RelativePath);
        Assert.Equal(_repo.Resolve("src/beta/__main__.py"), entry.FullPath);
        Assert.Null(RunPlanner.FindEntry(_repo.Path, "lib/*/__main__.py"));
    }

    [Fact]
    public void A_monorepo_with_a_root_package_is_one_Node_project_and_separate_packages_are_one_each()
    {
        _repo.Write("package.json", """{ "name": "mono", "private": true, "workspaces": [ "packages/*" ] }""");
        _repo.Write("packages/web/package.json", """{ "name": "web", "scripts": { "dev": "vite" } }""");
        _repo.Write("packages/api/package.json", """{ "name": "api", "main": "index.js" }""");
        using var apart = new TempDirectory();
        apart.Write("frontend/package.json", """{ "name": "frontend", "dependencies": { "left-pad": "1.3.0" } }""");
        apart.Write("backend/package.json", """{ "name": "backend", "main": "server.js" }""");

        var mono = Assert.Single(ToolchainProjectDetector.Detect(_repo.Path, ToolchainCatalog.Default));
        var projects = ToolchainProjectDetector.Detect(apart.Path, ToolchainCatalog.Default);

        Assert.Equal(_repo.Path, mono.Directory);
        Assert.Equal([apart.Resolve("backend"), apart.Resolve("frontend")], projects.Select(p => p.Directory).Order(StringComparer.Ordinal));
        Assert.All(projects, p => Assert.Equal("node", p.Toolchain.Id));
    }

    [Fact]
    public void A_lock_file_installs_the_exact_packages_with_npm_ci()
    {
        _repo.Write("package.json", """{ "name": "app", "dependencies": { "left-pad": "1.3.0" } }""");
        var project = new ToolchainProject(Node, _repo.Path, "package.json");
        Assert.Equal("npm install", RunPlanner.DependenciesOf(project)?.Name);

        _repo.Write("package-lock.json", """{ "name": "app", "lockfileVersion": 3, "packages": {} }""");
        var locked = RunPlanner.DependenciesOf(project)!;
        var unlocked = RunPlanner.DependenciesOf(project, useLockFile: false)!;

        Assert.Equal("npm ci", locked.Name);
        Assert.Equal(["npm", "ci", "--no-fund", "--no-audit"], locked.Command);
        Assert.Equal("package.json", locked.Manifest);
        Assert.Equal("node_modules", locked.Installed);
        Assert.Equal("npm install", unlocked.Name);
        Assert.Equal(["ci", "--no-fund", "--no-audit"], RunPlanner.PlanDependencies(project, locked).Single().Arguments);
    }

    [Fact]
    public void The_fingerprint_changes_with_the_manifest_or_its_lock_file_and_only_then()
    {
        _repo.Write("package.json", """{ "name": "app", "dependencies": { "left-pad": "1.3.0" } }""");
        _repo.Write("index.js", "console.log(1);\n");
        var project = new ToolchainProject(Node, _repo.Path, "package.json");
        var dependency = RunPlanner.DependenciesOf(project)!;

        var first = RunPlanner.FingerprintOf(project, dependency);
        _repo.Write("index.js", "console.log(2);\n");
        var unchanged = RunPlanner.FingerprintOf(project, dependency);
        _repo.Write("package-lock.json", "{}");
        var locked = RunPlanner.FingerprintOf(project, dependency);
        _repo.Write("package.json", """{ "name": "app", "dependencies": { "left-pad": "1.3.1" } }""");
        var changed = RunPlanner.FingerprintOf(project, dependency);

        Assert.Equal(64, first.Length);
        Assert.Equal(first, unchanged);
        Assert.NotEqual(first, locked);
        Assert.NotEqual(locked, changed);
    }

    public void Dispose() => _repo.Dispose();
}
