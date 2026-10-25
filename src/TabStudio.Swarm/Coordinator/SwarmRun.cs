namespace TabStudio.Swarm.Coordinator;

/// <summary>A build running on the swarm: what was sent, and how each device received it.</summary>
public sealed class SwarmRun
{
    private readonly Lock _gate = new();
    private readonly List<SwarmDeployment> _deployments = [];

    internal SwarmRun(long id, SwarmBuild build, string? webSecret)
    {
        Id = id;
        Build = build;
        WebSecret = webSecret;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The run's number, counting up from 1 for every build the coordinator sends.</summary>
    public long Id { get; }

    /// <summary>The build.</summary>
    public SwarmBuild Build { get; }

    /// <summary>When the coordinator started sending it.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Whether the devices debug it.</summary>
    public bool IsDebugging => Build.Debug is not null;

    /// <summary>How each device received the build so far, in the order they finished, devices that joined later included.</summary>
    public IReadOnlyList<SwarmDeployment> Deployments
    {
        get
        {
            lock (_gate)
            {
                return [.. _deployments];
            }
        }
    }

    /// <summary>The secret a device's web gateway requires, for a web app.</summary>
    internal string? WebSecret { get; }

    internal void Add(SwarmDeployment deployment)
    {
        lock (_gate)
        {
            _deployments.RemoveAll(d => ReferenceEquals(d.Device, deployment.Device));
            _deployments.Add(deployment);
        }
    }
}

/// <summary>How one device received a build.</summary>
/// <param name="Device">The device.</param>
/// <param name="Error">Why it could not receive or start the build, or <see langword="null"/> when it did.</param>
/// <param name="FilesSent">How many files travelled; the device had the others already.</param>
/// <param name="FileCount">How many files the build has.</param>
/// <param name="BytesSent">How many bytes travelled.</param>
/// <param name="Duration">How long it took, from asking what the device lacks to its answer that the build runs.</param>
public sealed record SwarmDeployment(SwarmDevice Device, string? Error, int FilesSent, int FileCount, long BytesSent, TimeSpan Duration)
{
    /// <summary>Whether the device runs the build.</summary>
    public bool Succeeded => Error is null;
}
