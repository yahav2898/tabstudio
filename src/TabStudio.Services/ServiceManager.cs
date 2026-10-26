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
    private readonly List<string> _startedOrder = [];
    private readonly List<string> _secrets;
    private bool _disposed;

    public ServiceManager(ComposeProject project, IToolchainRuntime runtime, IServicePackages packages, ServiceStorage storage, IServiceNetwork? network = null)
    {
        Project = project; _runtime = runtime; _packages = packages; _storage = storage; _network = network ?? new LocalServiceNetwork();
        _runtime.ShareFolder(storage.Root);
        _runtime.ShareFolder(project.Directory);
        _secrets = project.Services.Values.SelectMany(s => s.Environment).Where(e => e.Value is { Length: > 0 } && new[] { "PASSWORD", "SECRET", "TOKEN", "KEY", "AUTH" }.Any(k => e.Key.Contains(k, StringComparison.OrdinalIgnoreCase))).Select(e => e.Value!).Distinct().OrderByDescending(x => x.Length).ToList();
        foreach (var service in project.Services.Values)
        {
            var result = ServiceCatalog.Map(service);
            _statuses[service.Name] = new(service, result.Supported ? ServiceState.Stopped : ServiceState.Unsupported, result.Mapping, [], result.UnsupportedReason ?? result.Mapping?.Note);
            _logs[service.Name] = new();
        }
    }
    public ComposeProject Project { get; }
    public IReadOnlyList<ServiceStatus> Statuses { get { lock (_gate) return _statuses.Values.ToArray(); } }
    public IReadOnlyDictionary<string, IReadOnlyList<ServicePort>> Endpoints { get { lock (_gate) return new Dictionary<string, IReadOnlyList<ServicePort>>(_endpoints, StringComparer.Ordinal); } }
    public event EventHandler<ServiceStatus>? Changed;
    public event EventHandler<ServiceLogLine>? Logged;
    public IReadOnlyList<ServiceLogLine> Logs(string name) { lock (_gate) return _logs.TryGetValue(name, out var lines) ? lines.ToArray() : []; }

    public Task StartAllAsync(CancellationToken cancellationToken = default) => StartAsync(Project.Services.Values.Where(s => s.Profiles.Count == 0 && ServiceCatalog.Map(s).Supported).Select(s => s.Name), cancellationToken);

    public async Task<IReadOnlyDictionary<string, string?>> StartForProjectAsync(string projectDirectory, string environmentName = "Development", CancellationToken cancellationToken = default)
    {
        var application = ConnectionEnvironment.FindApplication(Project, projectDirectory);
        var needed = application?.Dependencies.Select(d => d.Name).ToArray()
            ?? (Project.RunServices.Count > 0 ? Project.RunServices.Where(n => n != application?.Name).ToArray() : Project.Services.Values.Where(s => s.Profiles.Count == 0 && ServiceCatalog.Map(s).Supported).Select(s => s.Name).ToArray());
        await StartAsync(needed, cancellationToken).ConfigureAwait(false);
        return ConnectionEnvironment.ForProject(Project, projectDirectory, Endpoints, environmentName);
    }

    public async Task StartAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = linked.Token;
        await _operations.WaitAsync(token).ConfigureAwait(false);
        var added = new List<string>();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var order = DependencyOrder(names);
            // Allocate ports for the whole request before starting, so inter-service URLs use final ports.
            foreach (var name in order)
            {
                var status = Get(name);
                if (status.IsRunning) continue;
                var mapping = ServiceCatalog.Map(status.Definition);
                if (!mapping.Supported)
                {
                    Update(name, ServiceState.Unsupported, mapping.UnsupportedReason);
                    throw new ComposeException($"{name}: {mapping.UnsupportedReason}");
                }
                AllocatePorts(name, mapping.Mapping!);
            }
            foreach (var name in order)
            {
                token.ThrowIfCancellationRequested();
                if (Get(name).IsRunning) continue;
                if (_running.ContainsKey(name)) await StopOneAsync(name).ConfigureAwait(false);
                added.Add(name);
                await StartOneAsync(name, token).ConfigureAwait(false);
            }
        }
        catch
        {
            // A cancelled or failed group start never leaves its new processes behind.
            foreach (var name in added.AsEnumerable().Reverse()) await StopOneAsync(name).ConfigureAwait(false);
            throw;
        }
        finally { _operations.Release(); }
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
                if (!dependency.Required && Project.Services.TryGetValue(dependency.Name, out var optional) && !ServiceCatalog.Map(optional).Supported)
                { Log(name, $"Optional dependency '{dependency.Name}' is unsupported locally."); continue; }
                Visit(dependency.Name);
            }
            visiting.Remove(name); visited.Add(name); result.Add(name);
        }
        foreach (var root in roots) Visit(root);
        return result;
    }

    private async Task StartOneAsync(string name, CancellationToken token)
    {
        var definition = Get(name).Definition;
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
            var plan = NativeServicePlan.Create(definition, resolved.Mapping, _storage, Endpoints[name], value => ConnectionEnvironment.Rewrite(value, Endpoints));
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
            Update(name, definition.Healthcheck?.Disabled == true ? ServiceState.Running : ServiceState.Healthy, resolved.Mapping.Note);
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
                if (healthy || failures >= (health?.Retries ?? 3)) Update(name, healthy ? health?.Disabled == true ? ServiceState.Running : ServiceState.Healthy : ServiceState.Unhealthy, healthy ? Get(name).Mapping?.Note : "Health check failed; see logs.");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Update(name, ServiceState.Unhealthy, Redact(ex.Message)); }
    }

    private async Task StopOneAsync(string name)
    {
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
            Update(name, previous.State is ServiceState.Failed or ServiceState.Unsupported ? previous.State : ServiceState.Stopped, previous.State is ServiceState.Failed or ServiceState.Unsupported ? previous.Detail : Get(name).Mapping?.Note);
        }
    }

    private void AllocatePorts(string name, ServiceMapping mapping)
    {
        lock (_gate)
        {
            var used = _endpoints.Where(e => e.Key != name).SelectMany(e => e.Value).Select(p => p.Published).ToHashSet();
            var ports = new List<ServicePort>();
            foreach (var target in mapping.Ports)
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
