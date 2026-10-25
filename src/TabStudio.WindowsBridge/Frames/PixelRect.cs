namespace TabStudio.WindowsBridge.Frames;

/// <summary>A rectangle of whole pixels.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width; 0 for an empty rectangle.</param>
/// <param name="Height">The height; 0 for an empty rectangle.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>The empty rectangle.</summary>
    public static PixelRect Empty => default;

    /// <summary>Whether it covers no pixel.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Just past the right edge.</summary>
    public int Right => X + Width;

    /// <summary>Just past the bottom edge.</summary>
    public int Bottom => Y + Height;

    /// <summary>The pixels both rectangles cover.</summary>
    /// <param name="other">The other rectangle.</param>
    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top ? Empty : new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>The smallest rectangle that covers both.</summary>
    /// <param name="other">The other rectangle.</param>
    public PixelRect Union(PixelRect other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        return new PixelRect(left, top, Math.Max(Right, other.Right) - left, Math.Max(Bottom, other.Bottom) - top);
    }

    /// <summary>Whether a pixel is inside.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;
}
