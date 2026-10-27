namespace System.Drawing;

/// <summary>The application's shared <see cref="BufferedGraphicsContext"/>.</summary>
public static class BufferedGraphicsManager
{
    /// <summary>The shared context.</summary>
    public static BufferedGraphicsContext Current { get; } = new();
}
