using System.Globalization;

namespace TabStudio.App.Services.Link;

/// <summary>How TabStudio Link words times and latencies for people.</summary>
public static class LinkDisplay
{
    /// <summary>A latency in milliseconds, such as <c>12 ms</c>, or <see langword="null"/> before the first ping.</summary>
    /// <param name="latency">The round trip.</param>
    public static string? Latency(TimeSpan? latency) =>
        latency is { } value ? $"{Math.Max(1, (int)Math.Round(value.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture)} ms" : null;

    /// <summary>How long ago something happened, such as <c>just now</c>, <c>5 minutes ago</c> or <c>3 days ago</c>.</summary>
    /// <param name="moment">When it happened.</param>
    /// <param name="now">Now; the current time when <see langword="null"/>.</param>
    public static string Ago(DateTimeOffset moment, DateTimeOffset? now = null)
    {
        var elapsed = (now ?? DateTimeOffset.UtcNow) - moment;
        return elapsed switch
        {
            { TotalMinutes: < 1 } => "just now",
            { TotalMinutes: < 2 } => "a minute ago",
            { TotalHours: < 1 } => $"{((int)elapsed.TotalMinutes).ToString(CultureInfo.InvariantCulture)} minutes ago",
            { TotalHours: < 2 } => "an hour ago",
            { TotalDays: < 1 } => $"{((int)elapsed.TotalHours).ToString(CultureInfo.InvariantCulture)} hours ago",
            { TotalDays: < 2 } => "yesterday",
            _ => $"{((int)elapsed.TotalDays).ToString(CultureInfo.InvariantCulture)} days ago",
        };
    }
}
