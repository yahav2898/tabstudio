using System.ComponentModel;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>Raises Tick on the app's UI thread at an interval, so handlers can update controls directly.</summary>
[DefaultEvent(nameof(Tick))]
[DefaultProperty(nameof(Interval))]
public class Timer : Component
{
    private static readonly object EventTick = new();
    private int _interval = 100;
    private bool _enabled;
    private IDisposable? _running;

    /// <summary>Creates a stopped timer.</summary>
    public Timer()
    {
    }

    /// <summary>Creates a stopped timer in a container, as the designer does.</summary>
    /// <param name="container">The container, which disposes the timer with the form.</param>
    public Timer(IContainer container)
        : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    /// <summary>Occurs every <see cref="Interval"/> milliseconds while the timer runs.</summary>
    public event EventHandler? Tick
    {
        add => Events.AddHandler(EventTick, value);
        remove => Events.RemoveHandler(EventTick, value);
    }

    /// <summary>Whether the timer runs.</summary>
    [DefaultValue(false)]
    public virtual bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            Restart();
        }
    }

    /// <summary>The interval in milliseconds.</summary>
    [DefaultValue(100)]
    public int Interval
    {
        get => _interval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            if (_interval == value)
            {
                return;
            }

            _interval = value;
            Restart();
        }
    }

    /// <summary>Data about the timer.</summary>
    public object? Tag { get; set; }

    /// <summary>Starts the timer.</summary>
    public void Start() => Enabled = true;

    /// <summary>Stops the timer.</summary>
    public void Stop() => Enabled = false;

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()}, Interval: {_interval}";

    /// <summary>Raises the Tick event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnTick(EventArgs e) => ((EventHandler?)Events[EventTick])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _enabled = false;
            _running?.Dispose();
            _running = null;
        }

        base.Dispose(disposing);
    }

    private void Restart()
    {
        _running?.Dispose();
        _running = null;
        if (_enabled && FormsRuntime.Current is { } runtime && Site?.DesignMode != true)
        {
            _running = runtime.StartTimer(_interval, () =>
            {
                if (_enabled)
                {
                    OnTick(EventArgs.Empty);
                }
            });
        }
    }
}
