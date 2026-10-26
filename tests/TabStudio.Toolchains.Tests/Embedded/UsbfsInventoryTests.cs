using TabStudio.Testing;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class UsbfsInventoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Inventory_uses_actual_descriptors_and_exposes_only_the_granted_device_to_one_process()
    {
        using var connection = new Connection();
        string created;
        using (var inventory = UsbfsDeviceInventory.Create(connection, _temp.Path))
        {
            created = inventory.Directory;
            Assert.Equal("/dev/bus/usb/001/002", inventory.DevicePath);
            Assert.Equal(2, inventory.Mounts.Count);
            var device = Path.Combine(created, "sysfs", "1-1");
            Assert.Equal("0483\n", File.ReadAllText(Path.Combine(device, "idVendor")));
            Assert.Equal("02.00\n", File.ReadAllText(Path.Combine(device, "version")));
            Assert.Equal("1\n", File.ReadAllText(Path.Combine(device, "bConfigurationValue")));
            Assert.Single(Directory.EnumerateFiles(Path.Combine(created, "usbfs"), "*", SearchOption.AllDirectories));
            Assert.Equal("ff\n", File.ReadAllText(Path.Combine(device, "1-1:1.0", "bInterfaceClass")));
            var command = UsbBridgeProcess.CreateUsbfs(connection, inventory, new ProcessStartSpec { Program = "probe-rs", Arguments = ["list"] });
            Assert.Equal("3", command.Environment["TABSTUDIO_USB_FD"]);
            Assert.Equal(inventory.DevicePath, command.Environment["TABSTUDIO_USB_DEVPATH"]);
            Assert.Equal(3, command.InheritedDescriptors[42]);
            Assert.Equal(inventory.Mounts, command.Mounts);
            Assert.Contains("usbfs-fd.so", command.Environment["LD_PRELOAD"], StringComparison.Ordinal);
        }
        Assert.False(Directory.Exists(created));
    }

    [Fact]
    public void Inventory_refuses_missing_descriptors_wrong_identity_and_unknown_configuration()
    {
        using var connection = new Connection();
        connection.Bytes = [];
        Assert.Throws<InvalidDataException>(() => UsbfsDeviceInventory.Create(connection, _temp.Path));
        connection.Bytes = Descriptors();
        connection.Bytes[8] = 0;
        Assert.Throws<InvalidDataException>(() => UsbfsDeviceInventory.Create(connection, _temp.Path));
        connection.Bytes = Descriptors();
        connection.Configuration = null;
        Assert.Throws<InvalidDataException>(() => UsbfsDeviceInventory.Create(connection, _temp.Path));
        Assert.Empty(Directory.EnumerateDirectories(_temp.Path));
    }

    [Fact]
    public void Inventory_cleans_up_invalid_configuration_descriptors()
    {
        using var connection = new Connection();
        connection.Bytes = [.. Descriptors(), 0, 4];
        Assert.Throws<InvalidDataException>(() => UsbfsDeviceInventory.Create(connection, _temp.Path));
        Assert.Empty(Directory.EnumerateDirectories(_temp.Path));
    }

    private static byte[] Descriptors() =>
    [
        18, 1, 0, 2, 0, 0, 0, 64, 0x83, 0x04, 0x48, 0x37, 0, 1, 0, 0, 0, 1,
        9, 2, 18, 0, 1, 1, 0, 0x80, 50,
        9, 4, 0, 0, 0, 0xFF, 0, 0, 0
    ];

    public void Dispose() => _temp.Dispose();

    private sealed class Connection : IUsbConnection
    {
        public UsbDeviceInfo Device { get; } = new("/dev/bus/usb/001/002", 0x0483, 0x3748, "Synthetic ST-Link descriptor fixture", []);
        public int FileDescriptor => 42;
        public byte[] Bytes { get; set; } = Descriptors();
        public int? Configuration { get; set; } = 1;
        public ReadOnlyMemory<byte> RawDescriptors => Bytes;
        public int? ActiveConfiguration => Configuration;
        public void Dispose() { }
    }
}
