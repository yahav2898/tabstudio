using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Runtimes;

// This is a native Linux host proof of the production tablet installer/runtime, never a hardware test.
// Only the primary installer is a host provider: the embedded guest, downloader, setup steps, USB shim
// compilation, readiness markers and every frontend command use production implementations unchanged.
if (args.Length != 4)
{
    throw new ArgumentException("Supply the pinned PRoot binary, its loader, a fresh private work directory and the evidence output directory.");
}

var proot = Path.GetFullPath(args[0]);
var loader = Path.GetFullPath(args[1]);
var work = Path.GetFullPath(args[2]);
var output = Path.GetFullPath(args[3]);
Directory.CreateDirectory(work);
Directory.CreateDirectory(output);
var started = Stopwatch.StartNew();
var evidence = new Dictionary<string, object?>
{
    ["architecture"] = ToolchainPlatform.CurrentArchitecture,
    ["hardwareVerified"] = false,
    ["androidVerified"] = false,
    ["runtime"] = "Production EmbeddedGlibcToolchainInstaller / PRoot on a native Linux CPU",
};
var events = new EvidenceProgress(Path.Combine(output, "installation.log"));
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(35));
var cancellation = deadline.Token;
try
{
    if (FlutterToolchainInstaller.RootImage(ToolchainPlatform.CurrentArchitecture) is not { } image)
    {
        throw new InvalidOperationException("This proof requires a native ARM64 or x64 runner.");
    }

    evidence["ubuntuImage"] = image.Address.AbsoluteUri;
    evidence["ubuntuImageSha256"] = image.Sha256;
    var native = Path.Combine(work, "native");
    Directory.CreateDirectory(native);
    foreach (var (source, name) in new[] { (proot, "libproot.so"), (loader, "libproot-loader.so") })
    {
        var target = Path.Combine(native, name);
        if (!File.Exists(target))
        {
            File.CreateSymbolicLink(target, source);
        }
    }

    var project = Path.Combine(work, "uno-blink");
    Directory.CreateDirectory(Path.Combine(project, "src"));
    var platform = new ToolchainPlatform
    {
        NativeLibraryDirectory = native,
        Architecture = ToolchainPlatform.CurrentArchitecture,
        AndroidApiLevel = 35,
        SharedFolders = [project],
        DnsServers = ReadDns,
    };
    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("TabStudio-Embedded-Glibc-Verification/1.0");
    using var installer = new EmbeddedGlibcToolchainInstaller(new HostToolchainInstaller(new HostRuntime()),
        Path.Combine(work, "sdk"), platform, new Downloader(http), [project]);
    var definitions = ToolchainCatalog.Default.Toolchains.Where(d => d.Id is "platformio" or "arduino" or "probe-rs").ToArray();
    if (definitions.Length != 3)
    {
        throw new InvalidOperationException("The production catalog must contain PlatformIO, Arduino and probe-rs.");
    }
    _ = await installer.ReadStatusAsync(definitions, false, cancellation);
    foreach (var definition in definitions)
    {
        await installer.InstallAsync(definition, events, cancellation);
        if (!installer.IsToolchainReady(definition.Id))
        {
            throw new InvalidOperationException("The production readiness check failed: " + definition.Id);
        }
    }

    var statuses = await installer.ReadStatusAsync(definitions, false, cancellation);
    if (statuses.Any(s => s.State != ToolchainState.Installed))
    {
        throw new InvalidOperationException("The Store did not report every frontend Installed after its real validation.");
    }

    evidence["frontends"] = statuses.Select(s => new { id = s.Toolchain.Id, version = s.Version, state = s.State.ToString() }).ToArray();
    var privacy = await RunAsync(installer.Runtime, new ProcessStartSpec
    {
        Program = "/opt/tabstudio/platformio/bin/python",
        Arguments = ["-P", "-c", "from platformio.app import get_setting; assert get_setting('enable_telemetry') is False; print('PLATFORMIO_TELEMETRY_DISABLED')"],
    }, "platformio-policy.log");
    if (!privacy.Output.Contains("PLATFORMIO_TELEMETRY_DISABLED", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("PlatformIO did not confirm telemetry was disabled.");
    }

    // This second run checks the public runtime routing path, in addition to the installer's direct guest
    // self-test. Both must load the glibc versions of the exact same native shim sources.
    await ProbeRsDapCheck.VerifyAsync(installer.Runtime, project, Path.Combine(output, "probe-rs-dap-initialize.json"), cancellation);
    evidence["probeRsDapInitialized"] = true;
    var bridgeSpec = UsbBridgeProcess.CreateVirtualSelfTest();
    var bridge = await RunAsync(installer.Runtime, bridgeSpec with
    {
        Environment = new Dictionary<string, string?>(bridgeSpec.Environment) { [EmbeddedGlibcRuntime.RoutingMarker] = "1" }
    }, "glibc-serial-bridge.log");
    if (!bridge.Output.Contains("EMBEDDED_BRIDGE_PASSED", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The glibc POSIX serial/PTY proof did not complete.");
    }

    await File.WriteAllTextAsync(Path.Combine(project, "platformio.ini"), """
        [env:uno]
        platform = atmelavr@5.3.0
        board = uno
        framework = arduino
        """ + "\n", cancellation);
    await File.WriteAllTextAsync(Path.Combine(project, "src", "main.cpp"), """
        #include <Arduino.h>
        void setup() { pinMode(LED_BUILTIN, OUTPUT); }
        void loop() { digitalWrite(LED_BUILTIN, HIGH); delay(100); digitalWrite(LED_BUILTIN, LOW); delay(100); }
        """ + "\n", cancellation);
    var build = await RunAsync(installer.Runtime, new ProcessStartSpec { Program = "pio", Arguments = ["run", "--environment", "uno"], WorkingDirectory = project }, "platformio-build.log");
    var pio = definitions.Single(d => d.Id == "platformio");
    var errors = new ProblemMatcher(pio).FindErrors(build.Output.Split('\n'), _ => true);
    if (errors.Count != 0)
    {
        throw new InvalidOperationException("The production Error List parser found " + errors.Count + " compiler errors.");
    }

    var firmware = Path.Combine(project, ".pio", "build", "uno", "firmware.elf");
    var bytes = await File.ReadAllBytesAsync(firmware, cancellation);
    if (bytes.Length < 4 || !bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }))
    {
        throw new InvalidOperationException("The build did not produce an ELF firmware image.");
    }

    await File.WriteAllBytesAsync(Path.Combine(output, "uno-blink.elf"), bytes, cancellation);
    evidence["firmwareBytes"] = bytes.Length;
    evidence["firmwareSha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
    evidence["compilerErrors"] = errors.Count;
    evidence["passed"] = true;
    Console.WriteLine("EMBEDDED_GLIBC_FRONTENDS_PASSED " + ToolchainPlatform.CurrentArchitecture);
    return 0;
}
catch (Exception failure)
{
    evidence["passed"] = false;
    evidence["error"] = failure.ToString();
    Console.Error.WriteLine(failure);
    return 1;
}
finally
{
    evidence["seconds"] = started.Elapsed.TotalSeconds;
    await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }), CancellationToken.None);
    events.Dispose();
}

async Task<CommandResult> RunAsync(IToolchainRuntime runtime, ProcessStartSpec command, string logName)
{
    Console.WriteLine("+ " + command.CommandLine);
    var result = await CommandRunner.RunAsync(runtime, command, Console.WriteLine, cancellation);
    await File.WriteAllTextAsync(Path.Combine(output, logName), result.Output, CancellationToken.None);
    if (!result.Succeeded)
    {
        throw new InvalidOperationException(command.Program + " failed: " + result.Tail(12));
    }
    return result;
}

static IReadOnlyList<string> ReadDns() => File.ReadLines("/etc/resolv.conf")
    .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
    .Where(parts => parts.Length >= 2 && parts[0] == "nameserver")
    .Select(parts => parts[1]).Distinct(StringComparer.Ordinal).ToArray();

sealed class EvidenceProgress(string path) : IProgress<InstallProgress>, IDisposable
{
    private readonly StreamWriter _writer = new(path) { AutoFlush = true };
    private readonly Lock _gate = new();

    public void Report(InstallProgress value)
    {
        lock (_gate)
        {
            var message = value.Stage + ": " + value.Message;
            _writer.WriteLine(message);
            Console.WriteLine(message);
        }
    }

    public void Dispose() => _writer.Dispose();
}
