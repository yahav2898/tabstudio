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
    public void A_Flask_application_package_runs_with_flask_run()
    {
        // pallets/flask's examples/tutorial: the app is a package whose __init__.py defines create_app.
        _workspace.Write("pyproject.toml", "[project]\nname = \"flaskr\"\ndependencies = [\n    \"flask\",\n]\n");
        _workspace.Write("flaskr/__init__.py", "import os\n\nfrom flask import Flask\n\n\ndef create_app(test_config=None):\n    app = Flask(__name__, instance_relative_config=True)\n    return app\n");
        _workspace.Write("flaskr/db.py", "import sqlite3\n");
        _workspace.Write("tests/__init__.py");
        _workspace.Write("tests/conftest.py", "from flaskr import create_app\n");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, "pyproject.toml"))!;

        Assert.Equal("flask run (flaskr)", plan.EntryPoint);
        Assert.Equal("python3", plan.Run.Program);
        Assert.Equal(["-u", "-m", "flask", "run"], plan.Run.Arguments);
        Assert.Equal("flaskr", plan.Run.Environment["FLASK_APP"]);
        Assert.Equal("1", plan.Run.Environment["PYTHONUNBUFFERED"]);
    }

    [Fact]
    public void A_Flask_application_package_in_src_runs_from_src()
    {
        _workspace.Write("src/blog/__init__.py", "import flask\n");
        _workspace.Write("src/blog/views.py");
        _workspace.Write("src/helpers/__init__.py", "def helper():\n    pass\n");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, null));

        Assert.Null(plan);

        _workspace.Write("src/blog/__init__.py", "import flask\n\napp = flask.Flask(__name__)\n");
        plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, null))!;

        Assert.Equal(["-u", "-m", "flask", "run"], plan.Run.Arguments);
        Assert.Equal("blog", plan.Run.Environment["FLASK_APP"]);
        Assert.Equal("src", plan.Run.Environment["PYTHONPATH"]);
    }

    [Fact]
    public void A_package_that_is_not_a_Flask_app_is_not_run()
    {
        _workspace.Write("mylib/__init__.py", "from flask import Blueprint\n\nbp = Blueprint('x', __name__)\n");
        _workspace.Write("other/__init__.py", "def create_app():\n    return None\n");

        Assert.Null(RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, null)));
    }

    [Fact]
    public void A_flaskenv_still_decides_how_Flask_runs()
    {
        _workspace.Write(".flaskenv", "FLASK_APP=flaskr\n");
        _workspace.Write("flaskr/__init__.py", "from flask import Flask\n\ndef create_app():\n    return Flask(__name__)\n");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, null))!;

        Assert.Equal("flask run", plan.EntryPoint);
        Assert.False(plan.Run.Environment.ContainsKey("FLASK_APP"));
    }

    /// <summary>nsidnev/fastapi-realworld-example-app's app/main.py, abridged.</summary>
    private const string RealWorldMain = """
        from fastapi import FastAPI
        from starlette.middleware.cors import CORSMiddleware

        from app.core.config import get_app_settings


        def get_application() -> FastAPI:
            settings = get_app_settings()
            application = FastAPI(**settings.fastapi_kwargs)
            return application


        app = get_application()
        """;

    [Fact]
    public void A_FastAPI_app_in_a_package_runs_with_uvicorn_when_the_project_depends_on_it()
    {
        _workspace.Write("pyproject.toml", "[tool.poetry]\nname = \"realworld\"\n\n[tool.poetry.dependencies]\npython = \"^3.9\"\nuvicorn = \"^0.18.2\"\nfastapi = \"^0.79.1\"\n");
        _workspace.Write("app/__init__.py");
        _workspace.Write("app/main.py", RealWorldMain);
        _workspace.Write("app/core/config.py");
        _workspace.Write(".github/workflows/tests.yml");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, "pyproject.toml"))!;

        Assert.Equal("uvicorn app.main:app", plan.EntryPoint);
        Assert.Equal(["-u", "-m", "uvicorn", "app.main:app"], plan.Run.Arguments);
        Assert.Equal(_workspace.Path, plan.Run.WorkingDirectory);
        Assert.False(plan.Run.Environment.ContainsKey("PYTHONPATH"));
    }

    [Theory]
    [InlineData("requirements.txt", "fastapi==0.110.0\nuvicorn[standard]==0.29.0\n")]
    [InlineData("poetry.lock", "[[package]]\nname = \"uvicorn\"\nversion = \"0.18.2\"\n")]
    [InlineData("pyproject.toml", "[project]\nname = \"api\"\ndependencies = [\"fastapi>=0.110\", \"uvicorn[standard]>=0.29\"]\n")]
    [InlineData("Pipfile", "[packages]\nfastapi = \"*\"\nuvicorn = {extras = [\"standard\"], version = \"*\"}\n")]
    public void Uvicorn_is_found_in_any_file_that_lists_the_projects_packages(string file, string content)
    {
        _workspace.Write(file, content);
        _workspace.Write("src/shop/__init__.py");
        _workspace.Write("src/shop/main.py", "from fastapi import FastAPI\n\napp = FastAPI()\n");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, null))!;

        Assert.Equal("uvicorn shop.main:app", plan.EntryPoint);
        Assert.Equal(["-u", "-m", "uvicorn", "shop.main:app"], plan.Run.Arguments);
        Assert.Equal("src", plan.Run.Environment["PYTHONPATH"]);
    }

    [Fact]
    public void A_FastAPI_app_without_uvicorn_says_what_is_missing()
    {
        _workspace.Write("requirements.txt", "fastapi==0.110.0\n");
        _workspace.Write("app/main.py", RealWorldMain);
        var project = new ToolchainProject(Python, _workspace.Path, "requirements.txt");

        Assert.Null(RunPlanner.PlanProject(project));
        var unmet = RunPlanner.UnmetEntryOf(project)!;

        Assert.Equal("app/main.py", unmet.Entry.RelativePath);
        Assert.Equal("uvicorn app.main:app", unmet.EntryPoint);
        Assert.Equal(["uvicorn"], unmet.Missing);
    }

    [Fact]
    public void A_main_py_that_starts_its_own_server_still_runs_as_a_script()
    {
        _workspace.Write("requirements.txt", "fastapi\nuvicorn\n");
        _workspace.Write("main.py", "import uvicorn\nfrom fastapi import FastAPI\n\napp = FastAPI()\n\nif __name__ == \"__main__\":\n    uvicorn.run(app)\n");
        var project = new ToolchainProject(Python, _workspace.Path, "requirements.txt");

        var plan = RunPlanner.PlanProject(project)!;

        Assert.Equal("main.py", plan.EntryPoint);
        Assert.Equal(["-u", "main.py"], plan.Run.Arguments);
        Assert.Null(RunPlanner.UnmetEntryOf(project));
    }

    [Fact]
    public void A_root_FastAPI_module_runs_with_uvicorn()
    {
        _workspace.Write("requirements.txt", "fastapi\nuvicorn\n");
        _workspace.Write("main.py", "from fastapi import FastAPI\n\napp = FastAPI(title=\"api\")\n");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, "requirements.txt"))!;

        Assert.Equal("uvicorn main:app", plan.EntryPoint);
        Assert.Equal(["-u", "-m", "uvicorn", "main:app"], plan.Run.Arguments);
    }

    [Fact]
    public void A_script_named_main_py_is_not_mistaken_for_a_web_app()
    {
        _workspace.Write("requirements.txt", "uvicorn\n");
        _workspace.Write("main.py", "app = make_app()\nprint(app)\n");

        var plan = RunPlanner.PlanProject(new ToolchainProject(Python, _workspace.Path, "requirements.txt"))!;

        Assert.Equal("main.py", plan.EntryPoint);
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
        Assert.True(RunPlanner.IsPending(project, dependency));
        Assert.Equal(2, RunPlanner.PlanDependencies(project, dependency).Count);
        _workspace.Write(".venv/bin/python");
        Assert.False(RunPlanner.IsPending(project, dependency));
        Assert.Single(RunPlanner.PlanDependencies(project, dependency));
    }

    [Theory]
    [InlineData("next start", "next dev", "npm run dev")]
    [InlineData("nuxt start", "nuxt dev", "npm run dev")]
    [InlineData("vite preview", "vite", "npm run dev")]
    [InlineData("node server.js", "nodemon server.js", "npm start")]
    public void A_Node_project_whose_start_script_serves_a_production_build_runs_its_dev_script(string start, string dev, string expected)
    {
        // next start fails until next build has made .next, which a person runs once before deploying.
        _workspace.Write("package.json", $$"""{ "name": "web", "scripts": { "start": "{{start}}", "dev": "{{dev}}", "build": "next build" } }""");
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");

        Assert.Equal(expected, RunPlanner.PlanProject(project)!.EntryPoint);
    }

    [Fact]
    public void A_Node_project_with_only_a_production_start_script_still_runs_it()
    {
        _workspace.Write("package.json", """{ "name": "web", "scripts": { "start": "next start" } }""");
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");

        Assert.Equal("npm start", RunPlanner.PlanProject(project)!.EntryPoint);
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
    [InlineData("package.json", """{ "name": "plain", "scripts": { "start": "node index.js" } }""")]
    [InlineData("package.json", """{ "dependencies": {} }""")]
    [InlineData("requirements.txt", "# Nothing yet\n\n")]
    public void A_manifest_that_lists_no_packages_needs_no_install(string manifest, string content)
    {
        _workspace.Write(manifest, content);
        var toolchain = manifest == "package.json" ? Node : Python;
        var project = new ToolchainProject(toolchain, _workspace.Path, manifest);

        var dependency = RunPlanner.DependenciesOf(project)!;

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
