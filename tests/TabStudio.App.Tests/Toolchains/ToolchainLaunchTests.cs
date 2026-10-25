using System.Text.Json.Nodes;
using TabStudio.App.Services.Toolchains;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>What F5 debugs for a Python or Node.js project: the run plan's program, in debugpy's and js-debug's terms.</summary>
public sealed partial class ToolchainLaunchTests
{
    [Fact]
    public void A_Python_script_is_debugged_with_its_interpreter_options_arguments_and_environment()
    {
        var plan = Plan("/work/app/.venv/bin/python", ["-u", "main.py", "--name", "Ada"], "/work/app", "main.py", ("PYTHONUNBUFFERED", "1"), ("PATH", "/work/app/.venv/bin:$PATH"), ("VIRTUAL_ENV", "/work/app/.venv"));

        var configuration = ToolchainLaunch.Create(ToolchainLaunch.PythonType, plan)!;

        var properties = configuration.Properties;
        Assert.Equal("main.py", configuration.Name);
        Assert.Equal("debugpy", configuration.Type);
        Assert.False(configuration.IsAttach);
        Assert.Equal("/work/app/main.py", configuration.Program);
        Assert.Equal("/work/app/.venv/bin/python", (string?)properties["python"]);
        Assert.Equal(["-u"], Strings(properties["pythonArgs"]));
        Assert.Equal(["--name", "Ada"], configuration.Arguments);
        Assert.Equal("/work/app", configuration.WorkingDirectory);
        Assert.Equal("1", configuration.Environment["PYTHONUNBUFFERED"]);
        Assert.Equal("/work/app/.venv", configuration.Environment["VIRTUAL_ENV"]);

        // The IDE puts the .venv's bin on PATH when it starts the program for debugpy, which would keep "$PATH" as it is.
        Assert.False(configuration.Environment.ContainsKey("PATH"));
        Assert.Null(properties["console"]);
    }

    [Fact]
    public void A_Python_module_is_debugged_as_a_module()
    {
        var configuration = ToolchainLaunch.Create(ToolchainLaunch.PythonType, Plan("python3", ["-m", "http.server", "8000"], "/work/site", "http.server"))!;

        Assert.Equal("http.server", (string?)configuration.Properties["module"]);
        Assert.Null(configuration.Program);
        Assert.Equal(["8000"], configuration.Arguments);
        Assert.Null(configuration.Properties["pythonArgs"]);
    }

    [Fact]
    public void Python_code_given_on_the_command_line_is_not_launched_by_the_debugger()
    {
        Assert.Null(ToolchainLaunch.Create(ToolchainLaunch.PythonType, Plan("python3", ["-c", "print(1)"], "/work", "code")));
        Assert.Null(ToolchainLaunch.Create(ToolchainLaunch.PythonType, Plan("python3", ["-u"], "/work", "nothing")));
        Assert.Null(ToolchainLaunch.Create("lldb-dap", Plan("cargo", ["run"], "/work", "cargo run")));
    }

    [Fact]
    public void A_Node_file_is_debugged_as_the_program()
    {
        var configuration = ToolchainLaunch.Create(ToolchainLaunch.NodeType, Plan("node", ["--enable-source-maps", "server.js", "8080"], "/work/web", "server.js"))!;

        var properties = configuration.Properties;
        Assert.Equal("pwa-node", configuration.Type);
        Assert.Equal("node", (string?)properties["runtimeExecutable"]);
        Assert.Equal(["--enable-source-maps"], Strings(properties["runtimeArgs"]));
        Assert.Equal("/work/web/server.js", configuration.Program);
        Assert.Equal(["8080"], configuration.Arguments);
        Assert.Equal(["<node_internals>/**"], Strings(properties["skipFiles"]));
    }

    [Fact]
    public void A_start_script_that_runs_node_on_a_file_is_debugged_without_npm()
    {
        using var folder = new TempDirectory();
        folder.Write("package.json", """{ "name": "greeter", "scripts": { "start": "node index.js" } }""");

        var configuration = ToolchainLaunch.Create(ToolchainLaunch.NodeType, Plan("npm", ["start"], folder.Path, "npm start"))!;

        Assert.Equal("npm start", configuration.Name);
        Assert.Equal("node", (string?)configuration.Properties["runtimeExecutable"]);
        Assert.Equal(Path.Combine(folder.Path, "index.js"), configuration.Program);
    }

    [Fact]
    public void Other_start_scripts_run_through_npm_under_the_debugger()
    {
        using var folder = new TempDirectory();
        folder.Write("package.json", """{ "name": "site", "scripts": { "start": "vite --port 5173" } }""");

        var configuration = ToolchainLaunch.Create(ToolchainLaunch.NodeType, Plan("npm", ["start"], folder.Path, "npm start"))!;

        Assert.Equal("npm", (string?)configuration.Properties["runtimeExecutable"]);
        Assert.Equal(["start"], Strings(configuration.Properties["runtimeArgs"]));
        Assert.Null(configuration.Program);
    }

    private static RunPlan Plan(string program, string[] arguments, string folder, string entryPoint, params (string Name, string Value)[] environment) =>
        new(null, new ProcessStartSpec
        {
            Program = program,
            Arguments = arguments,
            WorkingDirectory = folder,
            Environment = environment.ToDictionary(e => e.Name, e => (string?)e.Value),
        }, entryPoint);

    private static List<string?> Strings(JsonNode? array) => array is JsonArray items ? [.. items.Select(i => (string?)i)] : [];
}
