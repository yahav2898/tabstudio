using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;

namespace TabStudio.LanguageServers.Tests.Fakes;

/// <summary>A workspace folder with a host whose servers are <see cref="FakeLanguageServer"/>s.</summary>
internal sealed class FakeWorkspace : IAsyncDisposable
{
    private readonly TempDirectory _temp = new();

    public FakeWorkspace(Action<FakeLanguageServer>? configure = null, LanguageServerHostOptions? options = null, string? settings = null)
    {
        if (settings is not null)
        {
            _temp.Write(".tabstudio/settings.json", settings);
        }

        Launcher = new FakeLauncher(configure);
        Host = new LanguageServerHost(WorkspaceConfiguration.Load(_temp.Path), Launcher, options ?? new LanguageServerHostOptions
        {
            RestartWindow = TimeSpan.FromMinutes(1),
        });
    }

    public string Root => _temp.Path;

    public FakeLauncher Launcher { get; }

    public LanguageServerHost Host { get; }

    /// <summary>Writes a file and opens it on its server.</summary>
    public async Task<(LanguageServerDocument Document, FakeLanguageServer Server)> OpenAsync(string relativePath, string text)
    {
        var path = _temp.Write(relativePath, text);
        var document = Host.OpenDocument(path, text) ?? throw new InvalidOperationException($"No server for {relativePath}.");
        Assert.True(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        var server = Launcher.Started[^1];
        await server.WaitForNotificationAsync("textDocument/didOpen", p => p.GetProperty("textDocument").GetProperty("uri").GetString()!.EndsWith(Path.GetFileName(relativePath), StringComparison.Ordinal));
        return (document, server);
    }

    public string Write(string relativePath, string text) => _temp.Write(relativePath, text);

    /// <summary>A JSON range from zero-based lines and characters.</summary>
    public static JsonObject Range(int startLine, int startCharacter, int endLine, int endCharacter) => new()
    {
        ["start"] = new JsonObject { ["line"] = startLine, ["character"] = startCharacter },
        ["end"] = new JsonObject { ["line"] = endLine, ["character"] = endCharacter },
    };

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await Launcher.DisposeAsync();
        _temp.Dispose();
    }
}
