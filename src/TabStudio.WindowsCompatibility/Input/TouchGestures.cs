namespace TabStudio.WindowsCompatibility.Input;

/// <summary>
/// Turns touch on the app preview into the mouse a Windows app expects: a tap clicks, a long press right-clicks,
/// a drag drags with the left button down, and two fingers moving together turn the wheel.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is pressed when a finger comes down, because it may still become a long press or the first finger of a
/// scroll: a tap clicks when the finger lifts, a drag presses once the finger has moved past
/// <see cref="Slop"/>, and a long press right-clicks after <see cref="LongPress"/> without moving. A second finger
/// that comes down before a drag starts turns the gesture into scrolling: the wheel turns a notch for every
/// <see cref="ScrollStep"/> the fingers move together, in both directions, the way a touchpad scrolls.
/// </para>
/// <para>Positions are in the desktop's pixels. Calls come from the UI thread; the long press fires on a timer's, under the same lock.</para>
/// </remarks>
public sealed class TouchGestures : IDisposable
{
    private readonly IDesktopInput _input;
    private readonly TimeProvider _time;
    private readonly Dictionary<int, (double X, double Y)> _fingers = [];
    private readonly Lock _gate = new();
    private ITimer? _longPress;
    private Mode _mode;
    private int _primary;
    private (double X, double Y) _start;
    private (double X, double Y) _scrollAnchor;
    private (double X, double Y) _scrolled;

    /// <summary>Creates the gestures.</summary>
    /// <param name="input">Where the mouse input goes.</param>
    /// <param name="time">The clock and timers, for the long press; the system's by default.</param>
    public TouchGestures(IDesktopInput input, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
        _time = time ?? TimeProvider.System;
    }

    private enum Mode
    {
        Idle,
        Pending,
        Dragging,
        Scrolling,
        Done,
    }

    /// <summary>How far a finger may move, in desktop pixels, and still tap or long-press.</summary>
    public double Slop { get; set; } = 10;

    /// <summary>How long a finger rests before it right-clicks.</summary>
    public TimeSpan LongPress { get; set; } = TimeSpan.FromMilliseconds(550);

    /// <summary>How far, in desktop pixels, two fingers move for one notch of the wheel.</summary>
    public double ScrollStep { get; set; } = 24;

    /// <summary>A long press right-clicked, for feedback such as a vibration. Raised on a timer's thread.</summary>
    public event EventHandler? LongPressed;

    /// <summary>A finger touched the preview.</summary>
    /// <param name="id">The finger, as the platform numbers touches.</param>
    /// <param name="x">Where, in desktop pixels.</param>
    /// <param name="y">Where, in desktop pixels.</param>
    public void Down(int id, double x, double y)
    {
        lock (_gate)
        {
            _fingers[id] = (x, y);
            switch (_mode)
            {
                case Mode.Idle:
                    _mode = Mode.Pending;
                    _primary = id;
                    _start = (x, y);
                    StartLongPress();
                    break;
                case Mode.Pending when _fingers.Count == 2:
                    StopLongPress();
                    _mode = Mode.Scrolling;
                    _scrollAnchor = Centroid();
                    _scrolled = (0, 0);
                    break;
            }
        }
    }

    /// <summary>A finger moved.</summary>
    /// <param name="id">The finger.</param>
    /// <param name="x">Where it is now.</param>
    /// <param name="y">Where it is now.</param>
    public void Move(int id, double x, double y)
    {
        lock (_gate)
        {
            if (!_fingers.ContainsKey(id))
            {
                return;
            }

            _fingers[id] = (x, y);
            switch (_mode)
            {
                case Mode.Pending when id == _primary && Distance(_start, (x, y)) > Slop:
                    StopLongPress();
                    _mode = Mode.Dragging;
                    _input.MovePointer(Round(_start.X), Round(_start.Y));
                    _input.Button(PointerButton.Left, pressed: true);
                    _input.MovePointer(Round(x), Round(y));
                    break;
                case Mode.Dragging when id == _primary:
                    _input.MovePointer(Round(x), Round(y));
                    break;
                case Mode.Scrolling:
                    Scroll();
                    break;
            }
        }
    }

    /// <summary>A finger lifted.</summary>
    /// <param name="id">The finger.</param>
    /// <param name="x">Where it lifted.</param>
    /// <param name="y">Where it lifted.</param>
    public void Up(int id, double x, double y)
    {
        lock (_gate)
        {
            if (!_fingers.Remove(id))
            {
                return;
            }

            switch (_mode)
            {
                case Mode.Pending when id == _primary:
                    StopLongPress();
                    _input.MovePointer(Round(_start.X), Round(_start.Y));
                    _input.Button(PointerButton.Left, pressed: true);
                    _input.Button(PointerButton.Left, pressed: false);
                    break;
                case Mode.Dragging when id == _primary:
                    _input.MovePointer(Round(x), Round(y));
                    _input.Button(PointerButton.Left, pressed: false);
                    _mode = Mode.Done;
                    break;
            }

            if (_fingers.Count == 0)
            {
                StopLongPress();
                _mode = Mode.Idle;
            }
            else if (_mode is Mode.Pending or Mode.Scrolling)
            {
                _mode = Mode.Done;
            }
        }
    }

    /// <summary>The platform took the touches away, such as for a system gesture: whatever was pressed is released.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            StopLongPress();
            if (_mode == Mode.Dragging)
            {
                _input.Button(PointerButton.Left, pressed: false);
            }

            _fingers.Clear();
            _mode = Mode.Idle;
        }
    }

    /// <inheritdoc />
    public void Dispose() => StopLongPress();

    private void StartLongPress()
    {
        StopLongPress();
        _longPress = _time.CreateTimer(_ => OnLongPress(), null, LongPress, Timeout.InfiniteTimeSpan);
    }

    private void StopLongPress()
    {
        _longPress?.Dispose();
        _longPress = null;
    }

    private void OnLongPress()
    {
        lock (_gate)
        {
            if (_mode != Mode.Pending)
            {
                return;
            }

            _mode = Mode.Done;
            _input.MovePointer(Round(_start.X), Round(_start.Y));
            _input.Button(PointerButton.Right, pressed: true);
            _input.Button(PointerButton.Right, pressed: false);
            LongPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Turns the wheel for how far the fingers moved together since the last notch.</summary>
    private void Scroll()
    {
        var center = Centroid();
        var moved = (X: center.X - _scrollAnchor.X - _scrolled.X, Y: center.Y - _scrollAnchor.Y - _scrolled.Y);

        // Fingers moving down pull the content down, as on a touchpad: the wheel turns up.
        var vertical = (int)(moved.Y / ScrollStep);
        var horizontal = (int)(-moved.X / ScrollStep);
        if (vertical != 0 || horizontal != 0)
        {
            _input.MovePointer(Round(center.X), Round(center.Y));
            _input.Wheel(horizontal, vertical);
            _scrolled = (_scrolled.X - (horizontal * ScrollStep), _scrolled.Y + (vertical * ScrollStep));
        }
    }

    private (double X, double Y) Centroid()
    {
        double x = 0, y = 0;
        foreach (var finger in _fingers.Values)
        {
            x += finger.X;
            y += finger.Y;
        }

        return (x / _fingers.Count, y / _fingers.Count);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
