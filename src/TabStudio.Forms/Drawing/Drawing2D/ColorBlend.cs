namespace System.Drawing.Drawing2D;

/// <summary>The colors of a gradient of more than two colors, and where each is, from 0 to 1.</summary>
public sealed class ColorBlend
{
    /// <summary>Creates a blend of one color.</summary>
    public ColorBlend()
        : this(1)
    {
    }

    /// <summary>Creates a blend of a number of colors.</summary>
    /// <param name="count">The number of colors.</param>
    public ColorBlend(int count)
    {
        Colors = new Color[count];
        Positions = new float[count];
    }

#pragma warning disable CA1819 // GDI+'s own shape.
    /// <summary>The colors.</summary>
    public Color[] Colors { get; set; }

    /// <summary>Where each color is, from 0 to 1.</summary>
    public float[] Positions { get; set; }
#pragma warning restore CA1819
}
