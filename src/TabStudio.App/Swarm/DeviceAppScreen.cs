using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.Swarm;

/// <summary>
/// A phone or tablet of the swarm as a screen on the tablet: a proxy for the app preview of the device, where the app
/// runs, over the swarm's encrypted TabStudio Link connection. Input, pictures, layout checks and texts are asked of the
/// device; the inputs people make there arrive as <see cref="InputCaptured"/>.
/// </summary>
/// <remarks>
/// The <see cref="SwarmCoordinatorService"/> creates one for every device of the swarm and keeps it in the
/// <see cref="AppScreenRegistry"/> while the device is in the swarm, so the Device Matrix shows it.
/// </remarks>
public sealed class DeviceAppScreen : IAppScreen, IAppScreenElements, IAppScreenNotices
{
    private ScreenProfile _profile;
    private bool _isRunning;

    /// <summary>Creates the screen of a device.</summary>
    /// <param name="device">The device, as the coordinator sees it.</param>
    public DeviceAppScreen(SwarmDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Device = device;
        Id = "swarm:" + device.Fingerprint.Value[..16];
        _profile = SwarmScreens.Profile(device.Name, device.Screen);
        _isRunning = IsAppRunning(device);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public event EventHandler<AppInput>? InputCaptured;

    /// <summary>The device.</summary>
    public SwarmDevice Device { get; }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public AppScreenKind Kind => AppScreenKind.Device;

    /// <inheritdoc />
    public ScreenProfile Profile => _profile;

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public Task<bool> SendInputAsync(AppInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Device.SendInputAsync(SwarmScreens.ToSwarm(input), cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]?> CaptureAsync(CancellationToken cancellationToken) => Device.CaptureAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken) =>
        [.. (await Device.InspectLayoutAsync(cancellationToken)).Select(SwarmScreens.ToApp).OfType<LayoutIssue>()];

    /// <inheritdoc />
    public Task<string?> ReadTextAsync(string elementPath, CancellationToken cancellationToken) => Device.ReadTextAsync(elementPath, cancellationToken);

    /// <inheritdoc />
    public void ShowNotice(string text) => _ = Device.ShowNoticeAsync(text);

    /// <summary>Reads the device's state again and tells the screen's watchers when it changed. UI thread.</summary>
    internal void Refresh()
    {
        var profile = SwarmScreens.Profile(Device.Name, Device.Screen);
        var running = IsAppRunning(Device);
        if (profile == _profile && running == _isRunning)
        {
            return;
        }

        _profile = profile;
        _isRunning = running;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A new build started on the device: what the app shows changed. UI thread.</summary>
    internal void OnUpdated() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>An input a person made on the device. UI thread.</summary>
    internal void OnInputCaptured(SwarmInput input) => InputCaptured?.Invoke(this, SwarmScreens.ToApp(input));

    private static bool IsAppRunning(SwarmDevice device) =>
        device.IsConnected && device.State is SwarmDeviceState.Running or SwarmDeviceState.Pausing or SwarmDeviceState.Paused;
}
