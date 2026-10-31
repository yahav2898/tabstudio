using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using TabStudio.Containers.Images;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Store;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Containers.Runtime;

/// <summary>What to run in a running container, as <c>docker exec</c>.</summary>
/// <param name="Command">The program and its arguments.</param>
public sealed record ExecSpec(IReadOnlyList<string> Command)
{
    /// <summary>Variables added to the container's.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
    /// <summary>The working directory; the container's when unset.</summary>
    public string? WorkingDirectory { get; init; }
    /// <summary>The user; the container's when unset.</summary>
    public string? User { get; init; }
    /// <summary>A terminal of this size, or pipes.</summary>
    public TerminalSize? Terminal { get; init; }
}

/// <summary>
/// The container engine: images, containers, volumes and their events, all under PRoot without root. A container
/// is its image's configuration, a writable copy of the image's files, a loopback address and published ports on
/// the shared network, and a PRoot process tree; restart policies, health checks, logs, exec and stats work as
/// Docker's do, within what Android allows.
/// </summary>
public sealed class ContainerEngine : IAsyncDisposable
{
    /// <summary>What cannot work without root, in one line each, for the UI and <c>docker info</c>.</summary>
    public static readonly IReadOnlyList<string> Limits =
    [
        "systemd or another init system cannot be PID 1: there is no PID namespace.",
        "iptables, kernel modules and --privileged need root, which apps do not have.",
        "Memory and CPU limits need cgroups, which apps cannot use; containers share the device's resources.",
        "Containers share the device's network: each gets its own loopback address, but there is no isolation between them.",
        "Read-only mounts and file ownership are simulated by PRoot; a process can still write a read-only mount.",
    ];

    private static readonly JsonSerializerOptions StateJson = new() { WriteIndented = true };
    private static readonly string[] Adjectives = ["brave", "calm", "eager", "fond", "keen", "lucid", "merry", "quiet", "swift", "witty"];
    private static readonly string[] Nouns = ["curie", "darwin", "hopper", "lovelace", "noether", "pascal", "ritchie", "turing", "wozniak", "yalow"];

    private readonly ContainerEngineOptions _options;
    private readonly RegistryClient _registry;
    private readonly ConcurrentDictionary<string, Instance> _instances = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _eventsGate = new();
    private readonly List<Channel<EngineEvent>> _subscribers = [];
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>Creates the engine.</summary>
    public ContainerEngine(ContainerEngineOptions options, RegistryClient registry)
    {
        _options = options;
        _registry = registry;
        Store = new ImageStore(options.StoreRoot);
        Network = new ContainerNetwork(Path.Combine(options.StoreRoot, "network"));
        foreach (var folder in Directory.EnumerateDirectories(Store.ContainersFolder))
        {
            var file = Path.Combine(folder, "container.json");
            try
            {
                if (File.Exists(file) && JsonSerializer.Deserialize<ContainerInfo>(File.ReadAllBytes(file), StateJson) is { } info)
                {
                    // Nothing survives the app: what was running is now exited.
                    var state = info.IsRunning ? info with { Status = ContainerStatus.Exited, Pid = 0, Ports = [], ExitCode = 255, FinishedAt = DateTimeOffset.UtcNow } : info;
                    _instances[info.Id] = new Instance(state, new ContainerLog(Path.Combine(folder, "log.jsonl")));
                }
            }
            catch (JsonException)
            {
            }
        }
    }

    /// <summary>The image store.</summary>
    public ImageStore Store { get; }

    /// <summary>The shared network.</summary>
    public ContainerNetwork Network { get; }

    /// <summary>The registry client.</summary>
    public RegistryClient Registry => _registry;

    /// <summary>The options.</summary>
    public ContainerEngineOptions Options => _options;

    /// <summary>Raised when a container or image changes, for the Containers window.</summary>
    public event EventHandler<EngineEvent>? Changed;

    /// <summary>Every container.</summary>
    public IReadOnlyList<ContainerInfo> Containers => [.. _instances.Values.Select(i => i.Info).OrderByDescending(c => c.Created)];

    /// <summary>Pulls an image.</summary>
    public async Task<StoredImage> PullAsync(string image, string? platform = null, IProgress<PullProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var reference = ImageReference.Parse(image);
        var preference = platform is null ? _options.Preference : [OciPlatform.Parse(platform)];
        StoredImage stored;
        try { stored = await Store.PullAsync(_registry, reference, preference, progress, cancellationToken).ConfigureAwait(false); }
        catch
        {
            progress?.Report(new PullProgress(image, cancellationToken.IsCancellationRequested ? "Cancelled" : "Failed", 0, 0, 0, 0));
            throw;
        }
        Publish("image", "pull", stored.Id, new Dictionary<string, string> { ["name"] = reference.Familiar });
        return stored;
    }

    /// <summary>The image by name or ID, pulled if it is not in the store and <paramref name="pull"/>.</summary>
    public async Task<StoredImage> ImageAsync(string image, bool pull, string? platform = null, IProgress<PullProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (await Store.FindAsync(image, cancellationToken).ConfigureAwait(false) is { } found
            && (platform is null || OciPlatform.Parse(platform).Matches(found.Platform)))
        {
            return found;
        }

        if (!pull) throw new ContainerException($"No such image: {image}");
        return await PullAsync(image, platform, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finds a container by ID, ID prefix or name.</summary>
    public ContainerInfo? Find(string idOrName)
    {
        var name = idOrName.TrimStart('/');
        if (_instances.TryGetValue(name, out var exact)) return exact.Info;
        var byName = _instances.Values.FirstOrDefault(i => i.Info.Name == name);
        if (byName is not null) return byName.Info;
        var byPrefix = _instances.Values.Where(i => i.Info.Id.StartsWith(name, StringComparison.Ordinal)).ToList();
        return byPrefix.Count == 1 ? byPrefix[0].Info : null;
    }

    /// <summary>A container's log.</summary>
    public ContainerLog Log(string idOrName) => Get(idOrName).Log;

    internal async Task<ContainerInfo> RebindStoppedPortsAsync(string id, IReadOnlyList<PortBinding> ports, CancellationToken cancellationToken)
    {
        var instance = Get(id);
        await instance.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (instance.Info.IsRunning) return instance.Info;
            instance.Info = instance.Info with { Spec = instance.Info.Spec with { Ports = ports } };
            Save(instance.Info);
            return instance.Info;
        }
        finally { instance.Gate.Release(); }
    }

    /// <summary>Creates a container: its writable copy of the image and its record. Nothing runs yet.</summary>
    public async Task<ContainerInfo> CreateAsync(ContainerSpec spec, IProgress<PullProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_options.Problem is { } problem) throw new ContainerException(problem);
        var image = await ImageAsync(spec.Image, pull: true, spec.Platform, progress, cancellationToken).ConfigureAwait(false);
        if (image.Platform.Os != "linux") throw new ContainerException("Only Linux OCI images can run in TabStudio's PRoot runtime.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string id;
        string name;
        try
        {
            name = spec.Name ?? MakeName();
            if (!IsValidName(name)) throw new ContainerException($"'{name}' is not a valid container name: use letters, digits, _ . and -.");
            if (_instances.Values.Any(i => i.Info.Name == name))
            {
                throw new ContainerException($"The container name \"{name}\" is already in use. Remove that container or choose another name.");
            }

            id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            _instances[id] = new Instance(new ContainerInfo { Id = id, Name = name, Spec = spec, ImageId = image.Id, ImageName = spec.Image, Status = ContainerStatus.Created },
                new ContainerLog(Path.Combine(Store.ContainersFolder, id, "log.jsonl")));
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            var folder = Path.Combine(Store.ContainersFolder, id);
            await TreeCopy.CopyAsync(Store.RootfsOf(image.Id), Path.Combine(folder, "rootfs"), cancellationToken).ConfigureAwait(false);
            var info = Get(id).Info;
            Save(info);
            Publish("container", "create", id, Attributes(info));
            return info;
        }
        catch
        {
            _instances.TryRemove(id, out _);
            var folder = Path.Combine(Store.ContainersFolder, id);
            if (Directory.Exists(folder)) LayerUnpacker.Delete(folder);
            throw;
        }
    }

    /// <summary>Starts a container's main process.</summary>
    public async Task<ContainerInfo> StartAsync(string idOrName, CancellationToken cancellationToken = default)
    {
        var instance = Get(idOrName);
        await instance.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (instance.Process is { HasExited: false }) return instance.Info;
            instance.Info = instance.Info with { StoppedByUser = false };
            await LaunchAsync(instance, cancellationToken).ConfigureAwait(false);
            return instance.Info;
        }
        finally
        {
            instance.Gate.Release();
        }
    }

    /// <summary>
    /// Stops a container: its stop signal to its main process, then, after <paramref name="timeout"/>, its whole
    /// process tree is killed.
    /// </summary>
    public async Task<ContainerInfo> StopAsync(string idOrName, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var instance = Get(idOrName);
        await instance.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            instance.Info = instance.Info with { StoppedByUser = true };
            instance.Restart?.Cancel();
            await TerminateAsync(instance, timeout ?? instance.Info.Spec.StopTimeout ?? TimeSpan.FromSeconds(10), signal: null).ConfigureAwait(false);
            return instance.Info;
        }
        finally
        {
            instance.Gate.Release();
        }
    }

    /// <summary>Sends a signal to a container's main process; SIGKILL ends it at once.</summary>
    public async Task KillAsync(string idOrName, string? signal = null, CancellationToken cancellationToken = default)
    {
        var instance = Get(idOrName);
        var number = Signals.Parse(signal ?? "SIGKILL");
        if (number == Signals.Kill)
        {
            await instance.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                instance.Info = instance.Info with { StoppedByUser = true };
                instance.Restart?.Cancel();
                await TerminateAsync(instance, TimeSpan.Zero, signal: null).ConfigureAwait(false);
            }
            finally
            {
                instance.Gate.Release();
            }

            return;
        }

        if (instance.Process is not { HasExited: false } process) throw new ContainerException($"Container {instance.Info.Name} is not running.");
        SignalMain(process, number);
    }

    /// <summary>Stops and starts a container.</summary>
    public async Task<ContainerInfo> RestartAsync(string idOrName, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await StopAsync(idOrName, timeout, cancellationToken).ConfigureAwait(false);
        return await StartAsync(idOrName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a container and its writable layer; a running one only with <paramref name="force"/>.</summary>
    public async Task RemoveAsync(string idOrName, bool force = false, CancellationToken cancellationToken = default)
    {
        var instance = Get(idOrName);
        if (instance.Info.IsRunning && !force)
        {
            throw new ContainerException($"Container {instance.Info.Name} is running: stop it first, or remove it with force (docker rm -f).");
        }

        await instance.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            instance.Removed = true;
            instance.Restart?.Cancel();
            await TerminateAsync(instance, TimeSpan.Zero, signal: null).ConfigureAwait(false);
            _instances.TryRemove(instance.Info.Id, out _);
            instance.Log.Complete();
            var folder = Path.Combine(Store.ContainersFolder, instance.Info.Id);
            await Task.Run(() => { if (Directory.Exists(folder)) LayerUnpacker.Delete(folder); }, CancellationToken.None).ConfigureAwait(false);
            var run = RunDirectory(instance.Info.Id);
            if (Directory.Exists(run)) LayerUnpacker.Delete(run);
            Publish("container", "destroy", instance.Info.Id, Attributes(instance.Info));
        }
        finally
        {
            instance.Gate.Release();
        }
    }

    /// <summary>Waits until a container's main process exits and returns its exit code.</summary>
    public async Task<int> WaitAsync(string idOrName, CancellationToken cancellationToken = default)
    {
        var instance = Get(idOrName);
        while (true)
        {
            var exited = instance.Exited;
            if (!instance.Info.IsRunning || instance.Removed) return instance.Info.ExitCode;
            await exited.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (instance.Info.Status != ContainerStatus.Restarting) return instance.Info.ExitCode;
        }
    }

    /// <summary>Runs a command in a running container, as <c>docker exec</c>.</summary>
    public ToolchainProcess Exec(string idOrName, ExecSpec exec)
    {
        var instance = Get(idOrName);
        if (instance.Process is not { HasExited: false } || instance.Lease is null)
        {
            throw new ContainerException($"Container {instance.Info.Name} is not running.");
        }

        var info = instance.Info;
        var image = instance.Image ?? throw new ContainerException($"Container {info.Name} is not running.");
        var rootfs = RootfsOf(info.Id);
        var user = ContainerUser.Resolve(rootfs, exec.User ?? info.Spec.User ?? image.Config.Config.User);
        var environment = Environment(image, info, user);
        foreach (var (key, value) in exec.Environment) environment[key] = value;
        var runtime = Runtime(info, instance.Lease, environment);
        var spec = ProcessSpec(rootfs, exec.Command, exec.WorkingDirectory ?? WorkingDirectory(image, info), environment, user, Mounts(info), exec.Terminal);
        return runtime.Start(Wrap(instance, spec));
    }

    /// <summary>Runs a command in a running container and collects its output.</summary>
    public async Task<(int ExitCode, string Output)> ExecCaptureAsync(string idOrName, IReadOnlyList<string> command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var process = Exec(idOrName, new ExecSpec(command));
        process.CloseInput();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        var output = ReadAllAsync(process.Output, limit.Token);
        var error = process.Error is { } stream ? ReadAllAsync(stream, limit.Token) : Task.FromResult("");
        try
        {
            var exit = await process.WaitAsync(limit.Token).ConfigureAwait(false);
            return (exit.ExitCode, await output.ConfigureAwait(false) + await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (-1, $"The command did not finish within {timeout.TotalSeconds:0} seconds.");
        }
    }

    /// <summary>A running container's resource use.</summary>
    public ContainerStats Stats(string idOrName) =>
        Get(idOrName).Process is { HasExited: false } process ? ProcessTree.Measure(process.Id) : new(0, 0, 0);

    /// <summary>A container's root file system on the device.</summary>
    public string RootfsOf(string id) => Path.Combine(Store.ContainersFolder, id, "rootfs");

    /// <summary>
    /// A runtime that starts processes inside a container's file system, for the IDE's terminal, builds, debugger
    /// and language servers (dev containers). It works while the container runs.
    /// </summary>
    public IToolchainRuntime RuntimeFor(string idOrName, IReadOnlyList<string> sharedFolders)
    {
        var instance = Get(idOrName);
        var image = instance.Image ?? throw new ContainerException($"Container {instance.Info.Name} is not running.");
        var user = ContainerUser.Resolve(RootfsOf(instance.Info.Id), instance.Info.Spec.User ?? image.Config.Config.User);
        var environment = Environment(image, instance.Info, user);
        return new ContainerToolchainRuntime(this, instance.Info, Runtime(instance.Info, instance.Lease!, environment, sharedFolders), environment, user, Mounts(instance.Info));
    }

    /// <summary>Follows engine events, as <c>docker events</c>.</summary>
    public async IAsyncEnumerable<EngineEvent> EventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<EngineEvent>();
        lock (_eventsGate) _subscribers.Add(channel);
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return item;
        }
        finally
        {
            lock (_eventsGate) _subscribers.Remove(channel);
        }
    }

    /// <summary>Creates a named volume, or returns the existing one.</summary>
    public VolumeInfo CreateVolume(string name, IReadOnlyDictionary<string, string>? labels = null)
    {
        if (!IsValidName(name)) throw new ContainerException($"'{name}' is not a valid volume name.");
        var folder = Path.Combine(Store.VolumesFolder, name);
        var data = Path.Combine(folder, "_data");
        var meta = Path.Combine(folder, "volume.json");
        if (!Directory.Exists(data))
        {
            Directory.CreateDirectory(data);
            File.WriteAllBytes(meta, JsonSerializer.SerializeToUtf8Bytes(new VolumeInfo(name, data, DateTimeOffset.UtcNow, labels ?? new Dictionary<string, string>())));
            Publish("volume", "create", name, new Dictionary<string, string>());
        }

        return ReadVolume(name)!;
    }

    /// <summary>Every named volume.</summary>
    public IReadOnlyList<VolumeInfo> Volumes => [.. Directory.EnumerateDirectories(Store.VolumesFolder).Select(f => ReadVolume(Path.GetFileName(f))).OfType<VolumeInfo>()];

    /// <summary>Removes a named volume no container uses.</summary>
    public void RemoveVolume(string name, bool force = false)
    {
        if (!force && _instances.Values.Any(i => i.Info.Spec.Mounts.Any(m => m.Type == "volume" && m.Source == name)))
        {
            throw new ContainerException($"Volume {name} is in use by a container; remove the container first.");
        }

        var folder = Path.Combine(Store.VolumesFolder, name);
        if (!Directory.Exists(folder)) throw new ContainerException($"No such volume: {name}");
        LayerUnpacker.Delete(folder);
        Publish("volume", "destroy", name, new Dictionary<string, string>());
    }

    /// <summary>Removes stopped containers, unused images and dangling blobs; returns the bytes freed.</summary>
    public async Task<long> PruneAsync(bool allImages, CancellationToken cancellationToken = default)
    {
        foreach (var stopped in _instances.Values.Where(i => !i.Info.IsRunning).Select(i => i.Info.Id).ToList())
        {
            await RemoveAsync(stopped, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await Store.PruneAsync(IsImageInUse, allImages, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether a container uses an image.</summary>
    public bool IsImageInUse(string imageId) => _instances.Values.Any(i => i.Info.ImageId == imageId);

    /// <summary>Stops every container, as the app closes.</summary>
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        foreach (var instance in _instances.Values.Where(i => i.Process is { HasExited: false }))
        {
            instance.Restart?.Cancel();
            await TerminateAsync(instance, TimeSpan.FromSeconds(3), signal: null).ConfigureAwait(false);
        }

        lock (_eventsGate)
        {
            foreach (var subscriber in _subscribers) subscriber.Writer.TryComplete();
        }

        foreach (var instance in _instances.Values) instance.Log.Dispose();
        Store.Dispose();
        _gate.Dispose();
        _lifetime.Dispose();
    }

    private async Task LaunchAsync(Instance instance, CancellationToken cancellationToken)
    {
        var info = instance.Info;
        var image = await Store.FindAsync(info.ImageId, cancellationToken).ConfigureAwait(false)
            ?? throw new ContainerException($"The image of {info.Name} ({info.ImageName}) was removed.");
        instance.Image = image;
        var rootfs = RootfsOf(info.Id);
        string? emulation = null;
        if (image.Platform.NeedsEmulation(_options.Host))
        {
            if (_options.Emulator is not { } emulator)
            {
                throw new ContainerException($"{info.ImageName} is built only for {image.Platform}. TabStudio runs such images through Box64, which comes with the Microchip PIC toolchain in the Toolchain Store; install it, or use an image built for {_options.Host}.");
            }

            if (emulator.Problem(image.Platform, rootfs) is { } problem) throw new ContainerException(problem);
            emulation = emulator.Description;
        }

        info = info with { Emulation = emulation };
        instance.Info = info;
        var user = ContainerUser.Resolve(rootfs, info.Spec.User ?? image.Config.Config.User);
        var environment = Environment(image, info, user);
        var ports = Publishing(image, info.Spec);
        var names = new List<string> { info.Spec.Hostname ?? info.ShortId, info.Name };
        names.AddRange(info.Spec.Aliases);
        var lease = Network.Allocate(info.Id, info.Address, names, ports);
        instance.Lease = lease;
        try
        {
            PrepareFiles(rootfs, info, lease);
            var command = Command(image, info.Spec);
            if (command.Count == 0) throw new ContainerException($"{info.ImageName} has no command to run: give one after the image name.");
            var spec = ProcessSpec(rootfs, command, WorkingDirectory(image, info), environment, user, Mounts(info), info.Spec.Tty ? TerminalSize.Default : null);
            var runtime = Runtime(info, lease, environment);
            var process = runtime.Start(Wrap(instance, spec));
            instance.Process = process;
            if (!info.Spec.OpenStdin) process.CloseInput();
            instance.Exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secrets = environment.Where(e => e.Value.Length >= 4 && (e.Key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) || e.Key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) || e.Key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)))
                .Select(e => e.Value).OrderByDescending(v => v.Length).ToList();
            instance.Log.SetRedaction(text => secrets.Aggregate(text, (current, secret) => current.Replace(secret, "***", StringComparison.Ordinal)));
            var readers = Task.WhenAll(
                instance.Log.PumpAsync(process.Output, 1, process.IsTerminal, CancellationToken.None),
                process.Error is { } error ? instance.Log.PumpAsync(error, 2, false, CancellationToken.None) : Task.CompletedTask);
            instance.Info = info with
            {
                Status = ContainerStatus.Running,
                StartedAt = DateTimeOffset.UtcNow,
                Pid = process.Id,
                Address = lease.Address,
                Ports = lease.Ports,
                Emulation = emulation,
                Error = lease.Ports.FirstOrDefault(p => p.Note is not null)?.Note,
                Health = HealthOf(image, info.Spec) is { Disabled: false } ? HealthStatus.Starting : HealthStatus.None,
                FailingStreak = 0,
            };
            Save(instance.Info);
            RefreshHosts();
            Publish("container", "start", info.Id, Attributes(instance.Info));
            var stop = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            instance.Monitor = stop;
            _ = MonitorAsync(instance, process, readers, stop.Token);
            if (HealthOf(image, info.Spec) is { Disabled: false } health) _ = HealthAsync(instance, health, stop.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Network.Release(info.Id);
            instance.Lease = null;
            instance.Info = instance.Info with { Status = ContainerStatus.Dead, Error = ex.Message, Pid = 0, Ports = [] };
            Save(instance.Info);
            Publish("container", "die", info.Id, Attributes(instance.Info));
            throw ex as ContainerException ?? new ContainerException($"{info.Name} could not start: {ex.Message}", ex);
        }
    }

    private async Task MonitorAsync(Instance instance, ToolchainProcess process, Task readers, CancellationToken stop)
    {
        var exit = await process.Completion.ConfigureAwait(false);
        try
        {
            await readers.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }

        instance.Monitor?.Cancel();
        process.Dispose();
        Network.Release(instance.Info.Id);
        instance.Lease = null;
        var code = exit.Signal is { } signal ? 128 + signal : exit.ExitCode;
        var restart = !instance.Removed && !stop.IsCancellationRequested && instance.Info.Spec.Restart.Restarts(code, instance.Info.RestartCount, instance.Info.StoppedByUser);
        instance.Info = instance.Info with
        {
            Status = restart ? ContainerStatus.Restarting : ContainerStatus.Exited,
            ExitCode = code,
            FinishedAt = DateTimeOffset.UtcNow,
            Pid = 0,
            Ports = [],
            Health = instance.Info.Health == HealthStatus.None ? HealthStatus.None : HealthStatus.Unhealthy,
        };
        if (!instance.Removed) Save(instance.Info);
        RefreshHosts();
        Publish("container", "die", instance.Info.Id, new Dictionary<string, string>(Attributes(instance.Info)) { ["exitCode"] = code.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        instance.Exited.TrySetResult();
        if (!restart)
        {
            if (instance.Info.Spec.AutoRemove && !instance.Removed)
            {
                _ = Task.Run(() => RemoveAsync(instance.Info.Id, force: true, cancellationToken: CancellationToken.None), CancellationToken.None);
            }

            return;
        }

        // Docker's back-off: 100 ms, doubling, at most a minute; reset after ten good seconds.
        var ran = DateTimeOffset.UtcNow - (instance.Info.StartedAt ?? DateTimeOffset.UtcNow);
        var delay = ran > TimeSpan.FromSeconds(10) ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromMilliseconds(Math.Min(60_000, 100 * Math.Pow(2, Math.Min(instance.Info.RestartCount, 10))));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        instance.Restart = cancel;
        try
        {
            await Task.Delay(delay, cancel.Token).ConfigureAwait(false);
            await instance.Gate.WaitAsync(cancel.Token).ConfigureAwait(false);
            try
            {
                instance.Info = instance.Info with { RestartCount = instance.Info.RestartCount + 1 };
                await LaunchAsync(instance, cancel.Token).ConfigureAwait(false);
            }
            finally
            {
                instance.Gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            instance.Info = instance.Info with { Status = ContainerStatus.Exited };
            Save(instance.Info);
        }
        catch (ContainerException ex)
        {
            instance.Log.Write(2, "Restart failed: " + ex.Message);
        }
        finally
        {
            instance.Restart = null;
        }
    }

    private async Task HealthAsync(Instance instance, HealthcheckSpec health, CancellationToken stop)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var inStart = DateTimeOffset.UtcNow - started < health.StartPeriod;
                await Task.Delay(instance.Info.Health == HealthStatus.Starting ? Min(health.StartInterval, health.Interval) : health.Interval, stop).ConfigureAwait(false);
                var (code, output) = await ExecCaptureAsync(instance.Info.Id, health.Command, health.Timeout, stop).ConfigureAwait(false);
                var streak = code == 0 ? 0 : instance.Info.FailingStreak + 1;
                if (code != 0 && (streak == 1 || streak == health.Retries)) instance.Log.Write(2, $"Health check exited {code}: {output.Trim()}");
                var status = code == 0 ? HealthStatus.Healthy
                    : streak >= health.Retries && !inStart ? HealthStatus.Unhealthy
                    : instance.Info.Health;
                var changed = status != instance.Info.Health;
                instance.Info = instance.Info with { Health = status, FailingStreak = inStart && code != 0 ? 0 : streak, HealthOutput = output.Length > 4096 ? output[^4096..] : output };
                if (changed) Publish("container", "health_status: " + status.ToString().ToLowerInvariant(), instance.Info.Id, Attributes(instance.Info));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ContainerException)
        {
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private async Task TerminateAsync(Instance instance, TimeSpan timeout, int? signal)
    {
        if (instance.Process is not { HasExited: false } process)
        {
            if (instance.Info.Status == ContainerStatus.Restarting)
            {
                instance.Info = instance.Info with { Status = ContainerStatus.Exited };
                Save(instance.Info);
            }

            return;
        }

        if (timeout > TimeSpan.Zero)
        {
            SignalMain(process, signal ?? Signals.Parse(instance.Info.Spec.StopSignal ?? instance.Image?.Config.Config.StopSignal ?? "SIGTERM"));
            try
            {
                await process.Completion.WaitAsync(timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }

        process.Stop();
        try
        {
            await instance.Exited.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    /// <summary>Signals the container's main process: PRoot's first child.</summary>
    private static void SignalMain(ToolchainProcess process, int signal)
    {
        var children = ProcessTree.Descendants(process.Id);
        if (children.Count == 0 || !UnixFiles.TrySignal(children[0], signal)) UnixFiles.TrySignal(process.Id, signal);
    }

    private ProotRuntime Runtime(ContainerInfo info, ContainerNetwork.Lease lease, Dictionary<string, string> environment, IReadOnlyList<string>? sharedFolders = null)
    {
        var extra = new List<string> { Network.ProotOption(lease) };
        if (info.Emulation is not null && _options.Emulator is { } emulator) extra.AddRange(emulator.ProotArguments);
        return new ProotRuntime(new ProotRuntimeOptions
        {
            ProotPath = _options.ProotPath ?? "",
            LoaderPath = _options.LoaderPath ?? "",
            Rootfs = RootfsOf(info.Id),
            TemporaryDirectory = Path.Combine(Store.TemporaryFolder, "proot"),
            DisplayName = $"Container {info.Name} ({info.ImageName})",
            Shell = File.Exists(Path.Combine(RootfsOf(info.Id), "bin/bash")) ? "/bin/bash" : "/bin/sh",
            SearchPath = environment.GetValueOrDefault("PATH") ?? ProotRuntime.SearchPath,
            ExtraArguments = extra,
            ShareWorkingDirectory = sharedFolders is { Count: > 0 },
            SharedFolders = sharedFolders ?? [],
            ProcStandInRoot = _options.ProcStandInRoot,
        });
    }

    private ProcessStartSpec Wrap(Instance instance, ProcessStartSpec spec) =>
        instance.Info.Emulation is not null && _options.Emulator is { } emulator ? emulator.Wrap(spec) : spec;

    internal static ProcessStartSpec ProcessSpec(string rootfs, IReadOnlyList<string> command, string workingDirectory, Dictionary<string, string> environment,
        ContainerUser user, IReadOnlyList<ProcessMount> mounts, TerminalSize? terminal)
    {
        var program = command[0];
        if (!program.Contains('/', StringComparison.Ordinal))
        {
            var path = environment.GetValueOrDefault("PATH") ?? ProotRuntime.SearchPath;
            program = path.Split(':', StringSplitOptions.RemoveEmptyEntries)
                .Select(folder => folder.TrimEnd('/') + "/" + program)
                .FirstOrDefault(candidate => Exists(SafePath.Resolve(rootfs, candidate)))
                ?? throw new ContainerException($"exec: \"{command[0]}\": executable file not found in $PATH");
        }
        else if (!program.StartsWith('/'))
        {
            program = workingDirectory.TrimEnd('/') + "/" + program;
        }

        Directory.CreateDirectory(SafePath.ResolveFollowing(rootfs, workingDirectory));
        return new ProcessStartSpec
        {
            Program = program,
            Arguments = [.. command.Skip(1)],
            WorkingDirectory = workingDirectory,
            Environment = environment.ToDictionary(e => e.Key, e => (string?)e.Value, StringComparer.Ordinal),
            Mounts = mounts,
            UserId = user.Uid == 0 ? null : user.Uid,
            GroupId = user.Uid == 0 ? null : user.Gid,
            Terminal = terminal,
        };
    }

    private static bool Exists(string path) => File.Exists(path) || new FileInfo(path).LinkTarget is not null;

    internal static Dictionary<string, string> Environment(StoredImage image, ContainerInfo info, ContainerUser user)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = ProotRuntime.SearchPath,
            ["HOSTNAME"] = info.Spec.Hostname ?? info.ShortId,
            ["HOME"] = user.Home,
            ["USER"] = user.Name,
            ["LOGNAME"] = user.Name,
            ["SHELL"] = "/bin/sh",
            ["LANG"] = "C.UTF-8",
        };
        foreach (var entry in image.Config.Config.Env ?? [])
        {
            var equals = entry.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0) environment[entry[..equals]] = entry[(equals + 1)..];
        }

        foreach (var (key, value) in info.Spec.Environment) environment[key] = value;
        if (info.Spec.Tty) environment["TERM"] = "xterm";
        return environment;
    }

    internal static string WorkingDirectory(StoredImage image, ContainerInfo info) =>
        info.Spec.WorkingDirectory is { Length: > 0 } dir ? dir : image.Config.Config.WorkingDir is { Length: > 0 } imageDir ? imageDir : "/";

    internal static List<string> Command(StoredImage image, ContainerSpec spec)
    {
        var entrypoint = spec.Entrypoint ?? image.Config.Config.Entrypoint ?? [];
        // An entrypoint given at run time drops the image's command, as Docker does.
        var command = spec.Command ?? (spec.Entrypoint is null ? image.Config.Config.Cmd : null) ?? [];
        return [.. entrypoint.Concat(command)];
    }

    internal static HealthcheckSpec? HealthOf(StoredImage image, ContainerSpec spec)
    {
        if (spec.Healthcheck is { } given) return given;
        if (image.Config.Config.Healthcheck is not { Test: { Count: > 0 } test } check) return null;
        static TimeSpan Ns(long? value, TimeSpan fallback) => value is > 0 ? TimeSpan.FromTicks(value.Value / 100) : fallback;
        return new HealthcheckSpec(test)
        {
            Interval = Ns(check.Interval, TimeSpan.FromSeconds(30)),
            Timeout = Ns(check.Timeout, TimeSpan.FromSeconds(30)),
            StartPeriod = Ns(check.StartPeriod, TimeSpan.Zero),
            StartInterval = Ns(check.StartInterval, TimeSpan.FromSeconds(5)),
            Retries = check.Retries is > 0 ? check.Retries.Value : 3,
        };
    }

    /// <summary>Maps declared TCP listeners as well as explicit ports, including IPv6-only listeners on shared loopback.</summary>
    internal static List<PortBinding> Publishing(StoredImage image, ContainerSpec spec)
    {
        var ports = new List<PortBinding>(spec.Ports);
        foreach (var exposed in Exposed(image, spec).Where(p => ports.All(b => b.ContainerPort != p)))
        {
            ports.Add(new PortBinding(exposed, "tcp", spec.PublishAll ? 0 : null));
        }

        return ports;
    }

    /// <summary>The ports an image and its container say they listen on.</summary>
    public static IReadOnlyList<int> Exposed(StoredImage image, ContainerSpec spec) =>
        [.. (image.Config.Config.ExposedPorts?.Keys ?? Enumerable.Empty<string>())
            .Where(k => !k.Contains('/', StringComparison.Ordinal) || k.EndsWith("/tcp", StringComparison.Ordinal))
            .Select(k => int.TryParse(k.Split('/')[0], System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0)
            .Where(p => p > 0).Concat(spec.Expose).Distinct().Order()];

    private List<ProcessMount> Mounts(ContainerInfo info)
    {
        var mounts = new List<ProcessMount>();
        // UNIX sockets have a 108-byte kernel path limit. A rootfs beneath an image's 64-digit ID exceeds it;
        // use a short private host path shared by the main trace tree and independent exec/health trace trees.
        var run = RunDirectory(info.Id);
        Directory.CreateDirectory(run);
        SeedVolume(info, "/run", run);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(run, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        mounts.Add(new ProcessMount(run, "/run"));
        var definitions = info.Spec.Mounts.ToList();
        foreach (var target in Get(info.Id).Image?.Config.Config.Volumes?.Keys ?? Enumerable.Empty<string>())
        {
            if (definitions.All(m => m.Target.TrimEnd('/') != target.TrimEnd('/')))
                definitions.Add(new ContainerMount("volume", info.ShortId + "-image-" + definitions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), target));
        }
        var index = 0;
        foreach (var mount in definitions)
        {
            string source;
            switch (mount.Type)
            {
                case "bind":
                    source = Path.GetFullPath(mount.Source ?? throw new ContainerException("A bind mount needs a source path."));
                    if (!File.Exists(source) && !Directory.Exists(source)) Directory.CreateDirectory(source);
                    break;
                case "volume":
                    var name = mount.Source ?? $"{info.ShortId}-anonymous-{index}";
                    source = CreateVolume(name).Path;
                    SeedVolume(info, mount.Target, source);
                    break;
                case "tmpfs":
                    source = Path.Combine(Store.ContainersFolder, info.Id, "tmpfs", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (Directory.Exists(source)) LayerUnpacker.Delete(source);
                    Directory.CreateDirectory(source);
                    break;
                default:
                    throw new ContainerException($"Mounts of type '{mount.Type}' are not supported.");
            }

            mounts.Add(new ProcessMount(source, "/" + string.Join('/', SafePath.Split(mount.Target))));
            index++;
        }

        return mounts;
    }

    private static string RunDirectory(string id) => Path.Combine(Path.GetTempPath(), "ts-run-" + id[..16]);

    /// <summary>An empty new volume starts with what the image has at its path, as Docker copies it.</summary>
    private void SeedVolume(ContainerInfo info, string target, string volume)
    {
        if (Directory.EnumerateFileSystemEntries(volume).Any()) return;
        var source = SafePath.ResolveFollowing(RootfsOf(info.Id), target);
        if (!Directory.Exists(source)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var destination = Path.Combine(volume, Path.GetFileName(entry));
            if (Directory.Exists(entry) && new FileInfo(entry).LinkTarget is null)
            {
                TreeCopy.CopyAsync(entry, destination).GetAwaiter().GetResult();
            }
            else if (new FileInfo(entry).LinkTarget is { } link)
            {
                File.CreateSymbolicLink(destination, link);
            }
            else
            {
                File.Copy(entry, destination);
            }
        }
    }

    private void PrepareFiles(string rootfs, ContainerInfo info, ContainerNetwork.Lease lease)
    {
        var etc = SafePath.ResolveFollowing(rootfs, "/etc");
        Directory.CreateDirectory(etc);
        Directory.CreateDirectory(SafePath.ResolveFollowing(rootfs, "/tmp"));
        WriteReplacing(Path.Combine(etc, "hosts"), Network.Hosts(lease, info.Spec.ExtraHosts));
        WriteReplacing(Path.Combine(etc, "hostname"), (info.Spec.Hostname ?? info.ShortId) + "\n");
        var dns = _options.DnsServers();
        if (dns.Count == 0 && File.Exists("/etc/resolv.conf"))
        {
            dns = [.. File.ReadLines("/etc/resolv.conf").Where(l => l.StartsWith("nameserver ", StringComparison.Ordinal)).Select(l => l[11..].Trim())
                .Where(a => !a.StartsWith("127.", StringComparison.Ordinal))];
        }

        if (dns.Count == 0) dns = ["8.8.8.8", "1.1.1.1"];
        WriteReplacing(Path.Combine(etc, "resolv.conf"), string.Concat(dns.Select(d => $"nameserver {d}\n")));
    }

    /// <summary>Rewrites every running container's <c>/etc/hosts</c>, so new containers resolve by name.</summary>
    private void RefreshHosts()
    {
        foreach (var instance in _instances.Values.Where(i => i.Lease is not null && i.Process is { HasExited: false }))
        {
            try
            {
                var etc = SafePath.ResolveFollowing(RootfsOf(instance.Info.Id), "/etc");
                WriteReplacing(Path.Combine(etc, "hosts"), Network.Hosts(instance.Lease!, instance.Info.Spec.ExtraHosts));
            }
            catch (IOException)
            {
            }
        }
    }

    private static void WriteReplacing(string path, string text)
    {
        if (new FileInfo(path).LinkTarget is not null || Directory.Exists(path)) LayerUnpacker.Delete(path);
        File.WriteAllText(path, text);
    }

    private VolumeInfo? ReadVolume(string name)
    {
        var meta = Path.Combine(Store.VolumesFolder, name, "volume.json");
        try
        {
            return File.Exists(meta) ? JsonSerializer.Deserialize<VolumeInfo>(File.ReadAllBytes(meta)) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Instance Get(string idOrName) =>
        Find(idOrName) is { } info && _instances.TryGetValue(info.Id, out var instance) ? instance : throw new ContainerException($"No such container: {idOrName}");

    private void Save(ContainerInfo info)
    {
        var folder = Path.Combine(Store.ContainersFolder, info.Id);
        if (!Directory.Exists(folder)) return;
        var file = Path.Combine(folder, "container.json");
        File.WriteAllBytes(file + ".partial", JsonSerializer.SerializeToUtf8Bytes(info, StateJson));
        File.Move(file + ".partial", file, overwrite: true);
    }

    private static Dictionary<string, string> Attributes(ContainerInfo info)
    {
        var attributes = new Dictionary<string, string>(info.Spec.Labels, StringComparer.Ordinal) { ["name"] = info.Name, ["image"] = info.ImageName };
        return attributes;
    }

    private void Publish(string type, string action, string id, IReadOnlyDictionary<string, string> attributes)
    {
        var item = new EngineEvent(type, action, id, attributes, DateTimeOffset.UtcNow);
        lock (_eventsGate)
        {
            foreach (var subscriber in _subscribers) subscriber.Writer.TryWrite(item);
        }

        Changed?.Invoke(this, item);
    }

    private string MakeName()
    {
        for (var attempt = 0; ; attempt++)
        {
            var name = $"{Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)]}_{Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)]}{(attempt > 5 ? RandomNumberGenerator.GetInt32(100).ToString(System.Globalization.CultureInfo.InvariantCulture) : "")}";
            if (_instances.Values.All(i => i.Info.Name != name)) return name;
        }
    }

    private static bool IsValidName(string name) => name.Length > 0 && char.IsAsciiLetterOrDigit(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-');

    private static async Task<string> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            return "";
        }
    }

    private sealed class Instance(ContainerInfo info, ContainerLog log)
    {
        public ContainerInfo Info { get; set; } = info;
        public ContainerLog Log { get; } = log;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public ToolchainProcess? Process { get; set; }
        public StoredImage? Image { get; set; }
        public ContainerNetwork.Lease? Lease { get; set; }
        public CancellationTokenSource? Monitor { get; set; }
        public CancellationTokenSource? Restart { get; set; }
        public TaskCompletionSource Exited { get; set; } = CompletedSource();
        public bool Removed { get; set; }

        private static TaskCompletionSource CompletedSource()
        {
            var source = new TaskCompletionSource();
            source.SetResult();
            return source;
        }
    }
}

