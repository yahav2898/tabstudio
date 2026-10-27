using System.Collections.ObjectModel;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>Shared prerequisites and process policies for embedded front ends in the existing glibc environment.</summary>
public static partial class EmbeddedSdkBootstrap
{
    /// <summary>Private wrappers limiting native build generators to the selected tablet job count.</summary>
    public const string BuildToolsDirectory = "/opt/tabstudio/embedded/build-bin";

    /// <summary>Maximum concurrent compiler jobs requested by a managed SDK build.</summary>
    public const string JobsVariable = "TABSTUDIO_EMBEDDED_JOBS";

    /// <summary>Small argv-preserving generator wrappers. ESP-IDF 5.4 has no jobs CLI option.</summary>
    public static IReadOnlyList<FileDefinition> BuildLimiters { get; } = Array.AsReadOnly(new[] { "ninja", "make" }
        .Select(program => new FileDefinition
        {
            Path = BuildToolsDirectory + "/" + program,
            Executable = true,
            Content = "#!/bin/sh\nset -eu\njobs=${" + JobsVariable + ":-2}\n"
                + "case \"$jobs\" in [1-9]|1[0-6]) ;; *) jobs=2 ;; esac\n"
                + "exec /usr/bin/" + program + " \"$@\" \"-j$jobs\"\n",
        }).ToArray());
    /// <summary>Packages installed through the distribution's signed apt indexes before catalog setup steps.</summary>
    public static IReadOnlyList<string> BasePackages { get; } = Array.AsReadOnly(new[]
    {
        "ca-certificates", "curl", "git", "python3", "python3-venv", "python3-pip", "build-essential",
        "cmake", "ninja-build", "xz-utils", "libusb-1.0-0-dev", "libhidapi-dev", "clangd"
    });

    private static readonly IReadOnlyDictionary<string, string?> FrontendEnvironment = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>
    {
        ["DEBIAN_FRONTEND"] = "noninteractive",
        ["PYTHONNOUSERSITE"] = "1",
        ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1",
        ["PIP_NO_INPUT"] = "1",
        ["PLATFORMIO_SETTING_ENABLE_TELEMETRY"] = "no",
    });

    /// <summary>Process environment applied before both installation and every embedded front-end launch.</summary>
    /// <param name="id">The catalog toolchain identifier.</param>
    public static IReadOnlyDictionary<string, string?> Environment(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (Recipes.Get(id) is not { } recipe)
        {
            return FrontendEnvironment;
        }
        var environment = new Dictionary<string, string?>(FrontendEnvironment);
        foreach (var entry in recipe.Environment)
        {
            environment[entry.Key] = entry.Value;
        }
        ApplyBuildLimits(id, environment);
        return new ReadOnlyDictionary<string, string?>(environment);
    }

    internal static void ApplyBuildLimits(string id, IDictionary<string, string?> environment)
    {
        if (Recipes.Get(id) is null)
        {
            return;
        }
        var path = environment.TryGetValue("PATH", out var configuredPath) && configuredPath is not null
            ? configuredPath : "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin";
        if (!path.StartsWith(BuildToolsDirectory + ":", StringComparison.Ordinal))
        {
            environment["PATH"] = BuildToolsDirectory + ":" + path;
        }
        environment.TryGetValue(JobsVariable, out var configuredJobs);
        if (!int.TryParse(configuredJobs, out var jobs) || jobs is < 1 or > 16)
        {
            environment[JobsVariable] = "2";
        }
    }

    /// <summary>Checks the real installed front end without opening a USB device or downloading a board SDK.</summary>
    /// <param name="id">PlatformIO, Arduino or probe-rs catalog identifier.</param>
    public static ProcessStartSpec ValidationCommand(string id)
    {
        var command = id switch
        {
            "platformio" => new[] { "pio", "--version" },
            "arduino" => ["arduino-cli", "version"],
            "probe-rs" => ["probe-rs", "--version"],
            "embedded-esptool" => ["esptool", "version"],
            "embedded-picotool" => ["picotool", "version"],
            "esp-idf" => ["idf.py", "--version"],
            "zephyr" => ["west", "--version"],
            "pico-sdk" => ["cmake", "--version"],
            "embedded-rust" => ["rustc", "--version"],
            _ => throw new ArgumentException("No embedded front-end validation command for " + id, nameof(id)),
        };
        return new ProcessStartSpec { Program = command[0], Arguments = command[1..], Environment = Environment(id), WorkingDirectory = "/" };
    }
}
