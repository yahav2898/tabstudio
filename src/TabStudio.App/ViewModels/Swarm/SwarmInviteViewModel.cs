using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Swarm;
using TabStudio.Link.Networking;
using TabStudio.Link.Qr;

namespace TabStudio.App.ViewModels.Swarm;

/// <summary>
/// Add a device: the swarm's QR code, which a phone or tablet scans to join, with the address and one-time code to type
/// without a camera. The code works once; after a device joins, the next one gets a fresh code.
/// </summary>
public sealed partial class SwarmInviteViewModel : ObservableObject
{
    private readonly SwarmCoordinatorService _swarm;
    private string? _qrText;

    /// <summary>Creates the dialog's view model.</summary>
    /// <param name="swarm">The tablet's swarm.</param>
    public SwarmInviteViewModel(SwarmCoordinatorService swarm)
    {
        ArgumentNullException.ThrowIfNull(swarm);
        _swarm = swarm;
    }

    /// <summary>Whether the dialog is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The QR code a device scans.</summary>
    [ObservableProperty]
    public partial QrCode? Code { get; private set; }

    /// <summary>The address a device types, such as <c>192.168.1.23:47813</c>.</summary>
    [ObservableProperty]
    public partial string? AddressText { get; private set; }

    /// <summary>The code a device types, such as <c>7K3Q-M9XA</c>.</summary>
    [ObservableProperty]
    public partial string? CodeText { get; private set; }

    /// <summary>Whether the tablet has an address on a local network a device can reach.</summary>
    [ObservableProperty]
    public partial bool HasNetwork { get; private set; } = true;

    /// <summary>How many devices joined, such as <c>2 devices in the swarm</c>.</summary>
    [ObservableProperty]
    public partial string JoinedText { get; private set; } = string.Empty;

    /// <summary>Why the swarm could not start, if it could not.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>The tablet, as devices see it.</summary>
    public string TabletText => $"This tablet is {_swarm.Name}";

    /// <summary>Shows the dialog, starting the swarm when it is off.</summary>
    public async Task OpenAsync()
    {
        if (!_swarm.IsOn && !await _swarm.StartAsync())
        {
            Error = _swarm.Error;
        }

        _swarm.RefreshAddresses();
        Refresh();
        IsOpen = true;
    }

    /// <summary>Reads the code and the devices again. UI thread.</summary>
    public void Refresh()
    {
        var coordinator = _swarm.Coordinator;
        var addresses = coordinator?.Addresses ?? [];
        HasNetwork = coordinator is null || addresses.Count > 0;
        var uri = coordinator?.Invitation?.ToUri();
        if (!string.Equals(uri, _qrText, StringComparison.Ordinal))
        {
            _qrText = uri;
            Code = uri is null ? null : QrCode.Encode(uri);
        }

        AddressText = coordinator is not null && addresses.Count > 0 ? new LinkEndpoint(addresses[0], coordinator.Port).ToString() : null;
        CodeText = coordinator?.PairingCode?.DisplayText;
        var count = _swarm.ConnectedCount;
        JoinedText = count switch
        {
            0 => "No device has joined yet.",
            1 => "1 device is in the swarm.",
            _ => $"{count.ToString(CultureInfo.CurrentCulture)} devices are in the swarm.",
        };
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    /// <summary>Draws a new code; the shown one stops working.</summary>
    [RelayCommand]
    private void NewCode()
    {
        _swarm.Coordinator?.RenewPairingCode();
        Refresh();
    }
}
