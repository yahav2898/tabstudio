using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Installation;

/// <summary>
/// Installs toolchains, or finds them, where TabStudio runs: in its Linux environment on Android
/// (<see cref="LinuxToolchainInstaller"/>), or on a desktop computer that has its own
/// (<see cref="HostToolchainInstaller"/>).
/// </summary>
public interface IToolchainInstaller
{
    /// <summary>Where installed toolchains run their programs.</summary>
    IToolchainRuntime Runtime { get; }

    /// <summary>Whether the user can install and remove toolchains here.</summary>
    bool CanInstall { get; }

    /// <summary>Why toolchains cannot be installed or run here, or <see langword="null"/>.</summary>
    string? Problem { get; }

    /// <summary>Whether <see cref="Runtime"/> can run programs now: on Android, once the Linux environment is set up.</summary>
    bool IsRuntimeReady { get; }

    /// <summary>
    /// Sets up what <see cref="Runtime"/> needs to run programs, such as the Linux environment for the Terminal;
    /// does nothing when it is ready. Installing a toolchain does this too.
    /// </summary>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Stops the setup; the download continues next time.</param>
    /// <exception cref="ToolchainInstallException">It failed.</exception>
    Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken);

    /// <summary>Reads every toolchain's state.</summary>
    /// <param name="toolchains">The toolchains.</param>
    /// <param name="checkForUpdates">Whether to ask the mirror for newer versions, which needs the network.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains, bool checkForUpdates, CancellationToken cancellationToken);

    /// <summary>Installs a toolchain.</summary>
    /// <param name="toolchain">The toolchain.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Stops the install; downloads are kept for the next attempt.</param>
    /// <exception cref="ToolchainInstallException">It failed.</exception>
    Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken);

    /// <summary>Updates an installed toolchain to the newest versions.</summary>
    /// <param name="toolchain">The toolchain.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Stops the update.</param>
    /// <exception cref="ToolchainInstallException">It failed.</exception>
    Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken);

    /// <summary>Removes a toolchain, and what it installed that no other toolchain needs.</summary>
    /// <param name="toolchain">The toolchain.</param>
    /// <param name="installed">The other toolchains that stay installed.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Stops the removal.</param>
    /// <exception cref="ToolchainInstallException">It failed.</exception>
    Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed, IProgress<InstallProgress>? progress, CancellationToken cancellationToken);

    /// <summary>The space toolchains take.</summary>
    /// <param name="toolchains">The catalog's toolchains; the installed ones among them are measured.</param>
    DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains);
}
