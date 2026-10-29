using System.Text.Json;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Debugging;
using TabStudio.Debugging.Building;
using TabStudio.Debugging.Symbols;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Swarm.Protocol;
using TabStudio.Swarm.Web;

namespace TabStudio.Swarm.Device;

/// <summary>
/// The app on this device: the builds the tablet sends, kept in the cache by hash, each replacing the one before, as
/// a save on the tablet restarts it, and how it ends.
/// </summary>
public sealed partial class SwarmMember
{
    /// <summary>Runs start and stop one at a time.</summary>
    private readonly SemaphoreSlim _runs = new(1, 1);
    private LocalRun? _app;
    private SwarmDeviceState _appState = SwarmDeviceState.Ready;
    private SwarmAppKind? _appKind;
    private string? _appName;
    private string? _appDetail;
    private long _appRun;

    /// <summary>What the device does with the app the tablet sent.</summary>
    public SwarmDeviceState AppState
    {
        get
        {
            lock (_gate)
            {
                return _appState;
            }
        }
    }

    /// <summary>The app the device runs, or ran last.</summary>
    public string? AppName
    {
        get
        {
            lock (_gate)
            {
                return _appName;
            }
        }
    }

    /// <summary>How the device runs the app, while one runs.</summary>
    public SwarmAppKind? AppKind
    {
        get
        {
            lock (_gate)
            {
                return _appKind;
            }
        }
    }

    /// <summary>More about <see cref="AppState"/>, such as why the app could not start.</summary>
    public string? AppDetail
    {
        get
        {
            lock (_gate)
            {
                return _appDetail;
            }
        }
    }

    /// <summary>Whether the tablet debugs the app on this device.</summary>
    public bool IsDebugging
    {
        get
        {
            lock (_gate)
            {
                return _app?.Debugger is not null;
            }
        }
    }

    /// <summary>The files of the build this device lacks.</summary>
    private SwarmPrepareResponse Prepare(SwarmPrepareRequest request) =>
        new([.. request.Files.Where(f => !_cache.Has(f.Hash)).Select(f => f.Hash).Distinct(StringComparer.Ordinal)]);

    /// <summary>Keeps the files of a build, then starts it in place of the app that runs, answering once it starts.</summary>
    /// <exception cref="LinkRemoteException">A file arrived damaged or is missing.</exception>
    private async Task RunAsync(SwarmRunRequest request, IReadOnlyList<byte[]> attachments, CancellationToken cancellationToken)
    {
        await _runs.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!request.Restart && Current() is { HasEnded: false } same && same.Id == request.Run)
            {
                // The tablet sends the run again when the device reconnects: the app keeps running, with today's breakpoints.
                if (request.Debug is { } debug)
                {
                    same.Debugger?.SetBreakpoints(debug.Breakpoints);
                }

                ReportState();
                return;
            }

            SetAppState(request.Run, SwarmDeviceState.Deploying, request.AppName, null, request.Kind);
            CompiledAssembly entry;
            List<CompiledAssembly> dependencies;
            List<string> packages;
            try
            {
                Store(request, attachments);
                entry = Load(request.Entry);
                dependencies = [.. request.Dependencies.Select(Load)];
                packages = [.. request.Packages.Select(p => _cache.PackagePath(p.Hash, p.FileName))];
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                SetAppState(request.Run, SwarmDeviceState.Failed, request.AppName, ex.Message, request.Kind);
                throw new LinkRemoteException(LinkErrorCodes.Failed, ex.Message);
            }

            await StopAppAsync().ConfigureAwait(false);
            var local = new LocalRun(request.Run, request.AppName, request.Kind);
            DebugSession? session = null;
            if (request.Debug is { } setup && request.Kind != SwarmAppKind.Web)
            {
                var build = new DebugBuild(new BuildResult(request.AppName, [], entry, dependencies, TimeSpan.Zero), DebugInfoMap.Create(setup.Statements, setup.Methods));
                session = new DebugSession(build) { BreakOnUnhandledExceptions = setup.BreakOnUnhandledExceptions };
                local.Debugger = new SwarmDebugAgent(session, request.Run, NotifyTabletAsync, state => SetAppState(request.Run, state, request.AppName, null, request.Kind));
                local.Debugger.SetBreakpoints(setup.Breakpoints);
                if (setup.PauseAtStart)
                {
                    session.Pause();
                }
            }

            lock (_gate)
            {
                _app = local;
            }

            SetAppState(request.Run, SwarmDeviceState.Starting, request.AppName, null, request.Kind);
            _ = Task.Run(() => ShowAsync(local, request, entry, dependencies, packages, session), CancellationToken.None);
            _cache.Prune(InUse(request));
        }
        finally
        {
            _runs.Release();
        }
    }

    /// <summary>Stops the app; the device waits for the next build.</summary>
    private async Task StopRunAsync(long run)
    {
        await _runs.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Current() is { } current && (run == 0 || current.Id == run))
            {
                await StopAppAsync().ConfigureAwait(false);
                SetAppState(0, SwarmDeviceState.Ready, null, null, null);
            }
        }
        finally
        {
            _runs.Release();
        }
    }

    /// <summary>Starts the app and reports how it ends. Runs in the background, so a pause at its start answers nothing late.</summary>
    private async Task ShowAsync(LocalRun local, SwarmRunRequest request, CompiledAssembly entry, List<CompiledAssembly> dependencies, List<string> packages, DebugSession? session)
    {
        RunResult result;
        try
        {
            var run = await StartAsync(local, request, entry, dependencies, packages, session).ConfigureAwait(false);
            local.Run = run;
            if (local.IsStopping)
            {
                // Stopped while it started; disposing twice does nothing.
                await run.DisposeAsync().ConfigureAwait(false);
                return;
            }

            lock (_gate)
            {
                if (ReferenceEquals(_app, local) && _appState == SwarmDeviceState.Starting)
                {
                    _appState = SwarmDeviceState.Running;
                }
            }

            ReportState();
            result = await run.Completion.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // XAML that does not load, or an exception in the app's startup code.
            result = new RunResult(RunOutcome.Crashed, -1, TimeSpan.Zero, ex);
        }

        local.HasEnded = true;
        session?.EndHostedRun(result);
        if (local.IsStopping || Current() != local)
        {
            return;
        }

        var fault = result.Exception is { } exception ? SwarmFault.From(exception) : null;
        var state = result.Outcome == RunOutcome.Crashed ? SwarmDeviceState.Crashed : SwarmDeviceState.Exited;

        // The exit, with the exception and its stack trace, reaches the tablet before the state that says it crashed,
        // so a device shown as crashed always has its fault to show.
        await NotifyTabletAsync(SwarmMethods.Exited, SwarmJson.Body(new SwarmExitNotice(local.Id, result.Outcome, result.ExitCode, fault), SwarmJsonContext.Default.SwarmExitNotice)).ConfigureAwait(false);
        SetAppState(local.Id, state, local.Name, fault?.Message, local.Kind);
    }

    private async Task<ISwarmAppRun> StartAsync(LocalRun local, SwarmRunRequest request, CompiledAssembly entry, List<CompiledAssembly> dependencies, List<string> packages, DebugSession? session)
    {
        switch (request.Kind)
        {
            case SwarmAppKind.Console:
                var output = new SwarmOutputSender(
                    (text, isError) => NotifyTabletAsync(SwarmMethods.Output, SwarmJson.Body(new SwarmOutputNotice(local.Id, text, isError), SwarmJsonContext.Default.SwarmOutputNotice)),
                    (text, isError) => ConsoleOutput?.Invoke(this, new SwarmConsoleText(text, isError)));
                return SwarmConsoleRun.Start(entry, dependencies, packages, request.Arguments, session, output);

            case SwarmAppKind.Web:
                var web = request.Web ?? throw new InvalidDataException("The web app came without its address.");
                var gateway = new SwarmWebGateway(() => Connection, web.Secret);
                gateway.Start();
                local.Gateway = gateway;
                return await _host.StartAsync(
                    new SwarmApp { RunId = local.Id, Name = local.Name, Kind = SwarmAppKind.Web, Address = gateway.LaunchAddress(web.LaunchPath) },
                    CancellationToken.None).ConfigureAwait(false);

            default:
                return await _host.StartAsync(
                    new SwarmApp
                    {
                        RunId = local.Id,
                        Name = local.Name,
                        Kind = SwarmAppKind.Avalonia,
                        Entry = entry,
                        Dependencies = dependencies,
                        PackageAssemblies = packages,
                        Debugger = session,
                    },
                    CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Stops the app that runs, if any.</summary>
    private async Task StopAppAsync()
    {
        LocalRun? app;
        lock (_gate)
        {
            app = _app;
            _app = null;
        }

        if (app is not null)
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private LocalRun? Current()
    {
        lock (_gate)
        {
            return _app;
        }
    }

    private void Store(SwarmRunRequest request, IReadOnlyList<byte[]> attachments)
    {
        if (attachments.Count != request.Attached.Count)
        {
            throw new InvalidDataException("The build arrived incomplete. Run the app again.");
        }

        var packages = request.Packages.GroupBy(p => p.Hash, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().FileName, StringComparer.Ordinal);
        for (var i = 0; i < attachments.Count; i++)
        {
            _cache.Store(request.Attached[i], attachments[i], packages.GetValueOrDefault(request.Attached[i]));
        }
    }

    private CompiledAssembly Load(SwarmAssemblyEntry assembly) =>
        new(assembly.Name, _cache.Read(assembly.Image), assembly.Symbols is { } symbols ? _cache.Read(symbols) : null);

    private static HashSet<string> InUse(SwarmRunRequest request) =>
        [.. request.Dependencies.Append(request.Entry).SelectMany(a => a.Symbols is { } s ? new[] { a.Image, s } : new[] { a.Image }), .. request.Packages.Select(p => p.Hash)];

    private void SetAppState(long run, SwarmDeviceState state, string? appName, string? detail, SwarmAppKind? kind)
    {
        lock (_gate)
        {
            if (run != 0 && run < _appRun)
            {
                // A late report of a run that was replaced.
                return;
            }

            _appRun = run == 0 ? _appRun : run;
            _appState = state;
            _appName = appName;
            _appDetail = detail;
            _appKind = kind;
        }

        ReportState();
    }

    /// <summary>Tells the tablet what the app does now, and the device's own screen.</summary>
    private void ReportState()
    {
        SwarmStateNotice notice;
        lock (_gate)
        {
            notice = new SwarmStateNotice(_appRun, _appState, _appName, _appDetail);
        }

        RaiseChanged();
        if (Connection is { } connection)
        {
            _ = NotifyAsync(connection, SwarmMethods.State, SwarmJson.Body(notice, SwarmJsonContext.Default.SwarmStateNotice));
        }
    }

    private Task NotifyTabletAsync(string method, JsonElement body) =>
        Connection is { } connection ? NotifyAsync(connection, method, body) : Task.CompletedTask;

    /// <summary>One build running on this device, with what it needs to stop.</summary>
    private sealed class LocalRun(long id, string name, SwarmAppKind kind) : IAsyncDisposable
    {
        private int _disposed;

        public long Id { get; } = id;

        public string Name { get; } = name;

        public SwarmAppKind Kind { get; } = kind;

        public SwarmDebugAgent? Debugger { get; set; }

        public ISwarmAppRun? Run { get; set; }

        public SwarmWebGateway? Gateway { get; set; }

        public bool IsStopping => Volatile.Read(ref _disposed) == 1;

        public bool HasEnded { get; set; }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            // Released first, so an app paused on its UI thread can stop.
            Debugger?.Session.Stop();
            if (Run is { } run)
            {
                await run.DisposeAsync().ConfigureAwait(false);
            }

            if (Gateway is { } gateway)
            {
                await gateway.DisposeAsync().ConfigureAwait(false);
            }

            Debugger?.Dispose();
            Debugger?.Session.Dispose();
        }
    }
}
