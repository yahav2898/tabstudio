using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Link.Networking;
using TabStudio.Link.Qr;

namespace TabStudio.App.ViewModels;

/// <summary>The be-a-host half of the TabStudio Link dialog.</summary>
public sealed partial class LinkViewModel
{
    private string? _qrText;

    /// <summary>Whether this device is a host, listening for a tablet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotHosting))]
    public partial bool IsHosting { get; private set; }

    /// <summary>Whether this device is not a host.</summary>
    public bool IsNotHosting => !IsHosting;

    /// <summary>The QR code a tablet scans, while hosting on a network.</summary>
    [ObservableProperty]
    public partial QrCode? HostQrCode { get; private set; }

    /// <summary>The address a tablet types, such as <c>192.168.1.23</c>.</summary>
    [ObservableProperty]
    public partial string? HostAddressText { get; private set; }

    /// <summary>The code a tablet types, such as <c>7K3Q-M9XA</c>.</summary>
    [ObservableProperty]
    public partial string? PairingCodeText { get; private set; }

    /// <summary>Whether this device has an address on a local network a tablet can reach.</summary>
    [ObservableProperty]
    public partial bool HasNetwork { get; private set; }

    /// <summary>The host's headline: waiting for a tablet, or serving one.</summary>
    [ObservableProperty]
    public partial string HostTitle { get; private set; } = string.Empty;

    /// <summary>What the host is doing, such as <c>Building HelloTablet for Yahav's tablet</c>.</summary>
    [ObservableProperty]
    public partial string? HostDetail { get; private set; }

    /// <summary>The host device's state: its temperature and cores.</summary>
    [ObservableProperty]
    public partial string HostDeviceText { get; private set; } = string.Empty;

    /// <summary>Whether a tablet is connected.</summary>
    [ObservableProperty]
    public partial bool IsServing { get; private set; }

    /// <summary>The connected tablet's name.</summary>
    [ObservableProperty]
    public partial string? ServedTabletName { get; private set; }

    /// <summary>Why hosting could not start.</summary>
    [ObservableProperty]
    public partial string? HostError { get; private set; }

    /// <summary>The tablets this device serves.</summary>
    public ObservableCollection<PairedDeviceViewModel> PairedTablets { get; } = [];

    /// <summary>Whether any tablet is paired with this host.</summary>
    [ObservableProperty]
    public partial bool HasPairedTablets { get; private set; }

    /// <summary>Starts hosting: the QR code and the code appear.</summary>
    [RelayCommand]
    private async Task StartHostingAsync()
    {
        HostError = null;
        if (!await _link.StartHostingAsync())
        {
            HostError = _link.HostError;
        }
    }

    /// <summary>Stops hosting; a linked tablet compiles on its own again.</summary>
    [RelayCommand]
    private Task StopHostingAsync() => _link.StopHostingAsync();

    /// <summary>Draws a new pairing code.</summary>
    [RelayCommand]
    private void NewCode() => _link.RenewPairingCode();

    /// <summary>Disconnects the served tablet.</summary>
    [RelayCommand]
    private void DisconnectTablet() => _link.DisconnectTablet();

    private void RefreshHost()
    {
        IsHosting = _link.IsHosting;
        var tablet = _link.ServedTablet;
        IsServing = tablet is not null;
        ServedTabletName = tablet?.Name;

        var addresses = _link.HostAddresses;
        HasNetwork = addresses.Count > 0;
        var invitation = IsHosting && HasNetwork ? _link.Invitation : null;
        var uri = invitation?.ToUri();
        if (!string.Equals(uri, _qrText, StringComparison.Ordinal))
        {
            _qrText = uri;
            HostQrCode = uri is null ? null : QrCode.Encode(uri);
        }

        HostAddressText = HasNetwork ? new LinkEndpoint(addresses[0], _link.HostPort).ToString() : null;
        PairingCodeText = _link.PairingCode?.DisplayText;
        HostTitle = tablet is not null ? $"Serving {tablet.Name}" : "Waiting for a tablet";
        HostDetail = tablet is null
            ? null
            : _link.HostActivity is { } activity
                ? $"{activity} for {tablet.Name}"
                : Join(_link.ServedWorkspace is { } workspace ? $"{workspace} is loaded" : "Connected", $"{_link.ServedRequests.ToString(CultureInfo.InvariantCulture)} requests answered", tablet.Security);

        var cores = $"{Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)} cores";
        // A device that is not hot says nothing about it (docs/design: calm by default).
        HostDeviceText = Join(_link.HostThermalNote, cores) ?? cores;
        HostError ??= _link.HostError;

        var served = tablet?.Fingerprint;
        Replace(PairedTablets, _link.PairedTablets.Devices.Select(d => new PairedDeviceViewModel(
            d,
            d.Fingerprint == served,
            connect: null,
            new RelayCommand(() => _link.ForgetTablet(d)))));
        HasPairedTablets = PairedTablets.Count > 0;
    }
}
