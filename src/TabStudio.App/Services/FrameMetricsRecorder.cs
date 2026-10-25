using System.Diagnostics.Metrics;

namespace TabStudio.App.Services;

/// <summary>
/// Collects the frame metrics Avalonia publishes when its diagnostics are on (the self test turns them on with the
/// <c>Avalonia.Diagnostics.Diagnostic.IsEnabled</c> switch): for each frame, the UI thread's layout and render passes,
/// and the compositor's drawing on the render thread. Without them it collects nothing.
/// </summary>
internal sealed class FrameMetricsRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Lock _lock = new();
    private readonly List<double> _uiFrames = [];
    private readonly List<double> _compositorFrames = [];
    private double _layout;

    /// <summary>Starts collecting.</summary>
    public FrameMetricsRecorder()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Avalonia.Diagnostic.Meter")
            {
                IsAvailable = true;
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => OnMeasurement(instrument.Name, value));
        _listener.Start();
    }

    /// <summary>Whether Avalonia publishes its metrics in this process.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>The UI thread's time for each frame: its layout passes and its render pass, in milliseconds.</summary>
    public IReadOnlyList<double> UiFrames
    {
        get
        {
            lock (_lock)
            {
                return [.. _uiFrames];
            }
        }
    }

    /// <summary>The compositor's time drawing each frame, in milliseconds.</summary>
    public IReadOnlyList<double> CompositorFrames
    {
        get
        {
            lock (_lock)
            {
                return [.. _compositorFrames];
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();

    private void OnMeasurement(string instrument, double value)
    {
        lock (_lock)
        {
            switch (instrument)
            {
                case "avalonia.ui.measure.time":
                case "avalonia.ui.arrange.time":
                    _layout += value;
                    break;
                case "avalonia.ui.render.time":
                    // The render pass closes the UI thread's frame.
                    _uiFrames.Add(_layout + value);
                    _layout = 0;
                    break;
                case "avalonia.comp.render.time":
                    _compositorFrames.Add(value);
                    break;
            }
        }
    }
}
