using Android.App;
using Android.Content;
using Android.Content.Res;
using System.Runtime.Versioning;
using Android.OS;
using TabStudio.App.Platform;

namespace TabStudio.Android;

/// <summary>
/// The device status on Android: memory and cores from <see cref="ActivityManager"/>, temperature from
/// <see cref="PowerManager"/>'s thermal status (Android 10 and later), and memory pressure from
/// <c>onTrimMemory</c>.
/// </summary>
public sealed class AndroidDeviceStatus : Java.Lang.Object, IDeviceStatus, IComponentCallbacks2, PowerManager.IOnThermalStatusChangedListener
{
    private readonly Context _context;
    private readonly BatteryReceiver _batteryReceiver;

    /// <summary>Reads the hardware and starts listening for thermal and memory events.</summary>
    /// <param name="context">The application context.</param>
    public AndroidDeviceStatus(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context.ApplicationContext ?? context;
        _batteryReceiver = new BatteryReceiver(this);
        var battery = _context.RegisterReceiver(_batteryReceiver, new IntentFilter(Intent.ActionBatteryChanged));
        if (battery is not null) ReadBattery(battery);
        var activityManager = (ActivityManager)context.GetSystemService(Context.ActivityService)!;
        var memory = new ActivityManager.MemoryInfo();
        activityManager.GetMemoryInfo(memory);
        Device = new DeviceInfo(memory.TotalMem, Java.Lang.Runtime.GetRuntime()!.AvailableProcessors(), activityManager.IsLowRamDevice);

        _context.RegisterComponentCallbacks(this);
        if (OperatingSystem.IsAndroidVersionAtLeast(29) && context.GetSystemService(Context.PowerService) is PowerManager power)
        {
            Thermal = Map(power.CurrentThermalStatus);
            power.AddThermalStatusListener(this);
        }
    }

    /// <inheritdoc />
    public DeviceInfo Device { get; }

    /// <inheritdoc />
    public ThermalState Thermal { get; private set; } = ThermalState.Normal;

    /// <inheritdoc />
    public int BatteryPercent { get; private set; } = -1;

    /// <inheritdoc />
    public bool IsCharging { get; private set; }

    /// <inheritdoc />
    public event EventHandler? PowerChanged;

    /// <inheritdoc />
    public event EventHandler? ThermalChanged;

    /// <inheritdoc />
    public event EventHandler<MemoryPressure>? MemoryPressureRaised;

    /// <inheritdoc />
    [SupportedOSPlatform("android29.0")]
    public void OnThermalStatusChanged(ThermalStatus status)
    {
        var state = Map(status);
        if (state != Thermal)
        {
            Thermal = state;
            ThermalChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void OnTrimMemory(TrimMemory level)
    {
        var pressure = level switch
        {
            TrimMemory.RunningCritical or TrimMemory.Complete => MemoryPressure.Critical,
            TrimMemory.RunningLow or TrimMemory.RunningModerate or TrimMemory.Moderate or TrimMemory.Background => MemoryPressure.Moderate,
            _ => (MemoryPressure?)null,
        };

        if (pressure is { } value)
        {
            MemoryPressureRaised?.Invoke(this, value);
        }
    }

    /// <inheritdoc />
    public void OnLowMemory() => MemoryPressureRaised?.Invoke(this, MemoryPressure.Critical);

    /// <inheritdoc />
    public void OnConfigurationChanged(Configuration newConfig)
    {
    }

    private void ReadBattery(Intent intent)
    {
        var level = intent.GetIntExtra(BatteryManager.ExtraLevel, -1);
        var scale = intent.GetIntExtra(BatteryManager.ExtraScale, 100);
        var status = intent.GetIntExtra(BatteryManager.ExtraStatus, -1);
        var percent = level >= 0 && scale > 0 ? Math.Clamp(level * 100 / scale, 0, 100) : -1;
        var charging = status is (int)BatteryStatus.Charging or (int)BatteryStatus.Full;
        if (percent == BatteryPercent && charging == IsCharging) return;
        BatteryPercent = percent;
        IsCharging = charging;
        PowerChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context.UnregisterReceiver(_batteryReceiver);
            _context.UnregisterComponentCallbacks(this);
            if (OperatingSystem.IsAndroidVersionAtLeast(29) && _context.GetSystemService(Context.PowerService) is PowerManager power)
                power.RemoveThermalStatusListener(this);
            _batteryReceiver.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class BatteryReceiver(AndroidDeviceStatus owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null) owner.ReadBattery(intent);
        }
    }

    [SupportedOSPlatform("android29.0")]
    private static ThermalState Map(ThermalStatus status) => status switch
    {
        ThermalStatus.None => ThermalState.Normal,
        ThermalStatus.Light or ThermalStatus.Moderate => ThermalState.Warm,
        _ => ThermalState.Hot,
    };
}
