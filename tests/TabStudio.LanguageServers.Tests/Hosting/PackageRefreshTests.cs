using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.Hosting;

/// <summary>
/// The project setup installs a repository's packages while its first files are already open, so their servers
/// started without them. <see cref="LanguageServerHost.RefreshAsync"/> brings them up to date: without it Pyright
/// squiggled every import of the project's packages red, never having been told about its <c>.venv</c>, and
/// typescript-language-server checked with the TypeScript it came with rather than the workspace's.
/// </summary>
public sealed class PackageRefreshTests
{
    [Fact]
    public async Task Pyright_is_pointed_at_the_venv_the_setup_created_and_reads_its_settings_again()
    {
        await using var workspace = new FakeWorkspace();
        var (_, server) = await workspace.OpenAsync("app.py", "import flask\n");
        var first = await server.WaitForNotificationAsync("workspace/didChangeConfiguration");
        Assert.False(first.GetProperty("settings").GetProperty("python").TryGetProperty("pythonPath", out _));
        var before = await server.RequestClientAsync("workspace/configuration", Items("python"));
        Assert.False(before[0].TryGetProperty("pythonPath", out _));

        var python = workspace.Write(".venv/bin/python", string.Empty);
        await workspace.Host.RefreshAsync();

        var again = await server.WaitForNotificationAsync("workspace/didChangeConfiguration", HasPythonPath);
        Assert.Equal(python, again.GetProperty("settings").GetProperty("python").GetProperty("pythonPath").GetString());
        var after = await server.RequestClientAsync("workspace/configuration", Items("python"));
        Assert.Equal(python, after[0].GetProperty("pythonPath").GetString());
        Assert.Equal("warning", after[0].GetProperty("analysis").GetProperty("diagnosticSeverityOverrides").GetProperty("reportMissingImports").GetString());
        Assert.Single(workspace.Launcher.Started);
    }

    [Fact]
    public async Task A_server_whose_options_name_a_package_the_setup_installed_starts_again_with_it()
    {
        await using var workspace = new FakeWorkspace();
        var logs = new ConcurrentQueue<string>();
        workspace.Host.LogReceived += (_, e) => logs.Enqueue(e.Message);
        var (document, first) = await workspace.OpenAsync("src/index.ts", "const answer: number = 42;\n");
        var initialize = await first.WaitForRequestAsync("initialize");
        Assert.False(initialize.GetProperty("initializationOptions").GetProperty("tsserver").TryGetProperty("path", out _));

        var tsserver = workspace.Write("node_modules/typescript/lib/tsserver.js", "// the workspace's TypeScript");
        await workspace.Host.RefreshAsync();

        var second = await workspace.Launcher.WaitForServerAsync(2);
        var restarted = await second.WaitForRequestAsync("initialize");
        Assert.Equal(tsserver, restarted.GetProperty("initializationOptions").GetProperty("tsserver").GetProperty("path").GetString());
        var reopened = await second.WaitForNotificationAsync("textDocument/didOpen");
        Assert.Equal("const answer: number = 42;\n", reopened.GetProperty("textDocument").GetProperty("text").GetString());
        Assert.Contains(first.Requests, r => r.Method == "shutdown");
        Assert.True(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        Assert.Equal(LanguageServerState.Running, document.ServerState);
        Assert.Contains(logs, l => l.Contains("Restarting TypeScript", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, l => l.Contains("stopped unexpectedly", StringComparison.Ordinal));

        // Nothing changed since: the server keeps running.
        await workspace.Host.RefreshAsync();
        Assert.Equal(2, workspace.Launcher.Started.Count);
    }

    private static bool HasPythonPath(JsonElement parameters) =>
        parameters.GetProperty("settings").GetProperty("python").TryGetProperty("pythonPath", out _);

    private static JsonObject Items(string section) => new() { ["items"] = new JsonArray(new JsonObject { ["section"] = section }) };
}
