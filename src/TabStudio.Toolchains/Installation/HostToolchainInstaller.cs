using System.Text.RegularExpressions;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Installation;

/// <summary>
/// Toolchains on a desktop computer: TabStudio uses the ones installed on it, found on <c>PATH</c>, and does not
/// install its own. The desktop build exists for development and tests; the tablet is where the Store installs.
/// </summary>
public sealed class HostToolchainInstaller : IToolchainInstaller
{
    private static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Creates the installer over a runtime.</summary>
    /// <param name="runtime">The computer's runtime.</param>
    public HostToolchainInstaller(IToolchainRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    /// <inheritdoc />
    public IToolchainRuntime Runtime { get; }

    /// <inheritdoc />
    public bool CanInstall => false;

    /// <inheritdoc />
    public string? Problem => null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains, bool checkForUpdates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        var result = new List<ToolchainStatus>();
        foreach (var toolchain in toolchains)
        {
            result.Add(await ReadStatusAsync(toolchain, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    /// <inheritdoc />
    public Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        throw new NotSupportedException("On a desktop computer TabStudio uses the toolchains installed on it.");

    /// <inheritdoc />
    public Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        throw new NotSupportedException("On a desktop computer TabStudio uses the toolchains installed on it.");

    /// <inheritdoc />
    public Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        throw new NotSupportedException("On a desktop computer TabStudio uses the toolchains installed on it.");

    /// <inheritdoc />
    public DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains) => DiskUsage.None;

    private async Task<ToolchainStatus> ReadStatusAsync(ToolchainDefinition toolchain, CancellationToken cancellationToken)
    {
        var host = toolchain.Host;
        var command = host?.Commands.FirstOrDefault(Runtime.HasCommand);
        if (host is null || command is null)
        {
            return new ToolchainStatus
            {
                Toolchain = toolchain,
                State = ToolchainState.Unavailable,
                Note = $"Not found on this computer. Install {toolchain.Name}, then open the Store again.",
            };
        }

        string? version = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(VersionTimeout);
            var result = await CommandRunner.RunAsync(Runtime, new ProcessStartSpec { Program = command, Arguments = host.VersionArguments }, null, timeout.Token).ConfigureAwait(false);
            if (host.VersionPattern is { } pattern && Regex.Match(result.Output, pattern) is { Success: true } match)
            {
                version = match.Groups["version"].Value;
            }
        }
        catch (Exception ex) when (ex is ToolchainRuntimeException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Installed, but it would not say its version.
        }

        return new ToolchainStatus
        {
            Toolchain = toolchain,
            State = ToolchainState.Installed,
            Version = version,
            Note = $"Uses {command} from this computer.",
        };
    }
}
