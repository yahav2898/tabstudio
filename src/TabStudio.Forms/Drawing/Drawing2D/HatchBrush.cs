using TabStudio.Forms.Rendering;

namespace System.Drawing.Drawing2D;

/// <summary>A brush that paints an 8 by 8 pattern of two colors, fixed to the surface's pixels.</summary>
public sealed class HatchBrush : Brush
{
    /// <summary>Creates a brush of lines on black.</summary>
    /// <param name="hatchstyle">The pattern.</param>
    /// <param name="foreColor">The color of the lines.</param>
    public HatchBrush(HatchStyle hatchstyle, Color foreColor)
        : this(hatchstyle, foreColor, Color.Black)
    {
    }

    /// <summary>Creates a brush of lines on a background.</summary>
    /// <param name="hatchstyle">The pattern.</param>
    /// <param name="foreColor">The color of the lines.</param>
    /// <param name="backColor">The color between them.</param>
    public HatchBrush(HatchStyle hatchstyle, Color foreColor, Color backColor)
    {
        if (hatchstyle < HatchStyle.Horizontal || hatchstyle > HatchStyle.SolidDiamond)
        {
            throw new ArgumentException($"The value of argument 'hatchstyle' ({(int)hatchstyle}) is invalid for Enum type 'HatchStyle'.", nameof(hatchstyle));
        }

        HatchStyle = hatchstyle;
        ForegroundColor = foreColor;
        BackgroundColor = backColor;
    }

    /// <summary>The pattern.</summary>
    public HatchStyle HatchStyle { get; }

    /// <summary>The color of the lines.</summary>
    public Color ForegroundColor { get; }

    /// <summary>The color between the lines.</summary>
    public Color BackgroundColor { get; }

    /// <inheritdoc />
    public override object Clone() => new HatchBrush(HatchStyle, ForegroundColor, BackgroundColor);

    internal override BrushSpec ToSpec() => new HatchBrushSpec(HatchStyle, SystemColorValues.Argb(ForegroundColor), SystemColorValues.Argb(BackgroundColor));
}
