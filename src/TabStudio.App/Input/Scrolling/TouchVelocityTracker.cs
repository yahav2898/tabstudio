using Avalonia;

namespace TabStudio.App.Input.Scrolling;

/// <summary>
/// Estimates how fast a finger moves when it lifts, as Android's <c>VelocityTracker</c> does with its default
/// least-squares strategy: a quadratic fitted to the last 100 milliseconds of movement on each axis, whose slope at the
/// newest point is the velocity; and no velocity at all when the finger rested for 40 milliseconds before lifting.
/// </summary>
public sealed class TouchVelocityTracker
{
    private const int HistorySize = 20;
    private static readonly TimeSpan Horizon = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan AssumeStopped = TimeSpan.FromMilliseconds(40);

    private readonly TimeSpan[] _times = new TimeSpan[HistorySize];
    private readonly Point[] _positions = new Point[HistorySize];
    private int _count;
    private int _newest = -1;

    /// <summary>Forgets every position, as a new touch does.</summary>
    public void Clear()
    {
        _count = 0;
        _newest = -1;
    }

    /// <summary>Adds where the finger is at a moment.</summary>
    /// <param name="time">The moment, on any clock that only moves forward.</param>
    /// <param name="position">The position, in dips.</param>
    public void Add(TimeSpan time, Point position)
    {
        if (_count > 0 && time < _times[_newest])
        {
            // A clock that went back belongs to a different gesture.
            Clear();
        }

        _newest = (_newest + 1) % HistorySize;
        _times[_newest] = time;
        _positions[_newest] = position;
        _count = Math.Min(_count + 1, HistorySize);
    }

    /// <summary>The velocity, in dips per second, when the finger lifts at a moment.</summary>
    /// <param name="now">The moment the finger lifted, on the clock of <see cref="Add"/>.</param>
    public Vector VelocityAt(TimeSpan now)
    {
        if (_count < 2 || now - _times[_newest] > AssumeStopped)
        {
            return default;
        }

        // The samples of the last 100 ms, newest first, relative to the newest one (times in seconds, zero or less).
        Span<double> t = stackalloc double[HistorySize];
        Span<double> x = stackalloc double[HistorySize];
        Span<double> y = stackalloc double[HistorySize];
        var newestTime = _times[_newest];
        var origin = _positions[_newest];
        var used = 0;
        for (var i = 0; i < _count; i++)
        {
            var index = (_newest - i + HistorySize) % HistorySize;
            var age = newestTime - _times[index];
            if (age > Horizon)
            {
                break;
            }

            t[used] = -age.TotalSeconds;
            x[used] = _positions[index].X - origin.X;
            y[used] = _positions[index].Y - origin.Y;
            used++;
        }

        if (used < 2)
        {
            return default;
        }

        return new Vector(Slope(t[..used], x[..used]), Slope(t[..used], y[..used]));
    }

    /// <summary>The slope at t = 0 of the least-squares quadratic through the points, or of the line when a quadratic is undetermined.</summary>
    private static double Slope(ReadOnlySpan<double> t, ReadOnlySpan<double> v)
    {
        double s0 = t.Length, s1 = 0, s2 = 0, s3 = 0, s4 = 0, r0 = 0, r1 = 0, r2 = 0;
        for (var i = 0; i < t.Length; i++)
        {
            var ti = t[i];
            var t2 = ti * ti;
            s1 += ti;
            s2 += t2;
            s3 += t2 * ti;
            s4 += t2 * t2;
            r0 += v[i];
            r1 += ti * v[i];
            r2 += t2 * v[i];
        }

        if (t.Length >= 3)
        {
            // The normal equations of a + b·t + c·t², solved for b by Cramer's rule.
            var determinant = (s0 * ((s2 * s4) - (s3 * s3))) - (s1 * ((s1 * s4) - (s3 * s2))) + (s2 * ((s1 * s3) - (s2 * s2)));
            if (Math.Abs(determinant) > 1e-18)
            {
                var numerator = (s0 * ((r1 * s4) - (s3 * r2))) - (r0 * ((s1 * s4) - (s3 * s2))) + (s2 * ((s1 * r2) - (r1 * s2)));
                return numerator / determinant;
            }
        }

        var lineDeterminant = (s0 * s2) - (s1 * s1);
        return Math.Abs(lineDeterminant) > 1e-18 ? ((s0 * r1) - (s1 * r0)) / lineDeterminant : 0;
    }
}
