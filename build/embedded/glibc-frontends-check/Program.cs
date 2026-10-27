using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Runtimes;

// This is a native Linux host proof of the production tablet installer/runtime, never a hardware test.
// Only the primary installer is a host provider: the embedded guest, downloader, setup steps, USB shim
// compilation, readiness markers and every frontend command use production implementations unchanged.
if (args.Length is not (4 or 5))
{
    throw new ArgumentException("Supply the pinned PRoot binary, its loader, a fresh private work directory, evidence directory and optional SDK identifier.");
}

var sdkId = args.Length == 5 ? args[4] : null;
var nordic = sdkId == EmbeddedNordicInstallation.Id;
if (sdkId is not null && !nordic && EmbeddedSdkBootstrap.Recipes.Get(sdkId) is null)
{
    throw new ArgumentException("The requested SDK has no production installation recipe: " + sdkId);
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
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(sdkId is null ? 35 : sdkId == "esp-idf" ? 85 : 50));
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
    var definitions = ToolchainCatalog.Default.Toolchains.Where(d => sdkId is null ? d.Id is "platformio" or "arduino" or "probe-rs" : d.Id == sdkId).ToArray();
    if (definitions.Length != (sdkId is null ? 3 : 1))
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
    await BuildToolCheck.VerifyAsync(installer.Runtime, project, output, cancellation);
    evidence["nativeBuildToolsVerified"] = true;
    if (nordic)
    {
        await NordicDfuCheck.VerifyAsync(installer.Runtime, project, output, cancellation);
        evidence["legacyNordicDfuVerified"] = true;
        evidence["passed"] = true;
        Console.WriteLine("EMBEDDED_GLIBC_NORDIC_PASSED " + ToolchainPlatform.CurrentArchitecture);
        return 0;
    }
    if (sdkId is not null)
    {
        evidence["sdk"] = sdkId;
        evidence["sourceRevision"] = EmbeddedSdkBootstrap.Recipes.Get(sdkId)!.Revision;
        await BuildSdkAsync(installer.Runtime, definitions[0], project);
        evidence["passed"] = true;
        Console.WriteLine("EMBEDDED_GLIBC_SDK_PASSED " + sdkId + " " + ToolchainPlatform.CurrentArchitecture);
        return 0;
    }
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
    await Box64Check.VerifyAsync(installer, project, output, cancellation);
    evidence["box64CompilerAndChildDescriptors"] = true;
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
    if (sdkId is not null && (command.Program is "cargo" or "cmake" or "west" or "idf.py")
        && !EmbeddedGlibcRuntime.UsesGlibc(command))
    {
        throw new InvalidOperationException("The public runtime did not route this SDK build to its installed glibc environment: " + command.CommandLine);
    }
    Console.WriteLine("+ " + command.CommandLine);
    var result = await CommandRunner.RunAsync(runtime, command, Console.WriteLine, cancellation);
    await File.WriteAllTextAsync(Path.Combine(output, logName), result.Output, CancellationToken.None);
    if (!result.Succeeded)
    {
        throw new InvalidOperationException(command.Program + " failed: " + result.Tail(12));
    }
    return result;
}

async Task BuildSdkAsync(IToolchainRuntime runtime, ToolchainDefinition definition, string project)
{
    string artifact;
    CommandResult build;
    switch (definition.Id)
    {
        case "pico-sdk":
            await File.WriteAllTextAsync(Path.Combine(project, "CMakeLists.txt"), """
                cmake_minimum_required(VERSION 3.20)
                include($ENV{PICO_SDK_PATH}/external/pico_sdk_import.cmake)
                project(tabstudio_sdk_check C CXX ASM)
                pico_sdk_init()
                add_executable(tabstudio_sdk_check src/main.c)
                target_link_libraries(tabstudio_sdk_check pico_stdlib)
                pico_add_extra_outputs(tabstudio_sdk_check)
                """ + "\n", cancellation);
            await File.WriteAllTextAsync(Path.Combine(project, "src/main.c"), """
                #include "pico/stdlib.h"
                int main(void) { gpio_init(PICO_DEFAULT_LED_PIN); gpio_set_dir(PICO_DEFAULT_LED_PIN, GPIO_OUT); gpio_put(PICO_DEFAULT_LED_PIN, 1); return 0; }
                """ + "\n", cancellation);
            _ = await RunAsync(runtime, new() { Program = "cmake", Arguments = ["-S", ".", "-B", "build", "-G", "Ninja", "-DPICO_BOARD=pico", "-DPICO_NO_PICOTOOL=1", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], WorkingDirectory = project }, "sdk-configure.log");
            build = await RunAsync(runtime, new() { Program = "cmake", Arguments = ["--build", "build", "--parallel", "2"], WorkingDirectory = project }, "sdk-build.log");
            artifact = Path.Combine(project, "build/tabstudio_sdk_check.elf");
            break;
        case "zephyr":
            await File.WriteAllTextAsync(Path.Combine(project, "CMakeLists.txt"), """
                cmake_minimum_required(VERSION 3.20)
                find_package(Zephyr REQUIRED HINTS $ENV{ZEPHYR_BASE})
                project(tabstudio_sdk_check)
                target_sources(app PRIVATE src/main.c)
                """ + "\n", cancellation);
            await File.WriteAllTextAsync(Path.Combine(project, "prj.conf"), "CONFIG_PRINTK=y\n", cancellation);
            await File.WriteAllTextAsync(Path.Combine(project, "src/main.c"), "#include <zephyr/kernel.h>\nint main(void) { printk(\"TabStudio SDK check\\n\"); return 0; }\n", cancellation);
            build = await RunAsync(runtime, new() { Program = "west", Arguments = ["build", "--build-opt=-j2", "-b", "qemu_cortex_m3", "-d", "build", "."], WorkingDirectory = project }, "sdk-build.log");
            artifact = Path.Combine(project, "build/zephyr/zephyr.elf");
            break;
        case "esp-idf":
            Directory.CreateDirectory(Path.Combine(project, "main"));
            await File.WriteAllTextAsync(Path.Combine(project, "CMakeLists.txt"), """
                cmake_minimum_required(VERSION 3.16)
                include($ENV{IDF_PATH}/tools/cmake/project.cmake)
                project(tabstudio_sdk_check)
                """ + "\n", cancellation);
            await File.WriteAllTextAsync(Path.Combine(project, "main/CMakeLists.txt"), "idf_component_register(SRCS \"main.c\" INCLUDE_DIRS \".\")\n", cancellation);
            await File.WriteAllTextAsync(Path.Combine(project, "main/main.c"), "#include <stdio.h>\nvoid app_main(void) { puts(\"TabStudio SDK check\"); }\n", cancellation);
            _ = await RunAsync(runtime, new() { Program = "idf.py", Arguments = ["set-target", "esp32c3"], WorkingDirectory = project }, "sdk-configure.log");
            build = await RunAsync(runtime, new() { Program = "idf.py", Arguments = ["build"], WorkingDirectory = project }, "sdk-build.log");
            artifact = Path.Combine(project, "build/tabstudio_sdk_check.elf");
            var flashSet = EspFlashManifestReader.Read(Path.Combine(project, "build/flasher_args.json"), "esp32c3");
            if (flashSet.Images.Count < 3 || flashSet.Inputs.Count <= flashSet.Images.Count)
            {
                throw new InvalidOperationException("The real ESP-IDF build did not produce a validated bootloader, partition table, application and bound metadata.");
            }
            evidence["flashManifest"] = new
            {
                chip = flashSet.Chip,
                inputs = flashSet.Inputs.Count,
                images = flashSet.Images.Select(image => new { address = image.Address, bytes = image.Length, sha256 = image.Sha256 }).ToArray(),
            };
            Console.WriteLine("ESP_BUILD_FLASH_MANIFEST_PASSED " + flashSet.Chip + " " + flashSet.Images.Count);
            break;
        case "embedded-rust":
            const string embassyRevision = "87b634a63df1ad49ad9f0b45173f1667f6a51c28";
            var repository = Path.Combine(project, "embassy");
            Directory.CreateDirectory(repository);
            foreach (var arguments in new IReadOnlyList<string>[]
            {
                ["-C", repository, "init", "-q"],
                ["-C", repository, "remote", "add", "origin", "https://github.com/embassy-rs/embassy.git"],
                ["-C", repository, "fetch", "--depth", "1", "--no-tags", "origin", embassyRevision],
                ["-C", repository, "checkout", "--detach", "FETCH_HEAD"],
            })
            {
                _ = await RunAsync(runtime, new() { Program = "git", Arguments = arguments, WorkingDirectory = project }, "embassy-checkout-" + arguments[2] + ".log");
            }
            var head = await RunAsync(runtime, new() { Program = "git", Arguments = ["-C", repository, "rev-parse", "HEAD"], WorkingDirectory = project }, "embassy-revision.log");
            if (!head.Output.Split('\n').Any(line => line.Trim() == embassyRevision))
            {
                throw new InvalidOperationException("The Embassy source checkout did not match the pinned revision.");
            }
            var example = Path.Combine(repository, "examples/rp");
            var targetDirectory = Path.Combine(project, "target");
            build = await RunAsync(runtime, new()
            {
                Program = "cargo",
                Arguments = ["build", "--manifest-path", Path.Combine(example, "Cargo.toml"), "--target", "thumbv6m-none-eabi", "--bin", "blinky", "--release", "--jobs", "2"],
                WorkingDirectory = example,
                Environment = new Dictionary<string, string?> { ["CARGO_TARGET_DIR"] = targetDirectory, ["CARGO_TERM_COLOR"] = "never" },
            }, "sdk-build.log");
            artifact = Path.Combine(targetDirectory, "thumbv6m-none-eabi/release/blinky");
            evidence["firmwareSourceRevision"] = embassyRevision;
            var cargoLock = Path.Combine(example, "Cargo.lock");
            if (File.Exists(cargoLock))
            {
                File.Copy(cargoLock, Path.Combine(output, "Cargo.lock"));
            }
            break;
        default:
            throw new InvalidOperationException("No real firmware proof is defined for " + definition.Id);
    }

    var errors = new ProblemMatcher(definition).FindErrors(build.Output.Split('\n'), _ => true);
    if (errors.Count != 0)
    {
        throw new InvalidOperationException("The production Error List parser found " + errors.Count + " SDK compiler errors.");
    }
    var bytes = await File.ReadAllBytesAsync(artifact, cancellation);
    if (bytes.Length < 4 || !bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }))
    {
        throw new InvalidOperationException("The SDK did not build an ELF firmware image.");
    }
    await File.WriteAllBytesAsync(Path.Combine(output, definition.Id + ".elf"), bytes, cancellation);
    evidence["firmwareBytes"] = bytes.Length;
    evidence["firmwareSha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
    evidence["compilerErrors"] = errors.Count;
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
