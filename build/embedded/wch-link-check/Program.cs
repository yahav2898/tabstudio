using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

// This fixture runs the unchanged official CLI against a test-only kernel ioctl backend.
// It is not a physical WCH-Link capture, target connection or flash verification.
if (args.Length != 6) throw new ArgumentException("Supply PRoot, official x64 wlink, kernel backend .so, raw URB client, padded V003 loader and private work directory.");
if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
    throw new PlatformNotSupportedException("This pinned official binary proof currently requires native Linux x64.");
var proot = Path.GetFullPath(args[0]);
var wlink = Path.GetFullPath(args[1]);
var backend = Path.GetFullPath(args[2]);
var rawClient = Path.GetFullPath(args[3]);
var loader = Path.GetFullPath(args[4]);
var work = Path.GetFullPath(args[5]);
Directory.CreateDirectory(work);
const string binaryHash = "3e6d96c2dfffc56c839e90e507245a698b5264387d073d6e517a31dc1002d13a";
const string loaderHash = "c80c9f055ece8b0a16ebce1c8f2381bcb44c45a7708681de43ca9acb958a528b";
if (Hash(wlink) != binaryHash || new FileInfo(loader).Length != 512 || Hash(loader) != loaderHash)
    throw new InvalidDataException("The official wlink binary or canonical V003 wire loader differs from its verified pin.");
var firmware = Path.Combine(work, "firmware.bin");
var bytes = Enumerable.Range(0, 1037).Select(i => unchecked((byte)(17 + i * 73))).ToArray();
await File.WriteAllBytesAsync(firmware, bytes);
var outcomes = new List<object>();
var summary = new Dictionary<string, object?>
{
    ["hardwareVerified"] = false, ["androidVerified"] = false, ["syntheticKernelBackend"] = true,
    ["upstreamSourceCommit"] = "7ed4b99321fb8e88eb05f7553211b39d41196a6c", ["officialBinarySha256"] = binaryHash,
    ["loaderWireSha256"] = loaderHash, ["outcomes"] = outcomes,
};
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
var runtime = new HostRuntime();
try
{
    var preflight = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
    {
        Program = proot, Arguments = ["/bin/false"], Environment = new Dictionary<string, string?> { ["TABSTUDIO_WCH_GUARD_CHECK"] = "1" },
    }, Console.WriteLine, deadline.Token);
    Require(preflight.Succeeded && preflight.Output.Trim() == "TABSTUDIO_WCH_GUARD_V1", "The actual PRoot did not prove its WCH guard before guest execution.");
    var flashState = Path.Combine(work, "programmed-flash-" + Guid.NewGuid().ToString("N") + ".bin");
    var identityPath = Path.Combine(work, "identity-" + Guid.NewGuid().ToString("N") + ".bin");
    var identity = await Run("read-only-identification", "normal", wlink,
        ["--chip", "CH32V003", "--no-detach", "dump", "0x08000000", "4", "--out", identityPath], flashState, "ch32v003-read-only-v1", null);
    Require(identity.Result.Succeeded && File.Exists(identityPath) && new FileInfo(identityPath).Length == 4 &&
        identity.Result.Output.Contains("CH32V003F4P6", StringComparison.Ordinal) && identity.Result.Output.Contains("0x00300500", StringComparison.Ordinal),
        "Read-only guarded discovery did not report the real synthetic attach ID.");
    var flash = await Run("normal-flash", "normal", wlink, ["--chip", "CH32V003", "--no-detach", "flash", "--no-run", firmware], flashState);
    Require(flash.Result.Succeeded && flash.Result.Output.Contains("Flash done", StringComparison.Ordinal), "The real official CLI did not complete its flash protocol.");
    Require(File.Exists(flashState) && (await File.ReadAllBytesAsync(flashState)).AsSpan(0, bytes.Length).SequenceEqual(bytes), "Native backend did not receive the exact firmware bytes through the guard.");
    Require(flash.Wire.Contains("loaderBytes=512 programBytes=1088", StringComparison.Ordinal), "The native backend did not receive the canonical loader and bounded padded firmware.");
    var dumpPath = Path.Combine(work, "readback-" + Guid.NewGuid().ToString("N") + ".bin");
    var dump = await Run("independent-dump", "normal", wlink, ["--chip", "CH32V003", "--no-detach", "dump", "0x08000000", bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), "--out", dumpPath], flashState, "ch32v003-read-only-v1");
    Require(dump.Result.Succeeded, "The official independent dump failed.");
    var readback = await File.ReadAllBytesAsync(dumpPath);
    Require(readback.Length == 1040 && readback.AsSpan(0, bytes.Length).SequenceEqual(bytes) && readback.AsSpan(bytes.Length).IndexOfAnyExcept((byte)0xff) < 0, "Independent dump differs from the actual programmed byte stream.");
    var reset = await Run("reset-only-after-comparison", "normal", wlink, ["--chip", "CH32V003", "--no-detach", "reset", "quit"], flashState, "ch32v003-reset-v1");
    Require(reset.Result.Succeeded && reset.Wire.Contains("data=810b0101", StringComparison.Ordinal), "Separate guarded reset was not completed after verification.");
    Console.WriteLine("WCH_OFFICIAL_NUSB_FLASH_READBACK_PASSED");

    foreach (var scenario in new[] { "protected", "write-protected", "wrong-chip", "unknown-read", "unknown-write", "malformed-attach", "short-attach" })
    {
        var result = await Run(scenario, scenario, wlink, ["--chip", "CH32V003", "--no-detach", "flash", "--no-run", firmware], Path.Combine(work, scenario + ".flash"));
        Require(!result.Result.Succeeded, "Unsafe/invalid scenario was accepted: " + scenario);
        Require(!result.Wire.Contains("OUT endpoint=02", StringComparison.Ordinal), "Unsafe scenario reached the native loader/data endpoint: " + scenario);
        Console.WriteLine("WCH_OFFICIAL_GUARD_REFUSAL_PASSED " + scenario);
    }
    var queued = await Run("raw-queue-cancel", "normal", rawClient, ["queue-cancel"], Path.Combine(work, "raw.flash"));
    Require(queued.Result.Succeeded && queued.Result.Output.Contains("WCH_RAW_QUEUED_CANCEL_REAP_PASSED", StringComparison.Ordinal), "Raw queued/cancelled URB identity proof failed.");
    foreach (var mode in new[] { "fragment", "coalesced", "unprotect" })
    {
        var result = await Run("raw-" + mode, "normal", rawClient, [mode], Path.Combine(work, "raw-" + mode + ".flash"));
        Require(result.Result.Succeeded && result.Result.Output.Contains("WCH_RAW_UNSAFE_COMMAND_REFUSED", StringComparison.Ordinal), "Raw unsafe ioctl was accepted: " + mode);
        Require(!result.Wire.Contains("OUT endpoint=", StringComparison.Ordinal), "Unsafe command prefix reached the native backend: " + mode);
    }
    summary["passed"] = true;
    Console.WriteLine("WCH_PERMISSIONED_STOCK_NUSB_GUARD_PASSED");
    return 0;
}
catch (Exception error)
{
    summary["passed"] = false; summary["error"] = error.ToString(); Console.Error.WriteLine(error); return 1;
}
finally
{
    await File.WriteAllTextAsync(Path.Combine(work, "summary.json"), JsonSerializer.Serialize(summary, SummaryJson.Indented));
}

async Task<(CommandResult Result, string Wire)> Run(string name, string scenario, string program, IReadOnlyList<string> command, string flashPath,
    string mode = "ch32v003-v1", string? exactChipId = "00300500")
{
    var wirePath = Path.Combine(work, name + "-" + Guid.NewGuid().ToString("N") + ".wire.log");
    using var connection = new SyntheticConnection();
    using var inventory = UsbfsDeviceInventory.Create(connection, work);
    var arguments = new List<string> { "--kill-on-exit", "--link2symlink", "--sysvipc", "-L", "-0", "-r", "/" };
    foreach (var mount in inventory.Mounts) arguments.AddRange(["-b", mount.Source + ":" + mount.Target]);
    arguments.AddRange(["/usr/bin/env", "-u", "LD_PRELOAD", "-u", "TABSTUDIO_TEST_TRACER_PID", program]);
    arguments.AddRange(command);
    var process = new ProcessStartSpec
    {
        Program = "/bin/sh",
        Arguments = ["-c", "export TABSTUDIO_TEST_TRACER_PID=$$; exec \"$@\"", "tabstudio-wch-fixture", proot, .. arguments],
        InheritedDescriptors = new Dictionary<int, int> { [connection.FileDescriptor] = 3 },
        Environment = new Dictionary<string, string?>
        {
            ["LD_PRELOAD"] = backend,
            ["TABSTUDIO_USB_FD"] = "3", ["TABSTUDIO_USB_DEVPATH"] = inventory.DevicePath,
            ["TABSTUDIO_USB_WCH_GUARD"] = mode, ["TABSTUDIO_WCH_CHIP_ID"] = exactChipId,
            ["TABSTUDIO_TEST_WCH_SCENARIO"] = scenario, ["TABSTUDIO_TEST_WCH_LOG"] = wirePath,
            ["TABSTUDIO_TEST_WCH_FIRMWARE"] = firmware, ["TABSTUDIO_TEST_WCH_FLASH"] = flashPath, ["TABSTUDIO_TEST_WCH_LOADER"] = loader,
        },
    };
    Console.WriteLine("Synthetic test: " + name);
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
    timeout.CancelAfter(TimeSpan.FromSeconds(20));
    var result = await CommandRunner.RunAsync(runtime, process, Console.WriteLine, timeout.Token);
    await File.WriteAllTextAsync(Path.Combine(work, name + ".tool.log"), result.Output, CancellationToken.None);
    var wire = File.Exists(wirePath) ? await File.ReadAllTextAsync(wirePath) : string.Empty;
    outcomes.Add(new { name, exitCode = result.Exit.ExitCode, wireLog = Path.GetFileName(wirePath), nativeBackendObserved = wire.Contains("BACKEND", StringComparison.Ordinal) });
    Require(wire.Contains("BACKEND", StringComparison.Ordinal), "The tracer did not use the native synthetic backend; this is not a guard proof: " + name);
    if (program == wlink)
        Require(wire.Contains("SYNTHETIC_DESCRIPTOR_REWIND", StringComparison.Ordinal), "The stock nusb client did not perform its ordinary USBFS descriptor rewind: " + name);
    Require(!wire.Contains("VIOLATION", StringComparison.Ordinal) && !wire.Contains("UNSUPPORTED_IOCTL", StringComparison.Ordinal), "An unsafe/unknown operation reached the native backend: " + name);
    return (result, wire);
}
static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

sealed class SyntheticConnection : IUsbConnection
{
    private readonly Socket _socket;
    private readonly Socket _peer;
    public unsafe SyntheticConnection()
    {
        var pair = stackalloc int[2];
        if (NativeSocket.Pair(1, 1, 0, pair) != 0) throw new IOException("socketpair failed: " + Marshal.GetLastPInvokeError());
        _socket = new Socket(new SafeSocketHandle((nint)pair[0], ownsHandle: true));
        _peer = new Socket(new SafeSocketHandle((nint)pair[1], ownsHandle: true));
        var descriptor = RawDescriptors.Span;
        while (descriptor.Length > 0) descriptor = descriptor[_peer.Send(descriptor)..];
        _peer.Shutdown(SocketShutdown.Send);
    }
    public UsbDeviceInfo Device { get; } = new("/dev/bus/usb/001/002", 0x1a86, 0x8010, "Synthetic WCH-Link protocol fixture", []);
    public int FileDescriptor => _socket.Handle.ToInt32();
    public int? ActiveConfiguration => 1;
    public ReadOnlyMemory<byte> RawDescriptors => new byte[]
    {
        18,1,0,2,0,0,0,64,0x86,0x1a,0x10,0x80,0,1,0,0,0,1,
        9,2,46,0,1,1,0,0x80,50, 9,4,0,0,4,0xff,0,0,0,
        7,5,1,2,64,0,0, 7,5,0x81,2,64,0,0,
        7,5,2,2,64,0,0, 7,5,0x82,2,64,0,0,
    };
    public void Dispose() { _socket.Dispose(); _peer.Dispose(); }
}
internal static partial class NativeSocket
{
    [LibraryImport("libc", EntryPoint = "socketpair", SetLastError = true)]
    internal static unsafe partial int Pair(int domain, int type, int protocol, int* descriptors);
}
internal static class SummaryJson
{
    internal static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
