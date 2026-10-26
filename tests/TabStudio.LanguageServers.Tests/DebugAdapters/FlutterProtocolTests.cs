using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.DebugAdapters;

public sealed class FlutterProtocolTests
{
    [Fact]
    public async Task Flutter_events_open_only_loopback_pages_and_reload_requests_preserve_the_reason()
    {
        var calls = new List<string>();
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            foreach (var command in new[] { "hotReload", "hotRestart" })
            {
                adapter.Connection.AddRequestHandler(command, (args, _) =>
                {
                    calls.Add(command + ":" + args.GetProperty("reason").GetString());
                    return Task.FromResult<JsonNode?>(null);
                });
            }
        });
        await using var session = await DebugAdapterSession.StartAsync(LanguageServerRegistry.BuiltIn.FindAdapter("flutter")!, "/fake/flutter", "/work", launcher);
        var address = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.FlutterWebLaunch += (_, uri) => address.TrySetResult(uri);
        var adapter = Assert.Single(launcher.Started);
        await adapter.Connection.SendEventAsync("flutter.forwardedEvent", new JsonObject
        {
            ["event"] = "app.webLaunchUrl", ["params"] = new JsonObject { ["url"] = "https://untrusted.example/" },
        });
        await session.ReloadFlutterAsync(false, "save", CancellationToken.None);
        Assert.False(address.Task.IsCompleted);
        await adapter.Connection.SendEventAsync("flutter.forwardedEvent", new JsonObject
        {
            ["event"] = "app.webLaunchUrl", ["params"] = new JsonObject { ["url"] = "http://127.0.0.1:12345/" },
        });
        Assert.Equal("http://127.0.0.1:12345/", (await address.Task.WaitAsync(TimeSpan.FromSeconds(5))).AbsoluteUri);
        await session.ReloadFlutterAsync(true, "manual", CancellationToken.None);
        Assert.Equal(["hotReload:save", "hotRestart:manual"], calls);
    }
}
