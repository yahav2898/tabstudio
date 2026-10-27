using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>The actual RTOS whose debug types should be read.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EmbeddedRtosKind>))]
public enum EmbeddedRtosKind
{
    /// <summary>FreeRTOS TCB_t/ListItem_t lists.</summary>
    FreeRtos,
    /// <summary>Zephyr CONFIG_THREAD_MONITOR list.</summary>
    Zephyr,
}

/// <summary>An optional reviewed readable RAM range for stack scans; not a guessed chip memory map.</summary>
public sealed record RtosStackMemoryRange(ulong Start, ulong Length);

/// <summary>A named task's reviewed free-stack threshold and optional expected allocation, all in bytes.</summary>
/// <param name="Name">Exact target-reported task name.</param>
/// <param name="MinimumFreeBytes">Warn when the verified fill-pattern minimum unused extent falls below this value.</param>
/// <param name="ExpectedStackBytes">Optional actual allocation; a mismatch invalidates the threshold comparison.</param>
/// <param name="TaskAddress">Optional exact TCB/k_thread address to disambiguate duplicate names.</param>
public sealed record RtosTaskLimit(string Name, ulong MinimumFreeBytes, ulong? ExpectedStackBytes = null, ulong? TaskAddress = null);

/// <summary>A task's threshold result. Missing pattern/extents or an ambiguous task stays explicitly unverified.</summary>
public sealed record RtosTaskLimitFinding(string Name, bool Verified, bool BelowMinimum, string Message);

/// <summary>Bounded explicit RTOS build facts; no fill pattern, growth direction, ABI offset or chip RAM is inferred.</summary>
public sealed record EmbeddedRtosProfile
{
    /// <summary>Workspace-local profile path.</summary>
    public const string FileName = ".tabstudio/embedded-rtos.json";
    /// <summary>Previous supported two-field file, read only when the new profile is absent.</summary>
    public const string LegacyFileName = ".tabstudio/rtos.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip, MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Actual RTOS selected by the developer.</summary>
    public required EmbeddedRtosKind Kind { get; init; }
    /// <summary>Verified stack initialization byte, or null to leave high-water marks unknown.</summary>
    public byte? StackFillByte { get; init; }
    /// <summary>Verified growth direction; required together with StackFillByte.</summary>
    public bool? StackGrowsDown { get; init; }
    /// <summary>Maximum linked-list task count, from 1 to 4096.</summary>
    public int MaximumThreads { get; init; } = 256;
    /// <summary>Maximum complete stack extent to scan, from 1 byte to 1 MiB.</summary>
    public int MaximumStackScanBytes { get; init; } = 64 * 1024;
    /// <summary>Combined stack bytes to scan per snapshot, from 1 byte to 16 MiB.</summary>
    public int MaximumTotalStackScanBytes { get; init; } = 1024 * 1024;
    /// <summary>Complete read-only snapshot timeout, from 1 to 120 seconds.</summary>
    public int TimeoutSeconds { get; init; } = 30;
    /// <summary>Optional reviewed RAM intervals; scans outside all supplied ranges are omitted.</summary>
    public IReadOnlyList<RtosStackMemoryRange> StackMemory { get; init; } = [];
    /// <summary>Optional exact task thresholds, in bytes rather than RTOS stack words.</summary>
    public IReadOnlyList<RtosTaskLimit> TaskLimits { get; init; } = [];

    /// <summary>Reads strict bounded JSON. Duplicate/unknown fields and incomplete fill facts fail explicitly.</summary>
    public static EmbeddedRtosProfile Parse(string json)
    {
        using var document = ReadDocument(json);
        var profile = document.RootElement.Deserialize<EmbeddedRtosProfile>(JsonOptions) ?? throw new InvalidDataException("RTOS profile is empty.");
        profile.Validate();
        return profile;
    }

    /// <summary>Preserves the old verifiedStackFillByte/stackGrowsDown profile after applying the same bounds/pair validation.</summary>
    public static EmbeddedRtosProfile ParseLegacy(string json, EmbeddedRtosKind kind)
    {
        using var document = ReadDocument(json);
        var legacy = document.RootElement.Deserialize<LegacyProfile>(JsonOptions) ?? throw new InvalidDataException("Legacy RTOS profile is empty.");
        var profile = new EmbeddedRtosProfile { Kind = kind, StackFillByte = legacy.VerifiedStackFillByte, StackGrowsDown = legacy.StackGrowsDown, MaximumThreads = 1024 };
        profile.Validate();
        return profile;
    }

    /// <summary>Creates read limits after validating all developer-supplied facts.</summary>
    public RtosReadOptions ToReadOptions()
    {
        Validate();
        return new(StackFillByte, StackGrowsDown ?? true, MaximumStackScanBytes, MaximumThreads,
            MaximumTotalStackScanBytes, StackMemory);
    }

    /// <summary>Validates configuration without reading target memory.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || StackFillByte.HasValue != StackGrowsDown.HasValue
            || MaximumThreads is < 1 or > 4096 || MaximumStackScanBytes is < 1 or > 1024 * 1024
            || MaximumTotalStackScanBytes is < 1 or > 16 * 1024 * 1024 || MaximumTotalStackScanBytes < MaximumStackScanBytes
            || TimeoutSeconds is < 1 or > 120 || StackMemory is null || TaskLimits is null || StackMemory.Count > 64 || TaskLimits.Count > 256)
        {
            throw new InvalidDataException("RTOS profile needs paired actual fill/growth facts, 1–4096 tasks, ≤1 MiB per stack/16 MiB total, and a 1–120-second timeout.");
        }
        foreach (var range in StackMemory)
        {
            if (range is null || range.Start == 0 || range.Length == 0 || range.Start > ulong.MaxValue - range.Length)
                throw new InvalidDataException("RTOS stack RAM ranges must be nonzero, non-overflowing actual memory intervals.");
        }
        foreach (var limit in TaskLimits)
        {
            if (limit is null || string.IsNullOrWhiteSpace(limit.Name) || limit.Name.Length > 128 || limit.Name.Any(char.IsControl)
                || limit.MinimumFreeBytes > (ulong)MaximumStackScanBytes || limit.ExpectedStackBytes is 0 or > 1024 * 1024
                || limit.ExpectedStackBytes is { } size && limit.MinimumFreeBytes > size || limit.TaskAddress == 0)
            {
                throw new InvalidDataException("RTOS task limits require an exact name/address and valid allocation/free-byte thresholds.");
            }
        }
        if (TaskLimits.GroupBy(limit => (limit.Name, limit.TaskAddress)).Any(group => group.Count() > 1))
            throw new InvalidDataException("RTOS task thresholds contain a duplicate name/address.");
    }

    /// <summary>Checks only actual matching task snapshots; unknown evidence cannot become a passing threshold.</summary>
    public IReadOnlyList<RtosTaskLimitFinding> CheckTaskLimits(IReadOnlyList<RtosThread> threads)
    {
        Validate();
        return TaskLimits.Select(limit =>
        {
            var matches = threads.Where(thread => thread.Name == limit.Name && (limit.TaskAddress is null || limit.TaskAddress == thread.Address)).ToArray();
            if (matches.Length != 1) return new RtosTaskLimitFinding(limit.Name, false, false, "Task missing or name ambiguous; supply the actual TCB/k_thread address for duplicate names.");
            var task = matches[0];
            if (task.StackBytes is null || limit.ExpectedStackBytes is { } expected && expected != task.StackBytes)
                return new RtosTaskLimitFinding(limit.Name, false, false, "Actual stack allocation is unavailable or differs from ExpectedStackBytes; threshold unverified.");
            if (task.MinimumFreeStackBytes is not { } unused)
                return new RtosTaskLimitFinding(limit.Name, false, false, "Fill-pattern minimum unused extent is unavailable; initialization facts, memory bounds or scan budgets are missing.");
            return new RtosTaskLimitFinding(limit.Name, true, unused < limit.MinimumFreeBytes,
                $"Verified-pattern minimum unused stack {unused} B; configured minimum {limit.MinimumFreeBytes} B of {task.StackBytes} B allocated.");
        }).ToArray();
    }

    private sealed record LegacyProfile(byte? VerifiedStackFillByte = null, bool? StackGrowsDown = null);
    private static JsonDocument ReadDocument(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 32 * 1024) throw new InvalidDataException("RTOS profile exceeds 32 KiB.");
        var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 8 });
        try { CheckProperties(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void CheckProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate RTOS profile field: " + property.Name);
                CheckProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) CheckProperties(item);
        }
    }
}
