using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TabStudio.Services.Compose;
using TabStudio.Services.Native;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Services;

/// <summary>One workspace's native processes, dependencies, health and bounded, redacted logs.</summary>
public sealed class ServiceManager : IAsyncDisposable
{
    private readonly IServiceImages? _images;
    private readonly Dictionary<string, IServiceImageProcess> _imageRunning = new(StringComparer.Ordinal);
    private readonly IToolchainRuntime _runtime;
    private readonly IServicePackages _packages;
    private readonly IServiceNetwork _network;
    private readonly ServiceStorage _storage;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ServiceStatus> _statuses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Instance> _running = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<ServiceLogLine>> _logs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ServicePort>> _endpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fallbackReasons = new(StringComparer.Ordinal);
    private readonly List<string> _startedOrder = [];
    private readonly List<string> _secrets;
    private bool _disposed;

    private static readonly string[] SecretNameParts = ["PASSWORD", "SECRET", "TOKEN", "KEY", "AUTH"];

    public ServiceManager(ComposeProject project, IToolchainRuntime runtime, IServicePackages packages, ServiceStorage storage, IServiceNetwork? network = null, IServiceImages? images = null)
    {
        _images = images;
        Project = project; _runtime = runtime; _packages = packages; _storage = storage; _network = network ?? new LocalServiceNetwork();
        _runtime.ShareFolder(storage.Root);
        _runtime.ShareFolder(project.Directory);
        _secrets = project.Services.Values.SelectMany(s => s.Environment).Where(e => e.Value is { Length: > 0 } && SecretNameParts.Any(k => e.Key.Contains(k, StringComparison.OrdinalIgnoreCase))).Select(e => e.Value!).Distinct().OrderByDescending(x => x.Length).ToList();
        foreach (var service in project.Services.Values)
        {
            var result = ServiceCatalog.Map(service);
            _statuses[service.Name] = new(service, (result.Supported || CanRunImage(service)) ? ServiceState.Stopped : ServiceState.Unsupported, result.Mapping, [], CanRunImage(service) ? "OCI image under PRoot" : result.UnsupportedReason ?? result.Mapping?.Note);
            _logs[service.Name] = new();
        }
    }
    public ComposeProject Project { get; }
    public IReadOnlyList<ServiceStatus> Statuses { get { lock (_gate) return _statuses.Values.ToArray(); } }
    public IReadOnlyDictionary<string, IReadOnlyList<ServicePort>> Endpoints { get { lock (_gate) return new Dictionary<string, IReadOnlyList<ServicePort>>(_endpoints, StringComparer.Ordinal); } }
    public event EventHandler<ServiceStatus>? Changed;
    public event EventHandler<ServiceLogLine>? Logged;
    public IReadOnlyList<ServiceLogLine> Logs(string name) { lock (_gate) return _logs.TryGetValue(name, out var lines) ? lines.ToArray() : []; }

    public Task StartAllAsync(CancellationToken cancellationToken = default) => StartAsync(Project.Services.Values.Where(s => s.Profiles.Count == 0 && (CanRunImage(s) || ServiceCatalog.Map(s).Supported)).Select(s => s.Name), cancellationToken);

    /// <summary>
    /// The services F5 starts for a project: the dependencies of the Compose service built from it (or of the
    /// devcontainer's service), a devcontainer's <c>runServices</c>, and otherwise only the services its run
    /// configuration points at (<see cref="ServiceTargets"/>). Nothing else starts by itself; the Services
    /// window offers the rest.
    /// </summary>
    /// <param name="projectDirectory">The project that runs.</param>
    /// <param name="environment">The run's environment variables, if any.</param>
    /// <param name="environmentName">The .NET environment.</param>
    public IReadOnlyList<string> NeededBy(string projectDirectory, IReadOnlyDictionary<string, string?>? environment = null, string environmentName = "Development")
    {
        var application = ConnectionEnvironment.FindApplication(Project, projectDirectory);
        if (application is { Dependencies.Count: > 0 })
        {
            return application.Dependencies.Select(d => d.Name).ToArray();
        }

        if (Project.RunServices.Count > 0)
        {
            return Project.RunServices.Where(n => n != application?.Name).ToArray();
        }

        // An app service with no depends_on still names what it uses, as DATABASE_URL=postgres://…@db/app: an empty
        // list ended the search here, and F5 debugged it with no database.
        var variables = new Dictionary<string, string?>(application?.Environment ?? new Dictionary<string, string?>(), StringComparer.Ordinal);
        foreach (var (name, value) in environment ?? new Dictionary<string, string?>()) variables[name] = value;
        return ServiceTargets.Referenced(Project, projectDirectory, variables, environmentName).Where(n => n != application?.Name).ToArray();
    }

    public async Task<IReadOnlyDictionary<string, string?>> StartForProjectAsync(string projectDirectory, string environmentName = "Development", IReadOnlyDictionary<string, string?>? environment = null, CancellationToken cancellationToken = default)
    {
        var needed = NeededBy(projectDirectory, environment, environmentName);
        if (needed.Count > 0) await StartAsync(needed, cancellationToken).ConfigureAwait(false);
        return ConnectionEnvironment.ForProject(Project, projectDirectory, Endpoints, environmentName);
    }

    /// <summary>
    /// Starts services and what they depend on. Each requested service starts on its own: one that fails, with its
    /// own error on its row, never stops or rolls back another that does not depend on it, and a failure in the
    /// middle of one service's group stops only the processes that group started.
    /// </summary>
    /// <exception cref="ComposeException">Some services did not start; the message names each with its error.</exception>
    public async Task StartAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = linked.Token;
        await _operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var failures = new List<string>();
            var groups = new List<IReadOnlyList<string>>();
            var planned = new HashSet<string>(StringComparer.Ordinal);
            // Allocate ports for the whole request before starting, so inter-service URLs use final ports.
            foreach (var root in names.Distinct(StringComparer.Ordinal).ToList())
            {
                string? planning = null;
                try
                {
                    var order = DependencyOrder([root]);
                    foreach (var name in order)
                    {
                        planning = name;
                        var status = Get(name);
                        if (status.IsRunning || planned.Contains(name)) continue;
                        var mapping = ServiceCatalog.Map(status.Definition);
                        if (!mapping.Supported && !CanRunImage(status.Definition))
                        {
                            Update(name, ServiceState.Unsupported, mapping.UnsupportedReason);
                            throw new ComposeException($"{name}: {mapping.UnsupportedReason}");
                        }
                        AllocatePorts(name, mapping.Mapping);
                        planned.Add(name);
                    }
                    groups.Add(order);
                }
                catch (ComposeException ex)
                {
                    if (planning is not null && planning != root && Get(planning).State != ServiceState.Unsupported) Update(planning, ServiceState.Failed, ex.Message.StartsWith(planning + ":", StringComparison.Ordinal) ? ex.Message[(planning.Length + 1)..].Trim() : ex.Message);
                    failures.Add(Fail(root, ex.Message, planning));
                }
            }
            foreach (var order in groups)
            {
                var added = new List<string>();
                string? starting = null;
                try
                {
                    foreach (var name in order)
                    {
                        starting = name;
                        token.ThrowIfCancellationRequested();
                        var current = Get(name);
                        if (_imageRunning.TryGetValue(name, out var imageProcess) && imageProcess.IsRunning) continue;
                        if (current.IsRunning && _running.TryGetValue(name, out var existing) && existing.Process is { HasExited: false })
                        {
                            if (current.State is ServiceState.Unhealthy or ServiceState.Starting)
                            {
                                await WaitUntilReadyAsync(name, existing, token).ConfigureAwait(false);
                                Update(name, current.Definition.Healthcheck?.Disabled == true ? ServiceState.Running : ServiceState.Healthy, current.Mapping?.Note);
                            }
                            continue;
                        }
                        if (_running.ContainsKey(name))
                        {
                            await StopOneAsync(name).ConfigureAwait(false);
                            AllocatePorts(name, ServiceCatalog.Map(current.Definition).Mapping!);
                        }
                        added.Add(name);
                        await StartOneAsync(name, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // A cancelled or failed start never leaves this service's new processes behind.
                    foreach (var name in added.AsEnumerable().Reverse()) await StopOneAsync(name).ConfigureAwait(false);
                    if (ex is OperationCanceledException) throw;
                    failures.Add(Fail(order[^1], Redact(ex.Message), starting));
                }
            }
            if (failures.Count > 0) throw new ComposeException(string.Join("\n", failures.Distinct(StringComparer.Ordinal)));
        }
        finally { _operations.Release(); }
    }

    /// <summary>Records why a requested service did not start, on its own row, and returns the message for it.</summary>
    /// <param name="root">The requested service.</param>
    /// <param name="message">The error.</param>
    /// <param name="failed">The service whose start failed, when it is one the requested service depends on.</param>
    private string Fail(string root, string message, string? failed = null)
    {
        ServiceStatus? status;
        lock (_gate) status = _statuses.GetValueOrDefault(root);
        if (status is null) return message;
        var own = message.StartsWith(root + ":", StringComparison.Ordinal) ? message[(root.Length + 1)..].Trim() : message;
        // A service already marked failed or unsupported keeps its own detail; a dependent says what stopped it.
        if (status.State is not (ServiceState.Failed or ServiceState.Unsupported))
            Update(root, ServiceState.Failed, failed is not null && failed != root ? $"Needs {failed}, which did not start." : own);
        return message.StartsWith(root + ":", StringComparison.Ordinal) || (failed is not null && message.StartsWith(failed + ":", StringComparison.Ordinal)) ? message : $"{failed ?? root}: {message}";
    }

    public async Task StopAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var names = name is null ? _startedOrder.ToArray() : _startedOrder.Where(n => n == name || DependsTransitively(n, name, new HashSet<string>(StringComparer.Ordinal))).ToArray();
            foreach (var item in names.Reverse()) await StopOneAsync(item).ConfigureAwait(false);
        }
        finally { _operations.Release(); }
    }
    public async Task RestartAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        var running = name is null ? _startedOrder.ToArray() : _startedOrder.Where(n => n == name || DependsTransitively(n, name, new HashSet<string>(StringComparer.Ordinal))).ToArray();
        await StopAsync(name, cancellationToken).ConfigureAwait(false);
        await StartAsync(running.Length > 0 ? running : name is not null ? [name] : Project.Services.Values.Where(s => ServiceCatalog.Map(s).Supported).Select(s => s.Name), cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<string> DependencyOrder(IEnumerable<string> roots)
    {
        var result = new List<string>(); var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string name)
        {
            if (visited.Contains(name)) return;
            if (!Project.Services.TryGetValue(name, out var service)) throw new ComposeException($"Unknown service dependency '{name}'.");
            if (!visiting.Add(name)) throw new ComposeException($"Compose dependencies contain a cycle at '{name}'.");
            foreach (var dependency in service.Dependencies)
            {
                if (!dependency.Required && !Project.Services.ContainsKey(dependency.Name)) { Log(name, $"Optional dependency '{dependency.Name}' is absent."); continue; }
                if (!dependency.Required && Project.Services.TryGetValue(dependency.Name, out var optional) && !CanRunImage(optional) && !ServiceCatalog.Map(optional).Supported)
                { Log(name, $"Optional dependency '{dependency.Name}' is unsupported locally."); continue; }
                Visit(dependency.Name);
            }
            visiting.Remove(name); visited.Add(name); result.Add(name);
        }
        foreach (var root in roots) Visit(root);
        return result;
    }

    private bool CanRunImage(ComposeService service) => _images?.CanRun(service) == true;

    private async Task StartOneAsync(string name, CancellationToken token)
    {
        var definition = Get(name).Definition;
        if (CanRunImage(definition))
        {
            Update(name, ServiceState.Starting, "Pulling and starting OCI image");
            try
            {
                var image = await _images!.StartAsync(Project, definition, Endpoints[name], text => Log(name, text),
                    (state, detail) => Update(name, state, detail), token).ConfigureAwait(false);
                lock (_gate)
                {
                    _imageRunning[name] = image;
                    _startedOrder.Add(name);
                    _endpoints[name] = image.Ports;
                    _statuses[name] = _statuses[name] with { Ports = image.Ports };
                }
                Update(name, image.IsRunning ? ServiceState.Healthy : ServiceState.Completed, "OCI image under PRoot");
                return;
            }
            catch (OperationCanceledException) { Update(name, ServiceState.Stopped, "Start cancelled"); throw; }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or TimeoutException or ComposeException)
            {
                var reason = Redact(ex.Message).Replace('\n', ' ');
                if (!ServiceCatalog.Map(definition).Supported)
                {
                    Update(name, ServiceState.Failed, reason);
                    throw new ComposeException($"{name}: {reason}");
                }
                Log(name, $"{name}: image could not run ({reason}); using native Alpine service.");
                _fallbackReasons[name] = $"Native fallback: {reason}";
                Update(name, ServiceState.Starting, _fallbackReasons[name]);
            }
        }
        Update(name, ServiceState.Installing, "Preparing Alpine packages");
        try
        {
            var resolved = await _packages.EnsureAsync(definition, text => Log(name, text), token).ConfigureAwait(false);
            if (!resolved.Supported)
            {
                Update(name, ServiceState.Unsupported, resolved.UnsupportedReason);
                throw new ComposeException($"{name}: {resolved.UnsupportedReason}");
            }
            lock (_gate) _statuses[name] = _statuses[name] with { Mapping = resolved.Mapping };
            if (resolved.Mapping!.Note is { } note) Log(name, note);
            var plan = NativeServicePlan.Create(definition, resolved.Mapping, _storage, Endpoints[name], value => ConnectionEnvironment.Rewrite(value, Endpoints), (key, value) => ConnectionEnvironment.Rewrite(value, Endpoints, key));
            var instance = new Instance(plan, CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token));
            lock (_gate) { _running.Add(name, instance); _startedOrder.Add(name); }
            Update(name, ServiceState.Starting, "Initializing and waiting for health");
            instance.Process = _runtime.Start(plan.Start);
            instance.Process.CloseInput();
            instance.Readers = Task.WhenAll(ReadLogAsync(name, instance.Process.Output), instance.Process.Error is { } error ? ReadLogAsync(name, error) : Task.CompletedTask);
            var oneShot = Project.Services.Values.Any(s => s.Dependencies.Any(d => d.Name == name && d.Condition == "service_completed_successfully"));
            if (oneShot)
            {
                var exit = await instance.Process.Completion.WaitAsync(TimeSpan.FromMinutes(5), token).ConfigureAwait(false);
                if (!exit.Succeeded) throw new ComposeException($"{name} did not complete successfully. See its Services logs.");
                Update(name, ServiceState.Completed, "Completed successfully");
                return;
            }
            await WaitUntilReadyAsync(name, instance, token).ConfigureAwait(false);
            Update(name, definition.Healthcheck?.Disabled == true ? ServiceState.Running : ServiceState.Healthy, NativeDetail(name));
            instance.Monitor = MonitorAsync(name, instance);
        }
        catch (OperationCanceledException) { Update(name, ServiceState.Stopped, "Start cancelled"); throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (Get(name).State != ServiceState.Unsupported) Update(name, ServiceState.Failed, Redact(ex.Message));
            throw;
        }
    }

    private async Task WaitUntilReadyAsync(string name, Instance instance, CancellationToken token)
    {
        var health = Get(name).Definition.Healthcheck;
        var timeout = health is { Disabled: false } ? health.StartPeriod + TimeSpan.FromTicks(Math.Max(health.Interval.Ticks, TimeSpan.FromSeconds(1).Ticks) * Math.Max(health.Retries, 1)) + TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(90);
        var clock = Stopwatch.StartNew();
        var failedChecks = 0;
        while (clock.Elapsed < timeout)
        {
            token.ThrowIfCancellationRequested();
            if (instance.Process!.HasExited)
            {
                await instance.Readers.ConfigureAwait(false);
                throw new ComposeException($"{name} exited during startup. See its Services logs.");
            }
            if (await ProbeAsync(instance, health, token).ConfigureAwait(false)) return;
            if (health is { Disabled: false } && clock.Elapsed >= health.StartPeriod && ++failedChecks >= health.Retries)
                throw new ComposeException($"{name} failed {health.Retries} health checks. See its Services logs.");
            var interval = health is { Disabled: false } ? clock.Elapsed < health.StartPeriod ? health.StartInterval : health.Interval : TimeSpan.FromMilliseconds(500);
            if (interval <= TimeSpan.Zero) interval = TimeSpan.FromMilliseconds(500);
            await Task.Delay(interval, token).ConfigureAwait(false);
        }
        throw new ComposeException($"{name} did not become healthy within {timeout.TotalSeconds:0} seconds. See its Services logs.");
    }

    private async Task<bool> ProbeAsync(Instance instance, ServiceHealthcheck? health, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(health?.Timeout > TimeSpan.Zero ? health.Timeout : TimeSpan.FromSeconds(5));
        try
        {
            // TCP readiness catches accidental /bin/true probes and makes all daemons observable.
            foreach (var port in instance.Plan.Ports)
            {
                if (!await _network.IsListeningAsync(port.Published, timeout.Token).ConfigureAwait(false)) return false;
            }
            if (health?.Disabled == true) return true;
            var probe = instance.Plan.Probe;
            var result = await CommandRunner.RunAsync(_runtime, probe with { Program = "/bin/sh", Arguments = ["-c", "exec \"$@\"", "tabstudio-health", probe.Program, .. probe.Arguments] }, null, timeout.Token).ConfigureAwait(false);
            return result.Succeeded;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or ToolchainRuntimeException) { token.ThrowIfCancellationRequested(); return false; }
    }

    private async Task MonitorAsync(string name, Instance instance)
    {
        var failures = 0; var health = Get(name).Definition.Healthcheck;
        try
        {
            while (!instance.Stop.IsCancellationRequested)
            {
                if (instance.Process!.HasExited)
                {
                    Update(name, ServiceState.Failed, $"Exited with code {(await instance.Process.Completion.ConfigureAwait(false)).ExitCode}. See service logs.");
                    return;
                }
                var interval = health?.Interval > TimeSpan.Zero ? health.Interval : TimeSpan.FromSeconds(5);
                var delay = Task.Delay(interval, instance.Stop.Token);
                if (await Task.WhenAny(delay, instance.Process.Completion).ConfigureAwait(false) == instance.Process.Completion) continue;
                await delay.ConfigureAwait(false);
                var healthy = await ProbeAsync(instance, health, instance.Stop.Token).ConfigureAwait(false);
                failures = healthy ? 0 : failures + 1;
                if (healthy || failures >= (health?.Retries ?? 3)) Update(name, healthy ? health?.Disabled == true ? ServiceState.Running : ServiceState.Healthy : ServiceState.Unhealthy, healthy ? NativeDetail(name) : "Health check failed; see logs.");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Update(name, ServiceState.Unhealthy, Redact(ex.Message)); }
    }

    private async Task StopOneAsync(string name)
    {
        if (_imageRunning.Remove(name, out var image))
        {
            _startedOrder.Remove(name);
            Update(name, ServiceState.Stopping, "Stopping OCI image");
            await image.StopAsync(CancellationToken.None).ConfigureAwait(false);
            Update(name, ServiceState.Stopped, "OCI image under PRoot");
            return;
        }
        Instance? instance;
        lock (_gate) { _running.Remove(name, out instance); _startedOrder.Remove(name); }
        if (instance is null) return;
        var previous = Get(name);
        Update(name, ServiceState.Stopping, "Stopping");
        instance.Stop.Cancel();
        if (instance.Monitor is { } monitor) await monitor.ConfigureAwait(false);
        try
        {
            if (instance.Process is { HasExited: false } process)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                try { await CommandRunner.RunAsync(_runtime, instance.Plan.Stop, text => Log(name, text), timeout.Token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { Log(name, "Graceful shutdown: " + ex.Message); }
                try { await process.Completion.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
                catch (TimeoutException) { process.Stop(); }
            }
        }
        finally
        {
            try { await instance.Readers.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
            catch (TimeoutException) { /* A child may still hold a pipe; closing it below ends its reader. */ }
            finally { instance.Process?.Dispose(); }
            instance.Stop.Dispose();
            Update(name, previous.State is ServiceState.Failed or ServiceState.Unsupported ? previous.State : ServiceState.Stopped, previous.State is ServiceState.Failed or ServiceState.Unsupported ? previous.Detail : NativeDetail(name));
        }
    }

    private string? NativeDetail(string name) => _fallbackReasons.TryGetValue(name, out var reason) ? reason : Get(name).Mapping?.Note;

    private void AllocatePorts(string name, ServiceMapping? mapping)
    {
        lock (_gate)
        {
            var used = _endpoints.Where(e => e.Key != name).SelectMany(e => e.Value).Select(p => p.Published).ToHashSet();
            var ports = new List<ServicePort>();
            var targets = CanRunImage(Project.Services[name])
                ? Project.Services[name].Ports.Select(p => p.Target).Concat(mapping?.Ports ?? []).Distinct()
                : mapping?.Ports ?? [];
            foreach (var target in targets)
            {
                var requested = Project.Services[name].Ports.Where(p => p.Target == target).ToArray();
                if (requested.Length > 1) throw new ComposeException($"{name}: publishing a target port more than once requires a remote host.");
                var published = requested.FirstOrDefault()?.Published ?? target;
                if (published is > 0 and < 1024 && _runtime.Kind == RuntimeKind.Linux) { Log(name, $"Port {published} needs root; assigning an unprivileged localhost port."); published = 0; }
                if (published > 0 && (used.Contains(published) || !_network.IsFree(published)))
                {
                    if (requested.FirstOrDefault()?.Published > 0) throw new ComposeException($"{name}: localhost port {published} is already in use.");
                    published = 0;
                }
                if (published == 0) do { published = _network.FreePort(); } while (used.Contains(published));
                used.Add(published); ports.Add(new(target, published, "tcp", "127.0.0.1"));
            }
            _endpoints[name] = ports;
            _statuses[name] = _statuses[name] with { Ports = ports };
        }
    }
    private bool DependsTransitively(string service, string target, HashSet<string> visited) => visited.Add(service) && Project.Services[service].Dependencies.Any(d => d.Name == target || Project.Services.ContainsKey(d.Name) && DependsTransitively(d.Name, target, visited));
    private ServiceStatus Get(string name) { lock (_gate) return _statuses.TryGetValue(name, out var status) ? status : throw new ComposeException($"Unknown service '{name}'."); }
    private void Update(string name, ServiceState state, string? detail)
    {
        ServiceStatus status;
        lock (_gate) _statuses[name] = status = _statuses[name] with { State = state, Detail = detail };
        Changed?.Invoke(this, status);
    }
    private async Task ReadLogAsync(string name, Stream stream)
    {
        try { using var reader = new StreamReader(stream); while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) Log(name, line); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }
    private string Redact(string text) { foreach (var secret in _secrets) text = text.Replace(secret, "***", StringComparison.Ordinal); return text.Length > 8192 ? text[..8192] : text; }
    private void Log(string name, string text)
    {
        var line = new ServiceLogLine(name, DateTimeOffset.UtcNow, Redact(text));
        lock (_gate) { var logs = _logs[name]; logs.Enqueue(line); while (logs.Count > 1000) logs.Dequeue(); }
        Logged?.Invoke(this, line);
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        await StopAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }
    private sealed class Instance(NativeServicePlan plan, CancellationTokenSource stop)
    {
        public NativeServicePlan Plan { get; } = plan;
        public CancellationTokenSource Stop { get; } = stop;
        public ToolchainProcess? Process { get; set; }
        public Task Readers { get; set; } = Task.CompletedTask;
        public Task? Monitor { get; set; }
    }
}

