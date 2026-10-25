using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Testing;

namespace TabStudio.LanguageServers.Tests.Configuration;

public sealed class RegistryTests
{
    [Theory]
    [InlineData("main.py", "python", "pyright")]
    [InlineData("app.TS", "typescript", "typescript-language-server")]
    [InlineData("view.tsx", "typescriptreact", "typescript-language-server")]
    [InlineData("index.mjs", "javascript", "typescript-language-server")]
    [InlineData("main.cpp", "cpp", "clangd")]
    [InlineData("lib.rs", "rust", "rust-analyzer")]
    [InlineData("main.go", "go", "gopls")]
    [InlineData("App.java", "java", "jdtls")]
    public void The_built_in_registry_knows_each_language_and_its_server(string file, string language, string server)
    {
        var registry = LanguageServerRegistry.BuiltIn;

        var found = registry.FindLanguage(file);

        Assert.Equal(language, found?.Id);
        Assert.Equal(server, registry.FindServers(language).First().Id);
        Assert.Empty(registry.Problems);
    }

    [Fact]
    public void The_built_in_registry_has_an_adapter_for_python_by_type_and_alias()
    {
        var registry = LanguageServerRegistry.BuiltIn;

        Assert.Equal("debugpy", registry.FindAdapter("debugpy")?.Id);
        Assert.Equal("debugpy", registry.FindAdapter("python")?.Id);
        Assert.Equal("debugpy", registry.FindAdaptersForLanguage("python").First().Id);
        Assert.Null(registry.FindLanguage("notes.txt"));
    }

    [Fact]
    public void Workspace_settings_change_a_server_add_a_language_and_set_settings()
    {
        var settings = JsonSettings.ParseObject("""
            {
              // Comments and trailing commas are fine, as in VS Code.
              "python.analysis.typeCheckingMode": "strict",
              "languageServers": {
                "pyright": { "command": "basedpyright-langserver" },
                "lua-ls": { "name": "Lua", "languages": [ "lua" ], "command": "lua-language-server", },
              },
              "languages": { "lua": { "name": "Lua", "extensions": [ "lua" ] } },
            }
            """);

        var registry = LanguageServerRegistry.BuiltIn.WithWorkspaceSettings(settings);

        var pyright = registry.FindServers("python").First();
        Assert.Equal("basedpyright-langserver", pyright.Command);
        Assert.Equal(new[] { "--stdio" }, pyright.Arguments);
        Assert.Equal("lua-ls", registry.FindServers(registry.FindLanguage("init.lua")!.Id).First().Id);
        var analysis = (JsonObject)JsonSettings.Select(registry.SettingsFor(pyright), "python.analysis")!;
        Assert.Equal("strict", (string?)analysis["typeCheckingMode"]);
        Assert.True((bool?)analysis["useLibraryCodeForTypes"]);
    }

    [Fact]
    public void A_server_turned_off_is_skipped()
    {
        var registry = LanguageServerRegistry.BuiltIn.WithWorkspaceSettings(JsonSettings.ParseObject("""{ "languageServers": { "pyright": { "enabled": false } } }"""));

        Assert.Empty(registry.FindServers("python"));
    }

    [Fact]
    public void A_server_without_a_command_is_a_problem_not_a_failure()
    {
        var registry = LanguageServerRegistry.Parse("""{ "languageServers": { "broken": { "languages": [ "x" ] } } }""");

        Assert.Empty(registry.Servers);
        Assert.Contains(registry.Problems, p => p.Contains("broken", StringComparison.Ordinal));
    }

    [Fact]
    public void Dotted_names_expand_into_nested_settings()
    {
        var expanded = JsonSettings.ExpandDottedNames(JsonSettings.ParseObject("""{ "a.b.c": 1, "a.b.d": 2, "a": { "e": 3 } }"""));

        Assert.Equal("""{"a":{"b":{"c":1,"d":2},"e":3}}""", expanded.ToJsonString());
    }
}

public sealed class WorkspaceConfigurationTests
{
    [Fact]
    public void Reads_settings_and_launch_configurations_from_the_tabstudio_folder()
    {
        using var temp = new TempDirectory();
        temp.Write(".tabstudio/settings.json", """{ "languageServers": { "pyright": { "args": [ "--stdio", "--verbose" ] } } }""");
        temp.Write(".tabstudio/launch.json", """
            {
              "version": "0.2.0",
              "configurations": [
                { "name": "Run main", "type": "debugpy", "request": "launch", "program": "${workspaceFolder}/main.py", "stopOnEntry": true },
                { "name": "Missing type" }
              ]
            }
            """);

        var configuration = WorkspaceConfiguration.Load(temp.Path);

        Assert.Equal(new[] { "--stdio", "--verbose" }, configuration.Registry.FindServers("python").First().Arguments);
        var launch = Assert.Single(configuration.LaunchConfigurations);
        Assert.Equal("Run main", launch.Name);
        Assert.True(launch.StopOnEntry);
        Assert.Contains(configuration.Problems, p => p.Contains("name", StringComparison.Ordinal));
    }

    [Fact]
    public void A_file_that_is_not_json_is_reported_and_left_out()
    {
        using var temp = new TempDirectory();
        temp.Write(".tabstudio/settings.json", "{ not json");

        var configuration = WorkspaceConfiguration.Load(temp.Path);

        Assert.Contains(configuration.Problems, p => p.Contains("settings.json", StringComparison.Ordinal));
        Assert.Equal("pyright-langserver", configuration.Registry.FindServers("python").First().Command);
    }
}

public sealed class LaunchConfigurationTests
{
    [Fact]
    public void Variables_are_replaced_in_every_string()
    {
        var root = Path.Combine(Path.GetTempPath(), "project");
        var file = Path.Combine(root, "src", "main.py");
        var configuration = LaunchConfiguration.ParseFile(JsonSettings.ParseObject("""
            {
              "configurations": [
                {
                  "name": "${fileBasenameNoExtension}",
                  "type": "debugpy",
                  "request": "launch",
                  "program": "${file}",
                  "cwd": "${fileDirname}",
                  "args": [ "${relativeFile}", "${workspaceFolderBasename}", "${unknown}" ],
                  "env": { "HOME_DIR": "${workspaceFolder}" }
                }
              ]
            }
            """), new List<string>()).Single();

        var substituted = configuration.Substitute(new LaunchVariables(root, file));

        Assert.Equal("main", substituted.Name);
        Assert.Equal(file, substituted.Program);
        Assert.Equal(Path.GetDirectoryName(file), substituted.WorkingDirectory);
        Assert.Equal(new[] { Path.Combine("src", "main.py"), "project", "${unknown}" }, substituted.Arguments);
        Assert.Equal(root, substituted.Environment["HOME_DIR"]);
    }

    [Fact]
    public void Request_arguments_put_the_configuration_over_the_adapter_defaults()
    {
        var adapter = LanguageServerRegistry.BuiltIn.FindAdapter("debugpy")!;
        var configuration = LaunchConfiguration.ForCurrentFile(adapter, "Python") with
        {
            Properties = new JsonObject { ["program"] = "/x.py", ["justMyCode"] = false },
        };

        var arguments = configuration.ToRequestArguments(adapter);

        Assert.Equal("internalConsole", (string?)arguments["console"]);
        Assert.False((bool?)arguments["justMyCode"]);
        Assert.Equal("/x.py", (string?)arguments["program"]);
    }

    [Fact]
    public void The_current_file_configuration_debugs_the_active_file()
    {
        var adapter = LanguageServerRegistry.BuiltIn.FindAdapter("debugpy")!;

        var configuration = LaunchConfiguration.ForCurrentFile(adapter, "Python").Substitute(new LaunchVariables("/work", "/work/app.py"));

        Assert.Equal("Python: Current File", configuration.Name);
        Assert.Equal("/work/app.py", configuration.Program);
        Assert.Equal("/work", configuration.WorkingDirectory);
        Assert.False(configuration.IsAttach);
    }
}
