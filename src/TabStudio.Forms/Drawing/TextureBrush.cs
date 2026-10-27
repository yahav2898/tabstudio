using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>A brush that paints with an image, tiled by default. It keeps a copy of the image as it was when made.</summary>
public sealed class TextureBrush : Brush
{
    private readonly Image _image;
    private readonly ImageSnapshot _snapshot;
    private readonly RectangleF? _source;
    private Matrix3x2 _transform = Matrix3x2.Identity;

    /// <summary>Creates a brush that tiles an image.</summary>
    /// <param name="bitmap">The image.</param>
    public TextureBrush(Image bitmap)
        : this(bitmap, WrapMode.Tile)
    {
    }

    /// <summary>Creates a brush that paints an image as a wrap mode says.</summary>
    /// <param name="image">The image.</param>
    /// <param name="wrapMode">How it repeats.</param>
    public TextureBrush(Image image, WrapMode wrapMode)
        : this(image, wrapMode, null)
    {
    }

    /// <summary>Creates a brush that paints part of an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="wrapMode">How it repeats.</param>
    /// <param name="dstRect">The part of the image, in pixels.</param>
    public TextureBrush(Image image, WrapMode wrapMode, RectangleF dstRect)
        : this(image, wrapMode, (RectangleF?)dstRect)
    {
    }

    /// <summary>Creates a brush that paints part of an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="wrapMode">How it repeats.</param>
    /// <param name="dstRect">The part of the image, in pixels.</param>
    public TextureBrush(Image image, WrapMode wrapMode, Rectangle dstRect)
        : this(image, wrapMode, (RectangleF?)dstRect)
    {
    }

    /// <summary>Creates a brush that tiles part of an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="dstRect">The part of the image, in pixels.</param>
    public TextureBrush(Image image, RectangleF dstRect)
        : this(image, WrapMode.Tile, (RectangleF?)dstRect)
    {
    }

    /// <summary>Creates a brush that tiles part of an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="dstRect">The part of the image, in pixels.</param>
    public TextureBrush(Image image, Rectangle dstRect)
        : this(image, WrapMode.Tile, (RectangleF?)dstRect)
    {
    }

    /// <summary>Creates a brush that tiles part of an image with color changes.</summary>
    /// <param name="image">The image.</param>
    /// <param name="dstRect">The part of the image, in pixels.</param>
    /// <param name="imageAttr">The color changes, or <see langword="null"/>.</param>
    public TextureBrush(Image image, RectangleF dstRect, ImageAttributes? imageAttr)
        : this(Attributed(image, imageAttr), WrapMode.Tile, (RectangleF?)dstRect)
    {
    }

    /// <summary>Creates a brush that tiles part of an image with color changes.</summary>
    /// <param name="image">The image.</param>
    /// <param name="dstRect">The part of the image, in pixels.</param>
    /// <param name="imageAttr">The color changes, or <see langword="null"/>.</param>
    public TextureBrush(Image image, Rectangle dstRect, ImageAttributes? imageAttr)
        : this(image, (RectangleF)dstRect, imageAttr)
    {
    }

    private TextureBrush(Image image, WrapMode wrapMode, RectangleF? source)
    {
        ArgumentNullException.ThrowIfNull(image);
        _image = (Image)image.Clone();
        _snapshot = _image.Snapshot();
        _source = source;
        WrapMode = wrapMode;
    }

    /// <summary>A copy of the brush's image.</summary>
    public Image Image => (Image)_image.Clone();

    /// <summary>How the image repeats.</summary>
    public WrapMode WrapMode { get; set; }

    /// <summary>The brush's own transformation: where the image is, and how big.</summary>
    public Matrix Transform
    {
        get => new(_transform);
        set => _transform = (value ?? throw new ArgumentNullException(nameof(value))).Value;
    }

    /// <summary>Resets the brush's transformation.</summary>
    public void ResetTransform() => _transform = Matrix3x2.Identity;

    /// <summary>Multiplies the brush's transformation.</summary>
    /// <param name="matrix">The matrix.</param>
    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    /// <summary>Multiplies the brush's transformation.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="order">The order.</param>
    public void MultiplyTransform(Matrix matrix, MatrixOrder order) =>
        _transform = BrushTransforms.Apply(_transform, (matrix ?? throw new ArgumentNullException(nameof(matrix))).Value, order);

    /// <summary>Translates the brush's transformation.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    /// <summary>Translates the brush's transformation.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <param name="order">The order.</param>
    public void TranslateTransform(float dx, float dy, MatrixOrder order) => _transform = BrushTransforms.Apply(_transform, Matrix3x2.CreateTranslation(dx, dy), order);

    /// <summary>Scales the brush's transformation.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    /// <summary>Scales the brush's transformation.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    /// <param name="order">The order.</param>
    public void ScaleTransform(float sx, float sy, MatrixOrder order) => _transform = BrushTransforms.Apply(_transform, Matrix3x2.CreateScale(sx, sy), order);

    /// <summary>Rotates the brush's transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    /// <summary>Rotates the brush's transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="order">The order.</param>
    public void RotateTransform(float angle, MatrixOrder order) => _transform = BrushTransforms.Apply(_transform, Matrix3x2.CreateRotation(angle * MathF.PI / 180), order);

    /// <inheritdoc />
    public override object Clone() => new TextureBrush(_image, WrapMode, _source) { _transform = _transform };

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _image.Dispose();
        base.Dispose(disposing);
    }

    internal override BrushSpec ToSpec() => new TextureBrushSpec(_snapshot, _source, WrapMode, _transform);

    private static Image Attributed(Image image, ImageAttributes? attributes)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (attributes is not { ChangesColors: true })
        {
            return image;
        }

        var copy = new Bitmap(image);
        attributes.Apply(copy.Buffer.Pixels());
        copy.Buffer.PixelsChanged();
        return copy;
    }
}
