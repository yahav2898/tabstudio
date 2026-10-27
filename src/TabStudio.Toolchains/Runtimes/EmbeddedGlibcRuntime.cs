using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Runtimes;

/// <summary>Routes glibc embedded SDK frontends and their USB workers to one isolated Ubuntu environment.</summary>
public sealed class EmbeddedGlibcRuntime(IToolchainRuntime primary, IToolchainRuntime embedded,
    Func<string, bool> isReady, Func<bool> isBaseReady) : IToolchainRuntime
{
    private string? _clangdDirectory;
    /// <summary>Explicit process marker retained when a USB serial worker wraps a glibc command in Python.</summary>
    public const string RoutingMarker = "TABSTUDIO_EMBEDDED_GLIBC";

    /// <inheritdoc />
    public string DisplayName => primary.DisplayName + " + embedded SDKs (Ubuntu glibc)";

    /// <inheritdoc />
    public RuntimeKind Kind => primary.Kind;

    /// <summary>Whether a program belongs to a glibc embedded SDK, independent of USB wrapper arguments.</summary>
    /// <param name="command">The program name or guest path.</param>
    public static bool IsEmbeddedCommand(string command) => ToolchainForCommand(command) is not null;

    /// <summary>The installed SDK owning a native command or its private package path.</summary>
    /// <param name="command">The program name or guest path.</param>
    public static string? ToolchainForCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.StartsWith("/usr/local/bin/", StringComparison.Ordinal) && command[15..].IndexOf('/') < 0)
        {
            command = command[15..];
        }
        if (command is "pio" or "platformio" || command.StartsWith("/opt/tabstudio/platformio/", StringComparison.Ordinal)
            || command.StartsWith("/root/.platformio/", StringComparison.Ordinal))
        {
            return "platformio";
        }
        if (command == "arduino-cli" || command.StartsWith("/opt/tabstudio/arduino-cli/", StringComparison.Ordinal)
            || command.StartsWith("/root/.arduino15/", StringComparison.Ordinal))
        {
            return "arduino";
        }
        if (command is "probe-rs" or "cargo-embed" or "cargo-flash"
            || command.StartsWith("/opt/tabstudio/probe-rs/", StringComparison.Ordinal))
        {
            return "probe-rs";
        }
        if (command is "esptool" or "esptool.py" || command.StartsWith("/opt/tabstudio/flash/esptool/", StringComparison.Ordinal))
        {
            return "embedded-esptool";
        }
        if (command == "picotool" || command.StartsWith("/opt/tabstudio/flash/picotool/", StringComparison.Ordinal))
        {
            return "embedded-picotool";
        }
        if (command == "nrfutil" || command.StartsWith(EmbeddedNordicInstallation.Prefix + "/", StringComparison.Ordinal))
        {
            return EmbeddedNordicInstallation.Id;
        }
        if (command == "idf.py" || command.StartsWith("/opt/tabstudio/esp-idf/", StringComparison.Ordinal)
            || command.StartsWith("/opt/tabstudio/esp-idf-tools/", StringComparison.Ordinal))
        {
            return "esp-idf";
        }
        if (command == "west" || command.StartsWith("/opt/tabstudio/zephyr-workspace/", StringComparison.Ordinal)
            || command.StartsWith("/opt/tabstudio/zephyr-python/", StringComparison.Ordinal))
        {
            return "zephyr";
        }
        if (command.StartsWith("/opt/tabstudio/pico-sdk/", StringComparison.Ordinal))
        {
            return "pico-sdk";
        }
        if (command.StartsWith("/opt/tabstudio/embedded-rust/", StringComparison.Ordinal))
        {
            return "embedded-rust";
        }
        return null;
    }

    /// <summary>Determines whether a complete process specification must remain in the glibc environment.</summary>
    /// <param name="spec">The process, including any explicit USB worker routing marker.</param>
    public static bool UsesGlibc(ProcessStartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return ToolchainForSpec(spec) is not null || spec.Environment.GetValueOrDefault(RoutingMarker) == "1";
    }

    /// <inheritdoc />
    public ToolchainProcess Start(ProcessStartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var fallbackClangd = IsClangd(spec.Program) && !primary.HasCommand(spec.Program) && isBaseReady();
        if (!UsesGlibc(spec) && !fallbackClangd)
        {
            if (IsClangd(spec.Program))
            {
                Volatile.Write(ref _clangdDirectory, null);
            }
            return primary.Start(spec);
        }
        var id = ToolchainForSpec(spec);
        if (!isBaseReady() || id is not null && !isReady(id))
        {
            throw new ToolchainRuntimeException($"The embedded glibc environment for {id ?? "this USB worker"} is not ready. Finish installing the SDK in the Toolchain Store.");
        }
        var environment = new Dictionary<string, string?>(spec.Environment)
        {
            [RoutingMarker] = "1",
            ["PLATFORMIO_SETTING_ENABLE_TELEMETRY"] = "no",
        };
        foreach (var entry in EmbeddedSdkBootstrap.Environment(id ?? "embedded"))
        {
            environment.TryAdd(entry.Key, entry.Value);
        }
        EmbeddedSdkBootstrap.ApplyBuildLimits(id ?? "embedded", environment);
        if (IsClangd(spec.Program))
        {
            Volatile.Write(ref _clangdDirectory, spec.WorkingDirectory);
        }
        // The original inherited descriptors, private USB inventory and PTY mounts are retained unchanged.
        var program = id == "embedded-rust" && IsRustTool(spec.Program)
            ? "/opt/tabstudio/embedded-rust/bin/" + Path.GetFileName(spec.Program) : spec.Program;
        var routed = spec with { Program = program, Environment = environment };
        if (id == "platformio" && environment.GetValueOrDefault(EmbeddedBox64Installation.Marker) == "1")
        {
            routed = EmbeddedBox64Installation.Wrap(routed);
        }
        return embedded.Start(routed);
    }

    /// <inheritdoc />
    public bool HasCommand(string command) => Resolve(command) is not null;

    /// <inheritdoc />
    public string? Resolve(string command)
    {
        var id = ToolchainForCommand(command);
        if (id is null && IsClangd(command) && isBaseReady() && !primary.HasCommand(command))
        {
            return embedded.Resolve(command);
        }
        return id is null ? primary.Resolve(command) : isBaseReady() && isReady(id) ? embedded.Resolve(command) : null;
    }

    /// <inheritdoc />
    public string? ResolveForDirectory(string command, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var id = ToolchainForSpec(new ProcessStartSpec { Program = command, WorkingDirectory = workingDirectory });
        if (id is null)
        {
            return primary.ResolveForDirectory(command, workingDirectory);
        }
        var program = id == "embedded-rust" && IsRustTool(command)
            ? "/opt/tabstudio/embedded-rust/bin/" + Path.GetFileName(command) : command;
        return isBaseReady() && isReady(id) ? embedded.Resolve(program) : null;
    }

    /// <inheritdoc />
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => primary.Shell(workingDirectory, size);

    /// <inheritdoc />
    public void ShareFolder(string folder)
    {
        primary.ShareFolder(folder);
        embedded.ShareFolder(folder);
    }

    /// <inheritdoc />
    public string? ToDevicePath(string programPath, string? workingDirectory)
    {
        var context = workingDirectory ?? Volatile.Read(ref _clangdDirectory);
        if (ToolchainForCommand(programPath) is not null)
        {
            return embedded.ToDevicePath(programPath, workingDirectory);
        }
        if (isBaseReady() && context is not null && OwningProject(context) is not null)
        {
            return embedded.ToDevicePath(programPath, workingDirectory) ?? primary.ToDevicePath(programPath, workingDirectory);
        }
        return primary.ToDevicePath(programPath, workingDirectory)
            ?? (isBaseReady() ? embedded.ToDevicePath(programPath, workingDirectory) : null);
    }

    private static string? ToolchainForSpec(ProcessStartSpec spec)
    {
        if (ToolchainForCommand(spec.Program) is { } direct)
        {
            return direct;
        }
        if (spec.Environment.GetValueOrDefault(RoutingMarker) == "1")
        {
            var separator = spec.Arguments.ToList().IndexOf("--");
            if (separator >= 0 && separator + 1 < spec.Arguments.Count)
            {
                return ToolchainForCommand(spec.Arguments[separator + 1]);
            }
        }
        if ((IsClangd(spec.Program) || Path.GetFileName(spec.Program) is "cmake" or "ninja" or "make")
            && spec.WorkingDirectory is { } directory)
        {
            return OwningProject(directory);
        }
        if (IsRustTool(spec.Program)
            && spec.WorkingDirectory is { } rustDirectory && OwningProject(rustDirectory) == "embedded-rust")
        {
            return "embedded-rust";
        }
        return null;
    }

    private static bool IsClangd(string command) => command is "clangd" or "/usr/bin/clangd" or "/usr/local/bin/clangd";

    private static bool IsRustTool(string command)
    {
        if (command.StartsWith("/usr/local/bin/", StringComparison.Ordinal)) command = command[15..];
        else if (command.StartsWith("/usr/bin/", StringComparison.Ordinal)) command = command[9..];
        return command is "cargo" or "rustc" or "rust-analyzer" or "rustfmt" or "cargo-fmt" or "cargo-clippy" or "clippy-driver";
    }

    private static string? OwningProject(string directory)
    {
        try
        {
            string? current = Path.GetFullPath(directory);
            for (var depth = 0; depth < 12 && current is not null; depth++, current = Path.GetDirectoryName(current))
            {
                var kind = EmbeddedProjectDetector.Read(current)?.Kind;
                if (kind is EmbeddedProjectKind.PlatformIO or EmbeddedProjectKind.Arduino or EmbeddedProjectKind.EspIdf
                    or EmbeddedProjectKind.Zephyr or EmbeddedProjectKind.PicoSdk or EmbeddedProjectKind.Rust)
                {
                    return EmbeddedBuildPlanner.ToolchainId(kind.Value);
                }
                if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A removed workspace does not redirect another project's language server.
        }
        return null;
    }
}
