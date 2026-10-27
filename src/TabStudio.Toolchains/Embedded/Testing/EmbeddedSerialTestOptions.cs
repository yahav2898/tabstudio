using System.Globalization;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Testing;

/// <summary>Explicit capture settings for already-programmed Unity firmware; no build, flash or target reset is performed.</summary>
public sealed record EmbeddedSerialTestOptions
{
    /// <summary>Workspace-local serial test settings, distinct from host/probe test profiles.</summary>
    public const string FileName = ".tabstudio/embedded-serial-tests.json";

    /// <summary>Actual firmware's serial baud rate; zero requires the operator to configure it.</summary>
    public int BaudRate { get; init; }

    /// <summary>Maximum wait for one complete Unity run, including its final trailer.</summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>Optional USB serial interface number, required when the adapter is ambiguous.</summary>
    public int? InterfaceNumber { get; init; }

    /// <summary>Optional user label; it never establishes the target chip or programmed firmware.</summary>
    public string? BoardLabel { get; init; }

    /// <summary>Checks explicit capture configuration before opening a device or starting a process.</summary>
    public void Validate()
    {
        if (BaudRate is < 1 or > 3_500_000 || TimeoutSeconds is < 1 or > 3600 || InterfaceNumber is < 0 or > 255)
        {
            throw new InvalidDataException("Set the running firmware's BaudRate (1..3500000), TimeoutSeconds (1..3600), and optional USB InterfaceNumber (0..255).");
        }
        if (BoardLabel is { } label && (label.Length > 256 || label.Any(char.IsControl)))
        {
            throw new InvalidDataException("BoardLabel must be at most 256 characters without control characters; it is a label, not chip identification.");
        }
    }

    /// <summary>Derives results' identity from the actual selected USB attachment, independently of the user's optional label.</summary>
    /// <param name="device">The permissioned platform attachment.</param>
    public string BoardIdentity(UsbDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Validate();
        if (string.IsNullOrWhiteSpace(device.Id) || device.Id.Length > 1024 || device.Id.Any(char.IsControl))
        {
            throw new InvalidDataException("The selected USB attachment did not provide a usable identity.");
        }
        var identity = string.Create(CultureInfo.InvariantCulture, $"USB {device.VendorId:x4}:{device.ProductId:x4} {device.Id} interface {InterfaceNumber?.ToString(CultureInfo.InvariantCulture) ?? "auto"}");
        return string.IsNullOrWhiteSpace(BoardLabel) ? identity : BoardLabel + " [" + identity + "]";
    }

    /// <summary>Creates a receive-only capture through the existing permissioned USB serial worker and native control shim.</summary>
    /// <param name="connection">Retained by the caller until the process ends.</param>
    /// <param name="directory">Workspace directory.</param>
    /// <param name="portAlias">Fresh private absolute PTY alias.</param>
    public ProcessStartSpec Command(IUsbConnection connection, string directory, string portAlias)
    {
        Validate();
        return UsbBridgeProcess.CreateSerial(connection, new ProcessStartSpec
        {
            Program = "python3", WorkingDirectory = directory,
            Arguments = [UsbBridgeInstallation.ToolsDirectory + "/serial_unity_reader.py", "--port", "{port}",
                "--baud", BaudRate.ToString(CultureInfo.InvariantCulture), "--timeout", TimeoutSeconds.ToString(CultureInfo.InvariantCulture)],
        }, portAlias, BaudRate, InterfaceNumber, preserveLines: true);
    }
}
