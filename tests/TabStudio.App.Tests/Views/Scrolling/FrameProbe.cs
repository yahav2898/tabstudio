using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>What Avalonia's own frame metrics measured since the last <see cref="FrameProbe.Take"/>.</summary>
/// <param name="MeasureMs">Time in layout's measure passes.</param>
/// <param name="ArrangeMs">Time in layout's arrange passes.</param>
/// <param name="RenderMs">Time recording what changed (the UI thread's render pass).</param>
/// <param name="RenderPasses">How many render passes ran.</param>
/// <param name="InputMs">Time handling raw input.</param>
/// <param name="CompositorMs">Time the compositor spent updating and drawing the scene (the render thread's work on a device).</param>
/// <param name="Visuals">The number of visuals in the tree, or -1 when unknown.</param>
internal readonly record struct PhaseMetrics(double MeasureMs, double ArrangeMs, double RenderMs, int RenderPasses, double InputMs, double CompositorMs, long Visuals);

/// <summary>
/// Listens to the frame metrics Avalonia publishes when its diagnostics are on (the test project turns them on with
/// the <c>Avalonia.Diagnostics.Diagnostic.IsEnabled</c> switch): the layout, render and compositor passes, and the
/// number of visuals. Optionally profiles layout by control type through Avalonia's activities.
/// </summary>
internal sealed class FrameProbe : IDisposable
{
    private const string MeterName = "Avalonia.Diagnostic.Meter";
    private const string ActivitySourceName = "Avalonia.Diagnostic.Source";

    private readonly MeterListener _listener = new();
    private readonly Lock _lock = new();
    private ActivityListener? _activities;
    private double _measure;
    private double _arrange;
    private double _render;
    private int _renderPasses;
    private double _input;
    private double _compositor;
    private long _visuals = -1;

    public FrameProbe()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MeterName)
            {
                HasAvaloniaMetrics = true;
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => OnMeasurement(instrument.Name, value));
        _listener.SetMeasurementEventCallback<int>((instrument, value, _, _) => OnCount(instrument, value));
        _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => OnCount(instrument, value));
        _listener.Start();
    }

    /// <summary>Whether Avalonia publishes its metrics in this process.</summary>
    public bool HasAvaloniaMetrics { get; private set; }

    /// <summary>Exclusive time per (activity, control type) while profiling, in milliseconds.</summary>
    public Dictionary<string, double> Profile { get; } = new(StringComparer.Ordinal);

    /// <summary>How many times styles were attached to a control while profiling.</summary>
    public int StyleAttachments { get; private set; }

    /// <summary>What was measured since the last call, and starts over.</summary>
    public PhaseMetrics Take()
    {
        _listener.RecordObservableInstruments();
        lock (_lock)
        {
            var metrics = new PhaseMetrics(_measure, _arrange, _render, _renderPasses, _input, _compositor, _visuals);
            _measure = _arrange = _render = _input = _compositor = 0;
            _renderPasses = 0;
            return metrics;
        }
    }

    /// <summary>Starts collecting layout time by control type and style attachments (slow: profile runs only).</summary>
    public void StartProfiling()
    {
        Profile.Clear();
        StyleAttachments = 0;
        var children = new Dictionary<Activity, double>();
        _activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                var total = activity.Duration.TotalMilliseconds;
                children.Remove(activity, out var nested);
                if (activity.Parent is { } parent)
                {
                    children[parent] = children.GetValueOrDefault(parent) + total;
                }

                if (activity.OperationName == "Avalonia.AttachingStyle")
                {
                    StyleAttachments++;
                }

                var type = activity.GetTagItem("Control")?.GetType().Name ?? "-";
                var key = $"{activity.OperationName.Replace("Avalonia.", string.Empty, StringComparison.Ordinal)} {type}";
                Profile[key] = Profile.GetValueOrDefault(key) + Math.Max(0, total - nested);
            },
        };
        ActivitySource.AddActivityListener(_activities);
    }

    /// <summary>Stops profiling.</summary>
    public void StopProfiling()
    {
        _activities?.Dispose();
        _activities = null;
    }

    public void Dispose()
    {
        StopProfiling();
        _listener.Dispose();
    }

    private void OnMeasurement(string instrument, double value)
    {
        lock (_lock)
        {
            switch (instrument)
            {
                case "avalonia.ui.measure.time":
                    _measure += value;
                    break;
                case "avalonia.ui.arrange.time":
                    _arrange += value;
                    break;
                case "avalonia.ui.render.time":
                    _render += value;
                    _renderPasses++;
                    break;
                case "avalonia.ui.input.time":
                    _input += value;
                    break;
                case "avalonia.comp.render.time":
                case "avalonia.comp.update.time":
                    _compositor += value;
                    break;
            }
        }
    }

    private void OnCount(Instrument instrument, long value)
    {
        if (instrument.Name == "avalonia.ui.visual.count")
        {
            lock (_lock)
            {
                _visuals = value;
            }
        }
    }
}
