using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.Hosting;

public sealed class LanguageServerHostTests
{
    [Fact]
    public async Task Server_resolution_receives_the_document_directory_before_choosing_a_native_SDK()
    {
        await using var workspace = new FakeWorkspace();
        var path = workspace.Write("firmware/src/main.rs", "#![no_std]");
        Assert.NotNull(workspace.Host.FindServer(path));
        Assert.Equal(("rust-analyzer", Path.GetDirectoryName(path)!), workspace.Launcher.LastResolutionContext);
    }

    [Fact]
    public async Task Opening_a_file_starts_its_server_and_initializes_it_with_honest_capabilities()
    {
        await using var workspace = new FakeWorkspace();

        var (_, server) = await workspace.OpenAsync("app/main.py", "print('hi')\n");

        var initialize = await server.WaitForRequestAsync("initialize");
        Assert.Equal(DocumentUri.FromPath(workspace.Root), initialize.GetProperty("rootUri").GetString());
        Assert.Equal("TabStudio", initialize.GetProperty("clientInfo").GetProperty("name").GetString());
        var completion = initialize.GetProperty("capabilities").GetProperty("textDocument").GetProperty("completion").GetProperty("completionItem");
        Assert.True(completion.GetProperty("snippetSupport").GetBoolean());
        Assert.False(initialize.GetProperty("capabilities").GetProperty("textDocument").TryGetProperty("diagnostic", out _));
        await server.WaitForNotificationAsync("initialized");
        var configuration = await server.WaitForNotificationAsync("workspace/didChangeConfiguration");
        Assert.True(configuration.GetProperty("settings").GetProperty("python").GetProperty("analysis").GetProperty("useLibraryCodeForTypes").GetBoolean());
        var open = await server.WaitForNotificationAsync("textDocument/didOpen");
        Assert.Equal("python", open.GetProperty("textDocument").GetProperty("languageId").GetString());
        Assert.Equal("print('hi')\n", open.GetProperty("textDocument").GetProperty("text").GetString());

        var started = Assert.Single(workspace.Launcher.StartInfos);
        Assert.Equal("/fake/bin/pyright-langserver", started.FileName);
        Assert.Equal(new[] { "--stdio" }, started.Arguments);
        Assert.Equal(workspace.Root, started.WorkingDirectory);
        var status = Assert.Single(workspace.Host.Servers);
        Assert.Equal(LanguageServerState.Running, status.State);
        Assert.Equal("fake-server 1.0", status.Version);
    }

    [Fact]
    public async Task Eclipse_JDT_is_told_no_client_process_so_its_watchdog_watches_its_own_parent()
    {
        // JDT exits by itself when, idle for 30 seconds, it cannot see the processId of initialize; under PRoot on
        // Android that killed it during builds (exit code 0, "stopped unexpectedly") and Java debugging with it.
        await using var workspace = new FakeWorkspace();

        var (_, python) = await workspace.OpenAsync("main.py", "pass\n");
        var (_, java) = await workspace.OpenAsync("src/main/java/App.java", "class App {}\n");

        var pythonInitialize = await python.WaitForRequestAsync("initialize");
        Assert.Equal(Environment.ProcessId, pythonInitialize.GetProperty("processId").GetInt32());
        var javaInitialize = await java.WaitForRequestAsync("initialize");
        Assert.Equal("/fake/bin/jdtls", workspace.Launcher.StartInfos[^1].FileName);
        Assert.True(javaInitialize.TryGetProperty("processId", out var processId), "processId is required by LSP, as null when there is none.");
        Assert.Equal(JsonValueKind.Null, processId.ValueKind);
    }

    [Fact]
    public async Task Servers_hear_about_files_created_and_deleted_outside_their_documents()
    {
        // Pyright kept "Import 'helper' could not be resolved" after helper.py was created, until the workspace reopened.
        await using var workspace = new FakeWorkspace();
        var (_, server) = await workspace.OpenAsync("main.py", "import helper\n");
        var initialize = await server.WaitForRequestAsync("initialize");
        Assert.True(initialize.GetProperty("capabilities").GetProperty("workspace").GetProperty("didChangeWatchedFiles").GetProperty("dynamicRegistration").GetBoolean());
        await server.WaitForNotificationAsync("textDocument/didOpen");

        var helper = Path.Combine(workspace.Root, "helper.py");
        await workspace.Host.NotifyFilesChangedAsync([(helper, FileChangeKind.Created), (Path.Combine(workspace.Root, "old.py"), FileChangeKind.Deleted)]);

        var notification = await server.WaitForNotificationAsync("workspace/didChangeWatchedFiles", _ => true);
        var changes = notification.GetProperty("changes").EnumerateArray().ToList();
        Assert.Equal(2, changes.Count);
        Assert.Equal(DocumentUri.FromPath(helper), changes[0].GetProperty("uri").GetString());
        Assert.Equal(1, changes[0].GetProperty("type").GetInt32());
        Assert.Equal(3, changes[1].GetProperty("type").GetInt32());
    }

    [Fact]
    public async Task Edits_go_to_the_server_as_incremental_changes_with_increasing_versions()
    {
        await using var workspace = new FakeWorkspace();
        var (document, server) = await workspace.OpenAsync("main.py", "x = 1\ny = 2\n");

        document.ApplyChange(4, 1, "10");
        document.ApplyChange(13, 0, "z");

        var first = await server.WaitForNotificationAsync("textDocument/didChange", p => Version(p) == 2);
        var second = await server.WaitForNotificationAsync("textDocument/didChange", p => Version(p) == 3);
        var change = Assert.Single(first.GetProperty("contentChanges").EnumerateArray());
        Assert.Equal("10", change.GetProperty("text").GetString());
        Assert.Equal(4, change.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32());
        Assert.Equal(5, change.GetProperty("range").GetProperty("end").GetProperty("character").GetInt32());
        Assert.Equal(2, second.GetProperty("contentChanges")[0].GetProperty("range").GetProperty("start").GetProperty("line").GetInt32());
        Assert.Equal("x = 10\ny = 2\nz", document.Snapshot.Text);
    }

    [Fact]
    public async Task A_server_that_wants_full_text_gets_the_whole_text_on_every_change()
    {
        await using var workspace = new FakeWorkspace(server => server.Capabilities["textDocumentSync"] = 1);
        var (document, server) = await workspace.OpenAsync("main.py", "a");

        document.ApplyChange(1, 0, "b");

        var change = await server.WaitForNotificationAsync("textDocument/didChange");
        var content = Assert.Single(change.GetProperty("contentChanges").EnumerateArray());
        Assert.False(content.TryGetProperty("range", out _));
        Assert.Equal("ab", content.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Saving_and_closing_a_document_tell_the_server()
    {
        await using var workspace = new FakeWorkspace();
        var (document, server) = await workspace.OpenAsync("main.py", "pass\n");

        document.NotifySaved();
        document.Dispose();

        await server.WaitForNotificationAsync("textDocument/didSave");
        await server.WaitForNotificationAsync("textDocument/didClose");
    }

    [Fact]
    public async Task Files_of_one_project_share_a_server_and_each_project_gets_its_own()
    {
        await using var workspace = new FakeWorkspace();
        workspace.Write("api/pyproject.toml", "[project]\nname = \"api\"\n");
        workspace.Write("web/pyproject.toml", "[project]\nname = \"web\"\n");

        await workspace.OpenAsync("api/a.py", "a = 1\n");
        await workspace.OpenAsync("api/sub/b.py", "b = 2\n");
        await workspace.OpenAsync("web/c.py", "c = 3\n");

        Assert.Equal(2, workspace.Launcher.Started.Count);
        Assert.Equal(
            new[] { Path.Combine(workspace.Root, "api"), Path.Combine(workspace.Root, "web") },
            workspace.Launcher.StartInfos.Select(s => s.WorkingDirectory));
    }

    [Fact]
    public async Task A_file_whose_server_is_not_installed_opens_without_one()
    {
        await using var workspace = new FakeWorkspace();
        workspace.Launcher.Missing.Add("pyright-langserver");
        var path = workspace.Write("main.py", "pass\n");

        Assert.Null(workspace.Host.OpenDocument(path, "pass\n"));
        Assert.Equal("pyright", Assert.Single(workspace.Host.FindMissingServers(path)).Id);
        Assert.Null(workspace.Host.FindServer(workspace.Write("notes.txt", "hi")));
    }

    [Fact]
    public async Task A_server_that_cannot_start_fails_with_its_reason()
    {
        await using var workspace = new FakeWorkspace();
        workspace.Launcher.StartFailure = "pyright-langserver could not be started: permission denied";
        var path = workspace.Write("main.py", "pass\n");

        using var document = workspace.Host.OpenDocument(path, "pass\n")!;

        Assert.False(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        Assert.Equal(LanguageServerState.Failed, document.ServerState);
        Assert.Contains("permission denied", document.ServerMessage, StringComparison.Ordinal);
        Assert.Null(await document.GetHoverAsync(0, CancellationToken.None));
    }

    [Fact]
    public async Task A_server_that_exits_during_initialization_restarts_without_failing_waiting_documents_or_requests()
    {
        var attempts = 0;
        var initialize = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var workspace = new FakeWorkspace(server =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                server.On("initialize", _ => { server.Crash(23); return null; });
            }
            else
            {
                server.OnAsync("initialize", _ => initialize.Task);
                server.On("workspace/executeCommand", _ => JsonValue.Create(4321));
            }
        });
        var logs = new ConcurrentQueue<string>();
        workspace.Host.LogReceived += (_, e) => logs.Enqueue(e.Message);
        using var document = workspace.Host.OpenDocument(workspace.Write("main.py", "x = 1\n"), "x = 1\n")!;
        var ready = document.WaitUntilReadyAsync(CancellationToken.None);
        var command = document.ExecuteServerCommandAsync("startDebugSession", [], CancellationToken.None);

        await WaitUntilAsync(() => document.ServerState == LanguageServerState.Restarting);
        document.ApplyChange(0, 0, "# edited before initialization\n");
        var second = await workspace.Launcher.WaitForServerAsync(2);
        await second.WaitForRequestAsync("initialize");
        Assert.False(ready.IsCompleted);
        Assert.False(command.IsCompleted);

        initialize.SetResult(new JsonObject { ["capabilities"] = second.Capabilities.DeepClone() });

        Assert.True(await ready.WaitAsync(TestTimeouts.Default));
        Assert.Equal(4321, (await command.WaitAsync(TestTimeouts.Default)).GetInt32());
        var opened = await second.WaitForNotificationAsync("textDocument/didOpen");
        Assert.Equal("# edited before initialization\nx = 1\n", opened.GetProperty("textDocument").GetProperty("text").GetString());
        Assert.Equal(LanguageServerState.Running, document.ServerState);
        Assert.Equal(2, workspace.Launcher.Started.Count);
        Assert.Contains(logs, l => l.Contains("could not be initialized: The connection closed.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Repeated_exits_during_initialization_exhaust_the_existing_restart_budget()
    {
        await using var workspace = new FakeWorkspace(server => server.On("initialize", _ => { server.Crash(23); return null; }),
            new LanguageServerHostOptions { MaxRestarts = 1, RestartWindow = TimeSpan.FromMinutes(5) });
        using var document = workspace.Host.OpenDocument(workspace.Write("main.py", "pass\n"), "pass\n")!;
        var command = document.ExecuteServerCommandAsync("startDebugSession", [], CancellationToken.None);

        Assert.False(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => command.WaitAsync(TestTimeouts.Default));
        Assert.Equal(LanguageServerState.Failed, document.ServerState);
        Assert.Contains("not started again", document.ServerMessage, StringComparison.Ordinal);
        Assert.Equal(2, workspace.Launcher.Started.Count);
    }

    [Fact]
    public async Task An_initialize_error_is_terminal_and_is_not_restarted()
    {
        await using var workspace = new FakeWorkspace(server => server.On("initialize", _ => throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams, "Unsupported client configuration")));
        using var document = workspace.Host.OpenDocument(workspace.Write("main.py", "pass\n"), "pass\n")!;

        Assert.False(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        Assert.Equal(LanguageServerState.Failed, document.ServerState);
        Assert.Contains("Unsupported client configuration", document.ServerMessage, StringComparison.Ordinal);
        Assert.Single(workspace.Launcher.Started);
    }

    [Fact]
    public async Task An_initialize_timeout_is_terminal_and_is_not_restarted()
    {
        var initialize = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var workspace = new FakeWorkspace(server => server.OnAsync("initialize", _ => initialize.Task),
            new LanguageServerHostOptions { InitializeTimeout = TimeSpan.FromMilliseconds(250) });
        using var document = workspace.Host.OpenDocument(workspace.Write("main.py", "pass\n"), "pass\n")!;
        try
        {
            Assert.False(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
            Assert.Equal(LanguageServerState.Failed, document.ServerState);
            Assert.Contains("did not answer within", document.ServerMessage, StringComparison.Ordinal);
            Assert.Single(workspace.Launcher.Started);
        }
        finally
        {
            initialize.TrySetResult(null);
        }
    }

    [Fact]
    public async Task Closing_the_workspace_during_initialization_does_not_restart_the_server()
    {
        var initialize = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var workspace = new FakeWorkspace(server => server.OnAsync("initialize", _ => initialize.Task));
        using var document = workspace.Host.OpenDocument(workspace.Write("main.py", "pass\n"), "pass\n")!;
        var ready = document.WaitUntilReadyAsync(CancellationToken.None);
        var server = await workspace.Launcher.WaitForServerAsync();
        await server.WaitForRequestAsync("initialize");
        try
        {
            await workspace.Host.DisposeAsync();

            Assert.False(await ready.WaitAsync(TestTimeouts.Default));
            Assert.Equal(LanguageServerState.Stopped, document.ServerState);
            Assert.Single(workspace.Launcher.Started);
        }
        finally
        {
            initialize.TrySetResult(null);
        }
    }

    [Fact]
    public async Task Closing_the_workspace_during_startup_backoff_cancels_the_restart()
    {
        await using var workspace = new FakeWorkspace(server => server.On("initialize", _ => { server.Crash(); return null; }));
        using var document = workspace.Host.OpenDocument(workspace.Write("main.py", "pass\n"), "pass\n")!;
        var ready = document.WaitUntilReadyAsync(CancellationToken.None);
        await WaitUntilAsync(() => document.ServerState == LanguageServerState.Restarting);

        await workspace.Host.DisposeAsync();

        Assert.False(await ready.WaitAsync(TestTimeouts.Default));
        Assert.Equal(LanguageServerState.Stopped, document.ServerState);
        Assert.Single(workspace.Launcher.Started);
    }

    [Fact]
    public async Task A_crashed_server_is_started_again_and_gets_its_documents_back_with_their_latest_text()
    {
        await using var workspace = new FakeWorkspace();
        var logs = new ConcurrentQueue<string>();
        workspace.Host.LogReceived += (_, e) => logs.Enqueue(e.Message);
        var (document, first) = await workspace.OpenAsync("main.py", "x = 1\n");

        first.Crash();
        document.ApplyChange(0, 0, "# edited while it was down\n");
        var second = await workspace.Launcher.WaitForServerAsync(2);

        var reopened = await second.WaitForNotificationAsync("textDocument/didOpen");
        Assert.Equal("# edited while it was down\nx = 1\n", reopened.GetProperty("textDocument").GetProperty("text").GetString());
        Assert.True(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        Assert.Contains(logs, l => l.Contains("stopped unexpectedly", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_server_that_keeps_crashing_is_not_started_again()
    {
        await using var workspace = new FakeWorkspace(options: new LanguageServerHostOptions { MaxRestarts = 1, RestartWindow = TimeSpan.FromMinutes(5) });
        var (document, first) = await workspace.OpenAsync("main.py", "x = 1\n");

        first.Crash();
        var second = await workspace.Launcher.WaitForServerAsync(2);
        await second.WaitForNotificationAsync("textDocument/didOpen");
        second.Crash();
        await WaitUntilAsync(() => document.ServerState == LanguageServerState.Failed);

        Assert.False(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        Assert.Contains("not started again", document.ServerMessage, StringComparison.Ordinal);
        Assert.Equal(2, workspace.Launcher.Started.Count);
    }

    [Fact]
    public async Task Closing_the_workspace_shuts_the_server_down_politely()
    {
        var workspace = new FakeWorkspace();
        var (_, server) = await workspace.OpenAsync("main.py", "pass\n");

        await workspace.DisposeAsync();

        Assert.Contains(server.Requests, r => r.Method == "shutdown");
        Assert.Contains(server.Notifications, n => n.Method == "exit");
    }

    [Fact]
    public async Task Configuration_requests_are_answered_from_the_server_and_workspace_settings()
    {
        await using var workspace = new FakeWorkspace(settings: """{ "python.analysis.typeCheckingMode": "strict" }""");
        var (_, server) = await workspace.OpenAsync("main.py", "pass\n");

        var answer = await server.RequestClientAsync("workspace/configuration", new JsonObject
        {
            ["items"] = new JsonArray(new JsonObject { ["section"] = "python.analysis" }, new JsonObject { ["section"] = "nothing.here" }),
        });

        Assert.Equal("strict", answer[0].GetProperty("typeCheckingMode").GetString());
        Assert.True(answer[0].GetProperty("autoSearchPaths").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer[1].ValueKind);
    }

    [Fact]
    public async Task A_capability_registered_later_turns_its_feature_on()
    {
        await using var workspace = new FakeWorkspace();
        var (document, server) = await workspace.OpenAsync("main.py", "pass\n");
        Assert.False(document.Capabilities.Hover);

        await server.RequestClientAsync("client/registerCapability", new JsonObject
        {
            ["registrations"] = new JsonArray(new JsonObject { ["id"] = "hover-1", ["method"] = "textDocument/hover", ["registerOptions"] = new JsonObject() }),
        });
        Assert.True(document.Capabilities.Hover);

        await server.RequestClientAsync("client/unregisterCapability", new JsonObject
        {
            ["unregisterations"] = new JsonArray(new JsonObject { ["id"] = "hover-1", ["method"] = "textDocument/hover" }),
        });
        Assert.False(document.Capabilities.Hover);
    }

    [Fact]
    public async Task Logs_messages_and_progress_reach_the_host()
    {
        await using var workspace = new FakeWorkspace();
        var logs = new ConcurrentQueue<LanguageServerLogEventArgs>();
        var messages = new ConcurrentQueue<LanguageServerMessageEventArgs>();
        var progress = new ConcurrentQueue<LanguageServerProgressEventArgs>();
        workspace.Host.LogReceived += (_, e) => logs.Enqueue(e);
        workspace.Host.MessageShown += (_, e) => messages.Enqueue(e);
        workspace.Host.ProgressChanged += (_, e) => progress.Enqueue(e);
        var (_, server) = await workspace.OpenAsync("main.py", "pass\n");

        await server.NotifyClientAsync("window/logMessage", new JsonObject { ["type"] = 4, ["message"] = "Found 3 source files" });
        await server.NotifyClientAsync("window/showMessage", new JsonObject { ["type"] = 2, ["message"] = "No interpreter found" });
        await server.NotifyClientAsync("$/progress", new JsonObject { ["token"] = "t", ["value"] = new JsonObject { ["kind"] = "begin", ["title"] = "Indexing" } });
        await server.NotifyClientAsync("$/progress", new JsonObject { ["token"] = "t", ["value"] = new JsonObject { ["kind"] = "report", ["percentage"] = 40 } });
        await server.NotifyClientAsync("$/progress", new JsonObject { ["token"] = "t", ["value"] = new JsonObject { ["kind"] = "end" } });
        await WaitUntilAsync(() => progress.Count == 3);

        Assert.Contains(logs, l => l.Level == LanguageServerLogLevel.Log && l.Message == "Found 3 source files" && l.ServerName == "Pyright");
        Assert.Equal(LanguageServerMessageType.Warning, Assert.Single(messages).Type);
        var steps = progress.ToList();
        Assert.Equal("Indexing", steps[1].Title);
        Assert.Equal(40, steps[1].Percentage);
        Assert.True(steps[2].IsDone);
    }

    [Fact]
    public async Task A_question_from_the_server_is_answered_by_the_handler()
    {
        await using var workspace = new FakeWorkspace();
        workspace.Host.MessageRequestHandler = (request, _) => Task.FromResult<string?>(request.Actions[1]);
        var (_, server) = await workspace.OpenAsync("main.py", "pass\n");

        var answer = await server.RequestClientAsync("window/showMessageRequest", new JsonObject
        {
            ["type"] = 3,
            ["message"] = "Create a configuration file?",
            ["actions"] = new JsonArray(new JsonObject { ["title"] = "No" }, new JsonObject { ["title"] = "Yes" }),
        });

        Assert.Equal("Yes", answer.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Diagnostics_reach_the_document_and_the_host_also_for_files_that_are_not_open()
    {
        await using var workspace = new FakeWorkspace();
        var published = new ConcurrentQueue<LanguageServerDiagnosticsEventArgs>();
        workspace.Host.DiagnosticsPublished += (_, e) => published.Enqueue(e);
        var (document, server) = await workspace.OpenAsync("main.py", "import os\nprint(valu)\n");
        var other = workspace.Write("other.py", "first\nsecond line\n");
        var changed = new TaskCompletionSource();
        document.DiagnosticsChanged += (_, _) => changed.TrySetResult();

        await server.NotifyClientAsync("textDocument/publishDiagnostics", new JsonObject
        {
            ["uri"] = DocumentUri.FromPath(document.FilePath),
            ["diagnostics"] = new JsonArray(
                Diagnostic(1, 6, 1, 10, 1, "reportUndefinedVariable", "\"valu\" is not defined"),
                Diagnostic(0, 7, 0, 9, 4, null, "\"os\" is not accessed", unnecessary: true)),
        });
        await server.NotifyClientAsync("textDocument/publishDiagnostics", new JsonObject
        {
            ["uri"] = DocumentUri.FromPath(other),
            ["diagnostics"] = new JsonArray(Diagnostic(1, 0, 1, 6, 2, 1234, "Unused")),
        });
        await changed.Task.WaitAsync(TestTimeouts.Default);
        await WaitUntilAsync(() => published.Count == 2);

        var error = document.Diagnostics[0];
        Assert.Equal(new TextSpan(16, 4), error.Span);
        Assert.Equal((2, 7), (error.Line, error.Column));
        Assert.Equal(LspDiagnosticSeverity.Error, error.Severity);
        Assert.Equal("reportUndefinedVariable", error.Code);
        Assert.Equal(new Uri("https://example.com/rules/reportUndefinedVariable"), error.CodeLink);
        Assert.Equal("Pyright", error.Source);
        Assert.True(document.Diagnostics[1].IsUnnecessary);
        Assert.Equal(LspDiagnosticSeverity.Hint, document.Diagnostics[1].Severity);
        var forOther = published.Single(p => p.FilePath == other).Diagnostics.Single();
        Assert.Equal(new TextSpan(6, 6), forOther.Span);
        Assert.Equal("1234", forOther.Code);
    }

    private static JsonObject Diagnostic(int startLine, int startCharacter, int endLine, int endCharacter, int severity, JsonNode? code, string message, bool unnecessary = false)
    {
        var diagnostic = new JsonObject
        {
            ["range"] = FakeWorkspace.Range(startLine, startCharacter, endLine, endCharacter),
            ["severity"] = severity,
            ["source"] = "Pyright",
            ["message"] = message,
        };
        if (code is not null)
        {
            diagnostic["code"] = code;
            diagnostic["codeDescription"] = new JsonObject { ["href"] = $"https://example.com/rules/{code}" };
        }

        if (unnecessary)
        {
            diagnostic["tags"] = new JsonArray(1);
        }

        return diagnostic;
    }

    private static int Version(JsonElement change) => change.GetProperty("textDocument").GetProperty("version").GetInt32();

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TestTimeouts.Default;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out.");
            await Task.Delay(20);
        }
    }
}
