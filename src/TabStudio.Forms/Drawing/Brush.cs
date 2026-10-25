using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>Fills the interiors of shapes and draws text.</summary>
public abstract class Brush : MarshalByRefObject, ICloneable, IDisposable
{
    /// <summary>Creates a copy of the brush.</summary>
    public abstract object Clone();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the brush.</summary>
    /// <param name="disposing">Whether this is called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>The brush as a display list records it.</summary>
    internal abstract BrushSpec ToSpec();
}

/// <summary>A brush of one color.</summary>
public sealed class SolidBrush : Brush
{
    private readonly bool _immutable;
    private Color _color;

    /// <summary>Creates a brush of a color.</summary>
    /// <param name="color">The color.</param>
    public SolidBrush(Color color) => _color = color;

    internal SolidBrush(Color color, bool immutable)
    {
        _color = color;
        _immutable = immutable;
    }

    /// <summary>The color.</summary>
    /// <exception cref="ArgumentException">The brush is one of <see cref="Brushes"/>, which cannot change.</exception>
    public Color Color
    {
        get => _color;
        set
        {
            if (_immutable)
            {
                throw new ArgumentException("Changes cannot be made to Brush because permissions are not valid.");
            }

            _color = value;
        }
    }

    /// <inheritdoc />
    public override object Clone() => new SolidBrush(_color);

    internal override BrushSpec ToSpec() => new SolidBrushSpec((uint)_color.ToArgb());
}
