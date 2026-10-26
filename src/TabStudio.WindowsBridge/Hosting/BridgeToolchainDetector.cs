using System.Diagnostics;
using System.Text.Json;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>Where the detector looks for tools; the PC's own folders by default, a test's folders in tests.</summary>
/// <param name="ProgramFiles">The <c>Program Files</c> folder.</param>
/// <param name="ProgramFilesX86">The <c>Program Files (x86)</c> folder, where vswhere, the Windows SDK and the targeting packs are.</param>
/// <param name="NuGetPackages">NuGet's global packages folder.</param>
/// <param name="ToolsFolder">The bridge's own tools, such as netcoredbg.</param>
public sealed record BridgeToolchainLocations(string ProgramFiles, string ProgramFilesX86, string NuGetPackages, string ToolsFolder)
{
    /// <summary>The PC's own folders.</summary>
    /// <param name="toolsFolder">The bridge's own tools folder.</param>
    public static BridgeToolchainLocations ForThisMachine(string toolsFolder)
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new BridgeToolchainLocations(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } packages ? packages : Path.Combine(user, ".nuget", "packages"),
            toolsFolder);
    }
}

/// <summary>What the detector found: the tools to show, what builds need, and what the PC can build.</summary>
/// <param name="Toolchains">Every SDK and tool found, for the bridge's window and the tablet.</param>
/// <param name="Tools">The tools builds use.</param>
/// <param name="Capabilities">What the PC can build and debug.</param>
public sealed record BridgeToolchainReport(IReadOnlyList<BridgeToolchain> Toolchains, BridgeToolset Tools, BridgeCapabilities Capabilities);

/// <summary>
/// Finds the PC's build tools: the .NET SDKs (<c>dotnet --list-sdks</c>), Visual Studio and its Build Tools with
/// MSBuild and the MSVC toolset (vswhere), the Windows SDK, the Windows App SDK, the .NET Framework targeting packs,
/// and netcoredbg.
/// </summary>
public static class BridgeToolchainDetector
{
    /// <summary>How long one tool may take to answer.</summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Looks for every tool.</summary>
    /// <param name="locations">Where to look.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public static async Task<BridgeToolchainReport> DetectAsync(BridgeToolchainLocations locations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(locations);
        var toolchains = new List<BridgeToolchain>();

        var dotnet = FindDotNet(locations);
        if (dotnet is not null)
        {
            var sdks = ParseSdks(await RunAsync(dotnet, ["--list-sdks"], cancellationToken).ConfigureAwait(false));
            toolchains.AddRange(sdks.Select(s => new BridgeToolchain(BridgeToolchainKind.DotNetSdk, $".NET SDK {s.Version}", s.Version, s.Location)));
            if (sdks.Count == 0)
            {
                dotnet = null;
            }
        }

        string? msbuild = null;
        string? msbuildVersion = null;
        var hasMsvc = false;
        var vswhere = Path.Combine(locations.ProgramFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (File.Exists(vswhere))
        {
            var instances = ParseVisualStudio(await RunAsync(vswhere, ["-products", "*", "-prerelease", "-format", "json", "-utf8", "-requires", "Microsoft.Component.MSBuild"], cancellationToken).ConfigureAwait(false));
            var withCpp = ParseVisualStudio(await RunAsync(vswhere, ["-products", "*", "-prerelease", "-format", "json", "-utf8", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64"], cancellationToken).ConfigureAwait(false))
                .Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // The newest installation with the C++ tools builds, as it can build everything.
            var ordered = instances
                .OrderByDescending(i => withCpp.Contains(i.Path))
                .ThenByDescending(i => SortableVersion(i.Version));
            foreach (var instance in ordered)
            {
                toolchains.Add(new BridgeToolchain(BridgeToolchainKind.VisualStudio, instance.Name, instance.Version, instance.Path));
                if (msbuild is null && FindMsBuild(instance.Path) is { } candidate)
                {
                    msbuild = candidate;
                    msbuildVersion = FileVersion(candidate) ?? instance.Version;
                    hasMsvc = withCpp.Contains(instance.Path);
                    toolchains.Add(new BridgeToolchain(BridgeToolchainKind.MSBuild, $"MSBuild {msbuildVersion}", msbuildVersion, Path.GetDirectoryName(candidate)));
                }

                if (withCpp.Contains(instance.Path) && LatestFolder(Path.Combine(instance.Path, "VC", "Tools", "MSVC")) is { } msvc)
                {
                    toolchains.Add(new BridgeToolchain(BridgeToolchainKind.Msvc, $"MSVC {msvc} ({instance.Name})", msvc, Path.Combine(instance.Path, "VC", "Tools", "MSVC", msvc)));
                }
            }
        }

        var windowsSdk = WindowsSdkVersions(locations);
        toolchains.AddRange(windowsSdk.Take(3).Select(v => new BridgeToolchain(BridgeToolchainKind.WindowsSdk, $"Windows SDK {v}", v, Path.Combine(locations.ProgramFilesX86, "Windows Kits", "10"))));

        var appRuntime = OperatingSystem.IsWindows() ? await WindowsAppRuntimeVersionsAsync(cancellationToken).ConfigureAwait(false) : [];
        toolchains.AddRange(appRuntime.Select(v => new BridgeToolchain(BridgeToolchainKind.WindowsAppSdk, $"Windows App Runtime {v}", v, null)));
        if (LatestFolder(Path.Combine(locations.NuGetPackages, "microsoft.windowsappsdk")) is { } package)
        {
            toolchains.Add(new BridgeToolchain(BridgeToolchainKind.WindowsAppSdk, $"Windows App SDK {package} (NuGet)", package, Path.Combine(locations.NuGetPackages, "microsoft.windowsappsdk", package)));
        }

        var frameworks = NetFrameworkPacks(locations);
        toolchains.AddRange(frameworks.Select(v => new BridgeToolchain(BridgeToolchainKind.NetFramework, $".NET Framework {v} targeting pack", v, null)));

        var netcoredbg = NetcoredbgInstaller.Find(locations.ToolsFolder);
        if (netcoredbg is not null)
        {
            toolchains.Add(new BridgeToolchain(BridgeToolchainKind.Debugger, $"netcoredbg {BridgeProtocol.NetcoredbgVersion}", BridgeProtocol.NetcoredbgVersion, Path.GetDirectoryName(netcoredbg)));
        }

        var mplab = MplabTools.Find([locations.ProgramFiles, locations.ProgramFilesX86]);
        if (mplab is not null) toolchains.Add(new BridgeToolchain(BridgeToolchainKind.MplabX, "MPLAB X / IPE", Path.GetFileName(mplab.Root), mplab.Root));
        var tools = new BridgeToolset(dotnet, msbuild, msbuildVersion, hasMsvc && msbuild is not null, windowsSdk.Count > 0, appRuntime.Count > 0, frameworks.Count > 0, netcoredbg) { Mplab = mplab };
        return new BridgeToolchainReport(toolchains, tools, CapabilitiesOf(tools));
    }

    /// <summary>What a set of tools can build and debug.</summary>
    /// <param name="tools">The tools.</param>
    public static BridgeCapabilities CapabilitiesOf(BridgeToolset tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var capabilities = BridgeCapabilities.None;
        if (tools.Mplab?.Make is not null) capabilities |= BridgeCapabilities.MplabX;
        if (tools.Mplab?.Ipe is not null) capabilities |= BridgeCapabilities.PicProgramming;
        if (tools.DotNet is not null)
        {
            capabilities |= BridgeCapabilities.DotNet;
            if (OperatingSystem.IsWindows())
            {
                capabilities |= BridgeCapabilities.WindowsDesktop;
            }
        }

        if (tools.MsBuild is not null)
        {
            capabilities |= BridgeCapabilities.MSBuild;
            if (tools.HasMsvc && tools.HasWindowsSdk)
            {
                capabilities |= BridgeCapabilities.Cpp;
            }

            if (tools.HasNetFrameworkPack)
            {
                capabilities |= BridgeCapabilities.NetFramework;
            }
        }

        if (tools.HasWindowsSdk && (tools.MsBuild is not null || tools.DotNet is not null))
        {
            capabilities |= BridgeCapabilities.WinUI;
        }

        if (tools.Netcoredbg is not null && tools.DotNet is not null)
        {
            capabilities |= BridgeCapabilities.DebugDotNet;
        }

        return capabilities;
    }

    /// <summary>Reads <c>dotnet --list-sdks</c>: lines such as <c>10.0.100 [C:\Program Files\dotnet\sdk]</c>, the newest first.</summary>
    /// <param name="output">The command's output.</param>
    public static IReadOnlyList<(string Version, string? Location)> ParseSdks(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var sdks = new List<(string Version, string? Location)>();
        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var version = space < 0 ? line : line[..space];
            if (version.Length == 0 || !char.IsAsciiDigit(version[0]))
            {
                continue;
            }

            var open = line.IndexOf('[', StringComparison.Ordinal);
            var close = line.LastIndexOf(']');
            var location = open >= 0 && close > open ? Path.Combine(line[(open + 1)..close], version) : null;
            sdks.Add((version, location));
        }

        return [.. sdks.OrderByDescending(s => SortableVersion(s.Version))];
    }

    /// <summary>Reads vswhere's JSON: each instance's display name, version and folder.</summary>
    /// <param name="json">vswhere's output with <c>-format json</c>.</param>
    public static IReadOnlyList<(string Name, string Version, string Path)> ParseVisualStudio(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var instances = new List<(string, string, string)>();
        try
        {
            using var document = JsonDocument.Parse(json.Length == 0 ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return instances;
            }

            foreach (var instance in document.RootElement.EnumerateArray())
            {
                var path = Text(instance, "installationPath");
                if (path is null)
                {
                    continue;
                }

                var name = Text(instance, "displayName") ?? "Visual Studio";
                var version = instance.TryGetProperty("catalog", out var catalog) && Text(catalog, "productDisplayVersion") is { } display
                    ? display
                    : Text(instance, "installationVersion") ?? string.Empty;
                instances.Add((name, version, path));
            }
        }
        catch (JsonException)
        {
            // vswhere failed; there is no Visual Studio to use.
        }

        return instances;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? FindDotNet(BridgeToolchainLocations locations)
    {
        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var candidates = new List<string>();
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root)
        {
            candidates.Add(Path.Combine(root, name));
        }

        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim('"'), name)));
        candidates.Add(Path.Combine(locations.ProgramFiles, "dotnet", name));
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindMsBuild(string installation)
    {
        string[] candidates =
        [
            Path.Combine(installation, "MSBuild", "Current", "Bin", "amd64", "MSBuild.exe"),
            Path.Combine(installation, "MSBuild", "Current", "Bin", "MSBuild.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion is { Length: > 0 } version ? version.Split(' ')[0] : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The newest version-named folder in a folder, such as <c>14.44.35207</c> in MSVC's tools.</summary>
    private static string? LatestFolder(string folder)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetDirectories(folder).Select(Path.GetFileName).OfType<string>().Where(n => n.Length > 0 && char.IsAsciiDigit(n[0])).OrderByDescending(SortableVersion).FirstOrDefault()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static List<string> WindowsSdkVersions(BridgeToolchainLocations locations)
    {
        var include = Path.Combine(locations.ProgramFilesX86, "Windows Kits", "10", "Include");
        try
        {
            return Directory.Exists(include)
                ? Directory.GetDirectories(include)
                    .Where(d => File.Exists(Path.Combine(d, "um", "windows.h")))
                    .Select(Path.GetFileName).OfType<string>()
                    .OrderByDescending(SortableVersion)
                    .ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static List<string> NetFrameworkPacks(BridgeToolchainLocations locations)
    {
        var root = Path.Combine(locations.ProgramFilesX86, "Reference Assemblies", "Microsoft", "Framework", ".NETFramework");
        try
        {
            return Directory.Exists(root)
                ? Directory.GetDirectories(root, "v4.*")
                    .Where(d => File.Exists(Path.Combine(d, "mscorlib.dll")))
                    .Select(d => Path.GetFileName(d)[1..])
                    .OrderByDescending(SortableVersion)
                    .ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The installed Windows App SDK runtimes, from Windows' package list, such as <c>1.7</c>.</summary>
    private static async Task<List<string>> WindowsAppRuntimeVersionsAsync(CancellationToken cancellationToken)
    {
        var output = await RunAsync(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", "Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.*' | ForEach-Object { $_.Name }"],
            cancellationToken).ConfigureAwait(false);
        return output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(n => n.StartsWith("Microsoft.WindowsAppRuntime.", StringComparison.Ordinal))
            .Select(n => n["Microsoft.WindowsAppRuntime.".Length..])
            .Where(v => v.Length > 0 && char.IsAsciiDigit(v[0]))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(SortableVersion)
            .ToList();
    }

    /// <summary>Runs a tool and returns its standard output, or nothing when it fails or takes too long.</summary>
    private static async Task<string> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        BridgeProcess process;
        try
        {
            process = BridgeProcess.Start(new BridgeProcessStart(fileName, arguments, Path.GetTempPath()));
        }
        catch (BridgeProcessException)
        {
            return string.Empty;
        }

        using (process)
        {
            var output = new System.Text.StringBuilder();
            process.OutputLine += (_, line) =>
            {
                lock (output)
                {
                    output.AppendLine(line);
                }
            };
            process.BeginReading();
            try
            {
                await process.Exited.WaitAsync(ProbeTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return string.Empty;
            }

            lock (output)
            {
                return output.ToString();
            }
        }
    }

    private static Version SortableVersion(string version)
    {
        var numeric = new string(version.TakeWhile(c => char.IsAsciiDigit(c) || c == '.').ToArray()).Trim('.');
        return Version.TryParse(numeric.Contains('.', StringComparison.Ordinal) ? numeric : numeric + ".0", out var parsed) ? parsed : new Version();
    }
}
