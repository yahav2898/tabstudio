using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Tests.Fakes;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.LanguageServers.Tests.DebugAdapters;

public sealed class EmbeddedAutoFaultProtocolTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(15u, false)]
    [InlineData(3u, true)]
    public async Task Actual_DAP_memory_path_gates_automatic_capture_on_current_ICSR_not_old_CFSR(uint active, bool captures)
    {
        var memoryReads = new ConcurrentQueue<uint>();
        var registers = new Dictionary<string, uint>
        {
            ["(unsigned int)$lr"] = 0xFFFFFFFD, ["(unsigned int)$msp"] = 0x20001000,
            ["(unsigned int)$psp"] = 0x20002000, ["(unsigned int)$pc"] = 0x08000100,
        };
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Capabilities["supportsReadMemoryRequest"] = true;
            adapter.Connection.AddRequestHandler("readMemory", (arguments, _) =>
            {
                var reference = arguments.GetProperty("memoryReference").GetString()!;
                var address = checked((uint)RtosDebugValue.ParseUnsigned(reference));
                memoryReads.Enqueue(address);
                uint[] words = address switch
                {
                    0xE000ED00 => [0x410FC241, active, 0x08000000],
                    0xE000ED28 => [1u << 24, 1u << 30, 0, 0, 0], // Latched even on nonfault stops.
                    0x0800000c => [0x08000101],
                    0x20002000 => [0, 1, 2, 3, 12, 0x08001241, 0x08001235, 1u << 24],
                    _ => throw new DebugAdapterException("Unexpected memory access in fault fixture."),
                };
                var bytes = new byte[words.Length * 4];
                for (var index = 0; index < words.Length; index++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
                Assert.Equal(bytes.Length, arguments.GetProperty("count").GetInt32());
                return Task.FromResult<JsonNode?>(new JsonObject { ["address"] = reference, ["data"] = Convert.ToBase64String(bytes) });
            });
        });
        await using var session = await DebugAdapterSession.StartAsync(
            new DebugAdapterDefinition { Id = "embedded-gdb", Name = "Cortex fault DAP fixture", Command = "fake" }, "/fake", "/work", launcher);
        launcher.Started[0].Connection.AddRequestHandler("evaluate", (arguments, _) =>
        {
            Assert.Equal(42, arguments.GetProperty("frameId").GetInt32());
            var value = registers[arguments.GetProperty("expression").GetString()!];
            return Task.FromResult<JsonNode?>(new JsonObject { ["result"] = "0x" + value.ToString("x", System.Globalization.CultureInfo.InvariantCulture), ["variablesReference"] = 0 });
        });
        var evidence = await CortexFaultCapture.CaptureAutomaticAsync(new Reader(session), "cortex-m-v7");
        Assert.Equal(captures, evidence is not null);
        if (captures)
        {
            Assert.Equal(active, evidence!.ActiveException);
            Assert.Equal(0x08001235u, evidence.Snapshot.StackedWords![6]);
            Assert.Contains(CrashDecoder.DecodeCortexM(evidence.Snapshot).Findings, finding => finding.Message.Contains("Unaligned", StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal(0xE000ED00u, Assert.Single(memoryReads));
            Assert.DoesNotContain(memoryReads, address => address == 0xE000ED28);
        }
        Assert.DoesNotContain(launcher.Started[0].Requests, request => request.Command is "writeMemory" or "continue" or "setVariable");
    }

    private sealed class Reader(DebugAdapterSession session) : ICortexFaultReader
    {
        public Task<byte[]> ReadMemoryAsync(uint address, int count, CancellationToken cancellationToken) =>
            session.ReadMemoryAsync(address, count, cancellationToken);
        public async Task<uint> ReadRegisterAsync(CortexFaultRegister registerKind, CancellationToken cancellationToken)
        {
            var register = registerKind switch
            {
                CortexFaultRegister.Link => "lr", CortexFaultRegister.MainStack => "msp",
                CortexFaultRegister.ProcessStack => "psp", CortexFaultRegister.ProgramCounter => "pc",
                _ => throw new ArgumentOutOfRangeException(nameof(registerKind)),
            };
            return checked((uint)RtosDebugValue.ParseUnsigned((await session.EvaluateAsync("(unsigned int)$" + register, 42,
                DapEvaluateContext.Watch, cancellationToken)).Result));
        }
    }
}
