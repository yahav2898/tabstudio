namespace TabStudio.WindowsBridge.Frames;

/// <summary>
/// The quality levels a frame's tiles are sent at. Level 0 is exact; each level above it sends fewer bits per color,
/// and the last one half the resolution too, so a slow Wi-Fi link still shows changes at once. Tiles sent at a lower
/// quality are sent again exactly once the window is still, so a picture that stops changing ends up exact.
/// </summary>
public static class FrameQuality
{
    /// <summary>Exact colors at full resolution.</summary>
    public const int Exact = 0;

    /// <summary>The roughest level: 4 bits per color at half the resolution.</summary>
    public const int Roughest = 4;

    /// <summary>The bits kept of each color channel at a level.</summary>
    /// <param name="level">The level, from <see cref="Exact"/> to <see cref="Roughest"/>.</param>
    public static int BitsPerChannel(int level) => Clamp(level) switch
    {
        0 => 8,
        1 => 6,
        2 => 5,
        _ => 4,
    };

    /// <summary>Whether a level sends half the resolution.</summary>
    /// <param name="level">The level.</param>
    public static bool IsHalfResolution(int level) => Clamp(level) == Roughest;

    /// <summary>A level kept in range.</summary>
    /// <param name="level">Any level.</param>
    public static int Clamp(int level) => Math.Clamp(level, Exact, Roughest);

    /// <summary>The level in words, for the Bridge pane: <c>Sharp</c>, <c>Good</c>, <c>Fast</c> or <c>Fastest</c>.</summary>
    /// <param name="level">The level.</param>
    public static string Describe(int level) => Clamp(level) switch
    {
        0 => "Sharp",
        1 or 2 => "Good",
        3 => "Fast",
        _ => "Fastest",
    };
}
