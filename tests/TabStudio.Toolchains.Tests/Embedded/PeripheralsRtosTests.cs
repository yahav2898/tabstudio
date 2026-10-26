using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class PeripheralsRtosTests
{
    [Fact]
    public async Task FreeRtos_uses_debugger_type_expressions_and_omits_unknown_stack_measurements()
    {
        var target = new FakeReader();
        target.Expressions["(unsigned long long)pxCurrentTCB"] = 0x400;
        target.Expressions["sizeof(pxReadyTasksLists) / sizeof(pxReadyTasksLists[0])"] = 1;
        target.Expressions["(pxReadyTasksLists[0]).uxNumberOfItems"] = 1;
        target.Expressions["(unsigned long long)(pxReadyTasksLists[0]).xListEnd.pxNext"] = 0x200;
        target.Expressions["(unsigned long long)((ListItem_t*)0x200)->pvOwner"] = 0x400;
        target.Expressions["(unsigned long long)((ListItem_t*)0x200)->pxNext"] = 0;
        target.Expressions["(unsigned long long)&((TCB_t*)0x400)->pcTaskName[0]"] = 0x600;
        target.Expressions["((TCB_t*)0x400)->uxPriority"] = 3;
        target.AddMemory(0x600, "Sensor\0"u8.ToArray());
        var thread = Assert.Single(await RtosThreadReader.ReadFreeRtosAsync(target));
        Assert.Equal("Sensor", thread.Name);
        Assert.Equal("Running", thread.State);
        Assert.Equal(3, thread.Priority);
        Assert.Null(thread.StackBytes);
        Assert.Null(thread.MinimumFreeStackBytes);
        Assert.DoesNotContain(target.Reads, read => read.Address < 0x600);
    }

    [Fact]
    public async Task Zephyr_reports_signed_priority_and_scans_only_verified_stack_initialization()
    {
        var target = ZephyrTarget();
        var thread = Assert.Single(await RtosThreadReader.ReadZephyrAsync(target, new(StackFillByte: 0xAA)));
        Assert.Equal("main", thread.Name);
        Assert.Equal(-1, thread.Priority);
        Assert.Equal("Flags 0x4", thread.State);
        Assert.Equal(8UL, thread.StackBytes);
        Assert.Equal(4UL, thread.MinimumFreeStackBytes);
        Assert.Contains(target.Reads, read => read.Address == 0x1000 && read.Count == 8);
        var noFillFact = ZephyrTarget();
        Assert.Null(Assert.Single(await RtosThreadReader.ReadZephyrAsync(noFillFact)).MinimumFreeStackBytes);
        Assert.DoesNotContain(noFillFact.Reads, read => read.Address == 0x1000);
    }

    [Fact]
    public async Task Corrupt_Rtos_linked_lists_are_bounded()
    {
        var target = ZephyrTarget();
        target.Expressions["(unsigned long long)((struct k_thread*)0x100)->next_thread"] = 0x100;
        await Assert.ThrowsAsync<InvalidDataException>(() => RtosThreadReader.ReadZephyrAsync(target));
        var noSymbols = new FakeReader();
        await Assert.ThrowsAsync<NotSupportedException>(() => RtosThreadReader.ReadFreeRtosAsync(noSymbols));
    }

    private static FakeReader ZephyrTarget()
    {
        var target = new FakeReader();
        target.Expressions["(unsigned long long)_kernel.threads"] = 0x100;
        target.Expressions["(unsigned long long)&((struct k_thread*)0x100)->name[0]"] = 0x300;
        target.Expressions["(int)((struct k_thread*)0x100)->base.prio + 128"] = 127;
        target.Expressions["((struct k_thread*)0x100)->base.thread_state"] = 4;
        target.Expressions["(unsigned long long)((struct k_thread*)0x100)->stack_info.start"] = 0x1000;
        target.Expressions["((struct k_thread*)0x100)->stack_info.size"] = 8;
        target.Expressions["(unsigned long long)((struct k_thread*)0x100)->next_thread"] = 0;
        target.AddMemory(0x300, "main\0"u8.ToArray());
        target.AddMemory(0x1000, [0xAA, 0xAA, 0xAA, 0xAA, 1, 2, 3, 4]);
        return target;
    }

    private sealed class FakeReader : IRtosDebugReader
    {
        public Dictionary<string, ulong> Expressions { get; } = [];
        public Dictionary<ulong, byte> Memory { get; } = [];
        public List<(ulong Address, int Count)> Reads { get; } = [];

        public void AddMemory(ulong address, byte[] data)
        {
            for (var i = 0; i < data.Length; i++) Memory[address + (ulong)i] = data[i];
        }

        public Task<ulong> EvaluateUnsignedAsync(string expression, CancellationToken cancellationToken) =>
            Task.FromResult(Expressions.TryGetValue(expression, out var value) ? value : throw new NotSupportedException("No matching symbol: " + expression));

        public Task<byte[]> ReadMemoryAsync(ulong address, int count, CancellationToken cancellationToken)
        {
            Reads.Add((address, count));
            return Task.FromResult(Enumerable.Range(0, count).Select(i => Memory[address + (ulong)i]).ToArray());
        }
    }
}
