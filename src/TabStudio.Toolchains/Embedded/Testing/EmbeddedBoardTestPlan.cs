using System.Globalization;
using System.Security.Cryptography;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Testing;

/// <summary>Explicit board-test settings in the existing embedded-tests.json file.</summary>
public sealed record EmbeddedBoardTestOptions
{
    /// <summary>Unity RTT text, native decoded defmt-test, or probe-rs embedded-test's Rust-style report.</summary>
    public EmbeddedTestFormat Format { get; init; } = EmbeddedTestFormat.Unity;
    /// <summary>The actual board's user-assigned serial/identity, retained on every Test Explorer result.</summary>
    public required string BoardId { get; init; }
    /// <summary>Maximum time for flashing, verification and tests together, between 5 and 3,600 seconds.</summary>
    public int TimeoutSeconds { get; init; } = 120;
    /// <summary>The single RTT up-channel to consume, from 0 to 15.</summary>
    public int RttChannel { get; init; }
    /// <summary>Exact RTT channel name when a multi-channel firmware makes probe-rs prefix each decoded chunk.</summary>
    public string? RttChannelName { get; init; }
}

/// <summary>An immutable, hashed ELF snapshot approved against the actual application-flash map.</summary>
/// <remarks>
/// probe-rs 0.32 commit 48f5e4d53c690a1d40c2454033c6f785b4f4f95c cmd/run.rs always downloads the
/// ELF before running it; there is no no-flash option. The command explicitly verifies and restores bytes
/// outside the image in erased sectors, never requests chip erase, and carries no arbitrary extra flags.
/// Old defmt-test runs as a normal RTT program; embedded-test uses upstream's native Rust-style test mode.
/// </remarks>
public sealed class EmbeddedBoardTestPlan : IDisposable
{
    private readonly string _directory;
    private EmbeddedBoardTestPlan(string directory, string firmware, string sha256, EmbeddedBoardTestOptions options, ProcessStartSpec command)
    {
        _directory = directory;
        FirmwarePath = firmware;
        FirmwareSha256 = sha256;
        Options = options;
        Command = command;
    }

    /// <summary>The private ELF snapshot passed to the native tool.</summary>
    public string FirmwarePath { get; }
    /// <summary>SHA-256 of the exact bytes inspected before planning and checked again before invocation.</summary>
    public string FirmwareSha256 { get; }
    /// <summary>Harness and board identity.</summary>
    public EmbeddedBoardTestOptions Options { get; }
    /// <summary>The argument-safe upstream run command before adding the granted USB bridge.</summary>
    public ProcessStartSpec Command { get; }

    /// <summary>Validates both profiles, actual image ranges and harness metadata, then saves a private stable ELF.</summary>
    public static EmbeddedBoardTestPlan Create(EmbeddedBoardTestOptions options, EmbeddedFlashRequest flash,
        EmbeddedDebugProfile debug, string scratchDirectory, string? selectedProbe = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(flash);
        ArgumentNullException.ThrowIfNull(debug);
        debug.Validate();
        if (debug.Backend != EmbeddedDebugBackend.ProbeRs || flash.Protocol != EmbeddedFlashProtocol.ProbeRs)
        {
            throw new InvalidDataException("Board tests require explicit ProbeRs flash and debug profiles.");
        }
        if (!string.Equals(flash.Target, debug.Chip, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Flash Target and debug Chip must name the same actual probe-rs chip.");
        }
        if (options.Format is not (EmbeddedTestFormat.Unity or EmbeddedTestFormat.DefmtTest or EmbeddedTestFormat.Rust))
        {
            throw new InvalidDataException("Board tests support Unity RTT, decoded DefmtTest, and native embedded-test (Format Rust).");
        }
        if (string.IsNullOrWhiteSpace(options.BoardId) || options.BoardId.StartsWith("SELECT_", StringComparison.Ordinal)
            || options.BoardId.Length > 256 || options.BoardId.Any(char.IsControl)
            || options.TimeoutSeconds is < 5 or > 3600 || options.RttChannel is < 0 or > 15
            || options.RttChannelName is { } channel && (channel.Length is < 1 or > 128 || channel.Any(c => char.IsControl(c) || c is '[' or ']')))
        {
            throw new InvalidDataException("Set the actual BoardId, a 5–3600-second timeout and a valid RTT channel/name.");
        }
        var firmware = Path.GetFullPath(flash.FirmwarePath, flash.WorkingDirectory);
        if (!string.Equals(firmware, debug.ResolveFirmware(flash.WorkingDirectory), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("Flash and debug profiles must select the same matching test ELF.");
        }
        if (new FileInfo(firmware).Length is <= 0 or > 128 * 1024 * 1024)
        {
            throw new InvalidDataException("Test firmware must contain 1 byte..128 MiB.");
        }
        var bytes = File.ReadAllBytes(firmware);
        FlashSafety.ValidateFirmware(FirmwareImageInspector.Inspect(bytes, ".elf"), flash.ApplicationFlash);
        EmbeddedConfigurationGuard.Validate(flash, bytes);
        var report = MemoryAnalyzer.AnalyzeElf(bytes);
        ulong? rtt = null;
        if (options.Format == EmbeddedTestFormat.Rust)
        {
            EmbeddedTestElfMetadata.RequireVersion(bytes);
        }
        else
        {
            rtt = ProbeRsLogPlan.ControlBlockAddress(report);
        }
        var selector = debug.ProbeSelector ?? selectedProbe;
        if (string.IsNullOrWhiteSpace(selector) || selector.Length > 256
            || selector.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is ':' or '-' or '_' or '.')))
        {
            throw new InvalidDataException("Select the exact probe before flashing board tests.");
        }
        if (flash.ProbeSelector is { } configured && !string.Equals(configured, selector, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Flash and debug profiles must select the same probe.");
        }
        Directory.CreateDirectory(scratchDirectory);
        var directory = Path.Combine(scratchDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var snapshot = Path.Combine(directory, "board-tests.elf");
        try
        {
            using (var output = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None)) output.Write(bytes);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(snapshot, UnixFileMode.UserRead);
            var arguments = new List<string>
            {
                "run", "--chip", debug.Chip!, "--probe", selector, "--non-interactive",
                "--binary-format", "elf", "--preverify", "--verify", "--restore-unwritten", "--disable-progressbars",
                "--no-catch-reset", "--no-catch-hardfault", "--no-catch-svc", "--no-catch-hlt",
                "--no-location", "--no-timestamps", "--log-format", "{m}",
                "--rtt-channel-mode", "block-if-full", "--rtt-up-channels", options.RttChannel.ToString(CultureInfo.InvariantCulture),
            };
            if (rtt is { } address) arguments.AddRange(["--scan-region", "0x" + address.ToString("x", CultureInfo.InvariantCulture)]);
            if (options.Format == EmbeddedTestFormat.Rust) arguments.AddRange(["--format", "pretty"]);
            arguments.Add("--");
            arguments.Add(snapshot);
            var command = new ProcessStartSpec
            {
                Program = "probe-rs", Arguments = arguments, WorkingDirectory = flash.WorkingDirectory,
                Environment = ProbeTargetInfoReader.SafeAttachEnvironment(),
            };
            return new(directory, snapshot, Convert.ToHexString(SHA256.HashData(bytes)), options, command);
        }
        catch
        {
            Directory.Delete(directory, true);
            throw;
        }
    }

    /// <summary>Refuses a snapshot modified after range validation, before any native tool is started.</summary>
    public void VerifySnapshot()
    {
        using var source = File.OpenRead(FirmwarePath);
        if (source.Length > 128 * 1024 * 1024 || !string.Equals(Convert.ToHexString(SHA256.HashData(source)), FirmwareSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The validated test ELF changed. No board-test command was started.");
        }
    }

    /// <summary>Deletes this plan's private snapshot after its native process has ended.</summary>
    public void Dispose()
    {
        try { Directory.Delete(_directory, true); }
        catch (DirectoryNotFoundException) { }
    }
}
