namespace TabStudio.Services.Compose;

/// <summary>A repository's Compose model, without a container engine.</summary>
public sealed record ComposeProject(string Directory, IReadOnlyList<string> Files, IReadOnlyDictionary<string, ComposeService> Services)
{
    public string? DevelopmentService { get; init; }
    public IReadOnlyList<string> RunServices { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Variables every run gets, rewritten to the services' local endpoints like a Compose application's own
    /// environment: for services TabStudio offers without a Compose file, such as the database a Prisma schema names,
    /// its <c>DATABASE_URL</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string?> RunEnvironment { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Variables every run gets set to a service's published port, by service name: a Laravel app's <c>DB_PORT</c>
    /// for the database its Compose file and <c>.env</c> name.
    /// </summary>
    public IReadOnlyDictionary<string, string> RunPorts { get; init; } = new Dictionary<string, string>();

    /// <summary>Where the services come from when it is not a Compose file, such as "src/prisma/schema.prisma".</summary>
    public string? Origin { get; init; }
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
    public string? WorkingDirectory { get; init; }
    public string? User { get; init; }
    public string? Platform { get; init; }
    public string? Restart { get; init; }
    public IReadOnlyList<string> Profiles { get; init; } = [];
    public IReadOnlyList<string> Unsupported { get; init; } = [];
}

public sealed record ServicePort(int Target, int Published, string Protocol = "tcp", string? HostIp = null);
/// <summary>A service's volume: a named volume, or a bind of a host folder or file.</summary>
/// <param name="Source">The named volume, or the bind's host path; <see langword="null"/> for an anonymous volume.</param>
/// <param name="Target">The path inside the service.</param>
/// <param name="ReadOnly">Whether the service only reads it.</param>
/// <param name="IsBind">Whether it binds a host path.</param>
/// <param name="CreateHostPath">
/// Whether a missing bind source is created as an empty folder, as Docker does: always for the short syntax, and
/// for the long syntax unless <c>bind.create_host_path</c> is <c>false</c>.
/// </param>
public sealed record ServiceVolume(string? Source, string Target, bool ReadOnly, bool IsBind, bool CreateHostPath = true);
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

