using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TabStudio.Toolchains.Python;

/// <summary>The platform a Python package installs on, as wheel tags name it.</summary>
/// <param name="Machine">The processor, as wheel tags write it: <c>aarch64</c>, <c>x86_64</c> or <c>armv7l</c>.</param>
/// <param name="Musl">Whether the C library is musl, as on Alpine Linux and so on the tablet, rather than glibc.</param>
public sealed record PythonPlatform(string Machine, bool Musl)
{
    /// <summary>The tablet's platform: Alpine Linux, with musl, on this device's processor.</summary>
    public static PythonPlatform Alpine => new(MachineOf(RuntimeInformation.ProcessArchitecture), Musl: true);

    /// <summary>A Linux computer's platform: glibc, on this computer's processor.</summary>
    public static PythonPlatform Glibc => new(MachineOf(RuntimeInformation.ProcessArchitecture), Musl: false);

    /// <summary>Whether a wheel's platform tag, such as <c>musllinux_1_2_aarch64</c>, installs here.</summary>
    /// <param name="tag">The platform tag.</param>
    public bool Accepts(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag == "any" || (tag.StartsWith(Musl ? "musllinux_" : "manylinux", StringComparison.Ordinal) && tag.EndsWith("_" + Machine, StringComparison.Ordinal));
    }

    /// <summary>Whether a platform tag is this kind of Linux on this processor, rather than a pure-Python <c>any</c>.</summary>
    /// <param name="tag">The platform tag.</param>
    public bool IsNative(string tag) => tag != "any" && Accepts(tag);

    private static string MachineOf(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => "aarch64",
        Architecture.Arm => "armv7l",
        Architecture.X86 => "i686",
        _ => "x86_64",
    };
}

/// <summary>A release of a package on PyPI: what it requires of Python, and the files it publishes.</summary>
/// <param name="Name">The package's normalized name.</param>
/// <param name="Version">The release.</param>
/// <param name="RequiresPython">Its <c>Requires-Python</c>, such as <c>&gt;=3.8</c>, or <see langword="null"/>.</param>
/// <param name="Files">Its file names: wheels (<c>.whl</c>) and source distributions.</param>
public sealed record PythonRelease(string Name, string Version, string? RequiresPython, IReadOnlyList<string> Files);

/// <summary>
/// Which Python versions a project's pinned packages install on without compiling. A package that publishes
/// wheels for this platform only for some Python versions (psycopg2-binary 2.9.9 up to 3.12, say) would be
/// compiled on a newer Python, which needs compilers and libraries and often fails; an older Python that has the
/// wheel installs it in seconds. A package without wheels for this platform is compiled on every version, but its
/// Linux wheels for other processors still name the Pythons its C code supports (asyncpg 0.26.0's stop at 3.10,
/// and its source does not build on 3.12), so they decide as well. Pure-Python packages, and packages without
/// Linux wheels, compile (or not) on any version alike, so they do not decide.
/// </summary>
public static class PythonWheels
{
    private static readonly Uri PyPi = new("https://pypi.org/pypi/");

    /// <summary>
    /// The newest of the candidate minor versions on which every release installs without compiling, or
    /// <see langword="null"/> when none does or the releases do not limit the version.
    /// </summary>
    /// <param name="releases">The pinned releases.</param>
    /// <param name="platform">The platform.</param>
    /// <param name="candidates">The minor versions to choose from, such as 9 to 14.</param>
    /// <returns>The minor version, with the releases that decided it.</returns>
    public static (int Minor, IReadOnlyList<PythonRelease> Limiting)? NewestSupported(IEnumerable<PythonRelease> releases, PythonPlatform platform, IEnumerable<int> candidates)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(candidates);
        var all = releases.ToList();
        var minors = candidates.Distinct().OrderDescending().ToList();
        if (minors.Count == 0)
        {
            return null;
        }

        var limiting = all.Where(r => minors.Any(m => !InstallsWithoutBuilding(r, m, platform))).ToList();
        if (limiting.Count == 0)
        {
            return null;
        }

        foreach (var minor in minors)
        {
            if (all.All(r => InstallsWithoutBuilding(r, minor, platform)))
            {
                return (minor, limiting);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a release installs on Python 3.<paramref name="minor"/> without compiling, or at least no worse
    /// than on any other version: it allows that Python, and it has a wheel for it when it has wheels for this
    /// platform at all, or else a Linux wheel for it for another processor when it has Linux wheels at all.
    /// </summary>
    /// <param name="release">The release.</param>
    /// <param name="minor">The minor version.</param>
    /// <param name="platform">The platform.</param>
    public static bool InstallsWithoutBuilding(PythonRelease release, int minor, PythonPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(platform);
        if (PythonVersionRange.Parse(release.RequiresPython) is { } requires && !requires.Allows(minor))
        {
            return false;
        }

        var wheels = release.Files.Select(WheelTags.Parse).OfType<WheelTags>().ToList();
        if (wheels.Any(w => w.Platforms.Contains("any") && w.Supports(minor)))
        {
            return true;
        }

        // Without a wheel for this platform, pip builds the package. The Linux wheels it publishes for other
        // processors or C libraries, as asyncpg 0.26.0 does for x86_64 alone up to 3.10, name the Pythons its C
        // source was written for: on a newer one the build usually fails against CPython's changed C API. A
        // release without Linux wheels at all builds on every Python alike.
        if (!wheels.Any(w => w.Platforms.Any(platform.IsNative)))
        {
            var linux = wheels.Where(w => w.Platforms.Any(IsLinux)).ToList();
            return linux.Count == 0 || linux.Any(w => w.Supports(minor));
        }

        return wheels.Any(w => w.Platforms.Any(platform.Accepts) && w.Supports(minor));
    }

    /// <summary>
    /// Reads pinned releases from PyPI's JSON API. Packages it cannot read, because the device is offline or the
    /// index does not know them, are left out: they then do not limit the version.
    /// </summary>
    /// <param name="http">The HTTP client.</param>
    /// <param name="pins">The pinned packages.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    public static Task<IReadOnlyList<PythonRelease>> ReadAsync(HttpClient http, IEnumerable<PythonPin> pins, CancellationToken cancellationToken) =>
        ReadAsync(http, pins, PyPi, cancellationToken);

    /// <summary>Reads pinned releases from a JSON API like PyPI's, as <see cref="ReadAsync(HttpClient, IEnumerable{PythonPin}, CancellationToken)"/>.</summary>
    /// <param name="http">The HTTP client.</param>
    /// <param name="pins">The pinned packages.</param>
    /// <param name="index">The JSON API's base address, such as <c>https://pypi.org/pypi/</c>.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    public static async Task<IReadOnlyList<PythonRelease>> ReadAsync(HttpClient http, IEnumerable<PythonPin> pins, Uri index, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(pins);
        using var gate = new SemaphoreSlim(8);
        var reads = pins.Take(400).Select(async pin =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ReadOneAsync(http, pin, index, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }).ToList();
        var releases = await Task.WhenAll(reads).ConfigureAwait(false);
        return [.. releases.OfType<PythonRelease>()];
    }

    /// <summary>Reads a release from PyPI's JSON API response.</summary>
    /// <param name="json">The response to <c>/pypi/&lt;name&gt;/&lt;version&gt;/json</c>.</param>
    /// <param name="pin">The release it describes.</param>
    public static PythonRelease? ParseRelease(string json, PythonPin pin)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(pin);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            string? requires = null;
            if (root.TryGetProperty("info", out var info) && info.TryGetProperty("requires_python", out var value) && value.ValueKind == JsonValueKind.String)
            {
                requires = value.GetString();
            }

            var files = new List<string>();
            if (root.TryGetProperty("urls", out var urls) && urls.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in urls.EnumerateArray())
                {
                    var yanked = file.TryGetProperty("yanked", out var y) && y.ValueKind == JsonValueKind.True;
                    if (!yanked && file.TryGetProperty("filename", out var name) && name.GetString() is { } filename)
                    {
                        files.Add(filename);
                    }
                }
            }

            return new PythonRelease(pin.Name, pin.Version, string.IsNullOrWhiteSpace(requires) ? null : requires, files);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<PythonRelease?> ReadOneAsync(HttpClient http, PythonPin pin, Uri index, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var address = new Uri(index, $"{Uri.EscapeDataString(pin.Name)}/{Uri.EscapeDataString(pin.Version)}/json");
            using var response = await http.GetAsync(address, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return ParseRelease(json, pin);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>Whether a platform tag is a Linux one, for any processor: <c>manylinux…</c>, <c>musllinux…</c> or <c>linux_…</c>.</summary>
    private static bool IsLinux(string tag) =>
        tag.StartsWith("manylinux", StringComparison.Ordinal) || tag.StartsWith("musllinux_", StringComparison.Ordinal) || tag.StartsWith("linux_", StringComparison.Ordinal);

    /// <summary>A wheel's compatibility tags, from its file name: <c>name-version(-build)-python-abi-platform.whl</c>.</summary>
    private sealed record WheelTags(string[] Pythons, string[] Abis, string[] Platforms)
    {
        public static WheelTags? Parse(string file)
        {
            if (!file.EndsWith(".whl", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var parts = file[..^4].Split('-');
            return parts.Length < 5 ? null : new WheelTags(parts[^3].Split('.'), parts[^2].Split('.'), parts[^1].Split('.'));
        }

        /// <summary>Whether CPython 3.<paramref name="minor"/> loads the wheel.</summary>
        public bool Supports(int minor)
        {
            foreach (var python in Pythons)
            {
                foreach (var abi in Abis)
                {
                    if (Supports(python, abi, minor))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Supports(string python, string abi, int minor)
        {
            if (python is "py3" or "py30" || (python.StartsWith("py3", StringComparison.Ordinal) && Minor(python[3..]) is { } least && least <= minor))
            {
                return abi == "none";
            }

            if (!python.StartsWith("cp3", StringComparison.Ordinal) || Minor(python[3..]) is not { } built)
            {
                return false;
            }

            // The stable ABI works on the version it was built for and every later one; others on theirs alone.
            return abi switch
            {
                "abi3" => built <= minor,
                "none" => built == minor,
                _ => built == minor && abi.StartsWith("cp3", StringComparison.Ordinal) && !abi.EndsWith('t') && !abi.EndsWith('d'),
            };
        }

        private static int? Minor(string digits) =>
            digits.Length > 0 && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
