using System.Globalization;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded;

/// <summary>A per-process, single-device sysfs/usbfs inventory created solely from a granted Android connection.</summary>
public sealed class UsbfsInventory : IDisposable
{
    internal UsbfsInventory(string directory, string devicePath, IReadOnlyList<ProcessMount> mounts)
    {
        Directory = directory;
        DevicePath = devicePath;
        Mounts = mounts;
    }

    /// <summary>Private host directory, owned by this launch and removed after its process exits.</summary>
    public string Directory { get; }

    /// <summary>The selected guest usbfs node, passed to the exact-FD bridge.</summary>
    public string DevicePath { get; }

    /// <summary>Private PRoot mounts; no host-wide sysfs/device state is changed.</summary>
    public IReadOnlyList<ProcessMount> Mounts { get; }

    /// <summary>Removes this launch's inventory after the child has exited. Does not close the borrowed Android connection.</summary>
    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, true); }
        catch (DirectoryNotFoundException) { }
    }
}

/// <summary>Builds the sysfs attributes nusb reads from actual USB descriptors, with no physical-topology or chip-ID guesses.</summary>
public static class UsbfsDeviceInventory
{
    /// <summary>Creates a unique inventory under private app storage. Call off the UI thread because reading active configuration is a USB control transfer.</summary>
    public static UsbfsInventory Create(IUsbConnection connection, string parentDirectory)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        if (!Path.IsPathFullyQualified(parentDirectory)) throw new ArgumentException("USB inventory storage must be absolute.", nameof(parentDirectory));
        if (connection.FileDescriptor < 3) throw new IOException("The granted USB connection has closed.");
        var path = connection.Device.Id;
        var components = path.Split('/');
        if (components.Length != 6 || !path.StartsWith("/dev/bus/usb/", StringComparison.Ordinal) ||
            !int.TryParse(components[4], NumberStyles.None, CultureInfo.InvariantCulture, out var bus) || bus is < 1 or > 255 ||
            !int.TryParse(components[5], NumberStyles.None, CultureInfo.InvariantCulture, out var address) || address is < 1 or > 127)
        {
            throw new InvalidDataException("The Android connection does not expose an exact USB bus/device identity.");
        }
        var raw = connection.RawDescriptors.ToArray();
        if (raw.Length < 18 || raw.Length > 1024 * 1024 || raw[0] != 18 || raw[1] != 1)
            throw new InvalidDataException("Actual device/configuration descriptors are required for usbfs enumeration.");
        if (Word(raw, 8) != connection.Device.VendorId || Word(raw, 10) != connection.Device.ProductId)
            throw new InvalidDataException("USB descriptors do not match the selected granted device.");
        if (connection.ActiveConfiguration is not { } active || active is < 0 or > 255)
            throw new InvalidDataException("The active USB configuration could not be read; TabStudio will not guess it.");
        var directory = Path.Combine(parentDirectory, "usb-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var sysfs = Path.Combine(directory, "sysfs");
        var usbfs = Path.Combine(directory, "usbfs");
        var name = bus.ToString(CultureInfo.InvariantCulture) + "-1"; // Synthetic private topology, not a physical port claim.
        var device = Path.Combine(sysfs, name);
        var devicePath = string.Create(CultureInfo.InvariantCulture, $"/dev/bus/usb/{bus:000}/{address:000}");
        System.IO.Directory.CreateDirectory(device);
        try
        {
            Write(device, "busnum", bus.ToString(CultureInfo.InvariantCulture));
            Write(device, "devnum", address.ToString(CultureInfo.InvariantCulture));
            Write(device, "idVendor", Word(raw, 8).ToString("x4", CultureInfo.InvariantCulture));
            Write(device, "idProduct", Word(raw, 10).ToString("x4", CultureInfo.InvariantCulture));
            Write(device, "bcdDevice", Word(raw, 12).ToString("x4", CultureInfo.InvariantCulture));
            Write(device, "version", string.Create(CultureInfo.InvariantCulture, $"{raw[3]:x2}.{raw[2]:x2}"));
            Write(device, "bDeviceClass", raw[4].ToString("x2", CultureInfo.InvariantCulture));
            Write(device, "bDeviceSubClass", raw[5].ToString("x2", CultureInfo.InvariantCulture));
            Write(device, "bDeviceProtocol", raw[6].ToString("x2", CultureInfo.InvariantCulture));
            Write(device, "bConfigurationValue", active.ToString(CultureInfo.InvariantCulture));
            File.WriteAllBytes(Path.Combine(device, "descriptors"), raw);
            var configuration = -1;
            var seen = new HashSet<int>();
            for (var offset = 18; offset < raw.Length;)
            {
                if (raw.Length - offset < 2 || raw[offset] < 2 || raw[offset] > raw.Length - offset)
                    throw new InvalidDataException("Truncated or invalid USB descriptor list.");
                var length = raw[offset];
                var type = raw[offset + 1];
                if (type == 2)
                {
                    if (length < 9) throw new InvalidDataException("Invalid USB configuration descriptor.");
                    var totalLength = Word(raw, offset + 2);
                    if (totalLength < length || totalLength > raw.Length - offset) throw new InvalidDataException("Truncated USB configuration descriptor payload.");
                    configuration = raw[offset + 5];
                }
                if (type == 4 && configuration == active)
                {
                    if (length < 9) throw new InvalidDataException("Invalid USB interface descriptor.");
                    var number = raw[offset + 2];
                    if (seen.Add(number))
                    {
                        var item = Path.Combine(device, string.Create(CultureInfo.InvariantCulture, $"{name}:{active}.{number}"));
                        System.IO.Directory.CreateDirectory(item);
                        Write(item, "bInterfaceNumber", number.ToString("x2", CultureInfo.InvariantCulture));
                        Write(item, "bInterfaceClass", raw[offset + 5].ToString("x2", CultureInfo.InvariantCulture));
                        Write(item, "bInterfaceSubClass", raw[offset + 6].ToString("x2", CultureInfo.InvariantCulture));
                        Write(item, "bInterfaceProtocol", raw[offset + 7].ToString("x2", CultureInfo.InvariantCulture));
                    }
                }
                offset += length;
            }
            if (active != 0 && seen.Count == 0) throw new InvalidDataException("Active configuration has no described USB interfaces.");
            var busFolder = Path.Combine(usbfs, bus.ToString("000", CultureInfo.InvariantCulture));
            System.IO.Directory.CreateDirectory(busFolder);
            File.WriteAllBytes(Path.Combine(busFolder, address.ToString("000", CultureInfo.InvariantCulture)), []);
            return new(directory, devicePath, [new(sysfs, "/sys/bus/usb/devices"), new(usbfs, "/dev/bus/usb")]);
        }
        catch
        {
            System.IO.Directory.Delete(directory, true);
            throw;
        }
    }

    private static ushort Word(byte[] data, int offset) => (ushort)(data[offset] | data[offset + 1] << 8);
    private static void Write(string directory, string name, string value) => File.WriteAllText(Path.Combine(directory, name), value + "\n");
}
