namespace TabStudio.Link.Messaging;

/// <summary>How a <see cref="LinkConnection"/> notices that the other device is gone.</summary>
public sealed record LinkConnectionOptions
{
    /// <summary>Neither pings nor gives up on silence.</summary>
    public static LinkConnectionOptions Default { get; } = new();

    /// <summary>
    /// How often this side pings the other, which also measures the latency; <see langword="null"/> to never ping.
    /// Clients ping, so a host that sleeps or leaves the network is noticed within seconds.
    /// </summary>
    public TimeSpan? PingInterval { get; init; }

    /// <summary>How long a ping may go unanswered before the connection is closed as lost.</summary>
    public TimeSpan PingTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long the other side may stay silent before the connection is closed as lost; <see langword="null"/> for
    /// no limit. Hosts use it, since clients ping them.
    /// </summary>
    public TimeSpan? IdleTimeout { get; init; }
}
