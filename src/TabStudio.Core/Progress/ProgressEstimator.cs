using System.Globalization;

namespace TabStudio.Core.Progress;

/// <summary>
/// Estimates how long a stage of an operation has left from how fast its numbers moved: the rate over the last
/// stretch of samples, so a download that speeds up or stalls is reflected within seconds, and nothing at all until
/// there is enough to go on (a few seconds and a few percent), rather than a wild first guess.
/// </summary>
public sealed class ProgressEstimator
{
    private static readonly TimeSpan MinimumElapsed = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(20);
    private const double MinimumFraction = 0.02;

    private readonly Queue<(TimeSpan Time, double Fraction)> _samples = new();
    private string? _stage;
    private TimeSpan _stageStarted;

    /// <summary>Adds a sample. A new stage starts the estimate over.</summary>
    /// <param name="progress">Where the operation stands.</param>
    /// <param name="now">The time since the operation started.</param>
    public void Add(OperationProgress progress, TimeSpan now)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (!string.Equals(progress.Stage, _stage, StringComparison.Ordinal))
        {
            _stage = progress.Stage;
            _stageStarted = now;
            _samples.Clear();
        }

        if (progress.Fraction is not { } fraction)
        {
            return;
        }

        if (_samples.Count > 0 && fraction < _samples.Last().Fraction)
        {
            // The numbers went back, as when a tool restarts its count: start over.
            _samples.Clear();
            _stageStarted = now;
        }

        _samples.Enqueue((now, fraction));
        while (_samples.Count > 2 && now - _samples.Peek().Time > Window)
        {
            _samples.Dequeue();
        }
    }

    /// <summary>The time left in the current stage, or <see langword="null"/> when there is not enough to estimate it.</summary>
    /// <param name="now">The time since the operation started.</param>
    public TimeSpan? Remaining(TimeSpan now)
    {
        if (_samples.Count < 2 || now - _stageStarted < MinimumElapsed)
        {
            return null;
        }

        var (firstTime, firstFraction) = _samples.Peek();
        var (_, lastFraction) = _samples.Last();
        if (lastFraction < MinimumFraction || lastFraction >= 1)
        {
            return null;
        }

        var seconds = (now - firstTime).TotalSeconds;
        var rate = (lastFraction - firstFraction) / seconds;
        if (seconds <= 0 || rate <= 0)
        {
            return null;
        }

        var left = (1 - lastFraction) / rate;
        return left > TimeSpan.FromDays(1).TotalSeconds ? null : TimeSpan.FromSeconds(left);
    }

    /// <summary>"about 40 s left", "about 3 min left", "about 1 h 10 min left"; "less than 5 s left" at the end.</summary>
    /// <param name="remaining">The time left.</param>
    public static string Format(TimeSpan remaining)
    {
        if (remaining < TimeSpan.FromSeconds(5))
        {
            return "less than 5 s left";
        }

        if (remaining < TimeSpan.FromSeconds(60))
        {
            // Tens of seconds are enough; a countdown that ticks every second reads as noise.
            var seconds = Math.Max(10, (int)Math.Round(remaining.TotalSeconds / 10, MidpointRounding.AwayFromZero) * 10);
            return seconds >= 60 ? "about 1 min left" : string.Create(CultureInfo.InvariantCulture, $"about {seconds} s left");
        }

        if (remaining < TimeSpan.FromMinutes(60))
        {
            var minutes = (int)Math.Round(remaining.TotalMinutes, MidpointRounding.AwayFromZero);
            return minutes >= 60 ? "about 1 h left" : string.Create(CultureInfo.InvariantCulture, $"about {minutes} min left");
        }

        var hours = (int)remaining.TotalHours;
        var rest = (int)Math.Round(remaining.TotalMinutes - (hours * 60));
        return rest == 0 ? string.Create(CultureInfo.InvariantCulture, $"about {hours} h left") : string.Create(CultureInfo.InvariantCulture, $"about {hours} h {rest} min left");
    }
}
