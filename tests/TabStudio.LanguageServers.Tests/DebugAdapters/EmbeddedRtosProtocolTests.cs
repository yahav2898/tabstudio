using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Tests.Fakes;
using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.LanguageServers.Tests.DebugAdapters;

public sealed class EmbeddedRtosProtocolTests
{
    [Theory]
    [InlineData(true, 4UL)]
    [InlineData(false, 0UL)]
    public async Task Actual_DAP_expression_and_base64_memory_path_reports_only_verified_bounded_stack_fill(bool growsDown, ulong expectedUnused)
    {
        var requests = new ConcurrentBag<(ulong Address, int Count)>();
        var expressions = Expressions();
        var memory = Memory();
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Capabilities["supportsReadMemoryRequest"] = true;
            adapter.Connection.AddRequestHandler("readMemory", (args, _) =>
            {
                var reference = args.GetProperty("memoryReference").GetString()!;
                var address = RtosDebugValue.ParseUnsigned(reference);
                var count = args.GetProperty("count").GetInt32();
                requests.Add((address, count));
                var bytes = Enumerable.Range(0, count).Select(index => memory[checked(address + (ulong)index)]).ToArray();
                return Task.FromResult<JsonNode?>(new JsonObject { ["address"] = reference, ["data"] = Convert.ToBase64String(bytes) });
            });
        });
        await using var session = await Start(launcher);
        SetExpressions(launcher, expressions);
        var profile = new EmbeddedRtosProfile
        {
            Kind = EmbeddedRtosKind.Zephyr, StackFillByte = 170, StackGrowsDown = growsDown,
            MaximumStackScanBytes = 8, MaximumTotalStackScanBytes = 8,
            StackMemory = [new(0x1000, 32)], TaskLimits = [new("Sensor", 5, 8, 0x100)],
        };
        var threads = await RtosThreadReader.ReadZephyrAsync(new Reader(session), profile.ToReadOptions());
        Assert.Equal(2, threads.Count);
        Assert.Equal("Sensor", threads[0].Name);
        Assert.Equal(-1, threads[0].Priority);
        Assert.Equal(expectedUnused, threads[0].MinimumFreeStackBytes);
        Assert.Null(threads[1].MinimumFreeStackBytes); // Shared budget is exhausted, never a partial scan.
        Assert.Single(requests, request => request.Count == 8);
        Assert.DoesNotContain(requests, request => request.Address == 0x1010);
        Assert.True(Assert.Single(profile.CheckTaskLimits(threads)).BelowMinimum);
        Assert.DoesNotContain(launcher.Started[0].Requests, request => request.Command is "writeMemory" or "continue" or "next" or "setVariable");
    }

    [Fact]
    public async Task Reviewed_RAM_map_refuses_an_outside_stack_without_guessing_or_reading_it()
    {
        var expressions = Expressions();
        var requests = new ConcurrentBag<ulong>();
        var memory = Memory();
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Connection.AddRequestHandler("readMemory", (args, _) =>
            {
                var reference = args.GetProperty("memoryReference").GetString()!;
                var address = RtosDebugValue.ParseUnsigned(reference);
                requests.Add(address);
                var count = args.GetProperty("count").GetInt32();
                return Task.FromResult<JsonNode?>(new JsonObject { ["address"] = reference,
                    ["data"] = Convert.ToBase64String(Enumerable.Range(0, count).Select(index => memory[address + (ulong)index]).ToArray()) });
            });
        });
        await using var session = await Start(launcher);
        SetExpressions(launcher, expressions);
        var threads = await RtosThreadReader.ReadZephyrAsync(new Reader(session), new(StackFillByte: 170,
            StackMemory: [new(0x8000, 4096)]));
        Assert.All(threads, thread => Assert.Null(thread.MinimumFreeStackBytes));
        Assert.DoesNotContain(requests, address => address is 0x1000 or 0x1010);
    }

    private static Dictionary<string, ulong> Expressions() => new()
    {
        ["(unsigned long long)_kernel.threads"] = 0x100,
        ["(unsigned long long)&((struct k_thread*)0x100)->name[0]"] = 0x300,
        ["(int)((struct k_thread*)0x100)->base.prio + 128"] = 127,
        ["((struct k_thread*)0x100)->base.thread_state"] = 4,
        ["(unsigned long long)((struct k_thread*)0x100)->stack_info.start"] = 0x1000,
        ["((struct k_thread*)0x100)->stack_info.size"] = 8,
        ["(unsigned long long)((struct k_thread*)0x100)->next_thread"] = 0x200,
        ["(unsigned long long)&((struct k_thread*)0x200)->name[0]"] = 0x400,
        ["(int)((struct k_thread*)0x200)->base.prio + 128"] = 130,
        ["((struct k_thread*)0x200)->base.thread_state"] = 2,
        ["(unsigned long long)((struct k_thread*)0x200)->stack_info.start"] = 0x1010,
        ["((struct k_thread*)0x200)->stack_info.size"] = 8,
        ["(unsigned long long)((struct k_thread*)0x200)->next_thread"] = 0,
    };
    private static Dictionary<ulong, byte> Memory()
    {
        var memory = new Dictionary<ulong, byte>();
        void Add(ulong address, byte[] bytes)
        {
            for (var index = 0; index < bytes.Length; index++) memory[address + (ulong)index] = bytes[index];
        }
        Add(0x300, "Sensor\0"u8.ToArray()); Add(0x400, "Control\0"u8.ToArray());
        Add(0x1000, [170, 170, 170, 170, 1, 2, 3, 4]); Add(0x1010, [170, 170, 170, 170, 170, 170, 1, 2]);
        return memory;
    }
    private sealed class Reader(DebugAdapterSession session) : IRtosDebugReader
    {
        public async Task<ulong> EvaluateUnsignedAsync(string expression, CancellationToken cancellationToken) =>
            RtosDebugValue.ParseUnsigned((await session.EvaluateAsync(expression, null, DapEvaluateContext.Watch, cancellationToken)).Result);
        public Task<byte[]> ReadMemoryAsync(ulong address, int count, CancellationToken cancellationToken) => session.ReadMemoryAsync(address, count, cancellationToken);
    }
    private static void SetExpressions(FakeDebugAdapter.Launcher launcher, Dictionary<string, ulong> expressions) =>
        launcher.Started[0].Connection.AddRequestHandler("evaluate", (args, _) =>
        {
            var expression = args.GetProperty("expression").GetString()!;
            var value = expressions.TryGetValue(expression, out var found) ? found : throw new DebugAdapterException("No matching debug symbol.");
            return Task.FromResult<JsonNode?>(new JsonObject { ["result"] = value.ToString(CultureInfo.InvariantCulture), ["variablesReference"] = 0 });
        });
    private static Task<DebugAdapterSession> Start(FakeDebugAdapter.Launcher launcher) => DebugAdapterSession.StartAsync(
        new DebugAdapterDefinition { Id = "fake-rtos", Name = "RTOS protocol fixture", Command = "fake" }, "/fake", "/work", launcher);
}
