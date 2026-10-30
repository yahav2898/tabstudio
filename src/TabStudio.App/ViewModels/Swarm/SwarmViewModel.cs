using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Swarm;
using TabStudio.Link.Networking;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.ViewModels.Swarm;

/// <summary>
/// The Swarm tool window, on the tablet: the devices of the swarm with their form factor, screen size, state, last
/// update and latency; Add a device, which shows the QR code; Remove; and the toggles Mirror input and Pause all on a
/// breakpoint. The UI test recorder sits at its bottom. The status bar shows "Swarm: 3 devices" from it.
/// </summary>
public sealed partial class SwarmViewModel : ObservableObject, IDisposable
{
    /// <summary>How often "updated 5 s ago" and the latencies are read again.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    /// <summary>How long a mirrored input that went nowhere stays in the window.</summary>
    private static readonly TimeSpan NoticeTime = TimeSpan.FromSeconds(4);

    private readonly SwarmCoordinatorService _swarm;
    private readonly IUiDispatcher _ui;
    private readonly CancellationTokenSource _closing = new();
    private int _notices;

    /// <summary>Creates the window's view model.</summary>
    /// <param name="swarm">The tablet's swarm.</param>
    /// <param name="ui">Gets refreshes onto the UI thread.</param>
    /// <param name="recorder">The UI test recorder.</param>
    /// <param name="runUiTests">Runs every recorded UI test on every screen, from the Test Explorer.</param>
    public SwarmViewModel(SwarmCoordinatorService swarm, IUiDispatcher ui, UiTestRecorderViewModel recorder, Func<Task>? runUiTests = null)
    {
        ArgumentNullException.ThrowIfNull(swarm);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(recorder);
        _swarm = swarm;
        _ui = ui;
        Recorder = recorder;
        RunUiTestsCommand = new AsyncRelayCommand(runUiTests ?? (() => Task.CompletedTask), () => runUiTests is not null);
        Invite = new SwarmInviteViewModel(swarm);
        _swarm.Changed += OnSwarmChanged;
        _swarm.Mirror.DeliveryFailed += OnMirrorFailed;
        Refresh();
        _ = RefreshPeriodicallyAsync(_closing.Token);
    }

    /// <summary>The tablet's swarm.</summary>
    public SwarmCoordinatorService Service => _swarm;

    /// <summary>Opens the Device Matrix, which shows the devices with the app's other screens; the workspace sets it.</summary>
    public ICommand? OpenDeviceMatrixCommand { get; set; }

    /// <summary>The Add a device dialog, with the QR code.</summary>
    public SwarmInviteViewModel Invite { get; }

    /// <summary>The UI test recorder.</summary>
    public UiTestRecorderViewModel Recorder { get; }

    /// <summary>Runs every recorded UI test on every screen, from the Test Explorer.</summary>
    public IAsyncRelayCommand RunUiTestsCommand { get; }

    /// <summary>The devices, in the order they joined.</summary>
    public ObservableCollection<SwarmDeviceRowViewModel> Devices { get; } = [];

    /// <summary>Whether the tablet coordinates a swarm.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; private set; }

    /// <summary>Whether any device is in the swarm.</summary>
    [ObservableProperty]
    public partial bool HasDevices { get; private set; }

    /// <summary><see cref="HasDevices"/>, for the refresh timer's thread.</summary>
    private bool _anyDevices;

    /// <summary>The status bar's text, such as "Swarm: 3 devices".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Swarm";

    /// <summary>The status bar's tooltip: what the devices do.</summary>
    [ObservableProperty]
    public partial string StatusTip { get; private set; } = string.Empty;

    /// <summary>Where the swarm listens, such as "Listening on 192.168.1.20:47813".</summary>
    [ObservableProperty]
    public partial string? ListeningText { get; private set; }

    /// <summary>Why the swarm could not start.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>A mirrored input that a screen could not take, for a few seconds.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>Whether every input on any screen happens on every screen.</summary>
    public bool MirrorInput
    {
        get => _swarm.MirrorInput;
        set
        {
            if (_swarm.MirrorInput != value)
            {
                _swarm.MirrorInput = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether a breakpoint on one device pauses every device, until they continue together.</summary>
    public bool PauseAllOnBreakpoint
    {
        get => _swarm.PauseAllOnBreakpoint;
        set
        {
            if (_swarm.PauseAllOnBreakpoint != value)
            {
                _swarm.PauseAllOnBreakpoint = value;
                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _swarm.Changed -= OnSwarmChanged;
        _swarm.Mirror.DeliveryFailed -= OnMirrorFailed;
        _closing.Cancel();
        _closing.Dispose();
        Recorder.Dispose();
    }

    /// <summary>Reads the swarm's state again. UI thread.</summary>
    public void Refresh()
    {
        IsOn = _swarm.IsOn;
        Error = _swarm.Error;
        var devices = _swarm.Devices;
        foreach (var gone in Devices.Where(row => !devices.Contains(row.Device)).ToList())
        {
            Devices.Remove(gone);
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var row = Devices.FirstOrDefault(r => ReferenceEquals(r.Device, devices[i]));
            if (row is null)
            {
                Devices.Insert(Math.Min(i, Devices.Count), new SwarmDeviceRowViewModel(devices[i], RemoveCommand));
            }
            else
            {
                row.Refresh();
            }
        }

        HasDevices = Devices.Count > 0;
        Volatile.Write(ref _anyDevices, HasDevices);
        var connected = _swarm.ConnectedCount;
        StatusText = connected == 1 ? "Swarm: 1 device" : string.Create(CultureInfo.CurrentCulture, $"Swarm: {connected} devices");
        StatusTip = Devices.Count == 0
            ? "No device has joined the swarm yet. Tap to add one."
            : string.Join("\n", Devices.Select(d => $"{d.Name}: {d.StateText}"));
        ListeningText = _swarm.Coordinator is { IsListening: true } coordinator
            ? coordinator.Addresses is [var address, ..]
                ? $"Listening on {new LinkEndpoint(address, coordinator.Port)}"
                : "Listening, but this tablet is on no network a device can reach"
            : null;
        if (Invite.IsOpen)
        {
            Invite.Refresh();
        }
    }

    /// <summary>Starts the swarm, so devices can join.</summary>
    [RelayCommand]
    private async Task StartAsync()
    {
        await _swarm.StartAsync();
        Refresh();
    }

    /// <summary>Stops the swarm: the app stops on every device.</summary>
    [RelayCommand]
    private async Task StopAsync()
    {
        await _swarm.StopAsync();
        Refresh();
    }

    /// <summary>Shows the QR code a device scans to join, starting the swarm when it is off.</summary>
    [RelayCommand]
    private async Task AddDeviceAsync()
    {
        await Invite.OpenAsync();
        Refresh();
    }

    /// <summary>Takes a device out of the swarm; it has to pair again to rejoin.</summary>
    /// <param name="row">The device's row.</param>
    [RelayCommand]
    private async Task RemoveAsync(SwarmDeviceRowViewModel? row)
    {
        if (row is not null && _swarm.Coordinator is { } coordinator)
        {
            await coordinator.RemoveAsync(row.Device);
            Refresh();
        }
    }

    /// <summary>Opens the UI test recorder and starts recording.</summary>
    [RelayCommand]
    private void RecordTest() => Recorder.Open(start: true);

    private void OnSwarmChanged(object? sender, EventArgs e) => Refresh();

    private void OnMirrorFailed(object? sender, SwarmMirrorFailure failure)
    {
        Notice = failure.Message;
        var shown = ++_notices;
        _ = ClearNoticeLaterAsync(shown);
    }

    private async Task ClearNoticeLaterAsync(int shown)
    {
        await Task.Delay(NoticeTime);
        if (shown == _notices)
        {
            Notice = null;
        }
    }

    private async Task RefreshPeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                // Without devices nothing needs refreshing: the UI thread is not woken every two seconds for nothing.
                if (!Volatile.Read(ref _anyDevices))
                {
                    continue;
                }

                _ui.Post(() =>
                {
                    if (!cancellationToken.IsCancellationRequested && HasDevices)
                    {
                        foreach (var row in Devices)
                        {
                            row.Refresh();
                        }
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
    }
}
