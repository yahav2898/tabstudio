using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Projects;

/// <summary>The SDK version a firmware project asks for, as its own files say it.</summary>
/// <param name="ToolchainId">The Toolchain Store entry of the SDK, such as <c>pico-sdk</c>.</param>
/// <param name="SdkName">The SDK's name for messages, such as <c>Pico SDK</c>.</param>
/// <param name="Minimum">The oldest version the project accepts, such as <c>2.3.0</c>, or <see langword="null"/>.</param>
/// <param name="Exact">The one version or revision the project pins, such as a west manifest's <c>v4.1.0</c>, or <see langword="null"/>.</param>
/// <param name="Source">The file that says so, relative to the project, with its line, such as <c>CMakeLists.txt:12</c>.</param>
public sealed record EmbeddedSdkRequirement(string ToolchainId, string SdkName, string? Minimum, string? Exact, string Source)
{
    /// <summary>The requirement as people write it, such as <c>≥ 2.3.0</c> or <c>v4.1.0</c>.</summary>
    public string Display => Exact ?? (Minimum is { } minimum ? "≥ " + minimum : "any version");
}

/// <summary>Whether the SDK on the device suits a project, and what to tell the person when it does not.</summary>
/// <param name="Satisfied">Whether the build can go ahead with the installed SDK.</param>
/// <param name="Message">
/// One sentence for the person, such as "This project needs Pico SDK ≥ 2.3.0; 2.1.1 is installed — Install 2.3.1.",
/// or <see langword="null"/> when there is nothing to say.
/// </param>
/// <param name="InstallVersion">The pinned, verified version that satisfies the project, when installing it is the fix.</param>
public sealed record EmbeddedSdkVerdict(bool Satisfied, string? Message, string? InstallVersion);

/// <summary>
/// Reads the SDK version a firmware project needs and compares it with the SDK the Toolchain Store installs:
/// <list type="bullet">
///   <item>Pico SDK projects check <c>PICO_SDK_VERSION_STRING VERSION_LESS "2.3.0"</c> in their CMake files, as
///   raspberrypi/pico-examples does.</item>
///   <item>ESP-IDF projects ask for a version in a component manifest's <c>idf: "&gt;=5.0"</c> dependency, and record the
///   one they were built with in <c>dependencies.lock</c>.</item>
///   <item>PlatformIO projects pin each environment's platform (<c>platform = espressif32@6.4.0</c>), which PlatformIO
///   installs itself.</item>
///   <item>Zephyr applications pin Zephyr's revision in their west manifest (<c>west.yml</c>).</item>
/// </list>
/// The installed SDK's version is read from its own version file, so an SDK installed by an earlier TabStudio is
/// reported as what it is.
/// </summary>
public static partial class EmbeddedSdkRequirements
{
    /// <summary>The Pico SDK releases TabStudio pins, newest first, with their immutable commits.</summary>
    public static IReadOnlyList<(string Version, string Commit)> PicoSdkReleases { get; } =
    [
        ("2.3.1", "079c6f39023649b154152db30f1d781e884879bc"),
        ("2.3.0", "98a542c1a62fb549ffb5d66a3e5892b06276b670"),
        ("2.2.0", "a1438dff1d38bd9c65dbd693f0e5db4b9ae91779"),
        ("2.1.1", "bddd20f928ce76142793bef434d4f75f4af6e433"),
    ];

    private const int MaxFileSize = 512 * 1024;

    /// <summary>Reads what a project asks of its SDK; <see langword="null"/> when it asks nothing that can be read.</summary>
    /// <param name="project">The detected project.</param>
    public static EmbeddedSdkRequirement? Read(EmbeddedProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind switch
        {
            EmbeddedProjectKind.PicoSdk => ReadPico(project.Directory),
            EmbeddedProjectKind.EspIdf => ReadEspIdf(project.Directory),
            EmbeddedProjectKind.Zephyr => ReadZephyr(project.Directory),
            EmbeddedProjectKind.PlatformIO => ReadPlatformIO(project.Directory),
            _ => null,
        };
    }

    /// <summary>
    /// The version of an SDK installed in the environment, from the SDK's own version file
    /// (<c>pico_sdk_version.cmake</c>, ESP-IDF's <c>tools/cmake/version.cmake</c>, Zephyr's <c>VERSION</c>), or
    /// <see langword="null"/> when it is not installed or says nothing.
    /// </summary>
    /// <param name="toolchainId">The Toolchain Store entry, such as <c>pico-sdk</c>.</param>
    /// <param name="sdkDirectory">The SDK's folder as the host sees it, such as the environment's <c>/opt/tabstudio/pico-sdk</c>.</param>
    public static string? InstalledVersion(string toolchainId, string sdkDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolchainId);
        ArgumentException.ThrowIfNullOrEmpty(sdkDirectory);
        return toolchainId switch
        {
            "pico-sdk" => ThreePart(ReadText(Path.Combine(sdkDirectory, "pico_sdk_version.cmake")), "PICO_SDK_VERSION_MAJOR", "PICO_SDK_VERSION_MINOR", "PICO_SDK_VERSION_REVISION", CMakeSet),
            "esp-idf" => ThreePart(ReadText(Path.Combine(sdkDirectory, "tools", "cmake", "version.cmake")), "IDF_VERSION_MAJOR", "IDF_VERSION_MINOR", "IDF_VERSION_PATCH", CMakeSet),
            "zephyr" => ThreePart(ReadText(Path.Combine(sdkDirectory, "VERSION")), "VERSION_MAJOR", "VERSION_MINOR", "PATCHLEVEL", KeyValue),
            _ => null,
        };
    }

    /// <summary>
    /// Compares a project's requirement with the installed SDK and the version the Toolchain Store pins.
    /// </summary>
    /// <param name="requirement">What the project asks for.</param>
    /// <param name="installed">The installed version, or <see langword="null"/> when none is installed.</param>
    /// <param name="pinned">The version the Toolchain Store installs.</param>
    public static EmbeddedSdkVerdict Check(EmbeddedSdkRequirement requirement, string? installed, string? pinned)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (requirement.Minimum is not { } minimum || !TryParse(minimum, out var needed))
        {
            return new EmbeddedSdkVerdict(true, null, null);
        }

        if (installed is not null && TryParse(installed, out var have) && have >= needed)
        {
            return new EmbeddedSdkVerdict(true, null, null);
        }

        var name = requirement.SdkName;
        var current = installed is null ? $"no {name} is installed" : $"{installed} is installed";
        if (pinned is not null && TryParse(pinned, out var available) && available >= needed)
        {
            return new EmbeddedSdkVerdict(false, $"This project needs {name} ≥ {minimum} ({requirement.Source}); {current} — Install {pinned}.", pinned);
        }

        return new EmbeddedSdkVerdict(
            false,
            $"This project needs {name} ≥ {minimum} ({requirement.Source}); {current}, and the newest {name} TabStudio has verified is {pinned ?? "none"}.",
            null);
    }

    /// <summary>
    /// The requirement a failed CMake configuration states, such as pico-examples' "Raspberry Pi Pico SDK version 2.3.0
    /// (or later) required. Your version is 2.1.1", so the Output shows one clear line instead of CMake's.
    /// </summary>
    /// <param name="output">CMake's output.</param>
    /// <returns>The required and the installed version, or <see langword="null"/> when the output says neither.</returns>
    public static (string Required, string Installed)? FromCMakeOutput(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return PicoVersionError().Match(output) is { Success: true } match
            ? (match.Groups["required"].Value, match.Groups["installed"].Value)
            : null;
    }

    /// <summary>
    /// What the Output panel calls a firmware build (<c>The program 'blink' has exited</c>): the chosen target, the
    /// CMake <c>project()</c>, or the project's folder, never a build file such as <c>CMakeLists.txt</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="target">The target chosen for the build, if any.</param>
    public static string ProgramName(EmbeddedProject project, string? target = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!string.IsNullOrWhiteSpace(target))
        {
            return target.Trim();
        }

        if (project.Marker.EndsWith("CMakeLists.txt", StringComparison.OrdinalIgnoreCase)
            && ReadText(project.MarkerPath) is { } cmake
            && CMakeProject().Match(cmake) is { Success: true } named)
        {
            return named.Groups["name"].Value;
        }

        return project.Name;
    }

    private static EmbeddedSdkRequirement? ReadPico(string directory)
    {
        foreach (var file in new[] { "CMakeLists.txt", "pico_sdk_import.cmake" })
        {
            if (ReadText(Path.Combine(directory, file)) is not { } text)
            {
                continue;
            }

            if (PicoMinimum().Match(text) is { Success: true } match)
            {
                return new EmbeddedSdkRequirement("pico-sdk", "Pico SDK", Normalize(match.Groups["version"].Value), null, file + ":" + LineOf(text, match.Index));
            }
        }

        return null;
    }

    private static EmbeddedSdkRequirement? ReadEspIdf(string directory)
    {
        foreach (var manifest in new[] { "main/idf_component.yml", "idf_component.yml" })
        {
            if (ReadText(Path.Combine(directory, manifest.Replace('/', Path.DirectorySeparatorChar))) is { } text
                && IdfDependency().Match(text) is { Success: true } match
                && MinimumOf(match.Groups["spec"].Value) is { } minimum)
            {
                return new EmbeddedSdkRequirement("esp-idf", "ESP-IDF", minimum, null, manifest + ":" + LineOf(text, match.Index));
            }
        }

        if (ReadText(Path.Combine(directory, "dependencies.lock")) is { } lockText && IdfLocked().Match(lockText) is { Success: true } locked)
        {
            var version = Normalize(locked.Groups["version"].Value);
            return new EmbeddedSdkRequirement("esp-idf", "ESP-IDF", null, version, "dependencies.lock:" + LineOf(lockText, locked.Index));
        }

        return null;
    }

    private static EmbeddedSdkRequirement? ReadZephyr(string directory)
    {
        foreach (var manifest in new[] { "west.yml", "west.yaml" })
        {
            if (ReadText(Path.Combine(directory, manifest)) is { } text && ZephyrRevision().Match(text) is { Success: true } match)
            {
                var revision = match.Groups["revision"].Value.Trim('"', '\'');
                var minimum = revision.StartsWith('v') && TryParse(revision[1..], out _) ? Normalize(revision[1..]) : null;
                return new EmbeddedSdkRequirement("zephyr", "Zephyr", minimum, revision, manifest + ":" + LineOf(text, match.Groups["revision"].Index));
            }
        }

        return null;
    }

    private static EmbeddedSdkRequirement? ReadPlatformIO(string directory)
    {
        if (ReadText(Path.Combine(directory, "platformio.ini")) is not { } text || PlatformSpec().Match(text) is not { Success: true } match)
        {
            return null;
        }

        // PlatformIO installs the platform version each environment names; TabStudio only reports it.
        var spec = match.Groups["spec"].Value.Trim();
        return new EmbeddedSdkRequirement("platformio", "PlatformIO platform " + match.Groups["platform"].Value, null, spec, "platformio.ini:" + LineOf(text, match.Index));
    }

    /// <summary>The lowest version a constraint such as <c>&gt;=5.0</c>, <c>^5.1</c> or <c>~5.1.2</c> accepts.</summary>
    private static string? MinimumOf(string spec)
    {
        var first = spec.Split(',', StringSplitOptions.TrimEntries)[0].Trim('"', '\'', ' ');
        var version = first.TrimStart('>', '=', '^', '~', ' ');
        if (first.StartsWith('<'))
        {
            return null;
        }

        return TryParse(version, out _) ? Normalize(version) : null;
    }

    /// <summary>A version with three parts, as SDKs report theirs: <c>5.0</c> becomes <c>5.0.0</c>.</summary>
    private static string Normalize(string version) =>
        TryParse(version, out var parsed) ? $"{parsed.Major}.{parsed.Minor}.{Math.Max(parsed.Build, 0)}" : version;

    /// <summary>Reads a version such as <c>v2.3</c> or <c>5.4.1-dev</c> as three parts, so 2.3 and 2.3.0 compare equal.</summary>
    private static bool TryParse(string text, out Version version)
    {
        var digits = new string([.. text.Trim().TrimStart('v', 'V').TakeWhile(c => char.IsAsciiDigit(c) || c == '.')]).TrimEnd('.');
        if (digits.Length > 0 && !digits.Contains('.', StringComparison.Ordinal))
        {
            digits += ".0";
        }

        if (Version.TryParse(digits, out var parsed))
        {
            version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
            return true;
        }

        version = new Version(0, 0, 0);
        return false;
    }

    private static string? ThreePart(string? text, string major, string minor, string patch, Func<string, string, string?> read)
    {
        if (text is null || read(text, major) is not { } a || read(text, minor) is not { } b)
        {
            return null;
        }

        return $"{a}.{b}.{read(text, patch) ?? "0"}";
    }

    private static string? CMakeSet(string text, string name) =>
        Regex.Match(text, @"set\(\s*" + Regex.Escape(name) + @"\s+""?(?<value>\d+)""?\s*\)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) is { Success: true } match
            ? match.Groups["value"].Value
            : null;

    private static string? KeyValue(string text, string name) =>
        Regex.Match(text, @"^\s*" + Regex.Escape(name) + @"\s*=\s*(?<value>\d+)", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) is { Success: true } match
            ? match.Groups["value"].Value
            : null;

    private static int LineOf(string text, int index) => text.AsSpan(0, Math.Min(index, text.Length)).Count('\n') + 1;

    private static string? ReadText(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxFileSize ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"PICO_SDK_VERSION_STRING\s+VERSION_LESS\s+""?(?<version>\d+\.\d+(\.\d+)?)""?", RegexOptions.CultureInvariant)]
    private static partial Regex PicoMinimum();

    [GeneratedRegex(@"Raspberry\s+Pi\s+Pico\s+SDK\s+version\s+(?<required>\d+\.\d+(\.\d+)?)\s+\(or\s+later\)\s+required\.\s+Your\s+version\s+is\s+(?<installed>[\w.-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex PicoVersionError();

    [GeneratedRegex(@"^\s*project\(\s*(?<name>[A-Za-z0-9_.+-]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CMakeProject();

    /// <summary>An <c>idf</c> dependency in a component manifest: <c>idf: "&gt;=5.0"</c> or <c>idf:\n  version: "&gt;=5.0"</c>.</summary>
    [GeneratedRegex(@"^\s*idf\s*:\s*(?:\r?\n\s+version\s*:\s*)?(?<spec>[""']?[<>=^~]*\s*v?\d+(\.\d+){0,2}[^\r\n]*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex IdfDependency();

    /// <summary>The ESP-IDF entry of <c>dependencies.lock</c>, whose <c>version</c> is the IDF the project was built with.</summary>
    [GeneratedRegex(@"^\s{2}idf\s*:\s*\r?\n(?:\s{4}.*\r?\n)*?\s{4}version\s*:\s*(?<version>v?\d+\.\d+(\.\d+)?)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex IdfLocked();

    /// <summary>The Zephyr project of a west manifest and its revision.</summary>
    [GeneratedRegex(@"-\s*name\s*:\s*zephyr\s*\r?\n(?:\s+(?!-).*\r?\n)*?\s+revision\s*:\s*(?<revision>[^\s#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ZephyrRevision();

    /// <summary>An environment's platform with a version, such as <c>platform = espressif32@6.4.0</c>.</summary>
    [GeneratedRegex(@"^\s*platform\s*=\s*(?<platform>[A-Za-z0-9_.-]+)\s*@\s*(?<spec>[^\s;#]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PlatformSpec();
}
