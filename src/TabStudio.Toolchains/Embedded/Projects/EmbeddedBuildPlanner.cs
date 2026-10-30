using System.Globalization;
using System.Text.Json;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Embedded.Projects;

/// <summary>Delegates builds to each project's native build system and keeps board and upload choices explicit.</summary>
public static class EmbeddedBuildPlanner
{
    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
    /// <summary>The workspace-local configuration file; existing native configuration is never rewritten.</summary>
    public const string OptionsFile = ".tabstudio/embedded.json";

    /// <summary>Resolves the Toolchain Store entry for a project system.</summary>
    /// <param name="kind">The project system.</param>
    public static string ToolchainId(EmbeddedProjectKind kind) => kind switch
    {
        EmbeddedProjectKind.PlatformIO => "platformio",
        EmbeddedProjectKind.Arduino => "arduino",
        EmbeddedProjectKind.EspIdf => "esp-idf",
        EmbeddedProjectKind.Stm32Cube => "embedded-arm",
        EmbeddedProjectKind.Zephyr => "zephyr",
        EmbeddedProjectKind.PicoSdk => "pico-sdk",
        EmbeddedProjectKind.Rust => "embedded-rust",
        EmbeddedProjectKind.MicroPython => "micropython",
        EmbeddedProjectKind.GccCMakeArm => "embedded-arm",
        EmbeddedProjectKind.GccCMakeAvr => "embedded-avr",
        EmbeddedProjectKind.Ch32Fun => EmbeddedCh32FunInstallation.ToolchainId,
        _ => "embedded-import",
    };

    /// <summary>Reads explicit project choices. Malformed or symbolic-link configuration fails visibly.</summary>
    /// <param name="directory">The project directory.</param>
    public static EmbeddedBuildOptions LoadOptions(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var path = Path.Combine(directory, OptionsFile);
        if (!File.Exists(path))
        {
            return new EmbeddedBuildOptions();
        }
        var info = new FileInfo(path);
        if (info.Length > 64 * 1024 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Embedded configuration must be a regular JSON file smaller than 64 KB.");
        }
        return JsonSerializer.Deserialize<EmbeddedBuildOptions>(File.ReadAllText(path), OptionsJson)
            ?? throw new JsonException("Embedded configuration must be a JSON object.");
    }

    /// <summary>Creates safe argument-vector commands. Missing selections return notes and no upload command.</summary>
    /// <param name="project">The detected project.</param>
    /// <param name="options">Explicit choices, or the workspace-local options file.</param>
    public static EmbeddedBuildPlan Plan(EmbeddedProject project, EmbeddedBuildOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        options ??= LoadOptions(project.Directory);
        if (options.HostPackageMode is not ("auto" or "native" or "boxed-x86_64"))
            throw new ArgumentException("HostPackageMode must be auto, native or boxed-x86_64.", nameof(options));
        if (options.HostPackageMode == "boxed-x86_64" && project.Kind != EmbeddedProjectKind.PlatformIO)
            throw new ArgumentException("Box64 host-package mode currently applies only to PlatformIO.", nameof(options));
        ValidateToken(options.Board, nameof(options.Board));
        ValidateToken(options.Environment, nameof(options.Environment));
        ValidateToken(options.Target, nameof(options.Target));
        ValidateToken(options.Port, nameof(options.Port));
        ValidateToken(options.Chip, nameof(options.Chip));
        ValidateToken(options.Runner, nameof(options.Runner));
        var preparation = new List<ProcessStartSpec>();
        var build = new List<ProcessStartSpec>();
        var upload = new List<ProcessStartSpec>();
        var notes = new List<string>(project.Notes);
        var buildDirectory = Path.Combine(project.Directory, ".tabstudio", "embedded", "build");
        var jobs = Math.Clamp(options.Jobs, 1, 16).ToString(CultureInfo.InvariantCulture);
        string? compilationDatabase = null;

        ProcessStartSpec Command(string program, params string[] arguments) => new()
        {
            Program = program, Arguments = arguments, WorkingDirectory = project.Directory,
            Environment = project.Kind is EmbeddedProjectKind.EspIdf or EmbeddedProjectKind.Zephyr or EmbeddedProjectKind.PicoSdk
                ? new Dictionary<string, string?> { [EmbeddedSdkBootstrap.JobsVariable] = jobs }
                : new Dictionary<string, string?>(),
        };

        switch (project.Kind)
        {
            case EmbeddedProjectKind.Ch32Fun:
                preparation.Add(Command("mkdir", "-p", buildDirectory));
                build.Add(Command("make", "-j" + jobs, "build", "GENERATED_LD_FILE=" + Path.Combine(buildDirectory, "ch32v003.ld")));
                notes.Add("UART output uses PD5 TX at 115200 baud. Read the starter's upload prerequisites; no option-byte or protection update is generated.");
                break;
            case EmbeddedProjectKind.PlatformIO:
            {
                var environment = options.Environment ?? project.DefaultEnvironment
                    ?? (!project.HasNativeEnvironmentSelection && project.Environments.Count == 1 ? project.Environments[0].Name : null);
                if (environment is not null)
                {
                    ValidateToken(environment, "environment");
                    if (project.Environments.Count > 0 && !project.Environments.Any(e => e.Name == environment))
                    {
                        notes.Add($"PlatformIO environment '{environment}' is not declared locally; Core resolves extra_configs and reports missing environments.");
                    }
                }
                var selection = environment is null ? Array.Empty<string>() : new[] { "--environment", environment };
                ProcessStartSpec PlatformIo(params string[] arguments)
                {
                    var command = Command("pio", arguments);
                    return command with
                    {
                        Environment = new Dictionary<string, string?> { [EmbeddedBox64Installation.Marker] = options.HostPackageMode == "native" ? "native" : "1" },
                    };
                }
                build.Add(PlatformIo(["run", .. selection, "--jobs", jobs]));
                if (environment is not null && options.Port is not null)
                {
                    upload.Add(PlatformIo(["run", .. selection, "--target", "upload", "--upload-port", options.Port]));
                }
                else
                {
                    notes.Add("Select one PlatformIO environment and a granted upload port before uploading.");
                }
                compilationDatabase = Path.Combine(project.Directory, "compile_commands.json");
                if (!File.Exists(compilationDatabase))
                {
                    // Run only after the native build succeeds, using Core's real
                    // SCons graph and the same environment/default selection.
                    // Existing user databases and extra_scripts remain untouched.
                    build.Add(PlatformIo(["run", .. selection, "--target", "compiledb", "--jobs", jobs]));
                    notes.Add("After the first successful build, PlatformIO generates its native compilation database for clangd. A custom COMPILATIONDB_PATH remains owned by the project's scripts.");
                }
                else
                {
                    notes.Add("The existing compile_commands.json is preserved. Regenerate it with the project's own build configuration when compiler flags change.");
                }
                notes.Add("PlatformIO owns its package versions and SHA-256 verification. Host executables are checked against their actual ELF architecture before package scripts or builds run.");
                if (options.HostPackageMode != "native")
                    notes.Add("Native ARM64 packages are preferred automatically. Missing or mislabeled native artifacts may use unchanged x64 binaries through the APK's pinned Box64 translator in a private project package cache. Translation is slower; hostPackageMode=native disables it.");
                break;
            }
            case EmbeddedProjectKind.Arduino:
            {
                var sketchDirectory = project.Directory;
                var library = project.Marker == "library.properties";
                if (library)
                {
                    if (options.Sketch is null)
                    {
                        notes.Add("Set Sketch to a specific example directory inside the library; libraries are built through their examples.");
                        break;
                    }
                    sketchDirectory = ArduinoExampleDirectory(project.Directory, options.Sketch);
                }
                if (options.Board is not { } fqbn)
                {
                    notes.Add("Set board to the exact Arduino fully qualified board name (for example arduino:avr:uno).");
                    break;
                }
                var libraryArgs = library ? new[] { "--library", project.Directory } : Array.Empty<string>();
                string[] compilationArgs = ["compile", "--fqbn", fqbn, "--jobs", jobs, "--build-path", buildDirectory, .. libraryArgs, sketchDirectory];
                preparation.Add(Command("arduino-cli", [.. compilationArgs[..^1], "--only-compilation-database", sketchDirectory]));
                build.Add(Command("arduino-cli", compilationArgs));
                compilationDatabase = Path.Combine(buildDirectory, "compile_commands.json");
                if (options.Port is not null)
                {
                    upload.Add(Command("arduino-cli", "upload", "--fqbn", fqbn, "--port", options.Port,
                        "--input-dir", buildDirectory, sketchDirectory));
                }
                else
                {
                    notes.Add("Select a granted upload port. Leonardo/Micro/SAMD also require the serial bridge's bootloader re-enumeration flow.");
                }
                break;
            }
            case EmbeddedProjectKind.EspIdf:
            {
                var target = options.Target ?? project.Target;
                var selection = target is null ? Array.Empty<string>() : new[] { "-DIDF_TARGET=" + target };
                build.Add(Command("idf.py", ["-B", buildDirectory, .. selection, "build"]));
                compilationDatabase = Path.Combine(buildDirectory, "compile_commands.json");
                if (options.Port is not null)
                {
                    upload.Add(Command("idf.py", "-B", buildDirectory, "--port", options.Port, "flash"));
                }
                else
                {
                    notes.Add("Select the ESP board's granted serial port before upload; efuse commands are never generated.");
                }
                notes.Add("Use an installed ESP-IDF environment with its matching target tools and Python packages.");
                break;
            }
            case EmbeddedProjectKind.Zephyr:
            {
                var board = options.Board ?? project.Target;
                if (board is null)
                {
                    notes.Add("Set board to an installed west board identifier before building.");
                    break;
                }
                build.Add(Command("west", "build", "--pristine=auto", "--build-opt=-j" + jobs, "--build-dir", buildDirectory, "--board", board,
                    project.Directory, "--", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"));
                compilationDatabase = Path.Combine(buildDirectory, "compile_commands.json");
                if (options.Port is not null || options.Runner is not null)
                {
                    var runner = options.Runner is null ? Array.Empty<string>() : new[] { "--runner", options.Runner };
                    upload.Add(Command("west", ["flash", "--build-dir", buildDirectory, .. runner]));
                    notes.Add("west's selected runner must receive the granted USB/serial bridge environment from the device service.");
                }
                else
                {
                    notes.Add("Select a runner and granted device before west flash.");
                }
                break;
            }
            case EmbeddedProjectKind.PicoSdk:
            {
                var board = options.Board ?? project.Target;
                if (board is null)
                {
                    notes.Add("Pico SDK selects the board from the project's native configuration/defaults. Select an explicit matching board before upload.");
                }
                var boardArgs = board is null ? Array.Empty<string>() : new[] { "-DPICO_BOARD=" + board };
                if (CMakeConfiguration(project.Directory, buildDirectory, options.Environment,
                    [.. boardArgs, "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], notes) is not { } picoConfigure)
                {
                    break;
                }
                preparation.Add(picoConfigure);
                build.Add(Command("cmake", "--build", buildDirectory, "--parallel", jobs));
                compilationDatabase = Path.Combine(buildDirectory, "compile_commands.json");
                if (options.Firmware is not null && options.Port is not null)
                {
                    upload.Add(Command("picotool", "load", "-v", "-x", FirmwarePath(project.Directory, options.Firmware)));
                    notes.Add("picotool receives the granted BOOTSEL device through the USB bridge; port selects that device, not a kernel USB path.");
                }
                else
                {
                    notes.Add("Select the built UF2/ELF image and a granted BOOTSEL device for upload.");
                }
                break;
            }
            case EmbeddedProjectKind.Stm32Cube:
            case EmbeddedProjectKind.GccCMakeArm:
            case EmbeddedProjectKind.GccCMakeAvr:
            {
                if (File.Exists(Path.Combine(project.Directory, "CMakeLists.txt")))
                {
                    if (CMakeConfiguration(project.Directory, buildDirectory, options.Environment,
                        ["-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], notes) is not { } configure)
                    {
                        break;
                    }
                    preparation.Add(configure);
                    build.Add(Command("cmake", "--build", buildDirectory, "--parallel", jobs));
                    compilationDatabase = Path.Combine(buildDirectory, "compile_commands.json");
                }
                else if (File.Exists(Path.Combine(project.Directory, "Makefile")))
                {
                    build.Add(Command("make", "-j" + jobs));
                }
                notes.Add("The generated build retains its linker script, CPU flags and startup. Select a chip/probe upload profile separately; an MCU family never implies a safe flash address.");
                break;
            }
            case EmbeddedProjectKind.Rust:
            {
                var target = options.Target ?? project.Target;
                var selection = target is null ? Array.Empty<string>() : new[] { "--target", target };
                var locked = File.Exists(Path.Combine(project.Directory, "Cargo.lock")) ? new[] { "--locked" } : Array.Empty<string>();
                build.Add(Command("cargo", ["build", .. locked, .. selection, "--message-format=json", "--jobs", jobs]));
                if (options.Chip is not null && options.Firmware is not null && options.Port is not null)
                {
                    upload.Add(Command("probe-rs", "download", "--chip", options.Chip, FirmwarePath(project.Directory, options.Firmware)));
                    notes.Add("probe-rs receives the granted probe through the verified USBFS descriptor bridge; the device service must wrap this command before execution.");
                }
                else
                {
                    notes.Add("Select the exact chip, ELF image and granted probe for upload. Existing Cargo runner configuration is preserved.");
                }
                break;
            }
            case EmbeddedProjectKind.MicroPython:
            {
                if (options.Port is null)
                {
                    notes.Add("Select the board's granted serial port before syncing files or opening its REPL.");
                    break;
                }
                foreach (var path in Directory.EnumerateFiles(project.Directory, "*.py").Order(StringComparer.Ordinal))
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                    upload.Add(Command("mpremote", "connect", options.Port, "fs", "cp", Path.GetFullPath(path), ":" + Path.GetFileName(path)));
                }
                notes.Add("F5 copies only regular top-level .py files. Nested packages and data files require explicit selection; nothing is deleted from the board.");
                break;
            }
            case EmbeddedProjectKind.VendorImport:
                notes.Add("Run the vendor project importer first, then review the generated GCC/CMake notes.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(project));
        }
        return new EmbeddedBuildPlan(preparation, build, upload, ToolchainId(project.Kind), compilationDatabase, notes);
    }

    /// <summary>Adapts a native build to the existing F5 pipeline without opening any device or uploading firmware.</summary>
    /// <param name="project">The detected project.</param>
    /// <remarks>The embedded workflow performs explicit upload separately, with an acquired device lease and safety checks.</remarks>
    public static RunPlan RunPlan(EmbeddedProject project)
    {
        var plan = Plan(project);
        if (plan.Build.Count == 0)
        {
            throw new InvalidOperationException("Configure the embedded build in " + OptionsFile + ". "
                + string.Join(" ", plan.Notes));
        }
        return new RunPlan(null, plan.Build[^1], project.Marker, [.. plan.Preparation, .. plan.Build.SkipLast(1)]);
    }

    private static void ValidateToken(string? value, string name)
    {
        if (value is not null && (value.Length == 0 || value.Length > 512 || value.StartsWith('-')
            || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl)))
        {
            throw new ArgumentException($"{name} must be a nonempty native identifier without whitespace, controls or a leading dash.", name);
        }
    }

    private static string FirmwarePath(string directory, string firmware)
    {
        if (string.IsNullOrWhiteSpace(firmware) || firmware.Any(char.IsControl))
        {
            throw new ArgumentException("Firmware must be a file path without control characters.", nameof(firmware));
        }
        return Path.GetFullPath(firmware, directory);
    }

    private static string ArduinoExampleDirectory(string directory, string sketch)
    {
        var example = Path.GetFullPath(sketch, directory);
        var relative = Path.GetRelativePath(directory, example);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative) || !Directory.Exists(example))
        {
            throw new ArgumentException("Arduino Sketch must name an existing example directory inside the library.", nameof(sketch));
        }
        var cursor = directory;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            cursor = Path.Combine(cursor, part);
            if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Arduino example paths must not traverse symbolic links.", nameof(sketch));
            }
        }
        if (!File.Exists(Path.Combine(example, Path.GetFileName(example) + ".ino")))
        {
            throw new ArgumentException("The selected Arduino example must contain a sketch matching its folder name.", nameof(sketch));
        }
        return example;
    }

    private static ProcessStartSpec? CMakeConfiguration(string directory, string buildDirectory, string? requested,
        IReadOnlyList<string> arguments, List<string> notes)
    {
        var presets = new List<string>();
        var hasPresets = false;
        foreach (var name in new[] { "CMakePresets.json", "CMakeUserPresets.json" })
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
            {
                continue;
            }
            hasPresets = true;
            var info = new FileInfo(path);
            if (info.Length > 2 * 1024 * 1024 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("CMake presets must be regular JSON files smaller than 2 MB.");
            }
            using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 64,
            });
            if (!json.RootElement.TryGetProperty("configurePresets", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object
                    && (!entry.TryGetProperty("hidden", out var hidden) || hidden.ValueKind != JsonValueKind.True)
                    && entry.TryGetProperty("name", out var presetName) && presetName.ValueKind == JsonValueKind.String
                    && presetName.GetString() is { Length: > 0 } value)
                {
                    presets.Add(value);
                }
            }
        }
        var selected = requested;
        if (hasPresets && selected is null)
        {
            selected = presets.Contains("Debug", StringComparer.Ordinal) ? "Debug" : presets.Count == 1 ? presets[0] : null;
            if (selected is null)
            {
                notes.Add("Select Environment as an exact CMake configure preset. The project has multiple or imported presets; its toolchain settings must be preserved.");
                return null;
            }
        }
        var selection = selected is not null && hasPresets ? new[] { "--preset", selected } : Array.Empty<string>();
        return new ProcessStartSpec
        {
            Program = "cmake", Arguments = [.. selection, "-S", directory, "-B", buildDirectory, .. arguments],
            WorkingDirectory = directory,
        };
    }
}
