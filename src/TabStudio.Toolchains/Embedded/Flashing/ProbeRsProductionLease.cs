using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>A single production worker's private inventory and selector. The caller retains its granted connection until disposal.</summary>
public sealed class ProbeRsProductionLease : IDisposable
{
    private readonly IUsbConnection _connection;
    private readonly UsbfsInventory _inventory;
    private bool _disposed;

    private ProbeRsProductionLease(IUsbConnection connection, UsbfsInventory inventory, EmbeddedFlashPlan plan)
    {
        _connection = connection;
        _inventory = inventory;
        Plan = plan;
    }

    /// <summary>The application plan, rebound only to the one granted worker's VID/PID.</summary>
    public EmbeddedFlashPlan Plan { get; }

    /// <summary>Builds a private permissioned namespace before any programming starts, preserving the reviewed image digest.</summary>
    /// <param name="request">The explicit chip, image and application bounds.</param>
    /// <param name="reviewed">The batch's already reviewed and verified-upload plan.</param>
    /// <param name="connection">One independently granted probe connection, borrowed for the worker lifetime.</param>
    /// <param name="privateParent">The app's private inventory directory.</param>
    public static ProbeRsProductionLease Create(EmbeddedFlashRequest request, EmbeddedFlashPlan reviewed,
        IUsbConnection connection, string privateParent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reviewed);
        ArgumentNullException.ThrowIfNull(connection);
        if (request.Protocol != EmbeddedFlashProtocol.ProbeRs || !reviewed.Verified || request.Touch1200Baud)
            throw new NotSupportedException("Probe production requires a stable, verified probe-rs application plan.");
        if (connection.Device.Interfaces.Count > 0 && connection.Device.Interfaces.All(item => item.Class == 3))
            throw new NotSupportedException("Native probe-rs HID-only CMSIS-DAP v1 cannot use this bulk-USB batch route.");
        var plan = EmbeddedFlashPlanner.Plan(request with { ProbeSelector = Selector(connection.Device, request.ProbeSelector) });
        if (plan.FirmwareSha256 != reviewed.FirmwareSha256)
            throw new InvalidDataException("Firmware changed after the production batch was reviewed. No board has been programmed.");
        return new(connection, UsbfsDeviceInventory.Create(connection, privateParent), plan);
    }

    /// <summary>Rebinds a single probe's serial selector while preserving an explicitly selected FTDI channel.</summary>
    /// <param name="device">The actual granted attachment.</param>
    /// <param name="configured">The reviewed profile's optional serial/channel selector.</param>
    public static string Selector(UsbDeviceInfo device, string? configured)
    {
        ArgumentNullException.ThrowIfNull(device);
        var identity = FormattableString.Invariant($"{device.VendorId:x4}:{device.ProductId:x4}");
        var match = Regex.Match(configured ?? "", @"\A(?<identity>[0-9a-fA-F]{4}:[0-9a-fA-F]{4})(?<channel>-\d{1,3})(?::[^\r\n]+)?\z",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var hasChannel = configured?.Split(':').ElementAtOrDefault(1)?.Contains('-', StringComparison.Ordinal) == true;
        if (!match.Success)
        {
            if (hasChannel) throw new InvalidOperationException("The configured probe channel is malformed. Select its actual numeric channel before programming.");
            return identity;
        }
        if (!byte.TryParse(match.Groups["channel"].Value.AsSpan(1), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            throw new InvalidOperationException("The configured probe channel must be between 0 and 255.");
        }
        if (!identity.Equals(match.Groups["identity"].Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The configured probe channel belongs to another VID/PID. Select the actual channel before starting this batch.");
        }
        return identity + match.Groups["channel"].Value;
    }

    /// <summary>Exposes only this worker's probe and inherited grant to an unchanged upstream command.</summary>
    /// <param name="command">One step of the worker's validated application plan.</param>
    public ProcessStartSpec Wrap(ProcessStartSpec command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return UsbBridgeProcess.CreateUsbfs(_connection, _inventory, command);
    }

    /// <summary>Removes the private namespace after every worker process has finished.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inventory.Dispose();
    }
}
