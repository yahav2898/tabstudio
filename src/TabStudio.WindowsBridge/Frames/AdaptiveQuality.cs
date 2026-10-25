namespace TabStudio.WindowsBridge.Frames;

/// <summary>
/// Paces a stream and picks its quality from how fast the tablet shows frames: a frame is sent only while few are
/// on their way, so input never waits behind a queue of old pictures, and the quality drops when frames take too long
/// to arrive and rises again once they arrive quickly.
/// </summary>
/// <remarks>
/// The tablet says when it drew each frame. The time from sending to drawing includes the Wi-Fi's latency and its
/// throughput, so it is all the controller needs: frames slower than <see cref="Target"/> lower the quality at once,
/// and a run of frames faster than half of it raises the quality one level. Not thread-safe; the stream calls it from
/// one place at a time.
/// </remarks>
public sealed class AdaptiveQuality
{
    /// <summary>How many fast frames in a row raise the quality one level.</summary>
    private const int FastFramesToRise = 6;

    /// <summary>The bytes an exact tile takes, compressed, for the refinement budget; user interfaces compress well.</summary>
    private const int TypicalTileBytes = 4096;

    private readonly Dictionary<long, (int Bytes, TimeSpan At)> _inFlight = [];
    private int _fastFrames;
    private double _bytesPerSecond;

    /// <summary>Creates a controller.</summary>
    /// <param name="target">The longest a frame should take to reach the tablet; a quarter of a second by default.</param>
    /// <param name="maxInFlight">How many frames may be on their way at once.</param>
    public AdaptiveQuality(TimeSpan? target = null, int maxInFlight = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInFlight, 1);
        Target = target ?? TimeSpan.FromMilliseconds(250);
        MaxInFlight = maxInFlight;
    }

    /// <summary>The longest a frame should take to reach the tablet.</summary>
    public TimeSpan Target { get; }

    /// <summary>How many frames may be on their way at once.</summary>
    public int MaxInFlight { get; }

    /// <summary>The quality level for changed tiles now, <see cref="FrameQuality.Exact"/> on a fast link.</summary>
    public int Level { get; private set; }

    /// <summary>The frames sent and not drawn yet.</summary>
    public int InFlight => _inFlight.Count;

    /// <summary>Whether another frame may be sent now.</summary>
    public bool CanSend => _inFlight.Count < MaxInFlight;

    /// <summary>How long the last drawn frame took from sending to drawing.</summary>
    public TimeSpan? LastDelivery { get; private set; }

    /// <summary>The link's throughput as the frames measured it, in bytes per second; 0 before the first frame.</summary>
    public double BytesPerSecond => _bytesPerSecond;

    /// <summary>
    /// How many rough tiles may be sent again exactly now: none while frames are on their way, and otherwise what the
    /// link carries in a fifth of the target time.
    /// </summary>
    public int RefinementBudget
    {
        get
        {
            if (_inFlight.Count > 0)
            {
                return 0;
            }

            var bytes = _bytesPerSecond <= 0 ? 64 * 1024 : _bytesPerSecond * Target.TotalSeconds / 5;
            return (int)Math.Clamp(bytes / TypicalTileBytes, 2, 96);
        }
    }

    /// <summary>Records a frame that was sent.</summary>
    /// <param name="sequence">The frame's number.</param>
    /// <param name="bytes">Its size.</param>
    /// <param name="at">When it was sent, on a monotonic clock.</param>
    public void OnSent(long sequence, int bytes, TimeSpan at) => _inFlight[sequence] = (bytes, at);

    /// <summary>Records that the tablet drew a frame, and adjusts the quality.</summary>
    /// <param name="sequence">The frame's number.</param>
    /// <param name="at">When the bridge heard of it, on the same clock as <see cref="OnSent"/>.</param>
    public void OnShown(long sequence, TimeSpan at)
    {
        if (!_inFlight.Remove(sequence, out var sent))
        {
            return;
        }

        // Frames sent before this one and not drawn were dropped by the tablet, which only draws the newest.
        foreach (var older in _inFlight.Keys.Where(k => k < sequence).ToList())
        {
            _inFlight.Remove(older);
        }

        var delivery = at - sent.At;
        LastDelivery = delivery;
        var seconds = Math.Max(delivery.TotalSeconds, 0.001);
        var throughput = sent.Bytes / seconds;
        _bytesPerSecond = _bytesPerSecond <= 0 ? throughput : (_bytesPerSecond * 0.7) + (throughput * 0.3);

        if (delivery > Target)
        {
            Level = FrameQuality.Clamp(Level + (delivery > Target * 3 ? 2 : 1));
            _fastFrames = 0;
        }
        else if (delivery < Target / 2)
        {
            if (++_fastFrames >= FastFramesToRise && Level > FrameQuality.Exact)
            {
                Level--;
                _fastFrames = 0;
            }
        }
        else
        {
            _fastFrames = 0;
        }
    }

    /// <summary>Forgets the frames on their way, such as when the stream starts over with a keyframe.</summary>
    public void Reset()
    {
        _inFlight.Clear();
        _fastFrames = 0;
    }
}
