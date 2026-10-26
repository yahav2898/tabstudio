using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Installation;

/// <summary>
/// The toolchains on this device, for the Toolchain Store and the IDE: their states, and installing, updating
/// and removing them one at a time.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is installed unless the user asks: <see cref="InstallAsync"/> runs only from the Store's Install
/// button or from a prompt the user answered. Operations run one after another; one started while another runs
/// waits, and its toolchain says so.
/// </para>
/// <para>
/// <see cref="StatusChanged"/> is raised on the thread that changed the status, often a background thread;
/// the IDE moves it to its UI thread.
/// </para>
/// </remarks>
public sealed class ToolchainManager : IDisposable
{
    private readonly IToolchainInstaller _installer;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ToolchainStatus> _statuses = new(StringComparer.Ordinal);
    private DiskUsage _diskUsage = DiskUsage.None;

    /// <summary>Creates the manager.</summary>
    /// <param name="catalog">The toolchain catalog.</param>
    /// <param name="installer">Installs toolchains here, or finds them.</param>
    public ToolchainManager(ToolchainCatalog catalog, IToolchainInstaller installer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(installer);
        Catalog = catalog;
        _installer = installer;
        foreach (var toolchain in catalog.Toolchains)
        {
            _statuses[toolchain.Id] = new ToolchainStatus
            {
                Toolchain = toolchain,
                State = installer.CanInstall ? ToolchainState.NotInstalled : ToolchainState.Unavailable,
                Note = installer.Problem,
                DownloadSize = toolchain.EstimatedDownloadSize,
                InstallSize = toolchain.EstimatedInstalledSize,
            };
        }
    }

    /// <summary>Raised when a toolchain's status changes, with its new status.</summary>
    public event EventHandler<ToolchainStatus>? StatusChanged;

    /// <summary>Raised after an operation changed what is installed, and after a refresh.</summary>
    public event EventHandler? Changed;

    /// <summary>The catalog.</summary>
    public ToolchainCatalog Catalog { get; }

    /// <summary>Where toolchains run their programs.</summary>
    public IToolchainRuntime Runtime => _installer.Runtime;

    /// <summary>Whether toolchains can be installed here: on Android, with the Linux runtime in the app.</summary>
    public bool CanInstall => _installer.CanInstall;

    /// <summary>Why toolchains cannot be installed or run here, or <see langword="null"/>.</summary>
    public string? Problem => _installer.Problem;

    /// <summary>Whether <see cref="Runtime"/> can run programs now: on Android, once the Linux environment is set up.</summary>
    public bool IsRuntimeReady => _installer.IsRuntimeReady;

    /// <summary>The space toolchains take, as of the last refresh.</summary>
    public DiskUsage DiskUsage
    {
        get
        {
            lock (_gate)
            {
                return _diskUsage;
            }
        }
    }

    /// <summary>Every toolchain's status, in the catalog's order.</summary>
    public IReadOnlyList<ToolchainStatus> Statuses
    {
        get
        {
            lock (_gate)
            {
                return [.. Catalog.Toolchains.Select(t => _statuses[t.Id])];
            }
        }
    }

    /// <summary>A toolchain's status.</summary>
    /// <param name="id">The toolchain's id.</param>
    /// <exception cref="ArgumentException">The catalog has no such toolchain.</exception>
    public ToolchainStatus Status(string id)
    {
        lock (_gate)
        {
            return _statuses.TryGetValue(id, out var status) ? status : throw new ArgumentException($"The catalog has no toolchain '{id}'.", nameof(id));
        }
    }

    /// <summary>Whether a toolchain can run programs now.</summary>
    /// <param name="id">The toolchain's id.</param>
    public bool IsReady(string id) => Status(id).IsReady;

    /// <summary>Reads every toolchain's state and the disk usage again.</summary>
    /// <param name="checkForUpdates">Whether to ask the mirror for newer versions.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    public async Task RefreshAsync(bool checkForUpdates = false, CancellationToken cancellationToken = default)
    {
        var statuses = await _installer.ReadStatusAsync(Catalog.Toolchains, checkForUpdates, cancellationToken).ConfigureAwait(false);
        var usage = await Task.Run(() => _installer.MeasureDiskUsage(Catalog.Toolchains), cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _diskUsage = usage;
        }

        foreach (var status in statuses)
        {
            // An operation in progress owns its toolchain's status until it ends.
            if (!Status(status.Toolchain.Id).IsBusy)
            {
                Publish(status);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Installs a toolchain.</summary>
    /// <param name="id">The toolchain's id.</param>
    /// <param name="cancellationToken">Stops the install; downloads are kept for the next attempt.</param>
    /// <exception cref="ToolchainInstallException">It failed; the status's <see cref="ToolchainStatus.Error"/> says why too.</exception>
    public Task InstallAsync(string id, CancellationToken cancellationToken = default) =>
        RunAsync(id, ToolchainState.Installing, (toolchain, progress) => _installer.InstallAsync(toolchain, progress, cancellationToken), cancellationToken);

    /// <summary>Updates a toolchain.</summary>
    /// <param name="id">The toolchain's id.</param>
    /// <param name="cancellationToken">Stops the update.</param>
    /// <exception cref="ToolchainInstallException">It failed.</exception>
    public Task UpdateAsync(string id, CancellationToken cancellationToken = default) =>
        RunAsync(id, ToolchainState.Updating, (toolchain, progress) => _installer.UpdateAsync(toolchain, progress, cancellationToken), cancellationToken);

    /// <summary>Removes a toolchain.</summary>
    /// <param name="id">The toolchain's id.</param>
    /// <param name="cancellationToken">Stops the removal.</param>
    /// <exception cref="ToolchainInstallException">It failed.</exception>
    public Task RemoveAsync(string id, CancellationToken cancellationToken = default) =>
        RunAsync(
            id,
            ToolchainState.Removing,
            (toolchain, progress) => _installer.RemoveAsync(toolchain, [.. Statuses.Where(s => s.IsReady).Select(s => s.Toolchain)], progress, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Sets up what <see cref="Runtime"/> needs to run programs, such as the Linux environment the Terminal runs
    /// in, after any operation in progress; does nothing when it is ready.
    /// </summary>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Stops the setup; the download continues next time.</param>
    /// <exception cref="ToolchainInstallException">It failed.</exception>
    public async Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsRuntimeReady)
        {
            return;
        }

        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _installer.PrepareRuntimeAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Serializes an additional package operation with Toolchain Store installs and removals.</summary>
    /// <param name="operation">The operation using the runtime's package database.</param>
    /// <param name="cancellationToken">Cancels waiting for other installers.</param>
    public async Task WithRuntimeOperationAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await operation().ConfigureAwait(false); }
        finally { _operations.Release(); }
    }

    /// <inheritdoc />
    public void Dispose() => _operations.Dispose();

    private async Task RunAsync(string id, ToolchainState state, Func<ToolchainDefinition, IProgress<InstallProgress>, Task> operation, CancellationToken cancellationToken)
    {
        var before = Status(id);
        if (before.IsBusy)
        {
            return;
        }

        var toolchain = before.Toolchain;
        Publish(before with { State = state, Error = null, Progress = new InstallProgress(InstallStage.Preparing, "Waiting for the other toolchain to finish", null) });
        try
        {
            await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Publish(before with { Error = null, Progress = null });
            throw;
        }

        try
        {
            var progress = new Reporter(p => Publish(Status(id) with { State = state, Progress = p }));
            await operation(toolchain, progress).ConfigureAwait(false);
        }
        catch (ToolchainInstallException ex)
        {
            Publish(before with { Error = ex.Message, Progress = null });
            throw;
        }
        catch (OperationCanceledException)
        {
            Publish(before with { Error = null, Progress = null });
            throw;
        }
        finally
        {
            _operations.Release();
        }

        // The operation is over: read what is installed now, without asking the mirror again.
        var statuses = await _installer.ReadStatusAsync([toolchain], checkForUpdates: false, CancellationToken.None).ConfigureAwait(false);
        Publish(statuses[0]);
        var usage = await Task.Run(() => _installer.MeasureDiskUsage(Catalog.Toolchains), CancellationToken.None).ConfigureAwait(false);
        lock (_gate)
        {
            _diskUsage = usage;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Publish(ToolchainStatus status)
    {
        lock (_gate)
        {
            _statuses[status.Toolchain.Id] = status;
        }

        StatusChanged?.Invoke(this, status);
    }

    /// <summary>Reports progress on the reporting thread; the IDE posts it to its UI thread.</summary>
    private sealed class Reporter(Action<InstallProgress> report) : IProgress<InstallProgress>
    {
        public void Report(InstallProgress value) => report(value);
    }
}
