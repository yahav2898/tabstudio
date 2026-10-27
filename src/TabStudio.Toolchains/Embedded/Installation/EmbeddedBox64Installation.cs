using System.Text.Json;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>Explicit, project-scoped translation of unavailable ARM64 PlatformIO host packages.</summary>
public static class EmbeddedBox64Installation
{
    /// <summary>Opt-in process marker; ordinary native PlatformIO commands never use this path.</summary>
    public const string Marker = "TABSTUDIO_PLATFORMIO_BOX64";
    /// <summary>The private translator and open-source runtime, separate from every compiler licence.</summary>
    public const string Directory = "/opt/tabstudio/box64";

    /// <summary>Runs PlatformIO's unchanged Python modules with a native-first package policy.</summary>
    /// <param name="command">A marked PlatformIO command, preserving arguments, USB descriptors and mounts.</param>
    public static ProcessStartSpec Wrap(ProcessStartSpec command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Environment.TryGetValue(Marker, out var marker) || marker != "1") return command;
        if (Path.GetFileName(command.Program) is "python3" or "python"
            && command.Arguments.Count > 0 && command.Arguments[0] == UsbBridgeInstallation.ToolsDirectory + "/serial_bridge.py")
        {
            var separator = command.Arguments.ToList().IndexOf("--");
            if (separator < 0 || separator + 1 >= command.Arguments.Count)
                throw new ArgumentException("The serial bridge is missing its PlatformIO command.", nameof(command));
            var inner = Wrap(command with
            {
                Program = command.Arguments[separator + 1], Arguments = [.. command.Arguments.Skip(separator + 2)],
            });
            return command with { Arguments = [.. command.Arguments.Take(separator + 1), inner.Program, .. inner.Arguments] };
        }
        if (Path.GetFileName(command.Program) is not ("pio" or "platformio"))
            throw new ArgumentException("Box64 host-package mode applies only to PlatformIO.", nameof(command));
        return command with
        {
            Program = "/opt/tabstudio/platformio/bin/python",
            Arguments = ["-P", UsbBridgeInstallation.ToolsDirectory + "/platformio_box64.py", "--", .. command.Arguments],
        };
    }

    /// <summary>Reuses PIC's exact open-source runtime pins and archive extractor, without installing XC or requesting its licence.</summary>
    /// <param name="rootfs">The prepared embedded glibc root.</param>
    /// <param name="platform">The native tablet architecture and bundled translator.</param>
    /// <param name="downloader">The existing resumable SHA-256 verifier.</param>
    /// <param name="runtime">The native glibc guest runtime.</param>
    /// <param name="line">Download and validation output.</param>
    /// <param name="cancellationToken">Stops preparation.</param>
    public static async Task PrepareAsync(string rootfs, ToolchainPlatform platform, Downloader downloader,
        IToolchainRuntime runtime, Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootfs);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(line);
        if (platform.Architecture is not ("aarch64" or "x86_64"))
        {
            throw new NotSupportedException("Box64 host packages require 64-bit ARM or x64.");
        }
        var tools = Path.Combine(rootfs, Directory.TrimStart('/'));
        System.IO.Directory.CreateDirectory(tools);
        if (platform.Architecture == "aarch64")
        {
            var bundled = Path.Combine(platform.NativeLibraryDirectory ?? "", "libbox64.so");
            if (!File.Exists(bundled)) throw new IOException("This APK does not contain the pinned Box64 translator.");
            var header = new byte[20];
            await using (var binary = File.OpenRead(bundled))
                await binary.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            if (!header.AsSpan(0, 7).SequenceEqual(new byte[] { 0x7f, 69, 76, 70, 2, 1, 1 })
                || header[18] != 183 || header[19] != 0)
            {
                throw new InvalidDataException("The bundled Box64 must be a little-endian ARM64 ELF executable.");
            }
            var destination = Path.Combine(tools, "box64");
            File.Copy(bundled, destination, overwrite: true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(destination,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        var support = Path.Combine(tools, "support");
        System.IO.Directory.CreateDirectory(support);
        foreach (var name in new[] { "install.py", "assets.lock.json", "packs.lock.json" })
            await File.WriteAllTextAsync(Path.Combine(support, name), MicrochipInstallation.Resource(name), cancellationToken).ConfigureAwait(false);
        using var pins = JsonDocument.Parse(MicrochipInstallation.Resource("assets.lock.json"));
        var assets = pins.RootElement.GetProperty("assets");
        foreach (var architecture in platform.Architecture == "aarch64" ? new[] { "amd64", "arm64" } : new[] { "amd64" })
        {
            foreach (var package in new[] { "libc6", "libstdc++6", "libgcc-s1", "zlib1g" })
            {
                var name = package + "-" + architecture;
                var asset = assets.GetProperty(name);
                var destination = Path.Combine(tools, "downloads", name + ".deb");
                line("Verifying Box64 runtime " + name);
                await downloader.DownloadAsync(new DownloadRequest
                {
                    Address = new Uri(asset.GetProperty("url").GetString()!), Destination = destination,
                    Sha256 = asset.GetProperty("sha256").GetString(), Size = asset.GetProperty("size").GetInt64(), Name = name,
                }, null, cancellationToken).ConfigureAwait(false);
            }
        }
        var result = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
        {
            Program = "python3", Arguments = [UsbBridgeInstallation.ToolsDirectory + "/box64_runtime.py", "prepare"],
        }, line, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || !result.Output.Contains("BOX64_RUNTIME_PREPARED", StringComparison.Ordinal))
        {
            throw new IOException("The explicit Box64 runtime could not be validated: " + result.Tail());
        }
    }
}
