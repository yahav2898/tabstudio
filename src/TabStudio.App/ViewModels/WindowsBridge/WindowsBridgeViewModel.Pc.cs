using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Link.Networking;
using TabStudio.Link.Qr;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.ViewModels.WindowsBridge;

/// <summary>
/// The PC's half of the Windows Bridge tab: starting the bridge, its QR code and pairing code, what it can build, the
/// tablet it serves and the programs it runs for it, netcoredbg, and how the tablet's input reaches programs.
/// </summary>
public sealed partial class WindowsBridgeViewModel
{
    private string? _qrText;

    /// <summary>Whether this PC is a Windows Bridge now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBridging))]
    public partial bool IsBridging { get; private set; }

    /// <summary>Whether this PC is not a bridge yet.</summary>
    public bool IsNotBridging => !IsBridging;

    /// <summary>Whether the bridge is starting: looking for the PC's tools.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBridgeCommand))]
    public partial bool IsStarting { get; private set; }

    /// <summary>The QR code a tablet scans, while the bridge runs on a network.</summary>
    [ObservableProperty]
    public partial QrCode? BridgeQrCode { get; private set; }

    /// <summary>The address a tablet types, such as <c>192.168.1.40:47813</c>.</summary>
    [ObservableProperty]
    public partial string? BridgeAddressText { get; private set; }

    /// <summary>The code a tablet types.</summary>
    [ObservableProperty]
    public partial string? BridgeCodeText { get; private set; }

    /// <summary>Whether the PC is on a network a tablet can reach.</summary>
    [ObservableProperty]
    public partial bool HasNetwork { get; private set; }

    /// <summary>The bridge's headline: waiting for a tablet, or serving one.</summary>
    [ObservableProperty]
    public partial string BridgeTitle { get; private set; } = string.Empty;

    /// <summary>What the bridge is doing, such as <c>Building HelloWpf for Yahav's tablet</c>.</summary>
    [ObservableProperty]
    public partial string? BridgeDetail { get; private set; }

    /// <summary>Whether a tablet is connected.</summary>
    [ObservableProperty]
    public partial bool IsServing { get; private set; }

    /// <summary>Why the bridge could not start.</summary>
    [ObservableProperty]
    public partial string? BridgeError { get; private set; }

    /// <summary>The SDKs and tools the bridge found on this PC.</summary>
    public ObservableCollection<BridgeToolchainRow> Toolchains { get; } = [];

    /// <summary>What this PC can build, and what to install for the rest.</summary>
    public ObservableCollection<BridgeCapabilityRow> PcCapabilities { get; } = [];

    /// <summary>The programs running on this PC for tablets.</summary>
    public ObservableCollection<BridgeRunningAppRow> RunningApps { get; } = [];

    /// <summary>Whether a program runs for a tablet.</summary>
    [ObservableProperty]
    public partial bool HasRunningApps { get; private set; }

    /// <summary>The tablets paired with this PC's bridge.</summary>
    public ObservableCollection<PairedDeviceViewModel> BridgeTablets { get; } = [];

    /// <summary>Whether a tablet is paired with the bridge.</summary>
    [ObservableProperty]
    public partial bool HasBridgeTablets { get; private set; }

    /// <summary>Whether netcoredbg is installed, so tablets can debug .NET apps here.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallDebugger))]
    public partial bool HasDebugger { get; private set; }

    /// <summary>Whether netcoredbg is being downloaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallDebugger))]
    public partial bool IsInstallingDebugger { get; private set; }

    /// <summary>Whether the Install netcoredbg button shows.</summary>
    public bool CanInstallDebugger => IsBridging && !HasDebugger && !IsInstallingDebugger;

    /// <summary>Why installing netcoredbg failed.</summary>
    [ObservableProperty]
    public partial string? DebuggerError { get; private set; }

    /// <summary>How the tablet's input reaches programs on this PC.</summary>
    public BridgeInputPolicyOption InputPolicy
    {
        get => BridgeInputPolicyOption.All.First(o => o.Policy == _bridge.InputPolicy);
        set
        {
            if (value is not null && value.Policy != _bridge.InputPolicy)
            {
                _bridge.InputPolicy = value.Policy;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Starts being a Windows Bridge.</summary>
    [RelayCommand(CanExecute = nameof(CanStartBridge))]
    private async Task StartBridgeAsync()
    {
        BridgeError = null;
        IsStarting = true;
        try
        {
            if (!await _bridge.StartBridgeAsync())
            {
                BridgeError = _bridge.BridgeError;
            }
        }
        finally
        {
            IsStarting = false;
        }
    }

    private bool CanStartBridge() => !IsStarting;

    /// <summary>Stops being a bridge: the tablet is disconnected and its programs stop.</summary>
    [RelayCommand]
    private Task StopBridgeAsync() => _bridge.StopBridgeAsync();

    /// <summary>Draws a new pairing code.</summary>
    [RelayCommand]
    private void NewBridgeCode() => _bridge.BridgeServer?.RenewPairingCode();

    /// <summary>Disconnects the served tablet; its programs keep running.</summary>
    [RelayCommand]
    private void DisconnectBridgeTablet() => _bridge.BridgeServer?.DisconnectTablet();

    /// <summary>Downloads and installs netcoredbg.</summary>
    [RelayCommand]
    private Task InstallDebuggerAsync() => _bridge.InstallDebuggerAsync();

    /// <summary>Looks for the PC's tools again, after installing some.</summary>
    [RelayCommand]
    private async Task RefreshToolchainsAsync()
    {
        if (_bridge.BridgeServer is { } server)
        {
            await server.RefreshToolchainsAsync();
        }
    }

    private void ForgetTablet(TabStudio.Link.Security.LinkPairedDevice tablet)
    {
        if (_bridge.BridgeServer is { } server)
        {
            server.ForgetTablet(tablet);
        }
        else
        {
            _bridge.BridgeTablets.Forget(tablet.Fingerprint);
        }
    }

    private void RefreshPc()
    {
        if (!IsPc)
        {
            return;
        }

        IsBridging = _bridge.IsBridging;
        var server = _bridge.BridgeServer;
        var tablet = server?.ConnectedTablet;
        IsServing = tablet is not null;
        var addresses = _bridge.BridgeAddresses;
        HasNetwork = addresses.Count > 0;
        var uri = IsBridging && HasNetwork ? _bridge.BridgeInvitationUri : null;
        if (!string.Equals(uri, _qrText, StringComparison.Ordinal))
        {
            _qrText = uri;
            BridgeQrCode = uri is null ? null : QrCode.Encode(uri);
        }

        BridgeAddressText = HasNetwork && server is not null ? new LinkEndpoint(addresses[0], server.Port).ToString() : null;
        BridgeCodeText = server?.PairingCode?.DisplayText;
        BridgeTitle = tablet is not null ? $"Serving {tablet.Name}" : "Waiting for a tablet";
        BridgeDetail = tablet is null
            ? null
            : server?.Activity is { } activity ? $"{activity} for {tablet.Name}" : Join("Connected", tablet.Security);
        BridgeError ??= _bridge.BridgeError;

        var toolchains = server?.Toolchains;
        Replace(Toolchains, toolchains?.Toolchains.Select(t => new BridgeToolchainRow(t)) ?? []);
        Replace(PcCapabilities, toolchains is null ? [] : BridgeCapabilityRow.For(toolchains.Capabilities));
        HasDebugger = toolchains?.Capabilities.HasFlag(BridgeCapabilities.DebugDotNet) == true;
        IsInstallingDebugger = _bridge.IsInstallingDebugger;
        DebuggerError = _bridge.DebuggerError;
        OnPropertyChanged(nameof(CanInstallDebugger));

        Replace(RunningApps, server?.Apps.Select(a => new BridgeRunningAppRow(a.Tablet, a.App, new RelayCommand(() => server.StopApp(a.App.AppId)))) ?? []);
        HasRunningApps = RunningApps.Count > 0;

        var served = tablet?.Fingerprint;
        Replace(BridgeTablets, _bridge.BridgeTablets.Devices.Select(d => new PairedDeviceViewModel(
            d,
            d.Fingerprint == served,
            connect: null,
            new RelayCommand(() => ForgetTablet(d)))));
        HasBridgeTablets = BridgeTablets.Count > 0;
        if (IsBridging)
        {
            StatusText = tablet is not null ? $"Bridge for {tablet.Name}" : "Windows Bridge on";
        }
    }
}
