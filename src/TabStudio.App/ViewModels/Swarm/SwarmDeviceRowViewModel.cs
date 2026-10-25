using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Link;
using TabStudio.App.Swarm;
using TabStudio.Debugging;
using TabStudio.Link.Networking;
using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.ViewModels.Swarm;

/// <summary>One device in the Swarm window: its name, form factor, screen size, state, last update and latency.</summary>
public sealed partial class SwarmDeviceRowViewModel : ObservableObject
{
    /// <summary>Creates the row.</summary>
    /// <param name="device">The device.</param>
    /// <param name="remove">Takes it out of the swarm.</param>
    public SwarmDeviceRowViewModel(SwarmDevice device, IAsyncRelayCommand<SwarmDeviceRowViewModel> remove)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(remove);
        Device = device;
        RemoveCommand = remove;
        Refresh();
    }

    /// <summary>The device.</summary>
    public SwarmDevice Device { get; }

    /// <summary>Takes the device out of the swarm.</summary>
    public IAsyncRelayCommand<SwarmDeviceRowViewModel> RemoveCommand { get; }

    /// <summary>The device's name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    /// <summary>Its screen and system, such as <c>412 × 915 · Phone · Android</c>.</summary>
    [ObservableProperty]
    public partial string Detail { get; private set; } = string.Empty;

    /// <summary>What it does, such as <c>Running HelloAvalonia</c> or <c>Paused · MainViewModel.cs, line 12</c>.</summary>
    [ObservableProperty]
    public partial string StateText { get; private set; } = string.Empty;

    /// <summary>The state pill's word, such as <c>Running</c>.</summary>
    [ObservableProperty]
    public partial string StateLabel { get; private set; } = string.Empty;

    /// <summary>When it last received a build, and its latency, such as <c>Updated just now · 8 ms</c>.</summary>
    [ObservableProperty]
    public partial string UpdateText { get; private set; } = string.Empty;

    /// <summary>Whether it runs the app.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    /// <summary>Whether it is paused, or pausing, by the debugger.</summary>
    [ObservableProperty]
    public partial bool IsPaused { get; private set; }

    /// <summary>Whether the app crashed there or could not start.</summary>
    [ObservableProperty]
    public partial bool IsFailed { get; private set; }

    /// <summary>Whether the device is offline.</summary>
    [ObservableProperty]
    public partial bool IsOffline { get; private set; }

    /// <summary>Whether it is a phone.</summary>
    public bool IsPhone => Device.Kind == LinkDeviceKinds.Phone;

    /// <summary>Whether it is a tablet.</summary>
    public bool IsTablet => Device.Kind == LinkDeviceKinds.Tablet;

    /// <summary>Whether it is a computer.</summary>
    public bool IsComputer => !IsPhone && !IsTablet;

    /// <summary>Reads the device's state again. UI thread.</summary>
    public void Refresh()
    {
        var device = Device;
        var screen = device.Screen;
        var formFactor = SwarmScreens.Describe(SwarmScreens.FormFactorOf(screen.Width, screen.Height));
        Name = device.Name;
        Detail = string.Join(" · ", new[] { SwarmScreens.DescribeSize(screen.Width, screen.Height), formFactor, device.Platform }.Where(p => p.Length > 0));
        var state = device.State;
        IsRunning = state == SwarmDeviceState.Running;
        IsPaused = state is SwarmDeviceState.Paused or SwarmDeviceState.Pausing;
        IsFailed = state is SwarmDeviceState.Crashed or SwarmDeviceState.Failed;
        IsOffline = state == SwarmDeviceState.Offline;
        StateLabel = Label(state);
        StateText = Describe(device);
        var updated = device.LastUpdated is { } when ? $"Updated {LinkDisplay.Ago(when)}" : "No build yet";
        UpdateText = LinkDisplay.Latency(device.Latency) is { } latency && device.IsConnected ? $"{updated} · {latency}" : updated;
    }

    private static string Label(SwarmDeviceState state) => state switch
    {
        SwarmDeviceState.Ready => "Ready",
        SwarmDeviceState.Deploying => "Updating",
        SwarmDeviceState.Starting => "Starting",
        SwarmDeviceState.Running => "Running",
        SwarmDeviceState.Pausing => "Pausing",
        SwarmDeviceState.Paused => "Paused",
        SwarmDeviceState.Exited => "Exited",
        SwarmDeviceState.Crashed => "Crashed",
        SwarmDeviceState.Failed => "Failed",
        _ => "Offline",
    };

    private static string Describe(SwarmDevice device)
    {
        var app = device.AppName ?? "the app";
        return device.State switch
        {
            SwarmDeviceState.Ready => "Waiting for an app",
            SwarmDeviceState.Deploying => $"Receiving {app}",
            SwarmDeviceState.Starting => $"Starting {app}",
            SwarmDeviceState.Running => $"Running {app}",
            SwarmDeviceState.Pausing => $"Pausing {app} at its next statement",
            SwarmDeviceState.Paused => device.Pause is { } pause ? DescribePause(pause) : $"{app} is paused",
            SwarmDeviceState.Exited => $"{app} has exited",
            SwarmDeviceState.Crashed => device.LastFault is { } fault ? $"{app} crashed: {fault.Type.Split('.')[^1]}: {fault.Message}" : $"{app} crashed",
            SwarmDeviceState.Failed => device.Detail ?? $"{app} could not start",
            _ => "Offline · waiting for it to come back",
        };
    }

    private static string DescribePause(SwarmPause pause)
    {
        var where = pause.Location is { } location
            ? string.Create(CultureInfo.CurrentCulture, $"{Path.GetFileName(location.FilePath)}, line {location.Line}")
            : "external code";
        return pause.Reason switch
        {
            PauseReason.Breakpoint => $"Paused at a breakpoint · {where}",
            PauseReason.Exception => $"Paused at an exception · {where}",
            PauseReason.Pause => $"Paused (Break All) · {where}",
            _ => $"Paused · {where}",
        };
    }
}
