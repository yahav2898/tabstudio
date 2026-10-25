namespace TabStudio.App.Platform;

/// <summary>
/// The device status on platforms without thermal or memory-pressure signals, such as the desktop host.
/// </summary>
public sealed class DefaultDeviceStatus : IDeviceStatus
{
    /// <summary>Reads the machine's memory and core count.</summary>
    public DefaultDeviceStatus()
    {
        var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        Device = new DeviceInfo(total > 0 ? total : 8L * 1024 * 1024 * 1024, Environment.ProcessorCount, IsLowRamDevice: false);
    }

    /// <summary>Uses fixed hardware values, for tests.</summary>
    /// <param name="device">The hardware to report.</param>
    public DefaultDeviceStatus(DeviceInfo device)
    {
        Device = device;
    }

    /// <inheritdoc />
    public DeviceInfo Device { get; }

    /// <inheritdoc />
    public ThermalState Thermal { get; private set; } = ThermalState.Normal;

    /// <inheritdoc />
    public event EventHandler? ThermalChanged;

    /// <inheritdoc />
    public event EventHandler<MemoryPressure>? MemoryPressureRaised;

    /// <summary>Changes the reported thermal state. Used by tests and by hosts that learn it another way.</summary>
    /// <param name="state">The new state.</param>
    public void SetThermal(ThermalState state)
    {
        if (Thermal != state)
        {
            Thermal = state;
            ThermalChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Reports memory pressure, as the operating system would.</summary>
    /// <param name="pressure">How urgent it is.</param>
    public void RaiseMemoryPressure(MemoryPressure pressure) => MemoryPressureRaised?.Invoke(this, pressure);
}
