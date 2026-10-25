using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.App.ViewModels;

/// <summary>A paired host or tablet in the TabStudio Link dialog.</summary>
public sealed class PairedDeviceViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="device">The paired device.</param>
    /// <param name="isCurrent">Whether it is the device linked or served right now.</param>
    /// <param name="connect">Links to it, for a host; <see langword="null"/> for a tablet.</param>
    /// <param name="forget">Forgets it.</param>
    public PairedDeviceViewModel(LinkPairedDevice device, bool isCurrent, IAsyncRelayCommand? connect, IRelayCommand forget)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(forget);
        Device = device;
        IsCurrent = isCurrent;
        ConnectCommand = connect;
        ForgetCommand = forget;
    }

    /// <summary>The device.</summary>
    public LinkPairedDevice Device { get; }

    /// <summary>Its name.</summary>
    public string Name => Device.Name;

    /// <summary>When it paired and was last seen, such as <c>Paired 3 days ago · seen just now</c>.</summary>
    public string Detail => IsCurrent
        ? $"Paired {LinkDisplay.Ago(Device.PairedAt)} · connected now"
        : $"Paired {LinkDisplay.Ago(Device.PairedAt)} · last seen {LinkDisplay.Ago(Device.LastSeen)}";

    /// <summary>Whether it is the device linked or served right now.</summary>
    public bool IsCurrent { get; }

    /// <summary>Whether it is a phone.</summary>
    public bool IsPhone => Device.Kind == LinkDeviceKinds.Phone;

    /// <summary>Whether it is a tablet.</summary>
    public bool IsTablet => Device.Kind == LinkDeviceKinds.Tablet;

    /// <summary>Whether it is a computer.</summary>
    public bool IsComputer => !IsPhone && !IsTablet;

    /// <summary>Whether it can be linked to now: a host that is not linked already.</summary>
    public bool CanConnect => ConnectCommand is not null && !IsCurrent;

    /// <summary>Links to it, for a host.</summary>
    public IAsyncRelayCommand? ConnectCommand { get; }

    /// <summary>Forgets it: it has to pair again.</summary>
    public IRelayCommand ForgetCommand { get; }
}
