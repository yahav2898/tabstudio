using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TabStudio.Containers.Store;
using TabStudio.Services;
using TabStudio.Services.Compose;

namespace TabStudio.Containers.Runtime;

/// <summary>Runs Services definitions from their real OCI images, using the existing Compose parser and F5 planner.</summary>
public sealed class ComposeImages(ContainerEngine engine, IProgress<PullProgress>? progress = null) : IServiceImages
{
    /// <inheritdoc />
    public bool CanRun(ComposeService service) => engine.Options.Problem is null && service.Image is { Length: > 0 }
        && service.Unsupported.Count == 0 && service.Ports.All(p => p.Protocol == "tcp");

    /// <summary>Converts a parsed Compose service into a workspace-scoped container definition.</summary>
    public static ContainerSpec ToSpec(ComposeProject project, ComposeService service, IReadOnlyList<ServicePort> ports)
    {
        if (service.Image is not { Length: > 0 } image) throw new ComposeException($"{service.Name}: Dockerfile builds are deferred; run the application with F5.");
        if (service.Unsupported.Count > 0) throw new ComposeException($"{service.Name}: {string.Join(' ', service.Unsupported)}");
        var projectId = "compose-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(project.Directory))))[..12];
        var mounts = service.Volumes.Select(v =>
        {
            if (v.IsBind && v.Source is { } source && !Directory.Exists(source) && !File.Exists(source))
            {
                if (!v.CreateHostPath) throw new ComposeException($"{service.Name}: bind source does not exist: {source}");
                Directory.CreateDirectory(source);
            }
            return new ContainerMount(v.IsBind ? "bind" : "volume", v.IsBind ? v.Source : projectId + "-" + (v.Source ?? service.Name + "-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(v.Target)))[..8]), v.Target, v.ReadOnly);
        }).ToArray();
        return new ContainerSpec
        {
            Image = image, Name = projectId + "-" + service.Name,
            Command = service.Command, Entrypoint = service.Entrypoint,
            WorkingDirectory = service.WorkingDirectory, User = service.User, Platform = service.Platform,
            Restart = RestartPolicy.Parse(service.Restart),
            Environment = service.Environment.Where(e => e.Value is not null).ToDictionary(e => e.Key, e => e.Value!, StringComparer.Ordinal),
            Aliases = [service.Name], Mounts = mounts,
            Ports = [.. ports.Select(p => new PortBinding(p.Target, p.Protocol, p.Published, "127.0.0.1"))],
            PublishAll = ports.Count == 0,
            Labels = new Dictionary<string, string> { ["com.docker.compose.project"] = projectId, ["com.docker.compose.service"] = service.Name },
            Healthcheck = service.Healthcheck is { } h ? new HealthcheckSpec(h.Disabled ? ["NONE"] : h.Test)
            { Interval = h.Interval, Timeout = h.Timeout, StartPeriod = h.StartPeriod, StartInterval = h.StartInterval, Retries = h.Retries } : null,
        };
    }

    /// <inheritdoc />
    public async Task<IServiceImageProcess> StartAsync(ComposeProject project, ComposeService service,
        IReadOnlyList<ServicePort> ports, Action<string> log, Action<ServiceState, string?> changed, CancellationToken cancellationToken)
    {
        Session? session = null;
        ContainerInfo? container = null;
        try
        {
            var spec = ToSpec(project, service, ports);
            container = engine.Find(spec.Name!);
            if (container is not null && JsonSerializer.Serialize(container.Spec with { Ports = spec.Ports }) == JsonSerializer.Serialize(spec))
            {
                // Automatic device ports may change between starts; that must not discard the writable root.
                container = await engine.RebindStoppedPortsAsync(container.Id, spec.Ports, cancellationToken).ConfigureAwait(false);
            }
            else if (container is not null && JsonSerializer.Serialize(container.Spec) != JsonSerializer.Serialize(spec))
            {
                await engine.RemoveAsync(container.Id, force: true, cancellationToken).ConfigureAwait(false);
                container = null;
            }
            container ??= await engine.CreateAsync(spec, progress, cancellationToken).ConfigureAwait(false);
            session = new Session(engine, container.Id, log, changed);
            await engine.StartAsync(container.Id, cancellationToken).ConfigureAwait(false);
            var oneShot = project.Services.Values.Any(s => s.Dependencies.Any(d => d.Name == service.Name && d.Condition == "service_completed_successfully"));
            if (oneShot)
            {
                if (await engine.WaitAsync(container.Id, cancellationToken).WaitAsync(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false) != 0)
                    throw new ComposeException($"{service.Name}: image did not complete successfully; see service logs.");
            }
            else
            {
                var clock = Stopwatch.StartNew();
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var state = engine.Find(container.Id)!;
                    if (!state.IsRunning) throw new ComposeException($"{service.Name}: image exited with code {state.ExitCode}; see service logs.");
                    if (state.Health == HealthStatus.Unhealthy) throw new ComposeException($"{service.Name}: image health check failed: {state.HealthOutput?.Trim()}; see service logs.");
                    var listening = true;
                    foreach (var port in state.Ports)
                    {
                        using var socket = new TcpClient();
                        try { await socket.ConnectAsync("127.0.0.1", port.HostPort, cancellationToken).ConfigureAwait(false); }
                        catch (SocketException) { listening = false; break; }
                    }
                    if (listening && (state.Health is HealthStatus.None or HealthStatus.Healthy) && clock.Elapsed > TimeSpan.FromMilliseconds(300)) break;
                    if (clock.Elapsed > TimeSpan.FromSeconds(120)) throw new TimeoutException($"{service.Name}: image did not become ready in 120 seconds.");
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
            }
            return session;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            session?.Detach();
            if (container is not null)
            {
                await engine.StopAsync(container.Id, TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
                foreach (var entry in engine.Log(container.Id).Recent(8)) log(entry.Text);
            }
            if (ex is OperationCanceledException) throw;
            throw new ComposeException(ex.Message);
        }
    }

    private sealed class Session : IServiceImageProcess
    {
        private readonly ContainerEngine _engine;
        private readonly string _id;
        private readonly Action<string> _log;
        private readonly Action<ServiceState, string?> _changed;
        public Session(ContainerEngine engine, string id, Action<string> log, Action<ServiceState, string?> changed)
        {
            _engine = engine; _id = id; _log = log; _changed = changed;
            engine.Log(id).Written += OnLog;
            engine.Changed += OnChanged;
        }
        public bool IsRunning => _engine.Find(_id)?.IsRunning == true;
        public IReadOnlyList<ServicePort> Ports => _engine.Find(_id)?.Ports.Select(p => new ServicePort(p.ContainerPort, p.HostPort, p.Protocol, p.HostIp)).ToArray() ?? [];
        private void OnLog(object? sender, ContainerLogEntry entry) => _log(entry.Text);
        private void OnChanged(object? sender, EngineEvent e)
        {
            if (e.Id != _id || _engine.Find(_id) is not { } info) return;
            var state = !info.IsRunning ? info.StoppedByUser ? ServiceState.Stopped : info.ExitCode == 0 ? ServiceState.Completed : ServiceState.Failed
                : info.Health == HealthStatus.Unhealthy ? ServiceState.Unhealthy : ServiceState.Running;
            _changed(state, info.Error ?? (info.IsRunning ? "OCI image under PRoot" : $"Image exited with code {info.ExitCode}"));
        }
        public void Detach() { _engine.Log(_id).Written -= OnLog; _engine.Changed -= OnChanged; }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Detach();
            await _engine.StopAsync(_id, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
