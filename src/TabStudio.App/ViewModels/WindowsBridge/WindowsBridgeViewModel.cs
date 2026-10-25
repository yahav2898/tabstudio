using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Link;
using TabStudio.App.Services.WindowsBridge;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.ViewModels.WindowsBridge;

/// <summary>
/// The Windows Bridge tab of the TabStudio Link dialog. On a tablet it pairs with a Windows PC and shows what that PC
/// can build; on TabStudio for Windows it makes the PC a bridge (<c>WindowsBridgeViewModel.Pc.cs</c>).
/// </summary>
public sealed partial class WindowsBridgeViewModel : ObservableObject, IDisposable
{
    private readonly WindowsBridgeService _bridge;
    private readonly IQrScanner _scanner;
    private CancellationTokenSource? _pairing;

    /// <summary>Creates the tab's view model.</summary>
    /// <param name="bridge">The app's Windows Bridge.</param>
    /// <param name="scanner">Scans the PC's QR code with the camera, where there is one.</param>
    public WindowsBridgeViewModel(WindowsBridgeService bridge, IQrScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(scanner);
        _bridge = bridge;
        _scanner = scanner;
        _bridge.Changed += OnBridgeChanged;
        Refresh();
    }

    /// <summary>The app's Windows Bridge.</summary>
    public WindowsBridgeService Service => _bridge;

    /// <summary>Whether this device is a PC that can be a bridge, so the tab shows that half.</summary>
    public bool IsPc => _bridge.CanBeBridge;

    /// <summary>Whether this device uses a bridge, so the tab shows the pairing half.</summary>
    public bool IsTablet => !_bridge.CanBeBridge;

    /// <summary>The tab's name, for the dialog's tab strip.</summary>
    public string TabTitle => IsPc ? "Be a Windows Bridge" : "Windows PC";

    /// <summary>The short state for the status bar: <c>DESKTOP-7Q2M · 12 ms</c>, <c>DESKTOP-7Q2M is away</c>, or nothing without a PC.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Where the connection to the PC stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsConnecting), nameof(IsReconnecting), nameof(IsOff), nameof(CanDisconnect))]
    public partial LinkClientState State { get; private set; }

    /// <summary>Whether the PC is connected.</summary>
    public bool IsConnected => State == LinkClientState.Connected;

    /// <summary>Whether this tablet is connecting to the PC.</summary>
    public bool IsConnecting => State == LinkClientState.Connecting;

    /// <summary>Whether the PC was lost and this tablet tries to reconnect.</summary>
    public bool IsReconnecting => State == LinkClientState.Reconnecting;

    /// <summary>Whether no PC is connected.</summary>
    public bool IsOff => State == LinkClientState.Off;

    /// <summary>Whether there is a connection, or an attempt, to end.</summary>
    public bool CanDisconnect => State is LinkClientState.Connected or LinkClientState.Reconnecting;

    /// <summary>The headline: <c>Connected to DESKTOP-7Q2M</c>, <c>DESKTOP-7Q2M is away</c>, or that there is no PC.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>The details: latency, encryption, Windows version and what the PC is doing.</summary>
    [ObservableProperty]
    public partial string? Detail { get; private set; }

    /// <summary>Why connecting failed, or why the PC was lost.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>What the connected PC can build, such as <c>WPF and Windows Forms</c>, with whether it can.</summary>
    public ObservableCollection<BridgeCapabilityRow> Capabilities { get; } = [];

    /// <summary>Whether the PC's capabilities are known.</summary>
    [ObservableProperty]
    public partial bool HasCapabilities { get; private set; }

    /// <summary>The PCs this tablet is paired with.</summary>
    public ObservableCollection<PairedDeviceViewModel> PairedPcs { get; } = [];

    /// <summary>Whether a PC is paired.</summary>
    [ObservableProperty]
    public partial bool HasPairedPcs { get; private set; }

    /// <summary>The PC's address the user types, such as <c>192.168.1.40:47813</c>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairCommand))]
    public partial string AddressText { get; set; } = string.Empty;

    /// <summary>The pairing code the user types.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairCommand))]
    public partial string CodeText { get; set; } = string.Empty;

    /// <summary>Whether pairing or connecting is in progress.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairCommand), nameof(ScanCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Whether this device has a camera to scan the PC's QR code with.</summary>
    public bool CanScan => _scanner.IsAvailable;

    /// <inheritdoc />
    public void Dispose()
    {
        _bridge.Changed -= OnBridgeChanged;
        _pairing?.Cancel();
        _pairing?.Dispose();
    }

    /// <summary>Scans the PC's QR code with the camera, then pairs and connects.</summary>
    [RelayCommand(CanExecute = nameof(CanStartPairing))]
    private async Task ScanAsync()
    {
        Error = null;
        string? scanned;
        try
        {
            scanned = await _scanner.ScanAsync(CancellationToken.None);
        }
        catch (UnauthorizedAccessException)
        {
            Error = "TabStudio may not use the camera. Allow it in Android's settings, or type the address and code instead.";
            return;
        }
        catch (InvalidOperationException ex)
        {
            Error = $"The camera did not start: {ex.Message} Type the address and code instead.";
            return;
        }

        if (scanned is null)
        {
            return;
        }

        if (!BridgeInvitation.TryParse(scanned, out var invitation))
        {
            Error = BridgeInvitation.IsLinkHostCode(scanned)
                ? "That is a TabStudio Link host's code. On the PC, open File > TabStudio Link > Windows Bridge, and scan the code it shows there."
                : "That is not a Windows Bridge code. On the PC, open File > TabStudio Link > Windows Bridge, and scan the code it shows.";
            return;
        }

        await PairWithAsync(LinkTarget.FromInvitation(invitation));
    }

    /// <summary>Pairs with the PC at the typed address with the typed code, and connects.</summary>
    [RelayCommand(CanExecute = nameof(CanPair))]
    private async Task PairAsync()
    {
        Error = null;
        if (!LinkEndpoint.TryParse(AddressText, TabStudio.WindowsBridge.BridgeProtocol.DefaultPort, out var endpoint))
        {
            Error = "Type the address the PC shows, such as 192.168.1.40:47813.";
            return;
        }

        if (!LinkPairingCode.TryParse(CodeText, out var code))
        {
            Error = "Type the eight-character code the PC shows, such as 7K3Q-M9XA.";
            return;
        }

        if (await PairWithAsync(LinkTarget.FromCode(endpoint, code)))
        {
            CodeText = string.Empty;
        }
    }

    /// <summary>Ends the connection to the PC.</summary>
    [RelayCommand]
    private Task DisconnectAsync() => _bridge.DisconnectAsync();

    private bool CanStartPairing() => !IsBusy;

    private bool CanPair() => !IsBusy && !string.IsNullOrWhiteSpace(AddressText) && !string.IsNullOrWhiteSpace(CodeText);

    private async Task<bool> PairWithAsync(LinkTarget target)
    {
        IsBusy = true;
        _pairing?.Dispose();
        _pairing = new CancellationTokenSource();
        try
        {
            var connected = await _bridge.PairAsync(target, _pairing.Token);
            Error = connected ? null : _bridge.Error;
            return connected;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectToAsync(LinkPairedDevice pc)
    {
        IsBusy = true;
        try
        {
            Error = await _bridge.ConnectAsync(pc) ? null : _bridge.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnBridgeChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Reads the bridge's state into the tab. UI thread.</summary>
    private void Refresh()
    {
        RefreshTablet();
        RefreshPc();
    }

    private void RefreshTablet()
    {
        State = _bridge.State;
        var name = _bridge.PcName ?? "the PC";
        var info = _bridge.Info;
        Title = State switch
        {
            LinkClientState.Connected => $"Connected to {name}",
            LinkClientState.Connecting => "Connecting…",
            LinkClientState.Reconnecting when _bridge.IsPcAsleep => $"{name} is asleep or away",
            LinkClientState.Reconnecting => $"Reconnecting to {name}",
            _ => _bridge.HasPairedPc ? $"{name} is not connected" : "No Windows PC is paired",
        };
        Detail = State switch
        {
            LinkClientState.Connected => Join(
                LinkDisplay.Latency(_bridge.Latency),
                _bridge.ConnectedPc?.Security is { } security ? $"end-to-end encrypted ({security})" : null,
                info?.OperatingSystem,
                _bridge.Status?.Activity),
            LinkClientState.Reconnecting => "Keep editing here: Windows-only projects run on the PC again as soon as it is back.",
            LinkClientState.Connecting => null,
            _ => _bridge.HasPairedPc
                ? "It connects when you run a Windows-only project, or tap Connect below."
                : "Pair a Windows PC you own. WPF, Windows Forms, WinUI 3, .NET Framework and Visual C++ projects then build and run on it, and their windows stream here.",
        };

        if (State is LinkClientState.Reconnecting or LinkClientState.Off && _bridge.Error is { } error && !IsBusy)
        {
            Error = error;
        }
        else if (State == LinkClientState.Connected)
        {
            Error = null;
        }

        Replace(Capabilities, info is null ? [] : BridgeCapabilityRow.For(info.Capabilities));
        HasCapabilities = Capabilities.Count > 0;

        var current = State == LinkClientState.Connected ? _bridge.Pc?.Fingerprint : null;
        Replace(PairedPcs, _bridge.PairedPcs.Devices.Select(d => new PairedDeviceViewModel(
            d,
            d.Fingerprint == current,
            new AsyncRelayCommand(() => ConnectToAsync(d)),
            new AsyncRelayCommand(() => _bridge.ForgetPcAsync(d)))));
        HasPairedPcs = PairedPcs.Count > 0;

        StatusText = _bridge.HasPairedPc
            ? State switch
            {
                LinkClientState.Connected => Join(name, LinkDisplay.Latency(_bridge.Latency)),
                LinkClientState.Reconnecting when _bridge.IsPcAsleep => $"{name} is away",
                LinkClientState.Reconnecting => $"Reconnecting to {name}",
                LinkClientState.Connecting => $"Connecting to {name}",
                _ => name,
            }
            : null;
    }

    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return present.Count == 0 ? null : string.Join(" · ", present);
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
