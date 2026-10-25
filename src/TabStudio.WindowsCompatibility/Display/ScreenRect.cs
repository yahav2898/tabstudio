namespace TabStudio.WindowsCompatibility.Display;

/// <summary>A window's position and size on the screen.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    /// <summary>Whether it has no area.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>The smallest rectangle holding both.</summary>
    /// <param name="other">The other rectangle.</param>
    public ScreenRect Union(ScreenRect other)
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
        return new ScreenRect(left, top, Math.Max(X + Width, other.X + other.Width) - left, Math.Max(Y + Height, other.Y + other.Height) - top);
    }

    /// <summary>The part inside another rectangle.</summary>
    /// <param name="bounds">The other rectangle.</param>
    public ScreenRect Intersect(ScreenRect bounds)
    {
        var left = Math.Max(X, bounds.X);
        var top = Math.Max(Y, bounds.Y);
        var right = Math.Min(X + Width, bounds.X + bounds.Width);
        var bottom = Math.Min(Y + Height, bounds.Y + bounds.Height);
        return right <= left || bottom <= top ? default : new ScreenRect(left, top, right - left, bottom - top);
    }
}
