using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;
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
    public async Task InstallAsync(string id, CancellationToken cancellationToken = default)
    {
        // A toolchain that builds on others, as Kotlin builds on Java's debugger, gets them first.
        foreach (var required in RequiredBy(id).Where(r => !Status(r).IsReady))
        {
            await RunAsync(required, ToolchainState.Installing, (toolchain, progress) => _installer.InstallAsync(toolchain, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        await RunAsync(id, ToolchainState.Installing, (toolchain, progress) => _installer.InstallAsync(toolchain, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The toolchains a toolchain requires (<see cref="ToolchainDefinition.Requires"/>), those they require first, in
    /// the order to install them.
    /// </summary>
    /// <param name="id">The toolchain's id.</param>
    public IReadOnlyList<string> RequiredBy(string id)
    {
        var result = new List<string>();
        void Add(string current, HashSet<string> path)
        {
            foreach (var required in Catalog.Find(current)?.Requires ?? [])
            {
                if (path.Add(required))
                {
                    Add(required, path);
                    path.Remove(required);
                    if (!result.Contains(required, StringComparer.Ordinal) && required != id)
                    {
                        result.Add(required);
                    }
                }
            }
        }

        Add(id, new HashSet<string>(StringComparer.Ordinal) { id });
        return result;
    }

    /// <summary>Installs several toolchains serially, measuring the shared disk usage once at the end.</summary>
    /// <param name="ids">The toolchain ids; each distinct id is installed once.</param>
    /// <param name="cancellationToken">Stops the batch; completed installs and resumable downloads are kept.</param>
    public async Task InstallManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var pending = ids.SelectMany(id => RequiredBy(id).Where(r => !Status(r).IsReady).Append(id)).Distinct(StringComparer.Ordinal).ToList();
        foreach (var id in pending)
        {
            _ = Status(id); // Validate the complete request before changing any toolchain.
        }

        if (pending.Count == 0)
        {
            return;
        }

        var refreshMetadata = true;
        try
        {
            foreach (var id in pending)
            {
                await RunAsync(id, ToolchainState.Installing,
                    (toolchain, progress) => _installer.InstallAsync(toolchain, progress, refreshMetadata, cancellationToken),
                    cancellationToken, measureDiskUsage: false).ConfigureAwait(false);
                // A batch shares its first verified repository snapshot. A new
                // batch or an independent install refreshes metadata again.
                refreshMetadata = false;
            }
        }
        finally
        {
            // A full recursive scan is expensive after adding several SDKs. Each installed status is still
            // published immediately; refresh the shared total once, including any completed partial batch.
            await MeasureDiskUsageAsync().ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

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

    /// <summary>Serializes optional runtime support installation with Store package operations.</summary>
    /// <param name="configure">Installs verified support in the prepared runtime.</param>
    /// <param name="cancellationToken">Cancels setup.</param>
    public async Task ConfigureRuntimeAsync(Func<CancellationToken, Task> configure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configure);
        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _installer.PrepareRuntimeAsync(null, cancellationToken).ConfigureAwait(false);
            await configure(cancellationToken).ConfigureAwait(false);
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

    private async Task RunAsync(string id, ToolchainState state, Func<ToolchainDefinition, IProgress<InstallProgress>, Task> operation, CancellationToken cancellationToken, bool measureDiskUsage = true)
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ToolchainRuntimeException or InvalidDataException)
        {
            // Anything else left the toolchain "Installing" until the app restarted, with Install hidden and Cancel
            // doing nothing, and reached the Store's button as an error that ended the app.
            var failure = ToolchainInstallException.FromUnexpected(toolchain.Name, ex);
            Publish(before with { Error = failure.Message, Progress = null });
            throw failure;
        }
        finally
        {
            _operations.Release();
        }

        // The operation is over: read what is installed now, without asking the mirror again.
        var statuses = await _installer.ReadStatusAsync([toolchain], checkForUpdates: false, CancellationToken.None).ConfigureAwait(false);
        Publish(statuses[0]);
        if (measureDiskUsage)
        {
            await MeasureDiskUsageAsync().ConfigureAwait(false);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task MeasureDiskUsageAsync()
    {
        var usage = await Task.Run(() => _installer.MeasureDiskUsage(Catalog.Toolchains), CancellationToken.None).ConfigureAwait(false);
        lock (_gate)
        {
            _diskUsage = usage;
        }
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
