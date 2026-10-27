using TabStudio.Forms.Rendering;

namespace System.Drawing.Drawing2D;

/// <summary>How a gradient blends its two colors: the share of the second color (a factor) at positions from 0 to 1.</summary>
public sealed class Blend
{
    /// <summary>Creates a blend of one factor.</summary>
    public Blend()
        : this(1)
    {
    }

    /// <summary>Creates a blend of a number of factors.</summary>
    /// <param name="count">The number of factors.</param>
    public Blend(int count)
    {
        Factors = new float[count];
        Positions = new float[count];
    }

#pragma warning disable CA1819 // GDI+'s own shape.
    /// <summary>The shares of the second color, from 0 to 1.</summary>
    public float[] Factors { get; set; }

    /// <summary>Where each factor applies, from 0 to 1.</summary>
    public float[] Positions { get; set; }
#pragma warning restore CA1819

    /// <summary>A color between two, as 0xAARRGGBB.</summary>
    internal static uint Mix(Color first, Color second, float factor)
    {
        var a = SystemColorValues.Resolve(first);
        var b = SystemColorValues.Resolve(second);
        var t = Math.Clamp(factor, 0, 1);
        int Lerp(int x, int y) => (int)MathF.Round(x + ((y - x) * t));
        return (uint)Color.FromArgb(Lerp(a.A, b.A), Lerp(a.R, b.R), Lerp(a.G, b.G), Lerp(a.B, b.B)).ToArgb();
    }

    /// <summary>A bell-shaped blend, as <c>SetSigmaBellShape</c> makes it.</summary>
    internal static Blend SigmaBell(float focus, float scale)
    {
        const int Count = 33;
        var blend = new Blend(Count);
        for (var i = 0; i < Count; i++)
        {
            var position = i / (float)(Count - 1);
            var distance = position < focus ? (focus - position) / Math.Max(focus, 1e-3f) : (position - focus) / Math.Max(1 - focus, 1e-3f);
            blend.Positions[i] = position;
            blend.Factors[i] = scale * MathF.Exp(-4 * distance * distance);
        }

        return blend;
    }

    /// <summary>A triangular blend, as <c>SetBlendTriangularShape</c> makes it.</summary>
    internal static Blend Triangle(float focus, float scale) => focus <= 0
        ? new Blend(2) { Positions = [0, 1], Factors = [scale, 0] }
        : focus >= 1
            ? new Blend(2) { Positions = [0, 1], Factors = [0, scale] }
            : new Blend(3) { Positions = [0, focus, 1], Factors = [0, scale, 0] };
}
