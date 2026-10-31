using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Projects;

/// <summary>Recognizes real embedded project markers while skipping dependencies, build output and symbolic links.</summary>
public static class EmbeddedProjectDetector
{
    private const int MaxFolders = 2_000;
    private const int MaxManifestBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", "target", "out", "bin", "obj", "node_modules", "vendor", "managed_components", "components",
        "venv", "env", "__pycache__", "libdeps", "packages", "Middlewares", "Drivers", "CMSIS",
    };

    /// <summary>Finds embedded roots nearest the workspace root first. Nested explicit project markers are retained.</summary>
    /// <param name="root">The workspace directory.</param>
    /// <param name="maxDepth">The maximum relative folder depth.</param>
    public static IReadOnlyList<EmbeddedProject> Detect(string root, int maxDepth = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var result = new List<EmbeddedProject>();
        var queue = new Queue<(string Directory, int Depth)>();
        queue.Enqueue((Path.GetFullPath(root), 0));
        var visited = 0;
        while (queue.TryDequeue(out var item) && visited++ < MaxFolders)
        {
            try
            {
                if ((File.GetAttributes(item.Directory) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var project = Read(item.Directory);
                if (project is not null)
                {
                    result.Add(project);
                }

                if (item.Depth >= maxDepth)
                {
                    continue;
                }

                foreach (var child in Directory.EnumerateDirectories(item.Directory).Order(StringComparer.Ordinal))
                {
                    var name = Path.GetFileName(child);
                    if (!name.StartsWith('.') && !Ignored.Contains(name)
                        && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    {
                        queue.Enqueue((child, item.Depth + 1));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A disappearing or inaccessible directory must not prevent opening the remaining workspace.
            }
        }

        return result;
    }

    /// <summary>Reads the project system at one exact directory, without running configuration or following imports.</summary>
    /// <param name="directory">The project directory.</param>
    public static EmbeddedProject? Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        if (File.Exists(Path.Combine(directory, "platformio.ini")))
        {
            return ReadPlatformIO(directory);
        }
        if (File.Exists(Path.Combine(directory, ".tabstudio-ch32fun")))
        {
            return Project(EmbeddedProjectKind.Ch32Fun, directory, ".tabstudio-ch32fun", target: "CH32V003F4P6",
                notes: ["Uses pinned upstream CH32fun startup/linker code for 16 KiB flash and 2 KiB RAM. UART ISP requires the chip already in its ROM bootloader; no BOOT-pin or WCH-Link support is inferred."]);
        }

        var cmake = string.Join('\n', Text(Path.Combine(directory, "CMakeLists.txt")).Split('\n')
            .Where(line => !line.TrimStart().StartsWith('#')));
        var importedToolchain = Text(Path.Combine(directory, "tabstudio-gcc.cmake"));
        if (cmake.Contains("tabstudio-gcc.cmake", StringComparison.Ordinal) && importedToolchain.Length > 0)
        {
            var kind = importedToolchain.Contains("avr-gcc", StringComparison.Ordinal)
                ? EmbeddedProjectKind.GccCMakeAvr : EmbeddedProjectKind.GccCMakeArm;
            return Project(kind, directory, "CMakeLists.txt",
                notes: ["Review IMPORT.md and import-notes.json, supply the exact startup/linker/device settings, then build. Imported firmware never runs as a tablet executable."]);
        }
        if (cmake.Contains("idf.cmake", StringComparison.Ordinal)
            || cmake.Contains("$ENV{IDF_PATH}", StringComparison.Ordinal))
        {
            var sdkconfig = Text(Path.Combine(directory, "sdkconfig"));
            var target = Match(sdkconfig, "(?m)^CONFIG_IDF_TARGET=\"([^\"]+)\"");
            return Project(EmbeddedProjectKind.EspIdf, directory, "CMakeLists.txt", target: target);
        }

        if (Regex.IsMatch(cmake, @"\bfind_package\s*\(\s*Zephyr\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            return Project(EmbeddedProjectKind.Zephyr, directory, "CMakeLists.txt",
                notes: ["Select the exact west board identifier, including revision and qualifiers where required."]);
        }

        if (cmake.Contains("pico_sdk_import.cmake", StringComparison.Ordinal)
            || cmake.Contains("pico_sdk_init(", StringComparison.Ordinal))
        {
            return Project(EmbeddedProjectKind.PicoSdk, directory, "CMakeLists.txt",
                target: Match(cmake, @"(?im)^\s*set\s*\(\s*PICO_BOARD\s+""?([A-Za-z0-9_+-]+)""?\s*\)"));
        }

        var files = Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal).ToArray();
        if (File.Exists(Path.Combine(directory, "library.properties")))
        {
            return Project(EmbeddedProjectKind.Arduino, directory, "library.properties",
                notes: ["Select an example sketch under this Arduino library, or set Sketch in .tabstudio/embedded.json."]);
        }
        if (files.FirstOrDefault(f => f.EndsWith(".ioc", StringComparison.OrdinalIgnoreCase)) is { } cube)
        {
            var values = ReadProperties(Text(cube));
            return Project(EmbeddedProjectKind.Stm32Cube, directory, Path.GetFileName(cube),
                target: values.GetValueOrDefault("Mcu.CPN") ?? values.GetValueOrDefault("Mcu.Name"),
                notes: !File.Exists(Path.Combine(directory, "Makefile")) && cmake.Length == 0
                    ? ["Generate a Makefile or CMake project in CubeMX before building; the .ioc file is not a build script."] : []);
        }

        if (files.FirstOrDefault(f => f.EndsWith(".ino", StringComparison.OrdinalIgnoreCase)) is { } sketch)
        {
            var expected = Path.GetFileName(directory) + ".ino";
            return Project(EmbeddedProjectKind.Arduino, directory, Path.GetFileName(sketch),
                notes: !files.Any(f => Path.GetFileName(f).Equals(expected, StringComparison.Ordinal))
                    ? [$"Arduino expects the main sketch to be named {expected}; use a correctly named sketch folder."] : []);
        }

        if (File.Exists(Path.Combine(directory, "Cargo.toml")))
        {
            var config = Text(Path.Combine(directory, ".cargo", "config.toml"));
            if (config.Length == 0)
            {
                config = Text(Path.Combine(directory, ".cargo", "config"));
            }

            var target = ReadCargoTarget(config);
            // A library that also builds without std (anyhow, serde_json) is a library for computers too: only
            // firmware's program, its linker memory map or a bare-metal target make a Cargo package embedded.
            var embedded = File.Exists(Path.Combine(directory, "memory.x"))
                || target?.Contains("none", StringComparison.Ordinal) == true
                || Text(Path.Combine(directory, "src", "main.rs")).Contains("#![no_std]", StringComparison.Ordinal);
            if (embedded)
            {
                return Project(EmbeddedProjectKind.Rust, directory, "Cargo.toml", target: target,
                    notes: ["Cargo's existing linker script and runner remain authoritative; select a chip before probe upload."]);
            }
        }

        var pythonMarker = File.Exists(Path.Combine(directory, ".micropython")) ? ".micropython"
            : File.Exists(Path.Combine(directory, ".tabstudio", "micropython.json")) ? ".tabstudio/micropython.json"
            : File.Exists(Path.Combine(directory, "boot.py")) && File.Exists(Path.Combine(directory, "main.py")) ? "boot.py"
            : null;
        if (pythonMarker is not null)
        {
            return Project(EmbeddedProjectKind.MicroPython, directory, pythonMarker,
                notes: ["Board Python files are copied explicitly; .venv, credentials and desktop dependency folders are not synced."]);
        }

        if (files.FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() is ".uvprojx" or ".ewp" or ".cproj") is { } vendor)
        {
            return Project(EmbeddedProjectKind.VendorImport, directory, Path.GetFileName(vendor),
                notes: ["Import to a separate GCC/CMake project and review all port notes before building."]);
        }

        return null;
    }

    private static EmbeddedProject ReadPlatformIO(string directory)
    {
        var sections = ReadIni(Text(Path.Combine(directory, "platformio.ini")));
        var shared = sections.GetValueOrDefault("env") ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var environments = sections.Where(s => s.Key.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            .Select(s => new EmbeddedEnvironment(s.Key[4..], s.Value.GetValueOrDefault("board") ?? shared.GetValueOrDefault("board"),
                s.Value.GetValueOrDefault("framework") ?? shared.GetValueOrDefault("framework"))).ToArray();
        var defaults = sections.GetValueOrDefault("platformio")?.GetValueOrDefault("default_envs")
            ?.Split([',', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var defaultEnvironment = defaults is { Length: 1 } && !defaults[0].Contains("${", StringComparison.Ordinal) ? defaults[0] : null;
        var notes = new List<string>();
        if (defaults is { Length: > 1 })
        {
            notes.Add("platformio.ini declares multiple default environments; select one to upload one board.");
        }
        if (defaults?.Any(value => value.Contains("${", StringComparison.Ordinal)) == true)
        {
            notes.Add("PlatformIO resolves interpolated default environments itself; select a concrete environment before upload.");
        }
        if (environments.Length == 0)
        {
            notes.Add("No local [env:name] section; PlatformIO may supply environments through extra_configs.");
        }
        return new EmbeddedProject(EmbeddedProjectKind.PlatformIO, directory, "platformio.ini", environments, defaultEnvironment, null, notes)
        {
            HasNativeEnvironmentSelection = defaults is { Length: > 0 }
                || sections.GetValueOrDefault("platformio")?.ContainsKey("extra_configs") == true,
        };
    }

    internal static Dictionary<string, Dictionary<string, string>> ReadIni(string text)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        string? previous = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1].Trim();
                if (!sections.TryGetValue(name, out current))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections.Add(name, current);
                }
                previous = null;
                continue;
            }
            if (current is null)
            {
                continue;
            }
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                previous = line[..separator].Trim();
                current[previous] = line[(separator + 1)..].Trim();
            }
            else if (previous is not null && raw.Length > 0 && char.IsWhiteSpace(raw[0]))
            {
                current[previous] += "\n" + line;
            }
        }
        return sections;
    }

    private static Dictionary<string, string> ReadProperties(string text) => text.Split('\n')
        .Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#') && line.Contains('='))
        .Select(line => (Key: line[..line.IndexOf('=')].Trim(), Value: line[(line.IndexOf('=') + 1)..].Trim()))
        .GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);

    private static string? ReadCargoTarget(string text)
    {
        var inBuild = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inBuild = line == "[build]";
            }
            if (inBuild && Match(line, "^target\\s*=\\s*[\"']([^\"']+)[\"']") is { } target)
            {
                return target;
            }
        }
        return null;
    }

    private static string? Match(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string Text(string path)
    {
        if (!File.Exists(path))
        {
            return "";
        }
        var info = new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length > MaxManifestBytes)
        {
            return "";
        }
        return File.ReadAllText(path);
    }

    private static EmbeddedProject Project(EmbeddedProjectKind kind, string directory, string marker,
        string? target = null, IReadOnlyList<string>? notes = null) => new(kind, directory, marker, [], null, target, notes ?? []);
}
