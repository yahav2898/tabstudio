using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Linux;

namespace TabStudio.WindowsCompatibility.Sessions;

/// <summary>
/// On a desktop computer, where Wine and Xvfb are the computer's own, gets the rest of Windows Compatibility from
/// the catalog the Toolchain Store installs from: the .NET runtime for Windows, downloaded and checked against its
/// pinned hash, into TabStudio's data folder.
/// </summary>
/// <param name="downloader">Downloads with resume and integrity checks.</param>
public sealed class HostRuntimeInstaller(Downloader downloader)
{
    /// <summary>Downloads and unpacks the archives a layout lacks, from a toolchain's setup steps for this computer's processor.</summary>
    /// <param name="layout">Where the parts go: a desktop layout.</param>
    /// <param name="toolchain">The Windows Compatibility toolchain of the catalog.</param>
    /// <param name="progress">Receives what happens.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task EnsureAsync(WindowsLayout layout, ToolchainDefinition toolchain, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(toolchain);
        if (layout.IsComplete)
        {
            return;
        }

        var archives = toolchain.ForArchitecture(ToolchainPlatform.CurrentArchitecture).Setup
            .Where(step => step.Archive is { } archive && archive.Destination.StartsWith(WindowsLayout.LinuxRoot + "/", StringComparison.Ordinal))
            .ToList();
        foreach (var step in archives)
        {
            var archive = step.Archive!;
            var destination = layout.Root + archive.Destination[WindowsLayout.LinuxRoot.Length..];
            if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
            {
                continue;
            }

            progress?.Report($"Downloading {step.Name}");
            var file = Path.Combine(layout.Root, "downloads", Path.GetFileName(archive.Url.AbsolutePath));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await downloader.DownloadAsync(
                new DownloadRequest { Address = archive.Url, Destination = file, Name = step.Name, Size = archive.Size, Sha256 = archive.Sha256 },
                null,
                cancellationToken).ConfigureAwait(false);
            progress?.Report($"Unpacking {step.Name}");
            await ArchiveExtractor.ExtractAsync(file, destination, cancellationToken).ConfigureAwait(false);
            File.Delete(file);
        }

        layout.EnsureDesktopFramework();
    }
}
