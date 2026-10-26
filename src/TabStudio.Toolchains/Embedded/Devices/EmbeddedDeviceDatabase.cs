using System.Text.Json;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Embedded.Devices;

/// <summary>The evidence behind a device match; a USB identity alone never proves the target MCU.</summary>
public enum DeviceMatchSource
{
    /// <summary>A shipped VID/PID or interface match.</summary>
    UsbIdentity,
    /// <summary>A board selection saved by the user.</summary>
    UserSelection,
    /// <summary>An actual target identity read through a probe.</summary>
    ProbeIdentification,
}

/// <summary>A candidate transport for an attached USB device, not a claim that a target is verified.</summary>
/// <param name="Name">The transport's human-readable name.</param>
/// <param name="Route">Suggested tool or protocol family.</param>
/// <param name="Source">The evidence used to suggest it.</param>
/// <param name="Target">Explicitly selected or probed chip, otherwise null.</param>
public sealed record EmbeddedDeviceCandidate(string Name, string Route, DeviceMatchSource Source, string? Target = null);

/// <summary>A user-owned device association. USB serial numbers are optional; devices without one share a VID/PID association.</summary>
/// <param name="VendorId">The USB vendor ID.</param>
/// <param name="ProductId">The USB product ID.</param>
/// <param name="SerialNumber">A stable serial read after permission, or null.</param>
/// <param name="Board">The user's board name.</param>
/// <param name="Route">The user's transport choice.</param>
/// <param name="Target">The explicit target MCU, if known.</param>
public sealed record SavedEmbeddedDevice(int VendorId, int ProductId, string? SerialNumber, string Board, string Route, string? Target);

/// <summary>Identifies transports conservatively and persists user selections without opening devices or requesting USB permission.</summary>
public sealed class EmbeddedDeviceDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly List<SavedEmbeddedDevice> _saved = [];

    /// <summary>Saved associations; returned as a snapshot.</summary>
    public IReadOnlyList<SavedEmbeddedDevice> Saved => _saved.ToArray();

    /// <summary>Suggests transports using descriptors already available to the app.</summary>
    public IReadOnlyList<EmbeddedDeviceCandidate> Identify(UsbDeviceInfo device, string? serialNumber = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        var result = new List<EmbeddedDeviceCandidate>();
        foreach (var saved in _saved.Where(x => x.VendorId == device.VendorId && x.ProductId == device.ProductId &&
                     (x.SerialNumber is null || string.Equals(x.SerialNumber, serialNumber, StringComparison.Ordinal))))
        {
            result.Add(new(saved.Board, saved.Route, DeviceMatchSource.UserSelection, saved.Target));
        }

        (string Name, string Route)? known = (device.VendorId, device.ProductId) switch
        {
            (0x0483, 0x3748 or 0x374B or 0x374D or 0x374E or 0x374F) => ("ST-Link probe (target must be queried)", "openocd"),
            (0x1366, 0x0101 or 0x0105) => ("J-Link probe (target must be queried)", "openocd"),
            (0x2E8A, 0x000C) => ("Raspberry Pi Debug Probe / CMSIS-DAP", "openocd"),
            (0x2E8A, 0x0003) => ("RP2040 ROM BOOTSEL", "picotool"),
            (0x303A, 0x1001) => ("Espressif USB Serial/JTAG (chip must be queried)", "esptool"),
            (0x0483, 0xDF11) => ("STM32 USB DFU bootloader (target must be selected)", "dfu-util"),
            (0x2341 or 0x2A03, 0x0043 or 0x0001) => ("Arduino Uno USB-serial", "avrdude-stk500v1"),
            (0x2341 or 0x2A03, 0x0042 or 0x0010) => ("Arduino Mega USB-serial", "avrdude-stk500v2"),
            (0x2341 or 0x2A03, 0x8036 or 0x0036 or 0x8037 or 0x0037) => ("Arduino Leonardo/Micro", "avrdude-avr109"),
            (0x1A86, 0x7523 or 0x5523) => ("WCH USB-serial adapter (board unknown)", "serial"),
            (0x10C4, 0xEA60) => ("Silicon Labs CP210x adapter (board unknown)", "serial"),
            (0x0403, 0x6001 or 0x6010 or 0x6011 or 0x6014) => ("FTDI adapter (board unknown)", "serial"),
            (0x16C0, 0x0478) => ("Teensy HalfKay bootloader (board must be selected)", "teensy_loader_cli"),
            (0x1A86, 0x8010) => ("WCH-Link probe (target must be queried)", "openocd"),
            _ => null,
        };
        if (known is { } match)
        {
            result.Add(new(match.Name, match.Route, DeviceMatchSource.UsbIdentity));
        }

        if (device.Interfaces.Any(x => x.Class == 0xFE && x.Subclass == 1))
        {
            result.Add(new("USB DFU interface (alternate setting and memory map required)", "dfu-util", DeviceMatchSource.UsbIdentity));
        }
        if (device.Interfaces.Any(x => x.Class == 2 && x.Subclass == 2) && !result.Any(x => x.Route == "serial"))
        {
            result.Add(new("CDC-ACM serial interface (board unknown)", "serial", DeviceMatchSource.UsbIdentity));
        }
        return result.DistinctBy(x => (x.Name, x.Route, x.Source, x.Target)).ToArray();
    }

    /// <summary>Saves a user choice. It is never relabeled as a probe identification or hardware verification.</summary>
    public void Remember(SavedEmbeddedDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device.VendorId is < 0 or > ushort.MaxValue || device.ProductId is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(device), "USB IDs must be 16-bit values.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(device.Board);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.Route);
        if (device.Board.Length > 256 || device.Route.Length > 128 || device.Target?.Length > 256 || device.SerialNumber?.Length > 256)
        {
            throw new ArgumentException("Device association fields are too long.", nameof(device));
        }
        _saved.RemoveAll(x => x.VendorId == device.VendorId && x.ProductId == device.ProductId && x.SerialNumber == device.SerialNumber);
        _saved.Add(device);
    }

    /// <summary>Reads a bounded JSON association file. Corrupt data is reported instead of silently changing a target.</summary>
    public static async Task<EmbeddedDeviceDatabase> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var database = new EmbeddedDeviceDatabase();
        if (!File.Exists(path))
        {
            return database;
        }
        if (new FileInfo(path).Length > 1024 * 1024)
        {
            throw new InvalidDataException("Device database exceeds 1 MiB.");
        }
        await using var input = File.OpenRead(path);
        var devices = await JsonSerializer.DeserializeAsync<List<SavedEmbeddedDevice>>(input, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Device database is null.");
        if (devices.Count > 4096)
        {
            throw new InvalidDataException("Device database contains too many associations.");
        }
        foreach (var device in devices)
        {
            database.Remember(device);
        }
        return database;
    }

    /// <summary>Atomically replaces the database after writing a complete JSON file.</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(output, _saved.ToArray(), JsonOptions, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
