using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TabStudio.Containers.Build;
using TabStudio.Containers.Store;
using TabStudio.Services;
using TabStudio.Services.Compose;

namespace TabStudio.Containers.Runtime;

/// <summary>Runs Services definitions from their real OCI images, using the existing Compose parser and F5 planner.</summary>
public sealed class ComposeImages(ContainerEngine engine, IProgress<PullProgress>? progress = null) : IServiceImages
{
    /// <inheritdoc />
    /// <remarks>A service with only <c>build:</c> can run too: its image is built from its Dockerfile first.</remarks>
    public bool CanRun(ComposeService service) => engine.Problem is null && (service.Image is { Length: > 0 } || service.BuildContext is not null)
        && service.Unsupported.Count == 0 && service.Ports.All(p => p.Protocol == "tcp");

    /// <summary>Converts a parsed Compose service into a workspace-scoped container definition.</summary>
    /// <remarks>
    /// A service with <c>build:</c> and no <c>image:</c> runs the image built from its Dockerfile, named
    /// <see cref="BuiltImageName"/>; <see cref="BuildAsync"/> builds it.
    /// </remarks>
    public static ContainerSpec ToSpec(ComposeProject project, ComposeService service, IReadOnlyList<ServicePort> ports)
    {
        var image = service.Image is { Length: > 0 } named ? named
            : service.BuildContext is not null ? BuiltImageName(project, service)
            : throw new ComposeException($"{service.Name}: the service has neither an image nor a build.");
        if (service.Unsupported.Count > 0) throw new ComposeException($"{service.Name}: {string.Join(' ', service.Unsupported)}");
        var projectId = ProjectId(project);
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
            // container_name is the container's name, and a name the other services reach it by, as in Docker.
            Image = image, Name = service.ContainerName is { Length: > 0 } containerName ? containerName : projectId + "-" + service.Name,
            Hostname = service.Hostname,
            Expose = service.Expose,
            VolumeScope = projectId + "-" + service.Name,
            Command = service.Command, Entrypoint = service.Entrypoint,
            WorkingDirectory = service.WorkingDirectory, User = service.User, Platform = service.Platform,
            Restart = RestartPolicy.Parse(service.Restart),
            Environment = service.Environment.Where(e => e.Value is not null).ToDictionary(e => e.Key, e => e.Value!, StringComparer.Ordinal),
            Aliases = Aliases(service), Mounts = mounts,
            Ports = [.. ports.Select(p => new PortBinding(p.Target, p.Protocol, p.Published, "127.0.0.1"))],
            PublishAll = ports.Count == 0,
            Labels = new Dictionary<string, string> { ["com.docker.compose.project"] = projectId, ["com.docker.compose.service"] = service.Name },
            Healthcheck = service.Healthcheck is { } h ? new HealthcheckSpec(h.Disabled ? ["NONE"] : h.Test)
            { Interval = h.Interval, Timeout = h.Timeout, StartPeriod = h.StartPeriod, StartInterval = h.StartInterval, Retries = h.Retries } : null,
        };
    }

    /// <summary>
    /// The name of the image built for a service with <c>build:</c> and no <c>image:</c>: the project and the
    /// service, as Compose names it, such as <c>compose-1a2b3c4d5e6f-api</c>.
    /// </summary>
    /// <param name="project">The Compose project.</param>
    /// <param name="service">The service.</param>
    public static string BuiltImageName(ComposeProject project, ComposeService service)
    {
        var name = new StringBuilder(service.Name.Length);
        foreach (var c in service.Name.ToLowerInvariant()) name.Append(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) ? c : '-');
        var part = name.ToString().Trim('-');
        return ProjectId(project) + "-" + (part.Length > 0 ? part : "service");
    }

    /// <summary>
    /// Builds a service's image from its Dockerfile, as <c>docker compose build</c> does, named
    /// <c>image:</c> when the service has one and <see cref="BuiltImageName"/> otherwise. An image already built from
    /// the same Dockerfile, arguments and files is used again.
    /// </summary>
    /// <param name="project">The Compose project.</param>
    /// <param name="service">The service; it must have <c>build:</c>.</param>
    /// <param name="log">Receives the build's output.</param>
    /// <param name="cancellationToken">Stops the build.</param>
    /// <exception cref="ComposeException">The service has no <c>build:</c>.</exception>
    /// <exception cref="ContainerException">The Dockerfile is invalid or one of its steps failed.</exception>
    public async Task<StoredImage> BuildAsync(ComposeProject project, ComposeService service, Action<string> log, CancellationToken cancellationToken)
    {
        var context = service.BuildContext ?? throw new ComposeException($"{service.Name}: the service has no build.");
        var request = new BuildRequest(context, service.Image is { Length: > 0 } image ? image : BuiltImageName(project, service))
        {
            DockerfilePath = service.BuildDockerfile,
            Arguments = service.BuildArgs.Where(a => a.Value is not null).ToDictionary(a => a.Key, a => a.Value!, StringComparer.Ordinal),
            Target = service.BuildTarget,
            Platform = service.Platform,
        };
        return await new DockerfileBuilder(engine).BuildAsync(request, log, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a service's image for a start; a build that fails means the image cannot run here, which lets a
    /// service with a native counterpart fall back to it.
    /// </summary>
    private async Task<StoredImage> BuildForStartAsync(ComposeProject project, ComposeService service, Action<string> log, Action<ServiceState, string?> changed, CancellationToken cancellationToken)
    {
        changed(ServiceState.Starting, "Building its image from the Dockerfile");
        try
        {
            return await BuildAsync(project, service, log, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ContainerException or TabStudio.Containers.Registry.RegistryException or HttpRequestException or IOException
            or UnauthorizedAccessException or InvalidDataException or NotSupportedException or FormatException)
        {
            throw new ServiceImageUnavailableException($"{service.Name}: its image could not be built from its Dockerfile: {FirstLine(ex.Message)}", ex);
        }
    }

    /// <summary>A project's ID: a short digest of its folder, which its containers' names and volumes start with.</summary>
    private static string ProjectId(ComposeProject project) =>
        "compose-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(project.Directory))))[..12];

    /// <summary>The names other services reach a service by: its name, its networks' aliases and its hostname.</summary>
    private static string[] Aliases(ComposeService service)
    {
        var names = new List<string> { service.Name };
        names.AddRange(service.Aliases);
        if (service.Hostname is { Length: > 0 } host) names.Add(host);
        return [.. names.Distinct(StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public async Task<IServiceImageProcess> StartAsync(ComposeProject project, ComposeService service,
        IReadOnlyList<ServicePort> ports, Action<string> log, Action<ServiceState, string?> changed, CancellationToken cancellationToken)
    {
        Session? session = null;
        ContainerInfo? container = null;
        var launched = false;
        try
        {
            var spec = ToSpec(project, service, ports);
            container = engine.Find(spec.Name!);
            if (container is not null && (container.Spec.Labels.GetValueOrDefault("com.docker.compose.project") != spec.Labels["com.docker.compose.project"]
                || container.Spec.Labels.GetValueOrDefault("com.docker.compose.service") != service.Name))
            {
                throw new ComposeException($"{service.Name}: a container named {spec.Name} that is not this service's already exists; remove it in View > Containers or change container_name.");
            }
            // build: without image: has nothing to pull; its image is built here, and again only when its files change.
            var built = service.Image is not { Length: > 0 } ? await BuildForStartAsync(project, service, log, changed, cancellationToken).ConfigureAwait(false) : null;
            if (container is not null && built is not null && container.ImageId != built.Id)
            {
                // Rebuilt: a container keeps the files of the image it was created from, so it is created again.
                var stale = container.ImageId;
                await engine.RemoveAsync(container.Id, force: true, cancellationToken).ConfigureAwait(false);
                container = null;
                await new DockerfileBuilder(engine).RemoveQuietlyAsync(stale, cancellationToken).ConfigureAwait(false);
            }
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
            try
            {
                container ??= await engine.CreateAsync(spec, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TabStudio.Containers.Registry.RegistryException or HttpRequestException && service.BuildContext is not null && built is null)
            {
                // build: with image: names what the build produces; it is often in no registry at all, so it is built here.
                log($"{service.Name}: {service.Image} could not be pulled ({FirstLine(ex.Message)}); building it from its Dockerfile.");
                await BuildForStartAsync(project, service, log, changed, cancellationToken).ConfigureAwait(false);
                try
                {
                    container = await engine.CreateAsync(spec, progress, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception again) when (again is TabStudio.Containers.Registry.RegistryException or HttpRequestException or ContainerException or InvalidDataException or NotSupportedException)
                {
                    throw new ServiceImageUnavailableException($"{service.Name}: {FirstLine(again.Message)}", again);
                }
            }
            catch (Exception ex) when (ex is TabStudio.Containers.Registry.RegistryException or HttpRequestException or ContainerException or InvalidDataException or NotSupportedException)
            {
                throw new ServiceImageUnavailableException($"{service.Name}: {FirstLine(ex.Message)}", ex);
            }
            session = new Session(engine, container.Id, log, changed);
            try
            {
                await engine.StartAsync(container.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (ContainerException ex)
            {
                // It never ran: not for this device, or needing what apps do not have.
                throw new ServiceImageUnavailableException($"{service.Name}: {FirstLine(ex.Message)}", ex);
            }
            launched = true;
            var oneShot = project.Services.Values.Any(s => s.Dependencies.Any(d => d.Name == service.Name && d.Condition == "service_completed_successfully"));
            if (oneShot)
            {
                if (await engine.WaitAsync(container.Id, cancellationToken).WaitAsync(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false) != 0)
                    throw new ComposeException($"{service.Name}: image did not complete successfully; see service logs.");
            }
            else
            {
                var image = await engine.Store.FindAsync(container.ImageId, cancellationToken).ConfigureAwait(false);
                var health = image is null ? spec.Healthcheck : ContainerEngine.HealthOf(image, spec);
                var deadline = ReadinessDeadline(health);
                var clock = Stopwatch.StartNew();
                var lastLog = DateTimeOffset.UtcNow;
                void Written(object? sender, ContainerLogEntry entry) => lastLog = DateTimeOffset.UtcNow;
                engine.Log(container.Id).Written += Written;
                try
                {
                    var reported = TimeSpan.Zero;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var state = engine.Find(container.Id)!;
                        if (!state.IsRunning) throw new ComposeException($"{service.Name}: image exited with code {state.ExitCode}{(state.Error is { } error ? " (" + error + ")" : "")}; see service logs.");
                        if (state.Health == HealthStatus.Unhealthy) throw new ComposeException($"{service.Name}: image health check failed: {state.HealthOutput?.Trim()}; see service logs.");
                        var waitingFor = new List<int>();
                        var answering = 0;
                        foreach (var port in ReadinessPorts(state.Ports, spec.Ports))
                        {
                            using var socket = new TcpClient();
                            try { await socket.ConnectAsync("127.0.0.1", port.HostPort, cancellationToken).ConfigureAwait(false); answering++; }
                            catch (SocketException) { waitingFor.Add(port.ContainerPort); }
                        }
                        // Ports the Compose file publishes must all answer. Without any, one of the image's own is
                        // enough: images declare ports they never open, as MariaDB's 33060 and RabbitMQ's 5671.
                        if (spec.Ports.Count == 0 && answering > 0) waitingFor.Clear();
                        var healthy = state.Health is HealthStatus.None or HealthStatus.Healthy;
                        if (waitingFor.Count == 0 && healthy && clock.Elapsed > TimeSpan.FromMilliseconds(300)) break;
                        var quiet = DateTimeOffset.UtcNow - lastLog;
                        if (!KeepWaiting(clock.Elapsed, deadline, quiet))
                        {
                            var what = waitingFor.Count > 0 ? $"did not answer on port {string.Join(", ", waitingFor)}" : "did not report healthy";
                            var why = clock.Elapsed >= MaximumReadiness ? $"within the {MaximumReadiness.TotalMinutes:0}-minute limit" : $"within {clock.Elapsed.TotalSeconds:0} seconds and has written nothing for {quiet.TotalSeconds:0} seconds";
                            throw new TimeoutException($"{service.Name}: image {what} {why}; see service logs.");
                        }
                        if (clock.Elapsed - reported >= TimeSpan.FromSeconds(10))
                        {
                            reported = clock.Elapsed;
                            changed(ServiceState.Starting, waitingFor.Count > 0
                                ? $"Waiting for port {string.Join(", ", waitingFor)} · {clock.Elapsed.TotalSeconds:0} s"
                                : $"Waiting for its health check · {clock.Elapsed.TotalSeconds:0} s");
                        }
                        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    engine.Log(container.Id).Written -= Written;
                }
            }
            return session;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            session?.Detach();
            if (container is not null && engine.Find(container.Id) is not null)
            {
                try { await engine.StopAsync(container.Id, TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false); }
                catch (ContainerException) { }
                foreach (var entry in engine.Log(container.Id).Recent(8)) log(entry.Text);
            }
            if (ex is OperationCanceledException or ServiceImageUnavailableException) throw;
            if (!launched && ex is not ComposeException) throw new ServiceImageUnavailableException($"{service.Name}: {FirstLine(ex.Message)}", ex);
            if (ex is TimeoutException) throw;
            throw new ComposeException(ex.Message);
        }
    }

    /// <summary>
    /// How long an image may take to become ready while it keeps writing to its log: two minutes, or what its
    /// health check allows (its start period and every retry), whichever is longer. A first start on a tablet
    /// initializes a database far more slowly than a computer does; 120 seconds fixed failed MySQL there.
    /// </summary>
    public static TimeSpan ReadinessDeadline(HealthcheckSpec? health)
    {
        var deadline = TimeSpan.FromSeconds(120);
        if (health is { Test.Count: > 0 } && health.Test[0] != "NONE")
        {
            var checks = health.StartPeriod + (health.Interval + health.Timeout) * (health.Retries + 1);
            if (checks > deadline) deadline = checks;
        }

        return deadline < MaximumReadiness ? deadline : MaximumReadiness;
    }

    /// <summary>
    /// The ports readiness looks at: those the Compose file publishes when it publishes any, otherwise every port
    /// mapped for the image, of which one answering is enough.
    /// </summary>
    public static IReadOnlyList<PublishedPort> ReadinessPorts(IReadOnlyList<PublishedPort> mapped, IReadOnlyList<PortBinding> published) =>
        published.Count == 0 ? mapped : [.. mapped.Where(p => published.Any(b => b.ContainerPort == p.ContainerPort))];

    /// <summary>The longest any image is waited for, even one that keeps writing to its log.</summary>
    public static readonly TimeSpan MaximumReadiness = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Whether to keep waiting: before the deadline always; after it only while the image still writes to its log
    /// (it is initializing, not stuck), up to <see cref="MaximumReadiness"/>.
    /// </summary>
    public static bool KeepWaiting(TimeSpan elapsed, TimeSpan deadline, TimeSpan sinceLastLog) =>
        elapsed < deadline || (elapsed < MaximumReadiness && sinceLastLog < TimeSpan.FromSeconds(30));

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? text;

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
