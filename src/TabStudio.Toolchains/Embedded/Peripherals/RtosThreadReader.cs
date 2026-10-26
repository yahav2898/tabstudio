using System.Globalization;
using System.Text;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>Read-only target access. Expressions resolve structure layouts from the firmware's own debug information.</summary>
public interface IRtosDebugReader
{
    /// <summary>Evaluates a debugger expression without calling target functions or resuming execution.</summary>
    Task<ulong> EvaluateUnsignedAsync(string expression, CancellationToken cancellationToken);

    /// <summary>Reads a bounded target memory block while all target cores are halted.</summary>
    Task<byte[]> ReadMemoryAsync(ulong address, int count, CancellationToken cancellationToken);
}

/// <summary>A task obtained from the target's actual RTOS structures.</summary>
/// <param name="Address">TCB/k_thread address, used to deduplicate tasks.</param>
/// <param name="Name">Task name from target RAM.</param>
/// <param name="State">List-derived FreeRTOS state or raw Zephyr thread-state flags.</param>
/// <param name="Priority">Target priority; its ordering depends on the RTOS.</param>
/// <param name="StackBytes">Allocated stack extent when that metadata exists.</param>
/// <param name="MinimumFreeStackBytes">Fill-pattern-derived minimum unused stack bytes, only when the caller supplied verified initialization/growth settings.</param>
public sealed record RtosThread(ulong Address, string Name, string State, long Priority, ulong? StackBytes, ulong? MinimumFreeStackBytes);

/// <summary>RTOS build facts that cannot safely be guessed from RAM.</summary>
/// <param name="StackFillByte">Verified stack initialization pattern, or null to omit high-water scanning.</param>
/// <param name="StackGrowsDown">Verified growth direction; relevant only with a fill byte.</param>
/// <param name="MaximumStackScanBytes">Bound on memory read for stack-fill inspection.</param>
/// <param name="MaximumThreads">Corruption/cycle bound for linked lists.</param>
public sealed record RtosReadOptions(byte? StackFillByte = null, bool StackGrowsDown = true,
    int MaximumStackScanBytes = 64 * 1024, int MaximumThreads = 1024);

/// <summary>Reads FreeRTOS lists and Zephyr's monitored thread chain without compiling target-specific structure offsets into TabStudio.</summary>
public static class RtosThreadReader
{
    /// <summary>Reads upstream FreeRTOS task lists with TCB_t/ListItem_t types resolved by the debugger. Missing optional lists are skipped.</summary>
    public static async Task<IReadOnlyList<RtosThread>> ReadFreeRtosAsync(IRtosDebugReader reader, RtosReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        options ??= new();
        Validate(options);
        var result = new Dictionary<ulong, RtosThread>();
        var current = await Optional(reader, "(unsigned long long)pxCurrentTCB", cancellationToken).ConfigureAwait(false);
        var readyCount = await reader.EvaluateUnsignedAsync("sizeof(pxReadyTasksLists) / sizeof(pxReadyTasksLists[0])", cancellationToken).ConfigureAwait(false);
        if (readyCount is 0 or > 256) throw new InvalidDataException("FreeRTOS ready-list count is missing or invalid; matching debug symbols are required.");
        for (ulong i = 0; i < readyCount; i++)
            await ReadList($"pxReadyTasksLists[{i.ToString(CultureInfo.InvariantCulture)}]", "Ready").ConfigureAwait(false);
        foreach (var (expression, state) in new[]
        {
            ("*pxDelayedTaskList", "Blocked"), ("*pxOverflowDelayedTaskList", "Blocked"),
            ("xPendingReadyList", "Pending ready"), ("xSuspendedTaskList", "Suspended"),
            ("xTasksWaitingTermination", "Deleted")
        })
        {
            if (await Optional(reader, $"({expression}).uxNumberOfItems", cancellationToken).ConfigureAwait(false) is not null)
                await ReadList(expression, state).ConfigureAwait(false);
        }
        return result.Values.OrderBy(t => t.Address).ToArray();

        async Task ReadList(string expression, string state)
        {
            var count = await reader.EvaluateUnsignedAsync($"({expression}).uxNumberOfItems", cancellationToken).ConfigureAwait(false);
            if (count > (ulong)options.MaximumThreads) throw new InvalidDataException("FreeRTOS task list exceeds the configured bound.");
            var item = await reader.EvaluateUnsignedAsync($"(unsigned long long)({expression}).xListEnd.pxNext", cancellationToken).ConfigureAwait(false);
            var seenItems = new HashSet<ulong>();
            for (ulong i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item == 0 || !seenItems.Add(item)) throw new InvalidDataException("FreeRTOS task list is null, cyclic or corrupt.");
                var prefix = $"((ListItem_t*)0x{item:X})";
                var tcb = await reader.EvaluateUnsignedAsync($"(unsigned long long){prefix}->pvOwner", cancellationToken).ConfigureAwait(false);
                if (tcb == 0) throw new InvalidDataException("FreeRTOS list item has no task owner.");
                if (result.Count >= options.MaximumThreads && !result.ContainsKey(tcb)) throw new InvalidDataException("Too many RTOS tasks.");
                var task = $"((TCB_t*)0x{tcb:X})";
                var nameAddress = await reader.EvaluateUnsignedAsync($"(unsigned long long)&{task}->pcTaskName[0]", cancellationToken).ConfigureAwait(false);
                var name = await ReadName(reader, nameAddress, cancellationToken).ConfigureAwait(false);
                var priority = await reader.EvaluateUnsignedAsync($"{task}->uxPriority", cancellationToken).ConfigureAwait(false);
                ulong? stackSize = null;
                ulong? highWater = null;
                var stack = await Optional(reader, $"(unsigned long long){task}->pxStack", cancellationToken).ConfigureAwait(false);
                var end = await Optional(reader, $"(unsigned long long){task}->pxEndOfStack", cancellationToken).ConfigureAwait(false);
                var elementSize = await Optional(reader, "sizeof(StackType_t)", cancellationToken).ConfigureAwait(false);
                if (stack is { } start && end is { } finish && elementSize is > 0 and <= 16 && finish >= start)
                {
                    stackSize = checked(finish - start + elementSize.Value);
                    highWater = await ScanStack(reader, start, stackSize.Value, options, cancellationToken).ConfigureAwait(false);
                }
                result[tcb] = new(tcb, name, current == tcb ? "Running" : state, checked((long)priority), stackSize, highWater);
                item = await reader.EvaluateUnsignedAsync($"(unsigned long long){prefix}->pxNext", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Reads Zephyr CONFIG_THREAD_MONITOR's _kernel.threads chain. Stack information needs CONFIG_THREAD_STACK_INFO; names need CONFIG_THREAD_NAME.</summary>
    public static async Task<IReadOnlyList<RtosThread>> ReadZephyrAsync(IRtosDebugReader reader, RtosReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        options ??= new();
        Validate(options);
        var result = new List<RtosThread>();
        var seen = new HashSet<ulong>();
        var thread = await reader.EvaluateUnsignedAsync("(unsigned long long)_kernel.threads", cancellationToken).ConfigureAwait(false);
        while (thread != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(thread) || result.Count >= options.MaximumThreads) throw new InvalidDataException("Zephyr thread chain is cyclic, corrupt or exceeds its bound.");
            var prefix = $"((struct k_thread*)0x{thread:X})";
            var nameAddress = await Optional(reader, $"(unsigned long long)&{prefix}->name[0]", cancellationToken).ConfigureAwait(false);
            var name = nameAddress is > 0 ? await ReadName(reader, nameAddress.Value, cancellationToken).ConfigureAwait(false) : "Unnamed thread";
            // Cast the signed priority to a biased positive range to avoid a parser guessing the width of a char.
            var priority = await reader.EvaluateUnsignedAsync($"(int){prefix}->base.prio + 128", cancellationToken).ConfigureAwait(false);
            var state = await reader.EvaluateUnsignedAsync($"{prefix}->base.thread_state", cancellationToken).ConfigureAwait(false);
            var stack = await Optional(reader, $"(unsigned long long){prefix}->stack_info.start", cancellationToken).ConfigureAwait(false);
            var size = await Optional(reader, $"{prefix}->stack_info.size", cancellationToken).ConfigureAwait(false);
            var highWater = stack is { } start && size is { } length
                ? await ScanStack(reader, start, length, options, cancellationToken).ConfigureAwait(false) : null;
            result.Add(new(thread, name, $"Flags 0x{state:X}", checked((long)priority) - 128, size, highWater));
            thread = await reader.EvaluateUnsignedAsync($"(unsigned long long){prefix}->next_thread", cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    private static async Task<string> ReadName(IRtosDebugReader reader, ulong address, CancellationToken cancellationToken)
    {
        // Read a byte at a time so a short, valid task-name array cannot cause a read into unmapped memory.
        var bytes = new List<byte>();
        for (var i = 0; i < 128; i++)
        {
            var data = await reader.ReadMemoryAsync(checked(address + (ulong)i), 1, cancellationToken).ConfigureAwait(false);
            if (data.Length != 1) throw new InvalidDataException("Short target memory read while reading an RTOS task name.");
            if (data[0] == 0) return Encoding.UTF8.GetString(bytes.ToArray());
            bytes.Add(data[0]);
        }
        return Encoding.UTF8.GetString(bytes.ToArray()) + "…";
    }

    private static async Task<ulong?> ScanStack(IRtosDebugReader reader, ulong start, ulong size, RtosReadOptions options, CancellationToken cancellationToken)
    {
        if (options.StackFillByte is not { } fill) return null;
        if (size == 0 || size > (ulong)options.MaximumStackScanBytes) return null;
        var bytes = await reader.ReadMemoryAsync(start, checked((int)size), cancellationToken).ConfigureAwait(false);
        if ((ulong)bytes.Length != size) throw new InvalidDataException("Short RTOS stack memory read.");
        ulong unused = 0;
        if (options.StackGrowsDown)
        {
            while (unused < size && bytes[checked((int)unused)] == fill) unused++;
        }
        else
        {
            while (unused < size && bytes[checked((int)(size - unused - 1))] == fill) unused++;
        }
        return unused;
    }

    private static async Task<ulong?> Optional(IRtosDebugReader reader, string expression, CancellationToken cancellationToken)
    {
        try { return await reader.EvaluateUnsignedAsync(expression, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or NotSupportedException) { return null; }
    }

    private static void Validate(RtosReadOptions options)
    {
        if (options.MaximumThreads is < 1 or > 65536 || options.MaximumStackScanBytes is < 1 or > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(options), "RTOS read bounds are outside supported limits.");
    }
}
