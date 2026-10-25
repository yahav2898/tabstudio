namespace TabStudio.WindowsBridge.Input;

/// <summary>
/// Turns touch on the tablet's Bridge pane into mouse input for a window on the PC: a tap is a click, a long press a
/// right click, a drag with one finger a drag with the left button, and two fingers moving together turn the wheel.
/// </summary>
/// <remarks>
/// <para>
/// Positions are the pane's, in device-independent pixels, so the thresholds feel the same at any zoom; the
/// <see cref="FrameMapping"/> turns them into the window's pixels. A long press needs time to pass without input, so
/// the pane calls <see cref="Tick"/> while a finger is down.
/// </para>
/// <para>Not thread-safe: the pane calls it on the UI thread.</para>
/// </remarks>
public sealed class TouchGestureRecognizer
{
    /// <summary>How far a finger may move, in device-independent pixels, and still tap or long-press.</summary>
    public const double Slop = 10;

    /// <summary>How long a finger stays down, without moving, to right-click.</summary>
    public static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(550);

    /// <summary>How far two fingers move, in device-independent pixels, to turn the wheel one notch.</summary>
    public const double PixelsPerNotch = 40;

    private readonly Dictionary<long, (double X, double Y)> _fingers = [];
    private GestureState _state;
    private (double X, double Y) _start;
    private TimeSpan _startTime;
    private (double X, double Y) _centroid;
    private double _wheelX;
    private double _wheelY;

    private enum GestureState
    {
        /// <summary>No finger is down.</summary>
        Idle,

        /// <summary>One finger is down and has not moved far: it may tap, long-press or start a drag.</summary>
        Pending,

        /// <summary>One finger drags with the left button down.</summary>
        Dragging,

        /// <summary>Two fingers turn the wheel.</summary>
        Scrolling,

        /// <summary>The gesture is over (a long press clicked); the fingers are ignored until they lift.</summary>
        Finished,
    }

    /// <summary>Whether a finger is down, so the pane should call <see cref="Tick"/>.</summary>
    public bool IsActive => _state != GestureState.Idle;

    /// <summary>A finger touches the pane.</summary>
    /// <param name="id">The finger's pointer id.</param>
    /// <param name="x">Its position from the pane's left edge.</param>
    /// <param name="y">Its position from the pane's top edge.</param>
    /// <param name="time">When, on a monotonic clock.</param>
    /// <param name="map">Where the window is drawn in the pane.</param>
    /// <returns>The input to send, if any.</returns>
    public IReadOnlyList<BridgeInputEvent> Down(long id, double x, double y, TimeSpan time, FrameMapping map)
    {
        _fingers[id] = (x, y);
        switch (_state)
        {
            case GestureState.Idle:
                _state = GestureState.Pending;
                _start = (x, y);
                _startTime = time;
                return [];
            case GestureState.Pending when _fingers.Count == 2:
                // A second finger before the first moved: the two scroll.
                _state = GestureState.Scrolling;
                _centroid = Centroid();
                _wheelX = 0;
                _wheelY = 0;
                return [Move(map, _centroid)];
            default:
                return [];
        }
    }

    /// <summary>A finger moves.</summary>
    /// <param name="id">The finger's pointer id.</param>
    /// <param name="x">Its position from the pane's left edge.</param>
    /// <param name="y">Its position from the pane's top edge.</param>
    /// <param name="time">When.</param>
    /// <param name="map">Where the window is drawn in the pane.</param>
    /// <returns>The input to send, if any.</returns>
    public IReadOnlyList<BridgeInputEvent> Move(long id, double x, double y, TimeSpan time, FrameMapping map)
    {
        if (!_fingers.ContainsKey(id))
        {
            return [];
        }

        _fingers[id] = (x, y);
        switch (_state)
        {
            case GestureState.Pending when Distance((x, y), _start) > Slop:
                _state = GestureState.Dragging;
                var (sx, sy) = map.ToWindow(_start.X, _start.Y);
                return
                [
                    BridgeInputEvent.Pointer(BridgeInputKind.PointerMove, sx, sy),
                    BridgeInputEvent.Pointer(BridgeInputKind.PointerDown, sx, sy, BridgePointerButton.Left),
                    Move(map, (x, y), BridgePointerButton.Left),
                ];
            case GestureState.Pending:
                return Tick(time, map);
            case GestureState.Dragging:
                return [Move(map, (x, y), BridgePointerButton.Left)];
            case GestureState.Scrolling:
                return Scroll(map);
            default:
                return [];
        }
    }

    /// <summary>A finger lifts.</summary>
    /// <param name="id">The finger's pointer id.</param>
    /// <param name="x">Its last position from the pane's left edge.</param>
    /// <param name="y">Its last position from the pane's top edge.</param>
    /// <param name="time">When.</param>
    /// <param name="map">Where the window is drawn in the pane.</param>
    /// <returns>The input to send, if any.</returns>
    public IReadOnlyList<BridgeInputEvent> Up(long id, double x, double y, TimeSpan time, FrameMapping map)
    {
        if (!_fingers.Remove(id))
        {
            return [];
        }

        IReadOnlyList<BridgeInputEvent> events = [];
        switch (_state)
        {
            case GestureState.Pending:
                var due = Tick(time, map);
                if (due.Count > 0)
                {
                    events = due;
                    break;
                }

                // A tap: a click where the finger first touched.
                var (tx, ty) = map.ToWindow(_start.X, _start.Y);
                events =
                [
                    BridgeInputEvent.Pointer(BridgeInputKind.PointerMove, tx, ty),
                    BridgeInputEvent.Pointer(BridgeInputKind.PointerDown, tx, ty, BridgePointerButton.Left),
                    BridgeInputEvent.Pointer(BridgeInputKind.PointerUp, tx, ty, BridgePointerButton.Left),
                ];
                _state = GestureState.Finished;
                break;
            case GestureState.Dragging:
                var (ux, uy) = map.ToWindow(x, y);
                events = [BridgeInputEvent.Pointer(BridgeInputKind.PointerUp, ux, uy, BridgePointerButton.Left)];
                _state = GestureState.Finished;
                break;
            case GestureState.Scrolling when _fingers.Count < 2:
                _state = GestureState.Finished;
                break;
        }

        if (_fingers.Count == 0)
        {
            _state = GestureState.Idle;
        }

        return events;
    }

    /// <summary>Lets time pass while a finger is down: a finger that stayed still long enough right-clicks.</summary>
    /// <param name="time">Now.</param>
    /// <param name="map">Where the window is drawn in the pane.</param>
    /// <returns>The input to send, if any.</returns>
    public IReadOnlyList<BridgeInputEvent> Tick(TimeSpan time, FrameMapping map)
    {
        if (_state != GestureState.Pending || time - _startTime < LongPress)
        {
            return [];
        }

        _state = GestureState.Finished;
        var (x, y) = map.ToWindow(_start.X, _start.Y);
        return
        [
            BridgeInputEvent.Pointer(BridgeInputKind.PointerMove, x, y),
            BridgeInputEvent.Pointer(BridgeInputKind.PointerDown, x, y, BridgePointerButton.Right),
            BridgeInputEvent.Pointer(BridgeInputKind.PointerUp, x, y, BridgePointerButton.Right),
        ];
    }

    /// <summary>Forgets the fingers, such as when the pane loses them; a drag in progress ends where it is.</summary>
    /// <param name="map">Where the window is drawn in the pane.</param>
    /// <returns>The input that ends the gesture, if any.</returns>
    public IReadOnlyList<BridgeInputEvent> Cancel(FrameMapping map)
    {
        IReadOnlyList<BridgeInputEvent> events = [];
        if (_state == GestureState.Dragging && _fingers.Count > 0)
        {
            var finger = _fingers.Values.First();
            var (x, y) = map.ToWindow(finger.X, finger.Y);
            events = [BridgeInputEvent.Pointer(BridgeInputKind.PointerUp, x, y, BridgePointerButton.Left)];
        }

        _fingers.Clear();
        _state = GestureState.Idle;
        return events;
    }

    private static BridgeInputEvent Move(FrameMapping map, (double X, double Y) point, BridgePointerButton held = BridgePointerButton.None)
    {
        var (x, y) = map.ToWindow(point.X, point.Y);
        return BridgeInputEvent.Pointer(BridgeInputKind.PointerMove, x, y, held);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private (double X, double Y) Centroid()
    {
        var x = _fingers.Values.Average(f => f.X);
        var y = _fingers.Values.Average(f => f.Y);
        return (x, y);
    }

    /// <summary>Turns the wheel by how far the fingers moved together: content follows the fingers, as on a phone.</summary>
    private BridgeInputEvent[] Scroll(FrameMapping map)
    {
        var centroid = Centroid();
        _wheelY += (centroid.Y - _centroid.Y) / PixelsPerNotch * BridgeInputEvent.WheelNotch;
        _wheelX -= (centroid.X - _centroid.X) / PixelsPerNotch * BridgeInputEvent.WheelNotch;
        _centroid = centroid;
        var deltaY = (int)_wheelY;
        var deltaX = (int)_wheelX;
        if (deltaX == 0 && deltaY == 0)
        {
            return [];
        }

        _wheelY -= deltaY;
        _wheelX -= deltaX;
        var (x, y) = map.ToWindow(centroid.X, centroid.Y);
        return [BridgeInputEvent.Wheel(x, y, deltaX, deltaY)];
    }
}
