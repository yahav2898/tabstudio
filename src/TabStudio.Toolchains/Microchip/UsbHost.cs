using System.Text.Json;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Microchip;

/// <summary>A USB endpoint in an Android permissioned device.</summary>
/// <param name="Address">Endpoint address, including direction.</param>
/// <param name="Type">USB transfer type: 2 for bulk, 3 for interrupt.</param>
/// <param name="PacketSize">Maximum packet size.</param>
public sealed record UsbEndpointInfo(int Address, int Type, int PacketSize);

/// <summary>An interface and its endpoints, read from Android's USB descriptors.</summary>
/// <param name="Number">Interface number.</param>
/// <param name="Class">USB interface class.</param>
/// <param name="Subclass">USB interface subclass.</param>
/// <param name="Protocol">USB interface protocol.</param>
/// <param name="Endpoints">Endpoints.</param>
public sealed record UsbInterfaceInfo(int Number, int Class, int Subclass, int Protocol, IReadOnlyList<UsbEndpointInfo> Endpoints);

/// <summary>A USB device. Listing it never requests permission or opens it.</summary>
/// <param name="Id">Platform device name, used only during this attachment.</param>
/// <param name="VendorId">USB vendor id.</param>
/// <param name="ProductId">USB product id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Interfaces">Interfaces.</param>
public sealed record UsbDeviceInfo(string Id, int VendorId, int ProductId, string Name, IReadOnlyList<UsbInterfaceInfo> Interfaces)
{
    /// <inheritdoc />
    public override string ToString() => $"{Name} ({VendorId:X4}:{ProductId:X4})";
}

/// <summary>Owns an open Android connection. Keep it alive until the child process exits.</summary>
public interface IUsbConnection : IDisposable
{
    /// <summary>The permitted device.</summary>
    UsbDeviceInfo Device { get; }

    /// <summary>The borrowed platform descriptor, never a path to a USB device node.</summary>
    int FileDescriptor { get; }

    /// <summary>Device/configuration descriptor bytes obtained from the permissioned connection, or empty when unavailable.</summary>
    ReadOnlyMemory<byte> RawDescriptors => ReadOnlyMemory<byte>.Empty;

    /// <summary>Current USB configuration read from the permissioned device; unknown configurations cannot be guessed for usbfs clients.</summary>
    int? ActiveConfiguration => null;
}

/// <summary>Platform USB permission and descriptor ownership.</summary>
public interface IUsbHost
{
    /// <summary>Devices attached now. No permission prompt.</summary>
    IReadOnlyList<UsbDeviceInfo> Devices { get; }

    /// <summary>Asks Android for permission and opens exactly the selected device.</summary>
    /// <param name="device">A currently attached device.</param>
    /// <param name="cancellationToken">Cancels waiting for permission.</param>
    Task<IUsbConnection> OpenAsync(UsbDeviceInfo device, CancellationToken cancellationToken);
}

/// <summary>The attachment transition observed by a USB host.</summary>
public enum UsbDeviceChangeKind
{
    /// <summary>A device appeared in the platform USB service's current device list.</summary>
    Attached,
    /// <summary>A previously attached device disappeared from the platform USB service's list.</summary>
    Detached,
}

/// <summary>An attachment change. Device metadata does not imply USB permission or identify a target MCU.</summary>
/// <param name="device">Platform descriptor metadata for the affected device.</param>
/// <param name="kind">Whether the device appeared or disappeared.</param>
public sealed class UsbDeviceChangedEventArgs(UsbDeviceInfo device, UsbDeviceChangeKind kind) : EventArgs
{
    /// <summary>The affected USB attachment.</summary>
    public UsbDeviceInfo Device { get; } = device;

    /// <summary>The attachment transition.</summary>
    public UsbDeviceChangeKind Kind { get; } = kind;
}

/// <summary>Optional USB hot-plug notifications. Implementations observe only while subscribed; enumeration never requests permission.</summary>
public interface IUsbHotplugHost
{
    /// <summary>Raised after the platform reports a device-list change. Callers marshal UI work to their dispatcher.</summary>
    event EventHandler<UsbDeviceChangedEventArgs>? DevicesChanged;
}

/// <summary>Desktop hosts use their installed tools, or the Windows Bridge.</summary>
public sealed class NoUsbHost : IUsbHost
{
    /// <inheritdoc />
    public IReadOnlyList<UsbDeviceInfo> Devices => [];

    /// <inheritdoc />
    public Task<IUsbConnection> OpenAsync(UsbDeviceInfo device, CancellationToken cancellationToken) =>
        Task.FromException<IUsbConnection>(new NotSupportedException("USB-OTG requires Android. Use the Windows Bridge on this platform."));
}

/// <summary>Passes one permissioned descriptor to a native Alpine worker, without exposing other app handles.</summary>
public static class PicUsbProcess
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    /// <summary>Creates a worker launch. The connection must outlive the process.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="arguments">Arguments to the PIC USB worker.</param>
    /// <param name="workingDirectory">Workspace directory.</param>
    public static ProcessStartSpec Create(IUsbConnection connection, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.FileDescriptor < 3)
        {
            throw new IOException("The USB connection is closed.");
        }

        return new ProcessStartSpec
        {
            Program = "python3",
            Arguments = [MicrochipInstallation.ToolsDirectory + "/usb_worker.py", .. arguments],
            WorkingDirectory = workingDirectory,
            InheritedDescriptors = new Dictionary<int, int> { [connection.FileDescriptor] = 3 },
            Environment = new Dictionary<string, string?>
            {
                ["TABSTUDIO_USB_FD"] = "3",
                ["TABSTUDIO_USB_DEVICE"] = JsonSerializer.Serialize(connection.Device, JsonOptions),
            },
        };
    }
}
