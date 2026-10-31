using System.Text.Json.Serialization;

namespace TabStudio.Containers.Runtime;

/// <summary>How to create a container, as <c>docker run</c>, the Engine API and Compose describe one.</summary>
public sealed record ContainerSpec
{
    /// <summary>The image name or ID.</summary>
    public required string Image { get; init; }
    /// <summary>The container's name; one is made up when it has none.</summary>
    public string? Name { get; init; }
    /// <summary>Replaces the image's entrypoint; an empty list clears it.</summary>
    public IReadOnlyList<string>? Entrypoint { get; init; }
    /// <summary>Replaces the image's command.</summary>
    public IReadOnlyList<string>? Command { get; init; }
    /// <summary>Variables added to the image's environment, which they override.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
    /// <summary>Replaces the image's working directory.</summary>
    public string? WorkingDirectory { get; init; }
    /// <summary>Replaces the image's user: a name or number, with an optional <c>:group</c>.</summary>
    public string? User { get; init; }
    /// <summary>Ports to publish on the device.</summary>
    public IReadOnlyList<PortBinding> Ports { get; init; } = [];
    /// <summary>Ports the container listens on in addition to the image's, which other containers reach.</summary>
    public IReadOnlyList<int> Expose { get; init; } = [];
    /// <summary>Publish every exposed port on a free localhost port, as <c>docker run -P</c> does.</summary>
    public bool PublishAll { get; init; }
    /// <summary>Bind mounts, named volumes and temporary folders.</summary>
    public IReadOnlyList<ContainerMount> Mounts { get; init; } = [];
    /// <summary>What happens when it exits.</summary>
    public RestartPolicy Restart { get; init; } = RestartPolicy.No;
    /// <summary>Labels, such as Compose's project and service.</summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
    /// <summary>The host name it sees; its short ID when unset.</summary>
    public string? Hostname { get; init; }
    /// <summary>Other names other containers reach it by, such as its Compose service name.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];
    /// <summary>Extra <c>/etc/hosts</c> entries, <c>name:address</c>; <c>host-gateway</c> means the device.</summary>
    public IReadOnlyList<string> ExtraHosts { get; init; } = [];
    /// <summary>A terminal for its main process.</summary>
    public bool Tty { get; init; }
    /// <summary>Keeps its standard input open.</summary>
    public bool OpenStdin { get; init; }
    /// <summary>Removes it when it exits, as <c>--rm</c>.</summary>
    public bool AutoRemove { get; init; }
    /// <summary>Replaces the image's health check.</summary>
    public HealthcheckSpec? Healthcheck { get; init; }
    /// <summary>The platform to pull, such as <c>linux/amd64</c>, when it is not the device's own.</summary>
    public string? Platform { get; init; }
    /// <summary>The signal that stops it; the image's, or SIGTERM.</summary>
    public string? StopSignal { get; init; }
    /// <summary>How long a stop waits before it kills; ten seconds by default.</summary>
    public TimeSpan? StopTimeout { get; init; }
    /// <summary>Settings asked for that cannot work without root, each a one-line reason, shown and otherwise ignored.</summary>
    public IReadOnlyList<string> Unsupported { get; init; } = [];
}

/// <summary>A port published on the device.</summary>
/// <param name="ContainerPort">The port the container listens on.</param>
/// <param name="Protocol"><c>tcp</c> or <c>udp</c>.</param>
/// <param name="HostPort">The device port; a free one when unset.</param>
/// <param name="HostIp">The device address; localhost when unset, every interface for <c>0.0.0.0</c>.</param>
public sealed record PortBinding(int ContainerPort, string Protocol = "tcp", int? HostPort = null, string? HostIp = null)
{
    /// <summary>Parses <c>8080:80</c>, <c>127.0.0.1:8080:80/tcp</c>, <c>80</c> or <c>8000-8001:8000-8001</c> (each port).</summary>
    public static IReadOnlyList<PortBinding> Parse(string text)
    {
        var protocol = "tcp";
        var slash = text.LastIndexOf('/');
        if (slash > 0)
        {
            protocol = text[(slash + 1)..].ToLowerInvariant();
            text = text[..slash];
        }

        var parts = text.StartsWith('[') ? SplitV6(text) : text.Split(':');
        string? ip = null;
        string? host = null;
        string container;
        switch (parts.Length)
        {
            case 1: container = parts[0]; break;
            case 2: host = parts[0]; container = parts[1]; break;
            case 3: ip = parts[0].Trim('[', ']'); host = parts[1]; container = parts[2]; break;
            default: throw new FormatException($"'{text}' is not a port such as 8080:80.");
        }

        var containerPorts = Range(container);
        var hostPorts = string.IsNullOrEmpty(host) ? null : Range(host, allowZero: true);
        if (hostPorts is not null && hostPorts.Count != containerPorts.Count)
        {
            throw new FormatException($"'{text}' maps ranges of different sizes.");
        }

        return [.. containerPorts.Select((port, i) => new PortBinding(port, protocol, hostPorts is null ? null : hostPorts.Count == 1 && containerPorts.Count > 1 ? null : hostPorts[i], ip))];
    }

    private static string[] SplitV6(string text)
    {
        var close = text.IndexOf(']', StringComparison.Ordinal);
        return [text[..(close + 1)], .. text[(close + 2)..].Split(':')];
    }

    private static List<int> Range(string text, bool allowZero = false)
    {
        var dash = text.IndexOf('-', StringComparison.Ordinal);
        var first = int.Parse(dash < 0 ? text : text[..dash], System.Globalization.CultureInfo.InvariantCulture);
        var last = dash < 0 ? first : int.Parse(text[(dash + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        if (first < (allowZero ? 0 : 1) || first > 65535 || last < first || last > 65535 || first == 0 && last != 0)
        {
            throw new FormatException($"'{text}' is not a port or port range.");
        }

        return [.. Enumerable.Range(first, last - first + 1)];
    }
}

/// <summary>A mount: a device folder or file, a named volume, or a temporary folder.</summary>
/// <param name="Type"><c>bind</c>, <c>volume</c> or <c>tmpfs</c>.</param>
/// <param name="Source">The device path, the volume's name, or nothing for an anonymous volume or tmpfs.</param>
/// <param name="Target">The path in the container.</param>
/// <param name="ReadOnly">Whether it is mounted read-only; PRoot cannot enforce this, so it is advisory.</param>
public sealed record ContainerMount(string Type, string? Source, string Target, bool ReadOnly = false)
{
    /// <summary>Parses <c>-v</c>: <c>/device/path:/target[:ro]</c> or <c>name:/target</c>.</summary>
    public static ContainerMount ParseVolume(string text)
    {
        var parts = text.Split(':').ToList();
        var readOnly = false;
        string[] options = ["rw", "ro", "z", "Z", "cached", "delegated", "consistent", "nocopy"];
        if (parts.Count > 1 && parts[^1].Split(',').All(o => options.Contains(o, StringComparer.Ordinal)))
        {
            readOnly = parts[^1].Split(',').Contains("ro", StringComparer.Ordinal);
            parts.RemoveAt(parts.Count - 1);
        }

        if (parts.Count > 2)
        {
            throw new FormatException($"'{text}' is not a volume such as /path:/target or name:/target[:ro].");
        }

        return parts.Count switch
        {
            1 => new ContainerMount("volume", null, parts[0], readOnly),
            _ => new ContainerMount(parts[0].StartsWith('/') || parts[0].StartsWith('.') || parts[0].StartsWith('~') ? "bind" : "volume", parts[0], parts[1], readOnly),
        };
    }
}

/// <summary>A restart policy: <c>no</c>, <c>always</c>, <c>unless-stopped</c> or <c>on-failure[:max]</c>.</summary>
/// <param name="Name">The policy.</param>
/// <param name="MaximumRetries">For <c>on-failure</c>, how often to retry; 0 for no limit.</param>
public sealed record RestartPolicy(string Name, int MaximumRetries = 0)
{
    /// <summary>Never restarts.</summary>
    public static RestartPolicy No { get; } = new("no");

    /// <summary>Parses a policy.</summary>
    public static RestartPolicy Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return No;
        var parts = text.Trim().ToLowerInvariant().Split(':');
        return parts[0] switch
        {
            "no" or "always" or "unless-stopped" => new(parts[0]),
            "on-failure" => new("on-failure", parts.Length > 1 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0),
            _ => throw new FormatException($"'{text}' is not a restart policy (no, always, unless-stopped, on-failure[:max])."),
        };
    }

    /// <summary>Whether a container that exited with <paramref name="exitCode"/> restarts.</summary>
    public bool Restarts(int exitCode, int restarts, bool stoppedByUser) => !stoppedByUser && Name switch
    {
        "always" or "unless-stopped" => true,
        "on-failure" => exitCode != 0 && (MaximumRetries == 0 || restarts < MaximumRetries),
        _ => false,
    };
}

/// <summary>A health check: a command run in the container, and how often.</summary>
/// <param name="Test"><c>["CMD", …]</c>, <c>["CMD-SHELL", "…"]</c> or <c>["NONE"]</c>.</param>
public sealed record HealthcheckSpec(IReadOnlyList<string> Test)
{
    /// <summary>Time between checks.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>How long one check may take.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Failures in this time after starting do not count.</summary>
    public TimeSpan StartPeriod { get; init; }
    /// <summary>Time between checks during the start period.</summary>
    public TimeSpan StartInterval { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Failures in a row that make a container unhealthy.</summary>
    public int Retries { get; init; } = 3;
    /// <summary>Whether the check is turned off.</summary>
    [JsonIgnore] public bool Disabled => Test.Count == 0 || Test[0] == "NONE";

    /// <summary>The command to run for one check, as a program and its arguments.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Command => Test.Count == 0 ? [] : Test[0] switch
    {
        "CMD" => [.. Test.Skip(1)],
        "CMD-SHELL" => ["/bin/sh", "-c", string.Join(' ', Test.Skip(1))],
        _ => [.. Test],
    };
}

/// <summary>A container's state, as <c>docker ps</c> reports it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ContainerStatus>))]
public enum ContainerStatus
{
    /// <summary>Made but never started.</summary>
    Created,
    /// <summary>Its main process runs.</summary>
    Running,
    /// <summary>Waiting to start again under its restart policy.</summary>
    Restarting,
    /// <summary>Its main process ended.</summary>
    Exited,
    /// <summary>Being removed.</summary>
    Removing,
    /// <summary>It could not start; see its error.</summary>
    Dead,
}

/// <summary>A container's health, from its health check.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HealthStatus>))]
public enum HealthStatus
{
    /// <summary>It has no health check.</summary>
    None,
    /// <summary>Still in its start period.</summary>
    Starting,
    /// <summary>The last check passed.</summary>
    Healthy,
    /// <summary>Too many checks failed.</summary>
    Unhealthy,
}

/// <summary>A published port as it ended up: the device port it is on.</summary>
/// <param name="ContainerPort">The container's port.</param>
/// <param name="Protocol"><c>tcp</c> or <c>udp</c>.</param>
/// <param name="HostPort">The device port.</param>
/// <param name="HostIp">The device address.</param>
/// <param name="Note">Why it is not the port asked for, if it is not.</param>
public sealed record PublishedPort(int ContainerPort, string Protocol, int HostPort, string HostIp, string? Note = null);

/// <summary>A container, as the engine keeps it.</summary>
public sealed record ContainerInfo
{
    /// <summary>The ID: 64 hex digits.</summary>
    public required string Id { get; init; }
    /// <summary>The name, without Docker's leading slash.</summary>
    public required string Name { get; init; }
    /// <summary>How it was created.</summary>
    public required ContainerSpec Spec { get; init; }
    /// <summary>The image's ID.</summary>
    public required string ImageId { get; init; }
    /// <summary>The image's name as given.</summary>
    public required string ImageName { get; init; }
    /// <summary>When it was made.</summary>
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>Its state.</summary>
    public ContainerStatus Status { get; init; } = ContainerStatus.Created;
    /// <summary>Its main process's exit code, after it exited.</summary>
    public int ExitCode { get; init; }
    /// <summary>When it last started.</summary>
    public DateTimeOffset? StartedAt { get; init; }
    /// <summary>When it last exited.</summary>
    public DateTimeOffset? FinishedAt { get; init; }
    /// <summary>How often its restart policy restarted it.</summary>
    public int RestartCount { get; init; }
    /// <summary>The device process (PRoot) of its main process, while it runs.</summary>
    public int Pid { get; init; }
    /// <summary>Its own loopback address, by which other containers reach it.</summary>
    public string? Address { get; init; }
    /// <summary>Its published ports, while it runs.</summary>
    public IReadOnlyList<PublishedPort> Ports { get; init; } = [];
    /// <summary>Its health.</summary>
    public HealthStatus Health { get; init; }
    /// <summary>Failed checks in a row.</summary>
    public int FailingStreak { get; init; }
    /// <summary>The last check's output.</summary>
    public string? HealthOutput { get; init; }
    /// <summary>Why it did not start, or a note about how it runs.</summary>
    public string? Error { get; init; }
    /// <summary>Whether it runs its image through an emulator.</summary>
    public string? Emulation { get; init; }
    /// <summary>Whether a person stopped it, which <c>unless-stopped</c> respects.</summary>
    public bool StoppedByUser { get; init; }
    /// <summary>The first 12 digits of its ID.</summary>
    [JsonIgnore] public string ShortId => Id[..12];
    /// <summary>Whether its process runs.</summary>
    [JsonIgnore] public bool IsRunning => Status is ContainerStatus.Running or ContainerStatus.Restarting;
    /// <summary>A label's value.</summary>
    public string? Label(string name) => Spec.Labels.TryGetValue(name, out var value) ? value : null;
}

/// <summary>One line or chunk of a container's output.</summary>
/// <param name="Time">When it was written.</param>
/// <param name="Stream">1 for standard output, 2 for standard error.</param>
/// <param name="Text">What was written, a line without its line break, or a terminal chunk.</param>
public sealed record ContainerLogEntry(DateTimeOffset Time, int Stream, string Text);

/// <summary>Resource use, from <c>/proc</c>, of a container's processes.</summary>
/// <param name="CpuSeconds">Processor time used so far.</param>
/// <param name="MemoryBytes">Resident memory now.</param>
/// <param name="Processes">How many processes it has.</param>
public sealed record ContainerStats(double CpuSeconds, long MemoryBytes, int Processes);

/// <summary>Something that happened to a container or image, as <c>docker events</c> reports.</summary>
/// <param name="Type"><c>container</c>, <c>image</c>, <c>volume</c> or <c>network</c>.</param>
/// <param name="Action">Such as <c>create</c>, <c>start</c>, <c>die</c>, <c>health_status: healthy</c>.</param>
/// <param name="Id">The object's ID.</param>
/// <param name="Attributes">Its name, image and labels.</param>
/// <param name="Time">When.</param>
public sealed record EngineEvent(string Type, string Action, string Id, IReadOnlyDictionary<string, string> Attributes, DateTimeOffset Time);

/// <summary>A named volume.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its folder on the device.</param>
/// <param name="Created">When it was made.</param>
/// <param name="Labels">Labels, such as Compose's project.</param>
public sealed record VolumeInfo(string Name, string Path, DateTimeOffset Created, IReadOnlyDictionary<string, string> Labels);

/// <summary>A one-line problem that keeps a container from running here, which callers show as it is.</summary>
public sealed class ContainerException(string message, Exception? inner = null) : Exception(message, inner);

