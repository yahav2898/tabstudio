using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Swarm;
using TabStudio.Swarm;
using TabStudio.Swarm.Device;

namespace TabStudio.App.ViewModels.Swarm;

/// <summary>
/// What a phone or tablet of a swarm shows while it is in one: the app the tablet runs, full screen, under a small
/// overlay that names the tablet and says what the app does, with Leave to be the IDE again. Drawn over everything else.
/// </summary>
public sealed partial class SwarmDevicePageViewModel : ObservableObject, IDisposable
{
    private readonly SwarmMemberService _member;

    /// <summary>Creates the page.</summary>
    /// <param name="member">This device's side of a swarm.</param>
    public SwarmDevicePageViewModel(SwarmMemberService member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _member = member;
        _member.Changed += OnChanged;
        _member.Host.PropertyChanged += OnHostChanged;
        Refresh();
    }

    /// <summary>The device's screen: the app preview, web view or console output it shows.</summary>
    public SwarmDeviceHost Host => _member.Host;

    /// <summary>Whether the page is shown: while the device is in a swarm, or rejoining one.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>The tablet's name.</summary>
    [ObservableProperty]
    public partial string CoordinatorName { get; private set; } = string.Empty;

    /// <summary>What the app does, such as "Running HelloAvalonia" or "Paused on the tablet's breakpoint".</summary>
    [ObservableProperty]
    public partial string StateText { get; private set; } = string.Empty;

    /// <summary>More about it, such as the connection's latency, or why the app failed.</summary>
    [ObservableProperty]
    public partial string? DetailText { get; private set; }

    /// <summary>Whether the app runs.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    /// <summary>Whether the app is stopped in the tablet's debugger, so the screen does not take input.</summary>
    [ObservableProperty]
    public partial bool IsPaused { get; private set; }

    /// <summary>Whether the app crashed or failed to start.</summary>
    [ObservableProperty]
    public partial bool IsFailed { get; private set; }

    /// <summary>Whether the device lost the tablet and is reconnecting.</summary>
    [ObservableProperty]
    public partial bool IsReconnecting { get; private set; }

    /// <summary>Whether the device waits for the tablet to run an app.</summary>
    [ObservableProperty]
    public partial bool IsWaiting { get; private set; }

    /// <summary>Whether the overlay shows its details and Leave, after a tap on it.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Whether an Avalonia app is shown.</summary>
    public bool ShowsApp => Host.Mode == SwarmDeviceMode.App;

    /// <summary>Whether a web app is shown.</summary>
    public bool ShowsWeb => Host.Mode == SwarmDeviceMode.Web;

    /// <summary>Whether a console app's output is shown.</summary>
    public bool ShowsConsole => Host.Mode == SwarmDeviceMode.Console;

    /// <summary>Whether nothing runs yet.</summary>
    public bool ShowsWaiting => Host.Mode == SwarmDeviceMode.Waiting;

    /// <summary>A short message on the screen, such as an input mirrored here that found no element.</summary>
    public string? Notice => Host.Notice;

    /// <summary>Whether there is a message.</summary>
    public bool HasNotice => Host.Notice is not null;

    /// <summary>Tells the device's screen its size, as the page lays it out, for the tablet's Swarm window and layout checks.</summary>
    /// <param name="width">The app's width, in device-independent pixels.</param>
    /// <param name="height">The app's height.</param>
    /// <param name="scale">Physical pixels per device-independent pixel.</param>
    /// <param name="isDark">Whether the app is shown in its dark theme.</param>
    public void ReportSize(double width, double height, double scale, bool isDark) => Host.SetScreen(width, height, scale, isDark);

    /// <inheritdoc />
    public void Dispose()
    {
        _member.Changed -= OnChanged;
        _member.Host.PropertyChanged -= OnHostChanged;
    }

    /// <summary>Shows or hides the overlay's details.</summary>
    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    /// <summary>Leaves the swarm: the app stops here and the device is the IDE again.</summary>
    [RelayCommand]
    private async Task LeaveAsync()
    {
        IsExpanded = false;
        await _member.LeaveAsync();
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void OnHostChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SwarmDeviceHost.Mode):
                OnPropertyChanged(nameof(ShowsApp));
                OnPropertyChanged(nameof(ShowsWeb));
                OnPropertyChanged(nameof(ShowsConsole));
                OnPropertyChanged(nameof(ShowsWaiting));
                Refresh();
                break;
            case nameof(SwarmDeviceHost.Notice):
                OnPropertyChanged(nameof(Notice));
                OnPropertyChanged(nameof(HasNotice));
                break;
            case nameof(SwarmDeviceHost.AppName):
                Refresh();
                break;
        }
    }

    private void Refresh()
    {
        var member = _member.Member;
        IsVisible = _member.IsInSwarm;
        CoordinatorName = member.CoordinatorName ?? "the tablet";
        var app = member.AppName ?? Host.AppName ?? "the app";
        var state = member.AppState;
        IsReconnecting = member.Membership == SwarmMembership.Reconnecting;
        IsPaused = state is SwarmDeviceState.Paused;
        IsFailed = state is SwarmDeviceState.Crashed or SwarmDeviceState.Failed;
        IsRunning = state is SwarmDeviceState.Running or SwarmDeviceState.Pausing;
        IsWaiting = !IsReconnecting && state is SwarmDeviceState.Ready or SwarmDeviceState.Exited;
        StateText = IsReconnecting
            ? $"Reconnecting to {CoordinatorName}…"
            : state switch
            {
                SwarmDeviceState.Deploying => $"Receiving {app}…",
                SwarmDeviceState.Starting => $"Starting {app}…",
                SwarmDeviceState.Running when member.IsDebugging => $"Debugging {app}",
                SwarmDeviceState.Running => $"Running {app}",
                SwarmDeviceState.Pausing => $"Pausing {app}…",
                SwarmDeviceState.Paused => "Paused in the tablet's debugger",
                SwarmDeviceState.Exited => $"{app} ended",
                SwarmDeviceState.Crashed => $"{app} crashed",
                SwarmDeviceState.Failed => $"{app} could not start",
                _ => $"Waiting for {CoordinatorName} to run an app",
            };
        DetailText = member.AppDetail is { Length: > 0 } detail && (IsFailed || state == SwarmDeviceState.Exited)
            ? detail
            : member.Latency is { } latency
                ? string.Create(CultureInfo.InvariantCulture, $"Connected to {CoordinatorName} · {latency.TotalMilliseconds:0} ms")
                : $"Connected to {CoordinatorName}";
    }
}
