using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>How F5 runs Python and Node.js projects and files, from the catalog's rules.</summary>
public sealed class RunPlannerTests : IDisposable
{
    private readonly TempDirectory _workspace = new();

    private static ToolchainDefinition Python => ToolchainCatalog.Default.Find("python")!;

    private static ToolchainDefinition Node => ToolchainCatalog.Default.Find("node")!;

    [Fact]
    public void Runs_a_Python_file_unbuffered_with_the_system_python()
    {
        var file = _workspace.Write("tools/hello.py");
        var project = new ToolchainProject(Python, _workspace.Path, null);

        var plan = RunPlanner.PlanFile(project, file);

        Assert.Null(plan.Build);
        Assert.Equal("python3", plan.Run.Program);
        Assert.Equal(["-u", "tools/hello.py"], plan.Run.Arguments);
        Assert.Equal(_workspace.Path, plan.Run.WorkingDirectory);
        Assert.Equal("1", plan.Run.Environment["PYTHONUNBUFFERED"]);
        Assert.False(plan.Run.Environment.ContainsKey("VIRTUAL_ENV"));
        Assert.Equal("tools/hello.py", plan.EntryPoint);
    }

    [Fact]
    public void Runs_a_Python_project_in_its_virtual_environment_once_it_has_one()
    {
        _workspace.Write("app.py");
        _workspace.Write("main.py");
        _workspace.Write(".venv/bin/python");
        var project = new ToolchainProject(Python, _workspace.Path, null);

        var plan = RunPlanner.PlanProject(project)!;

        Assert.Equal("main.py", plan.EntryPoint);
        Assert.Equal(_workspace.Resolve(".venv/bin/python"), plan.Run.Program);
        Assert.Equal(["-u", "main.py"], plan.Run.Arguments);
        Assert.Equal($"{_workspace.Resolve(".venv/bin")}:$PATH", plan.Run.Environment["PATH"]);
        Assert.Equal(_workspace.Resolve(".venv"), plan.Run.Environment["VIRTUAL_ENV"]);
    }

    [Fact]
    public void A_Python_package_runs_as_its_main_module()
    {
        _workspace.Write("__main__.py");
        _workspace.Write("main.py");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, null))!;

        Assert.Equal("__main__.py", plan.EntryPoint);
    }

    [Fact]
    public void A_project_without_an_entry_point_has_no_plan()
    {
        _workspace.Write("lib.py");

        Assert.Null(RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, null)));
    }

    [Fact]
    public void A_toolchain_does_not_run_files_of_another_language()
    {
        var file = _workspace.Write("index.js");

        var error = Assert.Throws<ArgumentException>(() => RunPlanner.PlanFile(new ToolchainProject(Python, _workspace.Path, null), file));
        Assert.StartsWith("Python does not run index.js.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Requirements_install_into_a_new_virtual_environment()
    {
        _workspace.Write("requirements.txt", "requests\n");
        var project = new ToolchainProject(Python, _workspace.Path, "requirements.txt");

        var dependency = RunPlanner.DependenciesOf(project)!;
        var steps = RunPlanner.PlanDependencies(project, dependency);

        Assert.Equal("pip install -r requirements.txt", dependency.Name);
        Assert.True(RunPlanner.IsPending(project, dependency));
        Assert.Equal(["python3 -m venv .venv", $"{_workspace.Resolve(".venv/bin/python")} -m pip install --disable-pip-version-check -r requirements.txt"], steps.Select(s => s.CommandLine));
        Assert.All(steps, s => Assert.Equal(_workspace.Path, s.WorkingDirectory));

        Directory.CreateDirectory(_workspace.Resolve(".venv"));
        Assert.False(RunPlanner.IsPending(project, dependency));
        Assert.Single(RunPlanner.PlanDependencies(project, dependency));
    }

    [Fact]
    public void A_Node_project_runs_its_start_script()
    {
        _workspace.Write("package.json", """{ "name": "web", "main": "lib/index.js", "scripts": { "start": "node server.js" } }""");
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");

        var plan = RunPlanner.PlanProject(project)!;

        Assert.Equal("npm start", plan.EntryPoint);
        Assert.Equal("npm", plan.Run.Program);
        Assert.Equal(["start"], plan.Run.Arguments);
        Assert.Equal("1", plan.Run.Environment["NODE_DISABLE_COLORS"]);
    }

    [Fact]
    public void A_Node_project_without_a_start_script_runs_its_main_file()
    {
        _workspace.Write("package.json", """{ "name": "cli", "main": "lib/cli.js", }""");
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");

        var plan = RunPlanner.PlanProject(project)!;

        Assert.Equal("node lib/cli.js", plan.EntryPoint);
        Assert.Equal(["lib/cli.js"], plan.Run.Arguments);
    }

    [Fact]
    public void A_Node_project_without_either_runs_its_index_file()
    {
        _workspace.Write("package.json", """{ "name": "plain" }""");
        _workspace.Write("index.js");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Node, _workspace.Path, "package.json"))!;

        Assert.Equal("index.js", plan.EntryPoint);
        Assert.Equal("node", plan.Run.Program);
        Assert.Equal(["index.js"], plan.Run.Arguments);
    }

    [Fact]
    public void Node_packages_install_with_npm_until_node_modules_is_there()
    {
        _workspace.Write("package.json", """{ "dependencies": { "chalk": "^5.4.1" } }""");
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");

        var dependency = RunPlanner.DependenciesOf(project)!;

        Assert.Equal("npm install", dependency.Name);
        Assert.True(RunPlanner.IsPending(project, dependency));
        Assert.Equal(["npm install --no-fund --no-audit"], RunPlanner.PlanDependencies(project, dependency).Select(s => s.CommandLine));
        Directory.CreateDirectory(_workspace.Resolve("node_modules"));
        Assert.False(RunPlanner.IsPending(project, dependency));
    }

    [Theory]
    [InlineData("""{ "scripts": { "start": "node ." } }""", "scripts.start", "node .")]
    [InlineData("""{ "scripts": { "start": "" } }""", "scripts.start", null)]
    [InlineData("""{ "scripts": [] }""", "scripts.start", null)]
    [InlineData("""{ "main": 3 }""", "main", null)]
    [InlineData("""// a comment\n{ "main": "a.js" }""", "main", "a.js")]
    [InlineData("not json", "main", null)]
    public void Reads_a_string_from_a_JSON_file(string json, string path, string? expected)
    {
        var file = _workspace.Write("package.json", json.Replace("\\n", "\n", StringComparison.Ordinal));

        Assert.Equal(expected, RunPlanner.ReadJson(file, path));
    }

    public void Dispose() => _workspace.Dispose();
}
