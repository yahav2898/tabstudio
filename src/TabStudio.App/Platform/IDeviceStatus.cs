namespace TabStudio.App.Platform;

/// <summary>How warm the device is, from Android's thermal status.</summary>
public enum ThermalState
{
    /// <summary>No throttling.</summary>
    Normal,

    /// <summary>Light or moderate throttling. TabStudio compiles on one thread.</summary>
    Warm,

    /// <summary>Severe throttling or worse. TabStudio also pauses background analysis.</summary>
    Hot,
}

/// <summary>How urgently the operating system wants memory back.</summary>
public enum MemoryPressure
{
    /// <summary>Memory is getting low; drop what is cheap to rebuild.</summary>
    Moderate,

    /// <summary>The app is close to being stopped; drop everything that can be rebuilt.</summary>
    Critical,
}

/// <summary>The hardware TabStudio is running on.</summary>
/// <param name="TotalMemoryBytes">Physical memory, in bytes.</param>
/// <param name="ProcessorCount">Number of CPU cores.</param>
/// <param name="IsLowRamDevice">Whether the operating system classes the device as low-memory, such as Android Go devices.</param>
public sealed record DeviceInfo(long TotalMemoryBytes, int ProcessorCount, bool IsLowRamDevice)
{
    /// <summary>Physical memory in whole gigabytes, rounded to the nearest.</summary>
    public int TotalMemoryGigabytes => (int)Math.Round(TotalMemoryBytes / (1024.0 * 1024 * 1024));
}

/// <summary>
/// What the platform reports about the device while TabStudio runs: its hardware, its temperature and
/// memory pressure. Android implements this with PowerManager and onTrimMemory; the desktop host reports a
/// cool device with plenty of memory.
/// </summary>
public interface IDeviceStatus
{
    /// <summary>The device's hardware.</summary>
    DeviceInfo Device { get; }

    /// <summary>The current thermal state.</summary>
    ThermalState Thermal { get; }

    /// <summary>Raised when <see cref="Thermal"/> changes. May be raised on any thread.</summary>
    event EventHandler? ThermalChanged;

    /// <summary>Raised when the operating system asks the app to release memory. May be raised on any thread.</summary>
    event EventHandler<MemoryPressure>? MemoryPressureRaised;
}
