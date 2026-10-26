namespace TabStudio.Services.Compose;

/// <summary>A repository's Compose model, without a container engine.</summary>
public sealed record ComposeProject(string Directory, IReadOnlyList<string> Files, IReadOnlyDictionary<string, ComposeService> Services)
{
    public string? DevelopmentService { get; init; }
    public IReadOnlyList<string> RunServices { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record ComposeService(string Name, string? Image)
{
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();
    public IReadOnlyList<ServicePort> Ports { get; init; } = [];
    public IReadOnlyList<ServiceVolume> Volumes { get; init; } = [];
    public IReadOnlyList<ServiceDependency> Dependencies { get; init; } = [];
    public ServiceHealthcheck? Healthcheck { get; init; }
    public IReadOnlyList<string>? Command { get; init; }
    public IReadOnlyList<string>? Entrypoint { get; init; }
    public string? BuildContext { get; init; }
    public IReadOnlyList<string> Profiles { get; init; } = [];
    public IReadOnlyList<string> Unsupported { get; init; } = [];
}

public sealed record ServicePort(int Target, int Published, string Protocol = "tcp", string? HostIp = null);
public sealed record ServiceVolume(string? Source, string Target, bool ReadOnly, bool IsBind);
public sealed record ServiceDependency(string Name, string Condition = "service_started", bool Required = true);
public sealed record ServiceHealthcheck(IReadOnlyList<string> Test)
{
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan StartPeriod { get; init; }
    public TimeSpan StartInterval { get; init; } = TimeSpan.FromSeconds(5);
    public int Retries { get; init; } = 3;
    public bool Disabled { get; init; }
}

/// <summary>Configuration errors identify the source file without printing secret values.</summary>
public sealed class ComposeException(string message) : Exception(message);
