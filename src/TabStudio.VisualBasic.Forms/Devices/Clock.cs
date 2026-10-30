namespace Microsoft.VisualBasic.Devices;

/// <summary>The device's clock (<c>My.Computer.Clock</c>).</summary>
public class Clock
{
    /// <summary>Creates the clock object.</summary>
    public Clock()
    {
    }

    /// <summary>The time in Coordinated Universal Time.</summary>
    public DateTime GmtTime => DateTime.UtcNow;

    /// <summary>The device's local time.</summary>
    public DateTime LocalTime => DateTime.Now;

    /// <summary>The milliseconds since the device started; it wraps around after about 25 days.</summary>
    public int TickCount => Environment.TickCount;
}
