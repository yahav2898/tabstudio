// Synthetic descriptor fixture; this never represents a physical ST-Link capture.
using System.Diagnostics;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;

if (args.Length != 3) throw new ArgumentException("Supply PRoot binary, nusb-check binary, and private temporary parent directory.");
using var connection = new SyntheticConnection();
using var inventory = UsbfsDeviceInventory.Create(connection, Path.GetFullPath(args[2]));
var info = new ProcessStartInfo(args[0]) { UseShellExecute = false };
info.ArgumentList.Add("-r");
info.ArgumentList.Add("/");
foreach (var mount in inventory.Mounts)
{
    info.ArgumentList.Add("-b");
    info.ArgumentList.Add(mount.Source + ":" + mount.Target);
}
info.ArgumentList.Add(Path.GetFullPath(args[1]));
info.ArgumentList.Add("0483");
info.ArgumentList.Add("3748");
using var child = Process.Start(info) ?? throw new InvalidOperationException("PRoot did not start.");
await child.WaitForExitAsync();
if (child.ExitCode != 0) throw new InvalidOperationException("nusb enumeration proof failed with " + child.ExitCode);

sealed class SyntheticConnection : IUsbConnection
{
    public UsbDeviceInfo Device { get; } = new("/dev/bus/usb/001/002", 0x0483, 0x3748, "Synthetic descriptor fixture", []);
    public int FileDescriptor => 42; // Enumeration only: no device open or USB transfer is attempted.
    public int? ActiveConfiguration => 1;
    public ReadOnlyMemory<byte> RawDescriptors => new byte[]
    {
        18, 1, 0, 2, 0, 0, 0, 64, 0x83, 0x04, 0x48, 0x37, 0, 1, 0, 0, 0, 1,
        9, 2, 18, 0, 1, 1, 0, 0x80, 50,
        9, 4, 0, 0, 0, 0xFF, 0, 0, 0
    };
    public void Dispose() { }
}
