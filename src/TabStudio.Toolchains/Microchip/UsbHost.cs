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
