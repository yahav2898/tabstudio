using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Threading;

namespace TabStudio.App.Services;

/// <summary>A time the UI thread did not answer: it was busy with one piece of work, or the process was paused for a collection.</summary>
/// <param name="At">When it started, from the start of the monitor.</param>
/// <param name="Duration">How long the UI thread did not answer.</param>
/// <param name="Phase">What the app was doing then, as the code that started the monitor named it.</param>
/// <param name="GcPause">How much of it the runtime spent with every thread paused for garbage collection, where it says.</param>
public readonly record struct UiStall(TimeSpan At, TimeSpan Duration, string Phase, TimeSpan GcPause = default);

/// <summary>
/// Watches the UI thread from a thread of its own, as a finger on the screen would feel it: every few milliseconds it
/// asks the UI thread to answer at the highest priority, and records how long the answer took. An answer that takes
/// longer than the threshold is a stall: a tap, a keystroke or a frame would have waited that long. Used by the self test
/// on the device and by the tests, never in normal use.
/// </summary>
public sealed class UiStallMonitor : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _threshold;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _gate = new();
    private readonly List<UiStall> _stalls = [];
    private readonly List<double> _latencies = [];
    private readonly List<(TimeSpan At, string Label)> _marks = [];
    private readonly List<(TimeSpan At, string Phase)> _phases = [];
    private readonly List<(string Label, TimeSpan Took)> _timings = [];
    private readonly Thread _thread;
    private string _phase = string.Empty;
    private int _asked;
    private int _answered;
    private volatile bool _stopped;

    /// <summary>Starts watching the UI thread.</summary>
    /// <param name="dispatcher">The UI thread's dispatcher.</param>
    /// <param name="threshold">The shortest wait that counts as a stall; 100 ms by default.</param>
    /// <param name="interval">How long to wait between two questions; 16 ms by default, one frame at 60 Hz.</param>
    public UiStallMonitor(Dispatcher dispatcher, TimeSpan? threshold = null, TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        _threshold = threshold ?? TimeSpan.FromMilliseconds(100);
        _interval = interval ?? TimeSpan.FromMilliseconds(16);
        _thread = new Thread(Run) { IsBackground = true, Name = "TabStudio UI stall monitor" };
        _thread.Start();
    }

    /// <summary>The monitor the self test runs, if any, for its steps to name what they do.</summary>
    public static UiStallMonitor? Current { get; set; }

    /// <summary>
    /// What the app is doing now; a stall is recorded with this and every phase set while it lasted, so work that blocks
    /// the UI thread in one go is still named by the phases its code set.
    /// </summary>
    public string Phase
    {
        get => Volatile.Read(ref _phase);
        set
        {
            var phase = value ?? string.Empty;
            if (phase == Volatile.Read(ref _phase))
            {
                return;
            }

            Volatile.Write(ref _phase, phase);
            lock (_gate)
            {
                _phases.Add((_clock.Elapsed, phase));
            }
        }
    }

    /// <summary>Names what the app does now for the running monitor, if any; nothing happens without one.</summary>
    /// <param name="phase">What the app does, such as "creating the workspace view".</param>
    public static void Note(string phase)
    {
        if (Current is { } monitor)
        {
            monitor.Phase = phase;
        }
    }

    /// <summary>Records how long a piece of work on the UI thread took, for the running monitor, if any.</summary>
    /// <param name="label">The work.</param>
    /// <param name="took">How long it took.</param>
    public static void Time(string label, TimeSpan took)
    {
        if (Current is { } monitor)
        {
            lock (monitor._gate)
            {
                monitor._timings.Add((label, took));
            }
        }
    }

    /// <summary>The work timed with <see cref="Time"/>, longest first, one line: "workspace view 1234 ms, …".</summary>
    /// <param name="count">How many.</param>
    public string Timings(int count)
    {
        lock (_gate)
        {
            return string.Join(", ", _timings.OrderByDescending(t => t.Took).Take(count)
                .Select(t => string.Create(CultureInfo.InvariantCulture, $"{t.Label} {t.Took.TotalMilliseconds:0} ms")));
        }
    }

    /// <summary>The time since the monitor started.</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    /// <summary>Every stall so far, in order.</summary>
    public IReadOnlyList<UiStall> Stalls
    {
        get
        {
            lock (_gate)
            {
                return [.. _stalls];
            }
        }
    }

    /// <summary>The longest wait for an answer so far.</summary>
    public TimeSpan Longest
    {
        get
        {
            lock (_gate)
            {
                return _latencies.Count == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(_latencies.Max());
            }
        }
    }

    /// <summary>How many times the UI thread was asked and answered.</summary>
    public int Answers
    {
        get
        {
            lock (_gate)
            {
                return _latencies.Count;
            }
        }
    }

    /// <summary>
    /// Notes that a step just ended: a stall is reported with the first step that ended after it, so a step that runs
    /// without naming its phase still has its stalls attributed.
    /// </summary>
    /// <param name="label">The step, such as its result.</param>
    public void Mark(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        lock (_gate)
        {
            _marks.Add((_clock.Elapsed, label));
        }
    }

    /// <summary>A percentile of the waits for an answer, in milliseconds.</summary>
    /// <param name="fraction">The fraction, such as 0.99.</param>
    public double Percentile(double fraction)
    {
        lock (_gate)
        {
            if (_latencies.Count == 0)
            {
                return 0;
            }

            var sorted = _latencies.Order().ToList();
            return sorted[Math.Clamp((int)Math.Ceiling(fraction * sorted.Count) - 1, 0, sorted.Count - 1)];
        }
    }

    /// <summary>One line: how many stalls, the longest and the waits' percentiles.</summary>
    public string Summary()
    {
        var stalls = Stalls;
        var culture = CultureInfo.InvariantCulture;
        var total = stalls.Aggregate(TimeSpan.Zero, (sum, stall) => sum + stall.Duration);
        var longest = stalls.Count == 0 ? default : stalls.MaxBy(s => s.Duration);
        var text = string.Create(
            culture,
            $"{stalls.Count} over {_threshold.TotalMilliseconds:0} ms ({stalls.Count(s => s.Duration >= TimeSpan.FromMilliseconds(250))} over 250 ms, {stalls.Count(s => s.Duration >= TimeSpan.FromSeconds(1))} over 1 s), {total.TotalSeconds:0.0} s in all");
        if (stalls.Count > 0)
        {
            text += string.Create(culture, $", longest {longest.Duration.TotalMilliseconds:0} ms during {Describe(longest)}");
        }

        var gc = stalls.Aggregate(TimeSpan.Zero, (sum, stall) => sum + stall.GcPause);
        if (gc > TimeSpan.Zero)
        {
            text += string.Create(culture, $", {gc.TotalSeconds:0.0} s of it garbage collection");
        }

        return text + string.Create(culture, $"; answer p50 {Percentile(0.5):0.0} ms, p95 {Percentile(0.95):0.0} ms, p99 {Percentile(0.99):0.0} ms over {Answers} questions in {Elapsed.TotalSeconds:0} s");
    }

    /// <summary>The longest stalls, longest first, one line each.</summary>
    /// <param name="count">How many.</param>
    public IReadOnlyList<string> Worst(int count)
    {
        var culture = CultureInfo.InvariantCulture;
        return [.. Stalls.OrderByDescending(s => s.Duration).Take(count)
            .Select(s => string.Create(culture, $"{s.Duration.TotalMilliseconds:0} ms{Collected(s)} at {s.At.TotalSeconds:0.0} s during {Describe(s)}"))];
    }

    /// <summary>What the app was doing during a stall: its phase, or the step that ended after it.</summary>
    /// <param name="stall">The stall.</param>
    public string Describe(UiStall stall)
    {
        lock (_gate)
        {
            // The phase it started in and every phase set before it ended.
            var during = new List<string>();
            if (stall.Phase.Length > 0)
            {
                during.Add(stall.Phase);
            }

            foreach (var (at, phase) in _phases)
            {
                if (at > stall.At && at <= stall.At + stall.Duration && phase.Length > 0 && (during.Count == 0 || during[^1] != phase))
                {
                    during.Add(phase);
                }
            }

            if (during.Count > 0)
            {
                return Shorten(string.Join(" → ", during), 200);
            }

            foreach (var (at, label) in _marks)
            {
                if (at >= stall.At)
                {
                    return "the step that ended with: " + Shorten(label, 90);
                }
            }
        }

        return "the end of the run";
    }

    /// <summary>The stalls as Markdown table rows, longest first.</summary>
    /// <param name="count">How many.</param>
    public string Table(int count)
    {
        var table = new StringBuilder("| Stall | Of it, GC | At | During |\n| --- | --- | --- | --- |\n");
        foreach (var stall in Stalls.OrderByDescending(s => s.Duration).Take(count))
        {
            table.Append(CultureInfo.InvariantCulture, $"| {stall.Duration.TotalMilliseconds:0} ms | {stall.GcPause.TotalMilliseconds:0} ms | {stall.At.TotalSeconds:0.0} s | {Describe(stall).Replace('|', '/')} |\n");
        }

        return table.ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopped = true;
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    /// <summary>The runtime's total garbage collection pause so far, or zero where it does not say (Mono may not).</summary>
    private static TimeSpan PauseDuration()
    {
        try
        {
            return GC.GetTotalPauseDuration();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotImplementedException or NotSupportedException)
        {
            return TimeSpan.Zero;
        }
    }

    private static string Collected(UiStall stall) =>
        stall.GcPause > TimeSpan.Zero ? string.Create(CultureInfo.InvariantCulture, $" ({stall.GcPause.TotalMilliseconds:0} ms GC)") : string.Empty;

    private static string Shorten(string label, int length)
    {
        var line = label.ReplaceLineEndings(" ");
        return line.Length > length ? line[..length] + "…" : line;
    }

    private void Run()
    {
        while (!_stopped)
        {
            var question = ++_asked;
            var asked = _clock.Elapsed;
            var phase = Phase;
            var paused = PauseDuration();
            _dispatcher.Post(() => Volatile.Write(ref _answered, question), DispatcherPriority.Send);
            while (Volatile.Read(ref _answered) != question)
            {
                if (_stopped)
                {
                    return;
                }

                Thread.Sleep(1);
            }

            var waited = _clock.Elapsed - asked;
            lock (_gate)
            {
                _latencies.Add(waited.TotalMilliseconds);
                if (waited >= _threshold)
                {
                    _stalls.Add(new UiStall(asked, waited, phase, PauseDuration() - paused));
                }
            }

            Thread.Sleep(_interval);
        }
    }
}
