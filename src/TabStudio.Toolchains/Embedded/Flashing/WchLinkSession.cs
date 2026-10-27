using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>A CH32V003 image reviewed before any USB grant is opened. Padding is included in its permitted flash interval.</summary>
public sealed class WchLinkFlashPlan
{
    internal WchLinkFlashPlan(string firmwarePath, string firmwareSha256, string workingDirectory, string expectedTarget, int originalLength, int programmedLength)
    {
        FirmwarePath = firmwarePath; FirmwareSha256 = firmwareSha256; WorkingDirectory = workingDirectory;
        ExpectedTarget = expectedTarget; OriginalLength = originalLength; ProgrammedLength = programmedLength;
    }
    /// <summary>The actual reviewed input file.</summary>
    public string FirmwarePath { get; }
    /// <summary>Digest of the original input, before known FF padding.</summary>
    public string FirmwareSha256 { get; }
    /// <summary>Working directory shared with the tool runtime.</summary>
    public string WorkingDirectory { get; }
    /// <summary>Explicit family or known package required before writing.</summary>
    public string ExpectedTarget { get; }
    /// <summary>Original image length in bytes.</summary>
    public int OriginalLength { get; }
    /// <summary>Complete 64-byte padded interval included in the flash-range review.</summary>
    public int ProgrammedLength { get; }
}

/// <summary>The full chip ID actually returned by the guarded probe, including its revision bits.</summary>
/// <param name="ChipId">Exact attach response, required again by each native guard session.</param>
/// <param name="Part">Package identity decoded from the pinned upstream chip table.</param>
public sealed record WchLinkTarget(uint ChipId, string Part);

/// <summary>Runs unchanged wlink through the native USB protocol guard. Every worker owns a newly opened Android connection.</summary>
/// <remarks>The native guard refuses option/protection changes even if the upstream CLI tries to unprotect automatically.
/// This narrowly supports CH32V003 code flash; it is not permission to use arbitrary wlink commands.</remarks>
public static class WchLinkSession
{
    /// <summary>Marker emitted by guarded PRoot itself before executing the deliberately failing guest program.</summary>
    public const string GuardMarker = "TABSTUDIO_WCH_GUARD_V1";
    private const uint FlashStart = 0x08000000;
    private const int FlashBytes = 16 * 1024;
    private const string ReadMode = "ch32v003-read-only-v1";
    private const string FlashMode = "ch32v003-v1";
    private const string ResetMode = "ch32v003-reset-v1";
    private static readonly Regex Attached = new(@"Attached chip: CH32V003 \[(?<part>CH32V003(?:F4P6|F4U6|A4M6|J4M6))\] \(ChipID: 0x(?<id>[0-9a-fA-F]{8})\)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Validates a BIN and its entire 64-byte padded programming interval without opening a device.</summary>
    public static WchLinkFlashPlan Plan(EmbeddedFlashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Target);
        if (request.Protocol != EmbeddedFlashProtocol.WchLink || request.EspFlasherArguments is not null || request.Touch1200Baud || request.UseSerialTransport)
            throw new ArgumentException("WCH-Link requires its dedicated USB guard route without serial/reset metadata.", nameof(request));
        if (request.BinaryAddress is not (0 or FlashStart) || !string.Equals(Path.GetExtension(request.FirmwarePath), ".bin", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Guarded WCH-Link accepts only a CH32V003 raw BIN explicitly based at 0 or 0x08000000.", nameof(request));
        if (!KnownTarget(request.Target)) throw new ArgumentException("The reviewed WCH-Link guard supports CH32V003 and its four known packages only.", nameof(request));
        var path = Path.GetFullPath(request.FirmwarePath);
        var info = new FileInfo(path);
        if (info.Length is < 1 or > FlashBytes) throw new InvalidDataException("CH32V003 code flash must contain 1..16384 bytes.");
        var bytes = File.ReadAllBytes(path);
        var paddedLength = checked((bytes.Length + 63) & ~63);
        // CH32 has address-zero aliases, but every reviewed range and the native
        // programming command use the physical code-flash interval consistently.
        var ranges = request.ApplicationFlash.Select(range => range.Start < FlashStart
            ? new FlashAddressRange(checked(range.Start + FlashStart), range.Length) : range).ToArray();
        FlashSafety.ValidateFirmware([new FlashAddressRange(FlashStart, checked((ulong)paddedLength))], ranges);
        return new(path, Convert.ToHexStringLower(SHA256.HashData(bytes)), Path.GetFullPath(request.WorkingDirectory),
            request.Target.ToUpperInvariant(), bytes.Length, paddedLength);
    }

    /// <summary>Checks the native guard, identifies the real part, programs a private immutable padded image, independently verifies it, then resets.</summary>
    /// <param name="runtime">The existing Android Linux/PRoot runtime, with native wlink installed.</param>
    /// <param name="plan">Previously reviewed image and application interval.</param>
    /// <param name="openFresh">Opens a fresh exclusive grant for the same selected USB attachment for each worker. The session owns and closes it.</param>
    /// <param name="privateParent">Private app directory for per-worker inventories and temporary images.</param>
    /// <param name="output">Streaming tool output.</param>
    /// <param name="cancellationToken">Stops the current worker and prevents subsequent operations.</param>
    public static async Task<EmbeddedFlashResult> ExecuteAsync(IToolchainRuntime runtime, WchLinkFlashPlan plan,
        Func<CancellationToken, Task<IUsbConnection>> openFresh, string privateParent, IProgress<string>? output = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var operation = new Operation(runtime, openFresh, privateParent, output);
        await operation.PreflightAsync(cancellationToken).ConfigureAwait(false);
        var elapsed = Stopwatch.StartNew();
        var directory = operation.CreateDirectory();
        try
        {
            var bytes = await File.ReadAllBytesAsync(plan.FirmwarePath, cancellationToken).ConfigureAwait(false);
            ValidateOriginal(plan, bytes);
            var padded = Enumerable.Repeat((byte)0xff, plan.ProgrammedLength).ToArray();
            bytes.CopyTo(padded, 0);
            var image = Path.Combine(directory, "application.bin");
            await File.WriteAllBytesAsync(image, padded, cancellationToken).ConfigureAwait(false);
            var discovery = Path.Combine(directory, "identify.bin");
            var identity = await operation.RunAsync(ReadMode, null, ["dump", "0x08000000", "4", "--out", discovery], plan.WorkingDirectory, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(discovery) || new FileInfo(discovery).Length != 4) throw new InvalidDataException("WCH-Link identification did not complete its guarded read.");
            var target = ParseTarget(identity);
            if (plan.ExpectedTarget != "CH32V003" && plan.ExpectedTarget != target.Part)
                throw new InvalidDataException("The attached WCH-Link target is " + target.Part + ", not the configured " + plan.ExpectedTarget + ". No firmware was written.");
            output?.Report("Guarded WCH-Link identified " + target.Part + " (full ChipID 0x" + target.ChipId.ToString("x8", CultureInfo.InvariantCulture) + ").");
            await ValidateFilesAsync().ConfigureAwait(false);
            await operation.RunAsync(FlashMode, target, ["flash", "--no-run", "--address", "0x08000000", image], plan.WorkingDirectory, cancellationToken).ConfigureAwait(false);
            await ValidateFilesAsync().ConfigureAwait(false);
            var readback = Path.Combine(directory, "readback.bin");
            await operation.RunAsync(ReadMode, target, ["dump", "0x08000000", padded.Length.ToString(CultureInfo.InvariantCulture), "--out", readback], plan.WorkingDirectory, cancellationToken).ConfigureAwait(false);
            var comparison = await FirmwareBackup.CompareAsync(image, readback, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!comparison.Identical) throw new InvalidDataException("WCH-Link flash readback differs from the complete padded image. The target was not reset or started.");
            await ValidateFilesAsync().ConfigureAwait(false);
            // A separate native mode permits only this reset, never flash/data.
            // It is reached only after byte comparison of the independent dump.
            await operation.RunAsync(ResetMode, target, ["reset", "quit"], plan.WorkingDirectory, cancellationToken).ConfigureAwait(false);
            return new(plan.FirmwareSha256, elapsed.Elapsed, 4);

            async Task ValidateFilesAsync()
            {
                ValidateOriginal(plan, await File.ReadAllBytesAsync(plan.FirmwarePath, cancellationToken).ConfigureAwait(false));
                var actual = await File.ReadAllBytesAsync(image, cancellationToken).ConfigureAwait(false);
                if (!actual.AsSpan().SequenceEqual(padded)) throw new InvalidDataException("The private WCH-Link image changed; further operations were refused.");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>Reads an explicit CH32V003 code-flash interval twice using fresh protected-read-only workers and atomically saves identical bytes.</summary>
    public static async Task<string> BackupAsync(IToolchainRuntime runtime, Func<CancellationToken, Task<IUsbConnection>> openFresh,
        string privateParent, string destination, uint start = FlashStart, int length = FlashBytes,
        IProgress<string>? output = null, CancellationToken cancellationToken = default)
    {
        if (start < FlashStart || start % 4 != 0 || length < 4 || length % 4 != 0 || start - FlashStart > FlashBytes || length > FlashBytes - (start - FlashStart))
            throw new ArgumentException("WCH-Link backups require a 4-byte aligned interval inside the actual 16 KiB CH32V003 code flash.");
        var operation = new Operation(runtime, openFresh, privateParent, output);
        await operation.PreflightAsync(cancellationToken).ConfigureAwait(false);
        var directory = operation.CreateDirectory();
        WchLinkTarget? target = null;
        try
        {
            return await FirmwareBackup.CreateAsync(destination, async (stream, token) =>
            {
                var file = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".bin");
                var text = await operation.RunAsync(ReadMode, target,
                    ["dump", "0x" + start.ToString("x8", CultureInfo.InvariantCulture), length.ToString(CultureInfo.InvariantCulture), "--out", file], directory, token).ConfigureAwait(false);
                var actual = ParseTarget(text);
                if (target is not null && target != actual) throw new InvalidDataException("The WCH-Link full chip ID changed between backup reads.");
                target = actual;
                if (!File.Exists(file) || new FileInfo(file).Length != length) throw new InvalidDataException("WCH-Link returned an incomplete flash dump.");
                await using var input = File.OpenRead(file);
                await input.CopyToAsync(stream, token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>Parses a single actual attach report and validates its package against the full chip ID.</summary>
    public static WchLinkTarget ParseTarget(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Length > 65536) throw new InvalidDataException("WCH-Link identity output exceeds its bound.");
        var matches = Attached.Matches(output);
        if (matches.Count != 1) throw new InvalidDataException("A single exact guarded CH32V003 attach report is required.");
        var match = matches[0];
        var id = uint.Parse(match.Groups["id"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var part = (id & 0xffffff0f) switch
        {
            0x00300500 => "CH32V003F4P6", 0x00310500 => "CH32V003F4U6",
            0x00320500 => "CH32V003A4M6", 0x00330500 => "CH32V003J4M6", _ => null,
        };
        if (part is null || part != match.Groups["part"].Value) throw new InvalidDataException("WCH-Link reported an unknown or inconsistent CH32V003 package ID.");
        return new(id, part);
    }

    private static bool KnownTarget(string value) => value.ToUpperInvariant() is "CH32V003" or "CH32V003F4P6" or "CH32V003F4U6" or "CH32V003A4M6" or "CH32V003J4M6";
    private static void ValidateOriginal(WchLinkFlashPlan plan, byte[] bytes)
    {
        if (bytes.Length != plan.OriginalLength || plan.ProgrammedLength != ((bytes.Length + 63) & ~63) ||
            Convert.ToHexStringLower(SHA256.HashData(bytes)) != plan.FirmwareSha256)
        {
            throw new InvalidDataException("The WCH-Link firmware changed after its application-flash review; no further operation was run.");
        }
    }

    private sealed class Operation
    {
        private readonly IToolchainRuntime _runtime;
        private readonly Func<CancellationToken, Task<IUsbConnection>> _open;
        private readonly string _parent;
        private readonly IProgress<string>? _output;
        private readonly HashSet<IUsbConnection> _used = new(ReferenceEqualityComparer.Instance);
        private string? _attachment;

        public Operation(IToolchainRuntime runtime, Func<CancellationToken, Task<IUsbConnection>> open, string parent, IProgress<string>? output)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(open);
            if (runtime.Kind != RuntimeKind.Linux) throw new NotSupportedException("The guarded WCH-Link route requires TabStudio's patched Android PRoot runtime.");
            if (!Path.IsPathFullyQualified(parent)) throw new ArgumentException("WCH-Link temporary storage must be an absolute private app directory.", nameof(parent));
            if (!runtime.HasCommand("wlink")) throw new InvalidOperationException("Install the native WCH-Link client from the Toolchain Store first.");
            _runtime = runtime; _open = open; _parent = parent; _output = output;
        }

        public string CreateDirectory()
        {
            var path = Path.Combine(_parent, "wch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public async Task PreflightAsync(CancellationToken token)
        {
            var result = await RunBoundedAsync(new ProcessStartSpec
            {
                Program = "/bin/false", Environment = new Dictionary<string, string?>
                {
                    ["TABSTUDIO_WCH_GUARD_CHECK"] = "1", ["TABSTUDIO_USB_WCH_GUARD"] = null,
                    ["TABSTUDIO_WCH_CHIP_ID"] = null, ["TABSTUDIO_USB_FD"] = null,
                },
            }, token).ConfigureAwait(false);
            if (result.Output.Trim() != GuardMarker) throw new NotSupportedException("This PRoot build did not prove the required WCH USB guard. No device was opened or programmed.");
        }

        public async Task<string> RunAsync(string mode, WchLinkTarget? target, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using var connection = await _open(token).ConfigureAwait(false);
            if (!_used.Add(connection)) throw new IOException("Each WCH-Link worker requires a fresh exclusive Android connection; a previous grant cannot be reused.");
            if (connection.Device.VendorId != 0x1a86 || connection.Device.ProductId != 0x8010 || connection.FileDescriptor < 3)
                throw new IOException("Select the actual permissioned WCH-Link RISC-V USB attachment (1a86:8010).");
            if (_attachment is not null && connection.Device.Id != _attachment) throw new IOException("The selected WCH-Link USB attachment changed. The operation was stopped.");
            _attachment = connection.Device.Id;
            // Descriptor/configuration reads happen before the native worker.
            // No Java USB transfer may share this OFD while the worker is alive.
            using var inventory = UsbfsDeviceInventory.Create(connection, _parent);
            var command = new ProcessStartSpec
            {
                Program = "wlink", Arguments = ["--chip", "CH32V003", "--no-detach", .. arguments], WorkingDirectory = workingDirectory,
                Environment = new Dictionary<string, string?>
                {
                    ["TABSTUDIO_WCH_GUARD_CHECK"] = null, ["TABSTUDIO_USB_WCH_GUARD"] = mode,
                    ["TABSTUDIO_WCH_CHIP_ID"] = target?.ChipId.ToString("x8", CultureInfo.InvariantCulture),
                },
            };
            var result = await RunBoundedAsync(UsbBridgeProcess.CreateUsbfs(connection, inventory, command), token).ConfigureAwait(false);
            // The connection is closed even after a successful worker. In
            // particular, a killed tracer's retained Android OFD is never reused.
            return result.Output;
        }

        private async Task<CommandResult> RunBoundedAsync(ProcessStartSpec spec, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            using var process = _runtime.Start(spec);
            process.CloseInput();
            var output = new StringBuilder();
            var gate = new Lock();
            async Task PumpAsync(Stream stream)
            {
                try
                {
                    using var reader = new StreamReader(stream, new UTF8Encoding(false, true), leaveOpen: true);
                    var chars = new char[2048];
                    while (await reader.ReadAsync(chars.AsMemory(), deadline.Token).ConfigureAwait(false) is var read && read > 0)
                    {
                        var text = new string(chars, 0, read);
                        lock (gate)
                        {
                            if (output.Length + read > 65536) throw new InvalidDataException("WCH-Link worker output exceeded 64 KiB; the operation was stopped.");
                            output.Append(text);
                        }
                        _output?.Report(text);
                    }
                }
                catch { process.Stop(); throw; }
            }
            var readers = Task.WhenAll(PumpAsync(process.Output), process.Error is { } error ? PumpAsync(error) : Task.CompletedTask);
            var exit = await process.WaitAsync(deadline.Token).ConfigureAwait(false);
            await readers.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested) throw new IOException("The WCH-Link worker timed out; its USB connection was closed.");
            if (!exit.Succeeded) throw new IOException("The guarded WCH-Link worker failed with exit code " + exit.ExitCode + "; its USB connection was closed and no subsequent step was run.");
            return new(exit, output.ToString());
        }
    }
}
