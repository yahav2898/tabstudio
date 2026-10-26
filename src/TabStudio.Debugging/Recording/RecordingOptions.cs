namespace TabStudio.Debugging.Recording;

/// <summary>Limits an opt-in C# recording. No recorder is allocated for ordinary debugging.</summary>
public sealed record RecordingOptions
{
    /// <summary>The default encoded history capacity: 64 MiB.</summary>
    public const int DefaultCapacityBytes = 64 * 1024 * 1024;

    /// <summary>Capacity of the binary ring; oldest complete steps are evicted first.</summary>
    public int CapacityBytes { get; init; } = DefaultCapacityBytes;
}
