using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace TabStudio.Containers.Images;

/// <summary>An image's platform, such as <c>linux/arm64</c> or <c>linux/arm/v7</c>.</summary>
public sealed record OciPlatform
{
    /// <summary>The CPU architecture, as Go names it: <c>arm64</c>, <c>arm</c>, <c>amd64</c>.</summary>
    [JsonPropertyName("architecture")] public string Architecture { get; init; } = "";
    /// <summary>The operating system.</summary>
    [JsonPropertyName("os")] public string Os { get; init; } = "linux";
    /// <summary>The variant, such as <c>v7</c>.</summary>
    [JsonPropertyName("variant")] public string? Variant { get; init; }

    /// <summary>Creates a platform.</summary>
    public static OciPlatform Linux(string architecture, string? variant = null) => new() { Architecture = architecture, Variant = variant };

    /// <summary>Parses <c>linux/arm64</c>, <c>linux/arm/v7</c> or <c>arm64</c>.</summary>
    /// <exception cref="FormatException">It is not a platform.</exception>
    public static OciPlatform Parse(string text)
    {
        var parts = text.Trim().ToLowerInvariant().Split('/');
        var platform = parts.Length switch
        {
            1 => Linux(parts[0]),
            2 => new OciPlatform { Os = parts[0], Architecture = parts[1] },
            3 => new OciPlatform { Os = parts[0], Architecture = parts[1], Variant = parts[2] },
            _ => throw new FormatException($"'{text}' is not a platform such as linux/arm64."),
        };
        return platform.Architecture switch
        {
            "aarch64" => platform with { Architecture = "arm64" },
            "x86_64" or "x64" => platform with { Architecture = "amd64" },
            "armhf" or "armv7" => platform with { Architecture = "arm", Variant = "v7" },
            _ => platform,
        };
    }

    /// <summary>Whether this platform runs an image built for <paramref name="image"/>.</summary>
    public bool Matches(OciPlatform image) =>
        string.Equals(Os, image.Os, StringComparison.Ordinal) && string.Equals(Architecture, image.Architecture, StringComparison.Ordinal)
        && (Variant is null || image.Variant is null || string.Equals(Variant, image.Variant, StringComparison.Ordinal)
            || (Architecture == "arm64" && image.Variant is "v8" or null));

    /// <summary>The device's own platform.</summary>
    public static OciPlatform Host => RuntimeInformation.ProcessArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.Arm64 => Linux("arm64"),
        System.Runtime.InteropServices.Architecture.Arm => Linux("arm", "v7"),
        System.Runtime.InteropServices.Architecture.X86 => Linux("386"),
        _ => Linux("amd64"),
    };

    /// <summary>
    /// The platforms to choose from a multi-platform image, best first: on an ARM64 tablet arm64, then arm/v7
    /// (which ARM64 CPUs run natively in 32-bit mode), then amd64 through Box64; on an x86-64 emulator or computer
    /// its own amd64 first.
    /// </summary>
    public static IReadOnlyList<OciPlatform> Preference(OciPlatform host) => host.Architecture switch
    {
        "arm64" => [Linux("arm64"), Linux("arm", "v7"), Linux("amd64")],
        "arm" => [Linux("arm", "v7"), Linux("arm", "v6")],
        "amd64" => [Linux("amd64"), Linux("386")],
        _ => [host],
    };

    /// <summary>Whether an image of this platform runs only through an emulator on <paramref name="host"/>.</summary>
    public bool NeedsEmulation(OciPlatform host) => !(Architecture == host.Architecture || (host.Architecture == "arm64" && Architecture == "arm") || (host.Architecture == "amd64" && Architecture == "386"));

    /// <inheritdoc />
    public override string ToString() => Variant is null ? $"{Os}/{Architecture}" : $"{Os}/{Architecture}/{Variant}";
}
