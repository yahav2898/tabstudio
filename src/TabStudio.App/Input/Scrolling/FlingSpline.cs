namespace TabStudio.App.Input.Scrolling;

/// <summary>
/// The glide of a fling as Android's <c>OverScroller</c> computes it, so a flicked editor coasts and slows down exactly
/// like a native Android list: the distance and duration grow with the release velocity along Android's spline, and
/// the position follows the spline's shape from that velocity down to rest.
/// </summary>
/// <remarks>
/// <para>
/// A port of the spline fling of <c>android.widget.OverScroller.SplineOverScroller</c> from the Android Open Source
/// Project (Apache License 2.0), with Android's constants: the scroll friction of <c>ViewConfiguration</c> (0.015), the
/// deceleration rate ln(0.78) / ln(0.9), the inflexion point 0.35 and the physical coefficient of the earth's gravity
/// at 160 dots per inch with Android's 0.84 tuning factor.
/// </para>
/// <para>
/// Everything is in device-independent pixels, which is what Android's own formula gives at 160 dpi: the density
/// cancels out of the duration, and the distance scales with it, so a fling covers the same distance in dips on any
/// screen.
/// </para>
/// </remarks>
public readonly record struct FlingSpline
{
    /// <summary>The slowest release, in dips per second, that still flings, as Android's minimum fling velocity.</summary>
    public const double MinimumVelocity = 50;

    /// <summary>The fastest fling, in dips per second, as Android's maximum fling velocity.</summary>
    public const double MaximumVelocity = 8000;

    private const double Inflexion = 0.35;
    private const double StartTension = 0.5;
    private const double EndTension = 1.0;
    private const double P1 = StartTension * Inflexion;
    private const double P2 = 1.0 - (EndTension * (1.0 - Inflexion));
    private const int Samples = 100;
    private const double ScrollFriction = 0.015;

    // SensorManager.GRAVITY_EARTH (m/s²) × 39.37 inches per meter × 160 dips per inch × Android's 0.84 look and feel.
    private const double PhysicalCoefficient = 9.80665 * 39.37 * 160.0 * 0.84;

    private static readonly double DecelerationRate = Math.Log(0.78) / Math.Log(0.9);
    private static readonly (double[] Position, double[] Time) Spline = BuildSpline();

    private FlingSpline(double velocity, double splineDistance, double splineDuration, double distance, double duration)
    {
        Velocity = velocity;
        SplineDistance = splineDistance;
        SplineDurationSeconds = splineDuration;
        Distance = distance;
        DurationSeconds = duration;
    }

    /// <summary>The release velocity, in dips per second, signed like the distance.</summary>
    public double Velocity { get; }

    /// <summary>How far the fling travels, in dips, signed: positive scrolls toward the end of the content.</summary>
    public double Distance { get; }

    /// <summary>How long the fling lasts.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

    /// <summary>Whether the fling moves at all.</summary>
    public bool IsEmpty => DurationSeconds <= 0 || Distance == 0;

    private double SplineDistance { get; }

    private double SplineDurationSeconds { get; }

    private double DurationSeconds { get; }

    /// <summary>The fling of a release velocity, capped at <see cref="MaximumVelocity"/>.</summary>
    /// <param name="velocity">The velocity, in dips per second; its sign is the direction.</param>
    /// <returns>The fling; empty below <see cref="MinimumVelocity"/>.</returns>
    public static FlingSpline FromVelocity(double velocity)
    {
        var speed = Math.Min(Math.Abs(velocity), MaximumVelocity);
        if (double.IsNaN(speed) || speed < MinimumVelocity)
        {
            return default;
        }

        // Android's getSplineDeceleration, getSplineFlingDistance and getSplineFlingDuration.
        var deceleration = Math.Log(Inflexion * speed / (ScrollFriction * PhysicalCoefficient));
        var decelerationMinusOne = DecelerationRate - 1.0;
        var distance = ScrollFriction * PhysicalCoefficient * Math.Exp(DecelerationRate / decelerationMinusOne * deceleration);
        var duration = Math.Exp(deceleration / decelerationMinusOne);
        var signed = Math.CopySign(distance, velocity);
        return new FlingSpline(Math.CopySign(speed, velocity), signed, duration, signed, duration);
    }

    /// <summary>
    /// The same fling stopping where the content ends, as Android's <c>adjustDuration</c> does: it follows the same
    /// curve and ends at the moment the curve reaches the edge.
    /// </summary>
    /// <param name="before">How far the content can scroll against the fling's direction, in dips (zero or more).</param>
    /// <param name="after">How far the content can scroll in the fling's direction, in dips (zero or more).</param>
    public FlingSpline ClampedTo(double before, double after)
    {
        var limit = Distance >= 0 ? Math.Max(0, after) : -Math.Max(0, before);
        if (IsEmpty || Math.Abs(limit) >= Math.Abs(Distance))
        {
            return this;
        }

        if (limit == 0)
        {
            return default;
        }

        var x = Math.Abs(limit / SplineDistance);
        var index = (int)(Samples * x);
        var timeCoefficient = 1.0;
        if (index < Samples)
        {
            var xLow = (double)index / Samples;
            var xHigh = (double)(index + 1) / Samples;
            var tLow = Spline.Time[index];
            var tHigh = Spline.Time[index + 1];
            timeCoefficient = tLow + ((x - xLow) / (xHigh - xLow) * (tHigh - tLow));
        }

        return new FlingSpline(Velocity, SplineDistance, SplineDurationSeconds, limit, SplineDurationSeconds * timeCoefficient);
    }

    /// <summary>How far the fling has travelled after some time, in dips, signed like <see cref="Distance"/>.</summary>
    /// <param name="elapsed">The time since the release.</param>
    public double PositionAt(TimeSpan elapsed)
    {
        if (IsEmpty || elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        if (elapsed.TotalSeconds >= DurationSeconds)
        {
            return Distance;
        }

        var (distanceCoefficient, _) = Sample(elapsed.TotalSeconds / SplineDurationSeconds);
        var position = distanceCoefficient * SplineDistance;
        return Distance >= 0 ? Math.Min(position, Distance) : Math.Max(position, Distance);
    }

    /// <summary>The fling's velocity after some time, in dips per second, signed like <see cref="Distance"/>.</summary>
    /// <param name="elapsed">The time since the release.</param>
    public double VelocityAt(TimeSpan elapsed)
    {
        if (IsEmpty || elapsed.TotalSeconds >= DurationSeconds)
        {
            return 0;
        }

        var (_, velocityCoefficient) = Sample(Math.Max(0, elapsed.TotalSeconds) / SplineDurationSeconds);
        return velocityCoefficient * SplineDistance / SplineDurationSeconds;
    }

    /// <summary>The spline at a fraction of its duration: the fraction of the distance covered and the curve's slope there.</summary>
    private static (double Distance, double Velocity) Sample(double t)
    {
        var index = (int)(Samples * t);
        if (index >= Samples)
        {
            return (1, 0);
        }

        var tLow = (double)index / Samples;
        var tHigh = (double)(index + 1) / Samples;
        var dLow = Spline.Position[index];
        var dHigh = Spline.Position[index + 1];
        var velocity = (dHigh - dLow) / (tHigh - tLow);
        return (dLow + ((t - tLow) * velocity), velocity);
    }

    /// <summary>Android's SPLINE_POSITION and SPLINE_TIME tables, found by bisection on the two Bézier tension curves.</summary>
    private static (double[] Position, double[] Time) BuildSpline()
    {
        var position = new double[Samples + 1];
        var time = new double[Samples + 1];
        double xMin = 0, yMin = 0;
        for (var i = 0; i < Samples; i++)
        {
            var alpha = (double)i / Samples;

            double xMax = 1, x, coefficient;
            while (true)
            {
                x = xMin + ((xMax - xMin) / 2);
                coefficient = 3 * x * (1 - x);
                var tx = (coefficient * (((1 - x) * P1) + (x * P2))) + (x * x * x);
                if (Math.Abs(tx - alpha) < 1e-5)
                {
                    break;
                }

                if (tx > alpha)
                {
                    xMax = x;
                }
                else
                {
                    xMin = x;
                }
            }

            position[i] = (coefficient * (((1 - x) * StartTension) + x)) + (x * x * x);

            double yMax = 1, y;
            while (true)
            {
                y = yMin + ((yMax - yMin) / 2);
                coefficient = 3 * y * (1 - y);
                var dy = (coefficient * (((1 - y) * StartTension) + y)) + (y * y * y);
                if (Math.Abs(dy - alpha) < 1e-5)
                {
                    break;
                }

                if (dy > alpha)
                {
                    yMax = y;
                }
                else
                {
                    yMin = y;
                }
            }

            time[i] = (coefficient * (((1 - y) * P1) + (y * P2))) + (y * y * y);
        }

        position[Samples] = time[Samples] = 1;
        return (position, time);
    }
}
