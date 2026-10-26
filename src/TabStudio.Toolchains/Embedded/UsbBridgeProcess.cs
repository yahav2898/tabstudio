using System.Text.Json;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded;

/// <summary>Scopes native flashing tools to one Android permissioned USB connection.</summary>
public static class UsbBridgeProcess
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Runs an unchanged dynamically linked libusb client against exactly the granted device.</summary>
    /// <remarks>Does not support nusb, direct usbfs, hidraw, or statically linked libusb.</remarks>
    /// <param name="connection">Connection the caller keeps open until the process exits.</param>
    /// <param name="command">Program to run in the Linux environment.</param>
    public static ProcessStartSpec CreateLibUsb(IUsbConnection connection, ProcessStartSpec command)
    {
        var spec = WithConnection(connection, command);
        var environment = new Dictionary<string, string?>(spec.Environment);
        environment.TryGetValue("LD_PRELOAD", out var previous);
        environment["LD_PRELOAD"] = UsbBridgeInstallation.ToolsDirectory + "/libusb-fd.so" + (string.IsNullOrEmpty(previous) ? string.Empty : ":" + previous);
        return spec with { Environment = environment };
    }

    /// <summary>Runs an unchanged dynamically linked POSIX serial client through a raw PTY and control bridge.</summary>
    /// <param name="connection">Connection retained by the caller for the worker's lifetime.</param>
    /// <param name="command">Arguments may use <c>{port}</c>, replaced with the private PTY alias.</param>
    /// <param name="portAlias">Absolute, unused alias path, usually under the project's .tabstudio/dev folder.</param>
    /// <param name="baud">Initial baud rate; the client can change it with termios.</param>
    /// <param name="interfaceNumber">USB serial interface, or automatic selection.</param>
    public static ProcessStartSpec CreateSerial(IUsbConnection connection, ProcessStartSpec command, string portAlias, int baud = 115200, int? interfaceNumber = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(portAlias);
        if (!Path.IsPathFullyQualified(portAlias)) throw new ArgumentException("The serial port alias must be absolute.", nameof(portAlias));
        if (baud is < 1 or > 3_500_000) throw new ArgumentOutOfRangeException(nameof(baud));
        if (interfaceNumber is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(interfaceNumber));
        var spec = WithConnection(connection, command);
        List<string> arguments = [UsbBridgeInstallation.ToolsDirectory + "/serial_bridge.py", "--shim", UsbBridgeInstallation.ToolsDirectory + "/serial-posix.so", "--port", portAlias, "--baud", baud.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        if (interfaceNumber is { } number) arguments.AddRange(["--interface", number.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        arguments.AddRange(["--", command.Program, .. command.Arguments]);
        return spec with { Program = "python3", Arguments = arguments };
    }

    /// <summary>Performs the Arduino 1200-baud DTR touch and closes before the caller waits for re-enumeration.</summary>
    /// <param name="connection">Original application USB connection.</param>
    /// <param name="portAlias">Optional private alias; a unique temporary path is used by default.</param>
    public static ProcessStartSpec CreateTouch1200(IUsbConnection connection, string? portAlias = null)
    {
        const string script = "import os,sys,termios,fcntl,array; f=os.open(sys.argv[1],os.O_RDWR|os.O_NOCTTY); t=termios.tcgetattr(f); t[2]|=termios.HUPCL; t[4]=t[5]=termios.B1200; termios.tcsetattr(f,termios.TCSANOW,t); fcntl.ioctl(f,termios.TIOCMBIS,array.array('i',[termios.TIOCM_DTR])); os.close(f)";
        return CreateSerial(connection, new ProcessStartSpec { Program = "python3", Arguments = ["-c", script, "{port}"] }, portAlias ?? "/tmp/tabstudio-touch-" + Guid.NewGuid().ToString("N"), 1200);
    }

    private static ProcessStartSpec WithConnection(IUsbConnection connection, ProcessStartSpec command)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(command);
        if (connection.FileDescriptor < 3) throw new IOException("The USB connection is closed.");
        if (command.InheritedDescriptors.Any(pair => pair.Value == 3 && pair.Key != connection.FileDescriptor)) throw new ArgumentException("Child descriptor 3 is reserved for the permissioned USB connection.", nameof(command));
        var descriptors = new Dictionary<int, int>(command.InheritedDescriptors) { [connection.FileDescriptor] = 3 };
        var environment = new Dictionary<string, string?>(command.Environment)
        {
            ["TABSTUDIO_USB_FD"] = "3",
            ["TABSTUDIO_USB_DEVICE"] = JsonSerializer.Serialize(connection.Device, JsonOptions),
        };
        return command with { InheritedDescriptors = descriptors, Environment = environment };
    }
}
