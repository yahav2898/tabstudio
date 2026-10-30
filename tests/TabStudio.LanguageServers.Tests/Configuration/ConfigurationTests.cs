using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Testing;
using InitializationOptions = TabStudio.LanguageServers.Hosting.InitializationOptions;

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
    [InlineData("index.html", "html", "vscode-html-language-server")]
    [InlineData("site.css", "css", "vscode-css-language-server")]
    [InlineData("theme.scss", "scss", "vscode-css-language-server")]
    [InlineData("tsconfig.json", "jsonc", "vscode-json-language-server")]
    // JetBrains' Kotlin LSP first, when the Kotlin toolchain brings it; kotlin-language-server serves Android projects without it.
    [InlineData("MainActivity.kt", "kotlin", "kotlin-lsp")]
    [InlineData("index.php", "php", "phpactor")]
    [InlineData("main.swift", "swift", "sourcekit-lsp")]
    [InlineData("main.dart", "dart", "dart-analysis-server")]
    public void The_built_in_registry_knows_each_language_and_its_server(string file, string language, string server)
    {
        var registry = LanguageServerRegistry.BuiltIn;

        var found = registry.FindLanguage(file);

        Assert.Equal(language, found?.Id);
        Assert.Equal(server, registry.FindServers(language).First().Id);
        Assert.Empty(registry.Problems);
    }

    [Fact]
    public void JSON_validation_is_enabled_by_default_and_can_be_disabled_by_the_workspace()
    {
        var registry = LanguageServerRegistry.BuiltIn;
        var server = registry.FindServers("jsonc").First();
        Assert.True((bool?)JsonSettings.Select(registry.SettingsFor(server), "json.validate.enable"));
        var custom = registry.WithWorkspaceSettings(JsonSettings.ParseObject("""{ "json.validate.enable": false }"""));
        Assert.False((bool?)JsonSettings.Select(custom.SettingsFor(custom.FindServers("jsonc").First()), "json.validate.enable"));
    }

    [Theory]
    [InlineData("html", "css.validate")]
    [InlineData("html", "html.validate.styles")]
    [InlineData("css", "css.validate")]
    [InlineData("scss", "scss.validate")]
    [InlineData("less", "less.validate")]
    public void Web_servers_have_scoped_validation_settings(string language, string setting)
    {
        var registry = LanguageServerRegistry.BuiltIn;
        var server = registry.FindServers(language).First();
        Assert.True((bool?)JsonSettings.Select(registry.SettingsFor(server), setting));
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
    public void JavaScript_is_debugged_with_js_debug_over_tcp()
    {
        var registry = LanguageServerRegistry.BuiltIn;

        var adapter = registry.FindAdapter("node");

        Assert.Equal("pwa-node", adapter?.Id);
        Assert.Equal(DebugAdapterTransport.Tcp, adapter!.Transport);
        Assert.Contains("${port}", adapter.Arguments);
        Assert.Equal("pwa-node", registry.FindAdaptersForLanguage("javascript").First().Id);
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
    public void Programs_TabStudio_runs_use_the_terminal_defaults_under_the_configuration()
    {
        var debugpy = LanguageServerRegistry.BuiltIn.FindAdapter("debugpy")!;
        var jsDebug = LanguageServerRegistry.BuiltIn.FindAdapter("pwa-node")!;
        var mine = LaunchConfiguration.ForCurrentFile(debugpy, "Python") with
        {
            Properties = new JsonObject { ["program"] = "/x.py", ["console"] = "internalConsole" },
        };

        var python = LaunchConfiguration.ForCurrentFile(debugpy, "Python").ToRequestArguments(debugpy, inTerminal: true);
        var node = LaunchConfiguration.ForCurrentFile(jsDebug, "JavaScript").ToRequestArguments(jsDebug, inTerminal: true);

        Assert.Equal("integratedTerminal", (string?)python["console"]);
        Assert.False((bool?)python["redirectOutput"]);
        Assert.True((bool?)python["justMyCode"]);
        Assert.Equal("integratedTerminal", (string?)node["console"]);
        Assert.True((bool?)node["sourceMaps"]);
        Assert.Equal("internalConsole", (string?)mine.ToRequestArguments(debugpy, inTerminal: true)["console"]);
    }

    [Fact]
    public void Delve_listens_on_a_port_it_is_given()
    {
        var delve = LanguageServerRegistry.BuiltIn.FindAdapter("go")!;

        Assert.Equal(DebugAdapterTransport.Tcp, delve.Transport);
        Assert.Equal(["dap", "--listen", "127.0.0.1:${port}"], delve.Arguments);
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

public sealed class InitializationOptionsTests
{
    [Fact]
    public void A_package_file_is_found_beside_a_global_server_through_its_link()
    {
        using var temp = new TempDirectory();
        var cli = temp.Write("prefix/lib/node_modules/typescript-language-server/lib/cli.mjs", "// server");
        var tsserver = temp.Write("prefix/lib/node_modules/typescript/lib/tsserver.js", "// tsserver");
        var command = temp.Resolve("prefix/bin/typescript-language-server");
        Directory.CreateDirectory(Path.GetDirectoryName(command)!);
        File.CreateSymbolicLink(command, "../lib/node_modules/typescript-language-server/lib/cli.mjs");
        var options = new JsonObject { ["tsserver"] = new JsonObject { ["path"] = "${packageFile:typescript/lib/tsserver.js}" }, ["other"] = 1 };

        var resolved = InitializationOptions.Resolve(options, temp.Resolve("workspace"), command);

        Assert.True(File.Exists(cli));
        Assert.Equal(tsserver, resolved?["tsserver"]?["path"]?.GetValue<string>());
        Assert.Equal(1, resolved?["other"]?.GetValue<int>());
        Assert.Equal("${packageFile:typescript/lib/tsserver.js}", options["tsserver"]?["path"]?.GetValue<string>());
    }

    [Fact]
    public void A_package_file_of_a_server_in_another_environment_gets_the_path_the_server_sees()
    {
        // As in TabStudio's Linux environment on Android: the server sees /usr/local/..., which is under rootfs here.
        using var temp = new TempDirectory();
        var rootfs = temp.Resolve("rootfs");
        temp.Write("rootfs/usr/local/lib/node_modules/typescript-language-server/lib/cli.mjs", "// server");
        temp.Write("rootfs/usr/local/lib/node_modules/typescript/lib/tsserver.js", "// tsserver");
        var link = temp.Resolve("rootfs/usr/local/bin/typescript-language-server");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        File.CreateSymbolicLink(link, "../lib/node_modules/typescript-language-server/lib/cli.mjs");
        var checkedPaths = new List<string>();
        string? ToLocalPath(string path)
        {
            checkedPaths.Add(path);
            var local = Path.Combine(rootfs, path.TrimStart('/'));
            return File.Exists(local) ? local : null;
        }

        var options = new JsonObject { ["tsserver"] = new JsonObject { ["path"] = "${packageFile:typescript/lib/tsserver.js}" } };

        var resolved = InitializationOptions.Resolve(options, "/root/workspace", "/usr/local/bin/typescript-language-server", toLocalPath: ToLocalPath);

        Assert.Equal("/usr/local/lib/node_modules/typescript/lib/tsserver.js", resolved?["tsserver"]?["path"]?.GetValue<string>());
        Assert.Contains("/root/workspace/node_modules/typescript/lib/tsserver.js", checkedPaths);
    }

    [Fact]
    public void The_workspace_copy_of_a_package_wins_and_a_missing_one_is_left_out()
    {
        using var temp = new TempDirectory();
        var local = temp.Write("workspace/node_modules/typescript/lib/tsserver.js", "// local");
        var options = new JsonObject
        {
            ["tsserver"] = new JsonObject { ["path"] = "${packageFile:typescript/lib/tsserver.js}" },
            ["missing"] = "${packageFile:nothing/here.js}",
        };

        var resolved = InitializationOptions.Resolve(options, temp.Resolve("workspace"), temp.Resolve("bin/server"));

        Assert.Equal(local, resolved?["tsserver"]?["path"]?.GetValue<string>());
        Assert.False(resolved?.AsObject().ContainsKey("missing"));
    }
}
