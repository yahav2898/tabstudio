// Synthetic descriptor fixture; this never represents a physical ST-Link capture.
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

if (args.Length != 3) throw new ArgumentException("Supply PRoot binary, nusb-check binary, and private temporary parent directory.");
Directory.CreateDirectory(Path.GetFullPath(args[2]));
using var connection = new SyntheticConnection(Path.Combine(Path.GetFullPath(args[2]), "borrowed-descriptors-" + Guid.NewGuid().ToString("N") + ".bin"));
using var inventory = UsbfsDeviceInventory.Create(connection, Path.GetFullPath(args[2]));
var arguments = new List<string> { "-r", "/" };
foreach (var mount in inventory.Mounts)
{
    arguments.Add("-b");
    arguments.Add(mount.Source + ":" + mount.Target);
}
arguments.AddRange([Path.GetFullPath(args[1]), "0483", "3748"]);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var command = new ProcessStartSpec
{
    Program = args[0], Arguments = arguments,
    InheritedDescriptors = new Dictionary<int, int> { [connection.FileDescriptor] = 3 },
    Environment = new Dictionary<string, string?>
    {
        ["TABSTUDIO_USB_FD"] = "3", ["TABSTUDIO_USB_DEVPATH"] = inventory.DevicePath
    }
};
var runtime = new HostRuntime();
var result = await CommandRunner.RunAsync(runtime, command, Console.WriteLine, timeout.Token);
if (!result.Succeeded) throw new InvalidOperationException("nusb enumeration/open proof failed: " + result.Tail());
foreach (var marker in new[]
{
    "NUSB_PERMISSIONED_ENUMERATION_PASSED 0483:3748",
    "NUSB_PERMISSIONED_OPEN_REACHED_KERNEL_EPOLL_PASSED errno=1",
    "USBFS_FIXTURE_IOCTL_REACHED_KERNEL_ENOTTY_PASSED errno=25"
})
{
    if (!result.Output.Contains(marker, StringComparison.Ordinal)) throw new InvalidOperationException("Missing proof marker: " + marker);
}
var control = await CommandRunner.RunAsync(runtime, command with
{
    Arguments = [.. arguments, "--unbridged-control"],
    Environment = new Dictionary<string, string?> { ["TABSTUDIO_USB_FD"] = "3", ["TABSTUDIO_USB_DEVPATH"] = null }
}, Console.WriteLine, timeout.Token);
if (!control.Succeeded || !control.Output.Contains("NUSB_UNBRIDGED_EMPTY_PLACEHOLDER_REJECTED_PASSED", StringComparison.Ordinal))
    throw new InvalidOperationException("The unbridged empty-placeholder negative control did not reject the device: " + control.Tail());

sealed class SyntheticConnection : IUsbConnection
{
    private readonly FileStream _descriptors;
    private readonly string _path;

    public SyntheticConnection(string path)
    {
        _path = path;
        _descriptors = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        _descriptors.Write(RawDescriptors.Span);
        _descriptors.Flush();
        _descriptors.Position = 0;
    }

    public UsbDeviceInfo Device { get; } = new("/dev/bus/usb/001/002", 0x0483, 0x3748, "Synthetic descriptor fixture", []);
    public int FileDescriptor => _descriptors.SafeFileHandle.DangerousGetHandle().ToInt32();
    public int? ActiveConfiguration => 1;
    public ReadOnlyMemory<byte> RawDescriptors => new byte[]
    {
        18, 1, 0, 2, 0, 0, 0, 64, 0x83, 0x04, 0x48, 0x37, 0, 1, 0, 0, 0, 1,
        9, 2, 18, 0, 1, 1, 0, 0x80, 50,
        9, 4, 0, 0, 0, 0xFF, 0, 0, 0
    };
    public void Dispose()
    {
        _descriptors.Dispose();
        File.Delete(_path);
    }
}
