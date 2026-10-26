using TabStudio.Toolchains.Embedded.Projects;
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
            ["PYTHONSAFEPATH"] = "1",
        };
        if (IsClangd(spec.Program))
        {
            Volatile.Write(ref _clangdDirectory, spec.WorkingDirectory);
        }
        // The original inherited descriptors, private USB inventory and PTY mounts are retained unchanged.
        return embedded.Start(spec with { Environment = environment });
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
        if (ToolchainForCommand(programPath) is not null || isBaseReady() && context is not null && OwningProject(context) is not null)
        {
            return embedded.ToDevicePath(programPath, workingDirectory);
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
        if (IsClangd(spec.Program) && spec.WorkingDirectory is { } directory)
        {
            return OwningProject(directory);
        }
        return null;
    }

    private static bool IsClangd(string command) => command is "clangd" or "/usr/bin/clangd" or "/usr/local/bin/clangd";

    private static string? OwningProject(string directory)
    {
        try
        {
            var current = Path.GetFullPath(directory);
            for (var depth = 0; depth < 12 && current is not null; depth++, current = Path.GetDirectoryName(current))
            {
                var kind = EmbeddedProjectDetector.Read(current)?.Kind;
                if (kind is EmbeddedProjectKind.PlatformIO or EmbeddedProjectKind.Arduino)
                {
                    return kind == EmbeddedProjectKind.PlatformIO ? "platformio" : "arduino";
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
