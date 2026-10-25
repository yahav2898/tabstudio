using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.App.ViewModels;

/// <summary>The two halves of the TabStudio Link dialog.</summary>
public enum LinkTab
{
    /// <summary>Linking this device to a host that compiles for it.</summary>
    Connect,

    /// <summary>Being the host for another device.</summary>
    Host,

    /// <summary>Joining a tablet's swarm, to run the app it builds.</summary>
    Swarm,
}

/// <summary>
/// The TabStudio Link dialog: linking this device to a host (pairing by QR code or typed code, paired hosts, the
/// link's state), and being a host for another device (the QR code and code, the served tablet, what the host is
/// doing). One instance serves the start screen, the File menu and the status bar. The host half is in
/// <c>LinkViewModel.Host.cs</c>.
/// </summary>
public sealed partial class LinkViewModel : ObservableObject, IDisposable
{
    private readonly LinkService _link;
    private readonly IQrScanner _scanner;
    private CancellationTokenSource? _pairing;

    /// <summary>Creates the dialog's view model.</summary>
    /// <param name="link">The app's TabStudio Link.</param>
    /// <param name="scanner">Scans QR codes with the camera, where there is one.</param>
    /// <param name="swarm">The Join a swarm half, where the app has a swarm.</param>
    public LinkViewModel(LinkService link, IQrScanner scanner, Swarm.SwarmJoinViewModel? swarm = null)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(scanner);
        _link = link;
        _scanner = scanner;
        Swarm = swarm;
        if (swarm is not null)
        {
            // Once the device joins, it shows the swarm's app; the dialog is in the way.
            swarm.Joined += (_, _) => Close();
        }

        _link.Changed += OnLinkChanged;
        Refresh();
    }

    /// <summary>The app's TabStudio Link.</summary>
    public LinkService Service => _link;

    /// <summary>The Join a swarm half of the dialog, or <see langword="null"/> where the app has no swarm.</summary>
    public Swarm.SwarmJoinViewModel? Swarm { get; }

    /// <summary>Whether the dialog offers to join a swarm.</summary>
    public bool HasSwarm => Swarm is not null;

    /// <summary>Whether the Join a swarm half is shown.</summary>
    public bool IsSwarmTab => SelectedTab == LinkTab.Swarm;

    /// <summary>Whether the dialog is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The half of the dialog shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectTab), nameof(IsHostTab), nameof(IsSwarmTab))]
    public partial LinkTab SelectedTab { get; set; }

    /// <summary>Whether the link-to-a-host half is shown.</summary>
    public bool IsConnectTab => SelectedTab == LinkTab.Connect;

    /// <summary>Whether the be-a-host half is shown.</summary>
    public bool IsHostTab => SelectedTab == LinkTab.Host;

    /// <summary>This device, as the other device sees it: its name and the start of its fingerprint.</summary>
    public string DeviceText => $"This device is {_link.DeviceName}";

    /// <summary>The start of this device's fingerprint, for comparing two screens.</summary>
    public string FingerprintText => $"Fingerprint {_link.Identity.Fingerprint.ShortText}";

    /// <summary>The short state for the status bar and the start screen: <c>Local</c>, <c>Pixel 9 · 12 ms</c>, <c>Reconnecting to Pixel 9</c>.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Local";

    /// <summary>Where the link to a host stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinked), nameof(IsConnecting), nameof(IsReconnecting), nameof(IsNotLinked), nameof(CanDisconnect))]
    public partial LinkClientState ClientState { get; private set; }

    /// <summary>Whether a host compiles for this device.</summary>
    public bool IsLinked => ClientState == LinkClientState.Connected;

    /// <summary>Whether this device is connecting to a host.</summary>
    public bool IsConnecting => ClientState == LinkClientState.Connecting;

    /// <summary>Whether the link was lost and this device tries to reconnect.</summary>
    public bool IsReconnecting => ClientState == LinkClientState.Reconnecting;

    /// <summary>Whether this device compiles on its own.</summary>
    public bool IsNotLinked => ClientState == LinkClientState.Off;

    /// <summary>Whether there is a link, or an attempt, to end.</summary>
    public bool CanDisconnect => ClientState is LinkClientState.Connected or LinkClientState.Reconnecting;

    /// <summary>The link's headline: <c>Linked to Pixel 9</c>, <c>Reconnecting to Pixel 9</c>, or that this device works alone.</summary>
    [ObservableProperty]
    public partial string ClientTitle { get; private set; } = string.Empty;

    /// <summary>The link's details: latency, encryption and what the host is doing.</summary>
    [ObservableProperty]
    public partial string? ClientDetail { get; private set; }

    /// <summary>Why linking failed, or why the link was lost.</summary>
    [ObservableProperty]
    public partial string? ClientError { get; private set; }

    /// <summary>The hosts this device is paired with.</summary>
    public ObservableCollection<PairedDeviceViewModel> PairedHosts { get; } = [];

    /// <summary>Whether this device is paired with any host.</summary>
    [ObservableProperty]
    public partial bool HasPairedHosts { get; private set; }

    /// <summary>The host address the user types, such as <c>192.168.1.23</c>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairCommand))]
    public partial string AddressText { get; set; } = string.Empty;

    /// <summary>The pairing code the user types, such as <c>7K3Q-M9XA</c>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairCommand))]
    public partial string CodeText { get; set; } = string.Empty;

    /// <summary>Whether pairing or connecting is in progress.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairCommand), nameof(ScanCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Whether this device has a camera to scan the host's QR code with.</summary>
    public bool CanScan => _scanner.IsAvailable;

    /// <summary>Shows the dialog.</summary>
    /// <param name="tab">The half to show; the one that applies to this device when <see langword="null"/>.</param>
    public void Open(LinkTab? tab = null)
    {
        SelectedTab = tab ?? (_link.IsHosting && !IsLinked ? LinkTab.Host : LinkTab.Connect);
        _link.RefreshHostAddresses();
        Refresh();
        IsOpen = true;
    }

    /// <summary>Shows the dialog on the half that applies to this device.</summary>
    [RelayCommand]
    private void Show() => Open();

    /// <summary>Shows the dialog on its Join a swarm half, as the start screen's Swarm button does.</summary>
    [RelayCommand]
    private void ShowSwarm() => Open(Swarm is null ? null : LinkTab.Swarm);

    /// <inheritdoc />
    public void Dispose()
    {
        _link.Changed -= OnLinkChanged;
        _pairing?.Cancel();
        _pairing?.Dispose();
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        CodeText = string.Empty;
    }

    [RelayCommand]
    private void SelectTab(LinkTab tab) => SelectedTab = tab;

    /// <summary>Scans the host's QR code with the camera, then pairs and links.</summary>
    [RelayCommand(CanExecute = nameof(CanStartPairing))]
    private async Task ScanAsync()
    {
        ClientError = null;
        string? scanned;
        try
        {
            scanned = await _scanner.ScanAsync(CancellationToken.None);
        }
        catch (UnauthorizedAccessException)
        {
            ClientError = "TabStudio may not use the camera. Allow it in Android's settings, or type the address and code instead.";
            return;
        }
        catch (InvalidOperationException ex)
        {
            ClientError = $"The camera did not start: {ex.Message} Type the address and code instead.";
            return;
        }

        if (scanned is null)
        {
            return;
        }

        if (!LinkInvitation.TryParse(scanned, out var invitation))
        {
            ClientError = "That is not a TabStudio Link code. On the host, open File > TabStudio Link > Be a host, and scan the code it shows.";
            return;
        }

        // A swarm's code: this device joins the swarm, to run the app the tablet builds.
        if (invitation.Role == TabStudio.Link.LinkRole.Swarm && Swarm is { } swarm)
        {
            SelectedTab = LinkTab.Swarm;
            await swarm.JoinInvitationAsync(invitation);
            return;
        }

        await PairWithAsync(LinkTarget.FromInvitation(invitation));
    }

    /// <summary>Pairs with the host at the typed address with the typed code, and links.</summary>
    [RelayCommand(CanExecute = nameof(CanPair))]
    private async Task PairAsync()
    {
        ClientError = null;
        if (!LinkEndpoint.TryParse(AddressText, out var endpoint))
        {
            ClientError = "Type the address the host shows, such as 192.168.1.23.";
            return;
        }

        if (!LinkPairingCode.TryParse(CodeText, out var code))
        {
            ClientError = "Type the eight-character code the host shows, such as 7K3Q-M9XA.";
            return;
        }

        if (await PairWithAsync(LinkTarget.FromCode(endpoint, code)))
        {
            CodeText = string.Empty;
        }
    }

    /// <summary>Ends the link; this device compiles on its own again.</summary>
    [RelayCommand]
    private Task DisconnectAsync() => _link.DisconnectAsync();

    private bool CanStartPairing() => !IsBusy;

    private bool CanPair() => !IsBusy && !string.IsNullOrWhiteSpace(AddressText) && !string.IsNullOrWhiteSpace(CodeText);

    private async Task<bool> PairWithAsync(LinkTarget target)
    {
        IsBusy = true;
        _pairing?.Dispose();
        _pairing = new CancellationTokenSource();
        try
        {
            var linked = await _link.PairAsync(target, _pairing.Token);
            ClientError = linked ? null : _link.ClientError;
            return linked;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectToAsync(LinkPairedDevice host)
    {
        IsBusy = true;
        try
        {
            ClientError = await _link.ConnectAsync(host) ? null : _link.ClientError;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnLinkChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Reads the link's state into the dialog. UI thread.</summary>
    private void Refresh()
    {
        RefreshClient();
        RefreshHost();
        StatusText = ShortStatus();
    }

    private void RefreshClient()
    {
        ClientState = _link.ClientState;
        var host = _link.LinkedHost;
        var hostName = host?.Name ?? "the host";
        ClientTitle = ClientState switch
        {
            LinkClientState.Connected => $"Linked to {hostName}",
            LinkClientState.Connecting => "Connecting…",
            LinkClientState.Reconnecting => $"Reconnecting to {hostName}",
            _ => "This device compiles on its own",
        };

        ClientDetail = ClientState switch
        {
            LinkClientState.Connected => Join(
                LinkDisplay.Latency(_link.Latency),
                _link.ConnectedHost?.Security is { } security ? $"end-to-end encrypted ({security})" : null,
                _link.HostStatus?.Activity,
                _link.HostStatus?.ThermalNote is { } note ? $"{hostName}: {note}" : null),
            LinkClientState.Reconnecting => "Meanwhile this device compiles on its own. Nothing is lost.",
            LinkClientState.Connecting => null,
            _ => "Pair with a phone or PC running TabStudio, and it builds and runs IntelliSense for this device over Wi-Fi.",
        };

        if (ClientState is LinkClientState.Reconnecting or LinkClientState.Off && _link.ClientError is { } error && !IsBusy)
        {
            ClientError = error;
        }
        else if (ClientState == LinkClientState.Connected)
        {
            ClientError = null;
        }

        var current = host?.Fingerprint;
        Replace(PairedHosts, _link.PairedHosts.Devices.Select(d => new PairedDeviceViewModel(
            d,
            d.Fingerprint == current && ClientState == LinkClientState.Connected,
            new AsyncRelayCommand(() => ConnectToAsync(d)),
            new AsyncRelayCommand(() => _link.ForgetHostAsync(d)))));
        HasPairedHosts = PairedHosts.Count > 0;
    }

    private string ShortStatus()
    {
        var host = _link.LinkedHost?.Name ?? "host";
        return ClientState switch
        {
            LinkClientState.Connected => Join(host, LinkDisplay.Latency(_link.Latency)) ?? host,
            LinkClientState.Connecting => "Linking…",
            LinkClientState.Reconnecting => $"Reconnecting to {host}",
            _ when IsHosting => ServedTabletName is { } tablet ? $"Hosting {tablet}" : "Hosting",
            _ => "Local",
        };
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
