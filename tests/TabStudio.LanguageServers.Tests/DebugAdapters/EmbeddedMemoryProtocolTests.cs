using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.DebugAdapters;

public sealed class EmbeddedMemoryProtocolTests
{
    [Fact]
    public async Task Memory_requests_use_actual_DAP_base64_payload_and_complete_transfer_validation()
    {
        var requests = new List<JsonElement>();
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Connection.AddRequestHandler("readMemory", (args, _) =>
            {
                requests.Add(args.Clone());
                return Task.FromResult<JsonNode?>(new JsonObject { ["address"] = "0x40000000", ["data"] = Convert.ToBase64String([1, 2, 3, 4]) });
            });
            adapter.Connection.AddRequestHandler("writeMemory", (args, _) =>
            {
                requests.Add(args.Clone());
                return Task.FromResult<JsonNode?>(new JsonObject { ["bytesWritten"] = 4 });
            });
        });
        await using var session = await Start(launcher);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await session.ReadMemoryAsync(0x40000000, 4, CancellationToken.None));
        await session.WriteMemoryAsync(0x40000000, new byte[] { 4, 3, 2, 1 }, CancellationToken.None);
        Assert.Equal("0x40000000", requests[0].GetProperty("memoryReference").GetString());
        Assert.Equal(4, requests[0].GetProperty("count").GetInt32());
        Assert.False(requests[1].GetProperty("allowPartial").GetBoolean());
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, Convert.FromBase64String(requests[1].GetProperty("data").GetString()!));
    }

    [Theory]
    [InlineData("AQI=", 0)]
    [InlineData("AQIDBA==", 1)]
    [InlineData("not base64", 0)]
    public async Task Partial_or_invalid_memory_reads_are_refused(string data, int unreadable)
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter => adapter.Connection.AddRequestHandler("readMemory", (_, _) =>
            Task.FromResult<JsonNode?>(new JsonObject { ["address"] = "0x40000000", ["data"] = data, ["unreadableBytes"] = unreadable })));
        await using var session = await Start(launcher);
        await Assert.ThrowsAsync<DebugAdapterException>(() => session.ReadMemoryAsync(0x40000000, 4, CancellationToken.None));
    }

    [Fact]
    public async Task A_partial_memory_write_reports_failure_instead_of_retrying()
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter => adapter.Connection.AddRequestHandler("writeMemory", (_, _) =>
            Task.FromResult<JsonNode?>(new JsonObject { ["bytesWritten"] = 1 })));
        await using var session = await Start(launcher);
        await Assert.ThrowsAsync<DebugAdapterException>(() => session.WriteMemoryAsync(0x40000000, new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
    }

    [Theory]
    [InlineData("0x40000004")]
    [InlineData("opaque-address")]
    public async Task A_memory_read_at_a_different_or_unverifiable_address_is_refused(string address)
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter => adapter.Connection.AddRequestHandler("readMemory", (_, _) =>
            Task.FromResult<JsonNode?>(new JsonObject { ["address"] = address, ["data"] = "AQIDBA==" })));
        await using var session = await Start(launcher);
        await Assert.ThrowsAsync<DebugAdapterException>(() => session.ReadMemoryAsync(0x40000000, 4, CancellationToken.None));
    }

    [Fact]
    public async Task Memory_request_bounds_are_checked_before_contacting_the_adapter()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await Start(launcher);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ReadMemoryAsync(0, 1024 * 1024 + 1, CancellationToken.None));
        await Assert.ThrowsAsync<OverflowException>(() => session.ReadMemoryAsync(ulong.MaxValue, 4, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.WriteMemoryAsync(0, ReadOnlyMemory<byte>.Empty, CancellationToken.None));
        Assert.DoesNotContain(launcher.Started[0].Requests, request => request.Command is "readMemory" or "writeMemory");
    }

    private static Task<DebugAdapterSession> Start(FakeDebugAdapter.Launcher launcher) => DebugAdapterSession.StartAsync(
        new DebugAdapterDefinition { Id = "fake", Name = "Fake", Command = "fake" }, "/fake", "/work", launcher);
}
