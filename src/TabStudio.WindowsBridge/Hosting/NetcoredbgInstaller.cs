using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// Installs netcoredbg, Samsung's MIT-licensed debug adapter for .NET, into the bridge's tools folder from its
/// GitHub release, so the tablet's debugger can debug .NET apps on the PC. (Microsoft's vsdbg may only be used with
/// Microsoft's own tools.)
/// </summary>
public static class NetcoredbgInstaller
{
    /// <summary>The release's file for this machine, such as <c>netcoredbg-win64.zip</c>; <see langword="null"/> where there is none.</summary>
    public static string? AssetName => OperatingSystem.IsWindows() ? "netcoredbg-win64.zip"
        : OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64 ? "netcoredbg-linux-amd64.tar.gz"
        : OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "netcoredbg-linux-arm64.tar.gz"
        : OperatingSystem.IsMacOS() && RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "netcoredbg-osx-arm64.zip"
        : null;

    /// <summary>Where the release's file for this machine is downloaded from.</summary>
    public static Uri? Download => AssetName is { } asset
        ? new Uri($"https://github.com/Samsung/netcoredbg/releases/download/{BridgeProtocol.NetcoredbgVersion}/{asset}")
        : null;

    /// <summary>The installed netcoredbg in a tools folder, or <see langword="null"/>.</summary>
    /// <param name="toolsFolder">The bridge's tools folder.</param>
    public static string? Find(string toolsFolder)
    {
        ArgumentNullException.ThrowIfNull(toolsFolder);
        var path = Path.Combine(toolsFolder, "netcoredbg", OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg");
        return File.Exists(path) ? path : null;
    }

    /// <summary>Downloads and unpacks netcoredbg into a tools folder, replacing what is there.</summary>
    /// <param name="toolsFolder">The bridge's tools folder.</param>
    /// <param name="http">Downloads the release.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The installed program.</returns>
    /// <exception cref="PlatformNotSupportedException">netcoredbg has no release for this machine.</exception>
    /// <exception cref="HttpRequestException">The download failed.</exception>
    /// <exception cref="InvalidDataException">The download is not the release it should be.</exception>
    public static async Task<string> InstallAsync(string toolsFolder, HttpClient http, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolsFolder);
        ArgumentNullException.ThrowIfNull(http);
        var download = Download ?? throw new PlatformNotSupportedException("netcoredbg has no release for this kind of PC.");
        Directory.CreateDirectory(toolsFolder);
        var staging = Path.Combine(toolsFolder, "netcoredbg.download");
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        var archive = Path.Combine(staging, AssetName!);
        await using (var response = await http.GetStreamAsync(download, cancellationToken).ConfigureAwait(false))
        await using (var file = File.Create(archive))
        {
            await response.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        var unpacked = Path.Combine(staging, "unpacked");
        if (archive.EndsWith(".zip", StringComparison.Ordinal))
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(archive, unpacked, overwriteFiles: true), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Directory.CreateDirectory(unpacked);
            await using var compressed = File.OpenRead(archive);
            await using var tar = new GZipStream(compressed, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(tar, unpacked, overwriteFiles: true, cancellationToken).ConfigureAwait(false);
        }

        // The release holds a folder named netcoredbg with the program and its libraries.
        var source = Path.Combine(unpacked, "netcoredbg");
        var program = Path.Combine(source, OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg");
        if (!File.Exists(program))
        {
            throw new InvalidDataException("The netcoredbg download has no netcoredbg program in it.");
        }

        var target = Path.Combine(toolsFolder, "netcoredbg");
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.Move(source, target);
        Directory.Delete(staging, recursive: true);
        var installed = Find(toolsFolder)!;
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(installed, File.GetUnixFileMode(installed) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }

        return installed;
    }
}
