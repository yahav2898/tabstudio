using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class UsbBridgeTests
{
    [Fact]
    public void Libusb_bridge_inherits_only_explicit_device_and_preserves_command_context()
    {
        using var device = new Connection(42);
        var command = new ProcessStartSpec
        {
            Program = "dfu-util", Arguments = ["-D", "firmware.bin"], WorkingDirectory = "/project",
            Environment = new Dictionary<string, string?> { ["KEEP"] = "value", ["LD_PRELOAD"] = "/other.so" },
        };
        var spec = UsbBridgeProcess.CreateLibUsb(device, command);
        Assert.Equal(command.Program, spec.Program);
        Assert.Equal(command.Arguments, spec.Arguments);
        Assert.Equal("/project", spec.WorkingDirectory);
        Assert.Equal(3, spec.InheritedDescriptors[42]);
        Assert.Single(spec.InheritedDescriptors);
        Assert.Equal("3", spec.Environment["TABSTUDIO_USB_FD"]);
        Assert.Equal("value", spec.Environment["KEEP"]);
        Assert.Equal(UsbBridgeInstallation.ToolsDirectory + "/libusb-fd.so:/other.so", spec.Environment["LD_PRELOAD"]);
        Assert.False(device.Disposed);
    }

    [Fact]
    public void Bridge_rejects_closed_devices_and_descriptor_collisions_before_launch()
    {
        using var closed = new Connection(-1);
        using var open = new Connection(42);
        var command = new ProcessStartSpec { Program = "openocd" };
        Assert.Throws<IOException>(() => UsbBridgeProcess.CreateLibUsb(closed, command));
        Assert.Throws<ArgumentException>(() => UsbBridgeProcess.CreateLibUsb(open, command with
        {
            InheritedDescriptors = new Dictionary<int, int> { [99] = 3 },
        }));
    }

    [Fact]
    public void Serial_bridge_keeps_tool_arguments_out_of_shell_and_validates_line_settings()
    {
        using var device = new Connection(42);
        var alias = Path.Combine(Path.GetTempPath(), "tabstudio-tests", "ttyUSB0");
        var command = new ProcessStartSpec { Program = "avrdude", Arguments = ["-P", "{port}", "-U", "flash:w:name;literal.hex:i"] };
        var spec = UsbBridgeProcess.CreateSerial(device, command, alias, 1200, 2);
        Assert.Equal("python3", spec.Program);
        Assert.Contains("--interface", spec.Arguments);
        Assert.Contains(alias, spec.Arguments);
        Assert.Equal(command.Arguments, spec.Arguments.TakeLast(command.Arguments.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => UsbBridgeProcess.CreateSerial(device, command, alias, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => UsbBridgeProcess.CreateSerial(device, command, alias, interfaceNumber: 256));
        Assert.Throws<ArgumentException>(() => UsbBridgeProcess.CreateSerial(device, command, "relative/ttyUSB0"));
    }

    [Fact]
    public void Sources_are_packaged_and_both_native_build_steps_rebuild_after_source_updates()
    {
        var sources = UsbBridgeInstallation.SupportFiles;
        Assert.Contains("class SerialBridge", sources["serial_bridge.py"], StringComparison.Ordinal);
        Assert.Contains("int tcsetattr", sources["serial_posix.c"], StringComparison.Ordinal);
        Assert.Contains("libusb_wrap_sys_device", sources["usb_fd.c"], StringComparison.Ordinal);
        Assert.Contains("def configure", sources["usb_serial.py"], StringComparison.Ordinal);
        var native = UsbBridgeInstallation.Setup.Where(step => step.Command.Count > 0).ToList();
        Assert.Equal(2, native.Count);
        Assert.All(native, step => Assert.True(step.RunAfterSetupChanges));
        Assert.Equal(UsbBridgeInstallation.Setup.Count, UsbBridgeInstallation.Setup.Select(step => step.Name).Distinct().Count());
    }

    private sealed class Connection(int descriptor) : IUsbConnection
    {
        public UsbDeviceInfo Device { get; } = new("attached-device", 0x483, 0x3748, "ST-Link", []);
        public int FileDescriptor => descriptor;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
