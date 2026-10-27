using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.DebugAdapters;

public sealed class EmbeddedDisassemblyProtocolTests
{
    [Fact]
    public async Task Actual_DAP_disassemble_preserves_bytes_symbols_source_inheritance_and_invalid_filler()
    {
        JsonElement request = default;
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Capabilities["supportsDisassembleRequest"] = true;
            adapter.Capabilities["supportsReadMemoryRequest"] = true;
            adapter.Capabilities["supportsWriteMemoryRequest"] = true;
            adapter.Connection.AddRequestHandler("disassemble", (args, _) =>
            {
                request = args.Clone();
                return Task.FromResult(JsonNode.Parse("""
                    {"instructions":[
                      {"address":"0x8000","instruction":"push {r7, lr}","instructionBytes":"80 b5","symbol":"main","location":{"path":"/work/main.c"},"line":12,"column":3},
                      {"address":"32770","instruction":"mov r7, sp","line":13},
                      {"address":"0","instruction":"??","presentationHint":"invalid"}
                    ]}
                    """));
            });
        });
        await using var session = await Start(launcher);
        Assert.True(session.Capabilities.Disassemble);
        Assert.True(session.Capabilities.ReadMemory);
        Assert.True(session.Capabilities.WriteMemory);
        Assert.True(launcher.Started[0].Requests[0].Arguments.GetProperty("supportsMemoryReferences").GetBoolean());
        var rows = await session.DisassembleAsync(0x8000, 3, CancellationToken.None);
        Assert.Equal("0x8000", request.GetProperty("memoryReference").GetString());
        Assert.Equal(3, request.GetProperty("instructionCount").GetInt32());
        Assert.True(request.GetProperty("resolveSymbols").GetBoolean());
        Assert.False(request.TryGetProperty("instructionOffset", out _));
        Assert.Equal(0x8000UL, rows[0].Address);
        Assert.Equal("80 b5", rows[0].Bytes);
        Assert.Equal("main", rows[0].Symbol);
        Assert.Equal("/work/main.c", rows[1].SourcePath);
        Assert.Equal(13, rows[1].Line);
        Assert.True(rows[2].IsInvalid);
    }

    [Theory]
    [InlineData("{\"instructions\":[]}")]
    [InlineData("{\"instructions\":[{\"address\":\"opaque\",\"instruction\":\"nop\"}]}")]
    [InlineData("{\"instructions\":[{\"address\":\"-1\",\"instruction\":\"nop\"}]}")]
    [InlineData("{\"instructions\":[{\"address\":\"0x10000000000000000\",\"instruction\":\"nop\"}]}")]
    [InlineData("{\"instructions\":[{\"address\":\"0x8000\",\"instruction\":\"nop\",\"line\":0}]}")]
    [InlineData("{\"instructions\":[{\"address\":\"0x8000\",\"instruction\":\"nop\",\"column\":-1}]}")]
    [InlineData("{\"instructions\":[{\"address\":\"0x8000\",\"instruction\":\"nop\",\"location\":{\"path\":\"https://untrusted.example/source.c\"}}]}")]
    [InlineData("""{"instructions":[{"address":"0x8000","instruction":"nop","location":{"path":"\\\\server\\share\\source.c"}}]}""")]
    [InlineData("{\"instructions\":[{\"address\":\"0x8000\",\"instruction\":\"nop\\nmalformed\"}]}")]
    public async Task Partial_or_malformed_instruction_and_source_data_is_refused(string response)
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Capabilities["supportsDisassembleRequest"] = true;
            adapter.Connection.AddRequestHandler("disassemble", (_, _) => Task.FromResult(JsonNode.Parse(response)));
        });
        await using var session = await Start(launcher);
        await Assert.ThrowsAsync<DebugAdapterException>(() => session.DisassembleAsync(0x8000, 1, CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_valid_addresses_are_refused_but_unsupported_and_oversized_requests_never_contact_adapter()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var unsupported = await Start(launcher);
        await Assert.ThrowsAsync<DebugAdapterException>(() => unsupported.DisassembleAsync(0, 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => unsupported.DisassembleAsync(0, 257, CancellationToken.None));
        Assert.DoesNotContain(launcher.Started[0].Requests, request => request.Command == "disassemble");
        var duplicate = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Capabilities["supportsDisassembleRequest"] = true;
            adapter.Connection.AddRequestHandler("disassemble", (_, _) => Task.FromResult(JsonNode.Parse("""
                {"instructions":[{"address":"0x8000","instruction":"nop"},{"address":"0x8000","instruction":"nop"}]}
                """)));
        });
        await using var session = await Start(duplicate);
        await Assert.ThrowsAsync<DebugAdapterException>(() => session.DisassembleAsync(0x8000, 2, CancellationToken.None));
    }

    private static Task<DebugAdapterSession> Start(FakeDebugAdapter.Launcher launcher) => DebugAdapterSession.StartAsync(
        new DebugAdapterDefinition { Id = "fake", Name = "Fake", Command = "fake" }, "/fake", "/work", launcher);
}
