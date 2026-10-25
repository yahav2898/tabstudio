using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.VisualTree;

namespace TabStudio.App.Input.Scrolling;

/// <summary>
/// Scrolls the scroll viewer around a control with a finger or a pen, as a native Android list scrolls: once the finger
/// has moved past the touch slop the content follows it one to one, and when it lifts while moving the content flings
/// and glides to rest along Android's <c>OverScroller</c> spline, one step per display frame. A touch during a fling
/// catches it.
/// </summary>
/// <remarks>
/// <para>
/// Add it to the control inside the scroll viewer (the scroll viewer's content), not to the scroll viewer itself. It
/// sees a touch before the scroll viewer's own <see cref="ScrollGestureRecognizer"/> and takes the pointer at the same
/// distance, so that one never starts; and, unlike it, it moves the offset in pixels whatever the content's logical
/// scroll units, glides on display frames rather than a 16 ms timer, and ends a glide where Android's would.
/// </para>
/// <para>
/// A drag that starts mostly along one axis stays on it, so code scrolled up and down does not drift sideways.
/// </para>
/// </remarks>
public sealed class TouchScrollGestureRecognizer : GestureRecognizer
{
    // A drag is locked to an axis when it starts at least this many times faster along it than across it.
    private const double AxisLockRatio = 2;

    // A fling still moving faster than this, in dips per second, is caught by a touch rather than tapped through.
    private const double CatchVelocity = 100;

    private readonly TouchVelocityTracker _velocity = new();
    private IPointer? _tracking;
    private Point _pressed;
    private Point _last;
    private Vector _target;
    private bool _verticalOnly;
    private bool _horizontalOnly;
    private ScrollViewer? _scrollViewer;

    private FlingSpline _flingX;
    private FlingSpline _flingY;
    private Vector _flingStart;
    private long _flingStarted;
    private int _flingGeneration;

    /// <summary>Raised when a finger starts to scroll, after it moved past the touch slop.</summary>
    public event EventHandler? ScrollStarted;

    /// <summary>Raised when a glide ends, at rest, at an edge, caught by a touch or interrupted.</summary>
    public event EventHandler? FlingEnded;

    /// <summary>Whether a finger is scrolling the content now.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>Whether the content is gliding after a fling.</summary>
    public bool IsFlinging { get; private set; }

    /// <summary>Whether the touch now down stopped a fling, so it is not a tap.</summary>
    public bool CaughtFling { get; private set; }

    /// <summary>The frames a glide moves on; Avalonia's render loop unless a test drives them.</summary>
    public IFrameScheduler Frames { get; set; } = RenderLoopFrameScheduler.Instance;

    /// <summary>The clock of a glide; the system's unless a test drives it.</summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>The distance a finger moves before it scrolls, in dips; by default half the platform's touch tap size, as Avalonia's own scroll gesture.</summary>
    public double? TouchSlop { get; set; }

    /// <summary>The fling now gliding, vertically, for tests; empty when there is none.</summary>
    public FlingSpline VerticalFling => IsFlinging ? _flingY : default;

    /// <summary>Stops a glide where it is.</summary>
    public void StopFling()
    {
        if (!IsFlinging)
        {
            return;
        }

        _flingGeneration++;
        IsFlinging = false;
        FlingEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Pointer.Type is not (PointerType.Touch or PointerType.Pen) || _tracking is not null
            || !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            return;
        }

        CaughtFling = IsFlinging && Speed(Time.GetElapsedTime(_flingStarted)) > CatchVelocity;
        StopFling();
        _tracking = e.Pointer;
        _pressed = _last = e.GetPosition(null);
        _scrollViewer = (Target as Visual)?.FindAncestorOfType<ScrollViewer>();
        _velocity.Clear();
        _velocity.Add(TimeSpan.FromMilliseconds(e.Timestamp), _pressed);
    }

    /// <inheritdoc />
    protected override void PointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Pointer != _tracking || _scrollViewer is not { } scrollViewer)
        {
            return;
        }

        var point = e.GetPosition(null);
        _velocity.Add(TimeSpan.FromMilliseconds(e.Timestamp), point);
        if (!IsDragging && !TryStartDragging(e, scrollViewer, point))
        {
            return;
        }

        var delta = _last - point;
        _last = point;
        _target = Clamp(scrollViewer, _target + new Vector(_verticalOnly ? 0 : delta.X, _horizontalOnly ? 0 : delta.Y));
        scrollViewer.SetCurrentValue(ScrollViewer.OffsetProperty, _target);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Pointer != _tracking)
        {
            return;
        }

        var wasDragging = IsDragging;
        EndTracking();
        if (!wasDragging || _scrollViewer is not { } scrollViewer)
        {
            return;
        }

        // The content moves against the finger.
        var velocity = -_velocity.VelocityAt(TimeSpan.FromMilliseconds(e.Timestamp));
        e.Handled = true;
        StartFling(scrollViewer, new Vector(_verticalOnly ? 0 : velocity.X, _horizontalOnly ? 0 : velocity.Y));
    }

    /// <inheritdoc />
    protected override void PointerCaptureLost(IPointer pointer)
    {
        if (pointer == _tracking)
        {
            EndTracking();
        }
    }

    private bool TryStartDragging(PointerEventArgs e, ScrollViewer scrollViewer, Point point)
    {
        var moved = point - _pressed;
        var slop = TouchSlop ?? Math.Floor((TopLevel.GetTopLevel(scrollViewer)?.PlatformSettings?.GetTapSize(PointerType.Touch).Height ?? 10) / 2);
        var canX = scrollViewer.Extent.Width > scrollViewer.Viewport.Width;
        var canY = scrollViewer.Extent.Height > scrollViewer.Viewport.Height;
        if (!(canX && Math.Abs(moved.X) > slop) && !(canY && Math.Abs(moved.Y) > slop))
        {
            return false;
        }

        // From here the content follows the finger one to one; the slop itself is not scrolled, so nothing jumps.
        IsDragging = true;
        _verticalOnly = !canX || Math.Abs(moved.Y) >= AxisLockRatio * Math.Abs(moved.X);
        _horizontalOnly = !canY || Math.Abs(moved.X) >= AxisLockRatio * Math.Abs(moved.Y);
        if (_verticalOnly && _horizontalOnly)
        {
            // Moving along the axis that cannot scroll: the other one scrolls.
            _verticalOnly = !canX;
            _horizontalOnly = !canY;
        }

        var length = Math.Sqrt((moved.X * moved.X) + (moved.Y * moved.Y));
        _last = _pressed + (moved * (Math.Min(slop, length) / length));
        _target = scrollViewer.Offset;
        Capture(e.Pointer);
        ScrollStarted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void EndTracking()
    {
        _tracking = null;
        IsDragging = false;
    }

    private void StartFling(ScrollViewer scrollViewer, Vector velocity)
    {
        var offset = scrollViewer.Offset;
        var max = MaxOffset(scrollViewer);
        _flingX = FlingSpline.FromVelocity(velocity.X).ClampedTo(offset.X, max.X - offset.X);
        _flingY = FlingSpline.FromVelocity(velocity.Y).ClampedTo(offset.Y, max.Y - offset.Y);
        if (_flingX.IsEmpty && _flingY.IsEmpty)
        {
            return;
        }

        _flingStart = _target = offset;
        _flingStarted = Time.GetTimestamp();
        IsFlinging = true;
        var generation = ++_flingGeneration;
        RequestFlingFrame(scrollViewer, generation);
    }

    private void RequestFlingFrame(ScrollViewer scrollViewer, int generation)
    {
        if (!Frames.RequestFrame(scrollViewer, () => OnFlingFrame(scrollViewer, generation)))
        {
            StopFling();
        }
    }

    private void OnFlingFrame(ScrollViewer scrollViewer, int generation)
    {
        if (generation != _flingGeneration || !IsFlinging)
        {
            return;
        }

        // Anything else that scrolled the content since the last frame, such as the keyboard, ends the glide.
        if (Math.Abs(scrollViewer.Offset.X - _target.X) > 1 || Math.Abs(scrollViewer.Offset.Y - _target.Y) > 1)
        {
            StopFling();
            return;
        }

        var elapsed = Time.GetElapsedTime(_flingStarted);
        _target = Clamp(scrollViewer, _flingStart + new Vector(_flingX.PositionAt(elapsed), _flingY.PositionAt(elapsed)));
        scrollViewer.SetCurrentValue(ScrollViewer.OffsetProperty, _target);
        if (elapsed >= _flingX.Duration && elapsed >= _flingY.Duration)
        {
            StopFling();
            return;
        }

        RequestFlingFrame(scrollViewer, generation);
    }

    private double Speed(TimeSpan elapsed)
    {
        var x = _flingX.VelocityAt(elapsed);
        var y = _flingY.VelocityAt(elapsed);
        return Math.Sqrt((x * x) + (y * y));
    }

    private static Vector MaxOffset(ScrollViewer scrollViewer) => new(
        Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width),
        Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));

    private static Vector Clamp(ScrollViewer scrollViewer, Vector offset)
    {
        var max = MaxOffset(scrollViewer);
        return new Vector(Math.Clamp(offset.X, 0, max.X), Math.Clamp(offset.Y, 0, max.Y));
    }
}
