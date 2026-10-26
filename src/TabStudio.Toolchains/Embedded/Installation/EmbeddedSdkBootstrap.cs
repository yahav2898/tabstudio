using System.Collections.ObjectModel;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>Shared prerequisites and process policies for embedded front ends in the existing glibc environment.</summary>
public static class EmbeddedSdkBootstrap
{
    /// <summary>Packages installed through the distribution's signed apt indexes before catalog setup steps.</summary>
    public static IReadOnlyList<string> BasePackages { get; } = Array.AsReadOnly(new[]
    {
        "ca-certificates", "curl", "git", "python3", "python3-venv", "python3-pip", "build-essential",
        "cmake", "ninja-build", "xz-utils", "libusb-1.0-0-dev", "libhidapi-dev", "clangd"
    });

    private static readonly IReadOnlyDictionary<string, string?> FrontendEnvironment = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>
    {
        ["DEBIAN_FRONTEND"] = "noninteractive",
        ["PYTHONSAFEPATH"] = "1",
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
        return FrontendEnvironment;
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
            _ => throw new ArgumentException("No embedded front-end validation command for " + id, nameof(id)),
        };
        return new ProcessStartSpec { Program = command[0], Arguments = command[1..], Environment = Environment(id), WorkingDirectory = "/" };
    }
}
