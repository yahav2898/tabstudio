using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class UsbBridgeTests
{
    [Fact]
    public async Task Glibc_side_root_receives_sources_and_never_copies_an_Alpine_native_binary()
    {
        using var root = new TabStudio.Testing.TempDirectory();
        await UsbBridgeInstallation.WriteSourcesAsync(root.Path, CancellationToken.None);
        var directory = Path.Combine(root.Path, UsbBridgeInstallation.ToolsDirectory.TrimStart('/'));
        Assert.Equal(UsbBridgeInstallation.SupportFiles["usb_fd.c"], await File.ReadAllTextAsync(Path.Combine(directory, "usb_fd.c")));
        Assert.Equal(UsbBridgeInstallation.SupportFiles["serial_posix.c"], await File.ReadAllTextAsync(Path.Combine(directory, "serial_posix.c")));
        Assert.False(File.Exists(Path.Combine(directory, "libusb-fd.so")));
        Assert.False(File.Exists(Path.Combine(directory, "serial-posix.so")));
    }

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
    public void OpenOCD_HID_calls_use_the_same_package_libusb_backend_before_linked_hidraw()
    {
        using var device = new Connection(42);
        var command = new ProcessStartSpec { Program = "/usr/bin/openocd", Environment = new Dictionary<string, string?> { ["LD_PRELOAD"] = "/other.so" } };
        var spec = UsbBridgeProcess.CreateLibUsb(device, command);
        Assert.Equal(UsbBridgeInstallation.ToolsDirectory + "/libusb-fd.so:libhidapi-libusb.so.0:/other.so", spec.Environment["LD_PRELOAD"]);
        Assert.Contains("hidapi", UsbBridgeInstallation.Packages);
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
    public void Wrapped_glibc_SDKs_keep_explicit_ABI_routing_while_Alpine_flashers_do_not_change_ABI()
    {
        using var device = new Connection(42);
        var port = Path.Combine(Path.GetTempPath(), "tabstudio-serial-test", "ttyUSB0");
        var sdk = UsbBridgeProcess.CreateSerial(device, new ProcessStartSpec { Program = "arduino-cli", Arguments = ["upload", "--port", "{port}"] }, port);
        Assert.Equal("1", sdk.Environment[EmbeddedGlibcRuntime.RoutingMarker]);
        Assert.True(EmbeddedGlibcRuntime.UsesGlibc(sdk));
        var alpine = UsbBridgeProcess.CreateSerial(device, new ProcessStartSpec { Program = "avrdude", Arguments = ["-P", "{port}"] }, port);
        Assert.False(alpine.Environment.ContainsKey(EmbeddedGlibcRuntime.RoutingMarker));
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
        Assert.Contains(native, step => step.Command.Contains(UsbBridgeInstallation.ToolsDirectory + "/usb_fd.c"));
        Assert.Contains(native, step => step.Command.Contains(UsbBridgeInstallation.ToolsDirectory + "/serial_posix.c"));
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
