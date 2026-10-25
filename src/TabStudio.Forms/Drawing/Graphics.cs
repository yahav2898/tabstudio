using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>
/// A drawing surface: the <see cref="System.Windows.Forms.PaintEventArgs.Graphics"/> of a control's Paint event, or
/// the surface <see cref="System.Windows.Forms.Control.CreateGraphics"/> returns.
/// </summary>
/// <remarks>
/// Drawing is recorded and shown when the window is next drawn, which follows each event the app handles. Shapes
/// are drawn without antialiasing unless <see cref="SmoothingMode"/> asks for it, as in GDI+.
/// </remarks>
public sealed partial class Graphics : MarshalByRefObject, IDeviceContext
{
    private readonly DisplayListBuilder _builder;
    private readonly Matrix3x2 _origin;
    private readonly RectangleF _bounds;
    private readonly Action<Graphics>? _onDispose;
    private readonly Action? _onRecord;
    private Matrix3x2 _transform = Matrix3x2.Identity;
    private RectangleF? _clip;
    private bool _disposed;

    /// <summary>Creates a surface that records into a display list.</summary>
    /// <param name="builder">Where drawing is recorded.</param>
    /// <param name="origin">Where the surface's origin is in the window's client area.</param>
    /// <param name="bounds">What the surface may draw on, in the window's client area.</param>
    /// <param name="onDispose">Called when the surface is disposed, for surfaces the app creates itself.</param>
    /// <param name="onRecord">Called after each drawing operation, for surfaces that show drawing at once.</param>
    internal Graphics(DisplayListBuilder builder, PointF origin, RectangleF bounds, Action<Graphics>? onDispose = null, Action? onRecord = null)
    {
        _builder = builder;
        _origin = Matrix3x2.CreateTranslation(origin.X, origin.Y);
        _bounds = bounds;
        _onDispose = onDispose;
        _onRecord = onRecord;
    }

    /// <summary>Whether shapes are antialiased.</summary>
    public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.None;

    /// <summary>The quality of text rendering. Text is always antialiased on this device.</summary>
    public TextRenderingHint TextRenderingHint { get; set; }

    /// <summary>How images are scaled: <see cref="InterpolationMode.NearestNeighbor"/> keeps hard pixel edges.</summary>
    public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Bilinear;

    /// <summary>How pixels are offset. Kept for compatibility.</summary>
    public PixelOffsetMode PixelOffsetMode { get; set; }

    /// <summary>The compositing quality. Kept for compatibility.</summary>
    public CompositingQuality CompositingQuality { get; set; }

    /// <summary>The compositing mode. Kept for compatibility.</summary>
    public CompositingMode CompositingMode { get; set; }

    /// <summary>The gamma correction of text. Kept for compatibility.</summary>
    public int TextContrast { get; set; } = 4;

    /// <summary>The origin of dithering and hatch brushes. Kept for compatibility.</summary>
    public Point RenderingOrigin { get; set; }

    /// <summary>The unit of page coordinates: pixels.</summary>
    public GraphicsUnit PageUnit { get; set; } = GraphicsUnit.Display;

    /// <summary>The scale of page coordinates: 1.</summary>
    public float PageScale { get; set; } = 1f;

    /// <summary>The horizontal resolution: 96 dots per inch.</summary>
    public float DpiX => 96f;

    /// <summary>The vertical resolution: 96 dots per inch.</summary>
    public float DpiY => 96f;

    /// <summary>The world transformation: a copy of it, which <see cref="Transform"/> must be set to change.</summary>
    public Matrix Transform
    {
        get => new(_transform);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _transform = value.Value;
        }
    }

    /// <summary>The world transformation as a <see cref="Matrix3x2"/>.</summary>
    public Matrix3x2 TransformElements
    {
        get => _transform;
        set => _transform = value;
    }

    /// <summary>The clipping region.</summary>
    public Region Clip
    {
        get => new(ClipBounds);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetClip(value.Bounds, CombineMode.Replace);
        }
    }

    /// <summary>The bounds of the clipping region, in world coordinates.</summary>
    public RectangleF ClipBounds => ToWorld(EffectiveClip);

    /// <summary>The bounds of the visible area, in world coordinates.</summary>
    public RectangleF VisibleClipBounds => ClipBounds;

    /// <summary>Whether the clipping region is empty.</summary>
    public bool IsClipEmpty => EffectiveClip.Width <= 0 || EffectiveClip.Height <= 0;

    /// <summary>Whether the visible area is empty.</summary>
    public bool IsVisibleClipEmpty => IsClipEmpty;

    private Matrix3x2 Absolute => _transform * _origin;

    private RectangleF EffectiveClip => _clip is { } clip ? RectangleF.Intersect(_bounds, clip) : _bounds;

    private bool Antialias => SmoothingMode is SmoothingMode.AntiAlias or SmoothingMode.HighQuality;

    /// <summary>Drawing on an image is not supported on this device.</summary>
    /// <param name="image">The image.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public static Graphics FromImage(Image image) =>
        throw new NotSupportedException("Graphics.FromImage is not supported by TabStudio's Forms layer: draw in a control's Paint event, or set pixels with Bitmap.SetPixel.");

    /// <summary>Drawing on a window handle is not supported on this device.</summary>
    /// <param name="hwnd">The handle.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public static Graphics FromHwnd(IntPtr hwnd) =>
        throw new NotSupportedException("Graphics.FromHwnd is not supported by TabStudio's Forms layer: use Control.CreateGraphics, or draw in the Paint event.");

    /// <summary>Fills the clipping region with a color.</summary>
    /// <param name="color">The color.</param>
    public void Clear(Color color)
    {
        var clip = EffectiveClip;
        _builder.Add(Matrix3x2.Identity, clip, antialias: false, new RectangleCommand(clip, new SolidBrushSpec((uint)color.ToArgb()), null));
        _onRecord?.Invoke();
    }

    /// <summary>Applies a translation before the world transformation.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    /// <summary>Applies a translation to the world transformation.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <param name="order">Whether it is applied before or after the world transformation.</param>
    public void TranslateTransform(float dx, float dy, MatrixOrder order) => Apply(Matrix3x2.CreateTranslation(dx, dy), order);

    /// <summary>Applies a scaling before the world transformation.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    /// <summary>Applies a scaling to the world transformation.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    /// <param name="order">Whether it is applied before or after the world transformation.</param>
    public void ScaleTransform(float sx, float sy, MatrixOrder order) => Apply(Matrix3x2.CreateScale(sx, sy), order);

    /// <summary>Applies a clockwise rotation before the world transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    /// <summary>Applies a clockwise rotation to the world transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="order">Whether it is applied before or after the world transformation.</param>
    public void RotateTransform(float angle, MatrixOrder order) => Apply(Matrix3x2.CreateRotation(angle * MathF.PI / 180), order);

    /// <summary>Multiplies the world transformation by a matrix applied first.</summary>
    /// <param name="matrix">The matrix.</param>
    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    /// <summary>Multiplies the world transformation by a matrix.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="order">Whether it is applied before or after the world transformation.</param>
    public void MultiplyTransform(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        Apply(matrix.Value, order);
    }

    /// <summary>Resets the world transformation to the identity.</summary>
    public void ResetTransform() => _transform = Matrix3x2.Identity;

    /// <summary>Sets the clipping region to a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public void SetClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Replace);

    /// <summary>Sets the clipping region to a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public void SetClip(RectangleF rect) => SetClip(rect, CombineMode.Replace);

    /// <summary>Combines the clipping region with a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    /// <param name="combineMode">How they combine; union, xor and exclusion keep the bounding rectangle.</param>
    public void SetClip(Rectangle rect, CombineMode combineMode) => SetClip((RectangleF)rect, combineMode);

    /// <summary>Combines the clipping region with a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    /// <param name="combineMode">How they combine; union, xor and exclusion keep the bounding rectangle.</param>
    public void SetClip(RectangleF rect, CombineMode combineMode)
    {
        var device = ToDevice(rect);
        _clip = combineMode switch
        {
            CombineMode.Replace => device,
            CombineMode.Intersect => _clip is { } clip ? RectangleF.Intersect(clip, device) : device,
            CombineMode.Union => _clip is { } clip ? RectangleF.Union(clip, device) : null,
            _ => _clip,
        };
    }

    /// <summary>Sets the clipping region to the bounds of a path.</summary>
    /// <param name="path">The path.</param>
    public void SetClip(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        SetClip(path.GetBounds(), CombineMode.Replace);
    }

    /// <summary>Combines the clipping region with the bounds of a path.</summary>
    /// <param name="path">The path.</param>
    /// <param name="combineMode">How they combine.</param>
    public void SetClip(GraphicsPath path, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(path);
        SetClip(path.GetBounds(), combineMode);
    }

    /// <summary>Combines the clipping region with a region.</summary>
    /// <param name="region">The region.</param>
    /// <param name="combineMode">How they combine.</param>
    public void SetClip(Region region, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(region);
        SetClip(region.Bounds, combineMode);
    }

    /// <summary>Sets the clipping region to that of another surface.</summary>
    /// <param name="g">The other surface.</param>
    public void SetClip(Graphics g)
    {
        ArgumentNullException.ThrowIfNull(g);
        _clip = g.EffectiveClip;
    }

    /// <summary>Limits the clipping region to a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public void IntersectClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Intersect);

    /// <summary>Limits the clipping region to a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public void IntersectClip(RectangleF rect) => SetClip(rect, CombineMode.Intersect);

    /// <summary>Limits the clipping region to a region.</summary>
    /// <param name="region">The region.</param>
    public void IntersectClip(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        SetClip(region.Bounds, CombineMode.Intersect);
    }

    /// <summary>Excludes a rectangle from the clipping region. Rectangular clipping keeps the region as it is.</summary>
    /// <param name="rect">The rectangle.</param>
    public void ExcludeClip(Rectangle rect)
    {
        // Clipping is rectangular here; excluding a part keeps the bounding rectangle.
    }

    /// <summary>Moves the clipping region.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void TranslateClip(float dx, float dy)
    {
        if (_clip is { } clip)
        {
            _clip = new RectangleF(clip.X + dx, clip.Y + dy, clip.Width, clip.Height);
        }
    }

    /// <summary>Resets the clipping region to the whole surface.</summary>
    public void ResetClip() => _clip = null;

    /// <summary>Whether a point is in the clipping region.</summary>
    /// <param name="point">The point, in world coordinates.</param>
    public bool IsVisible(PointF point) => ClipBounds.Contains(point);

    /// <summary>Whether a point is in the clipping region.</summary>
    /// <param name="point">The point, in world coordinates.</param>
    public bool IsVisible(Point point) => IsVisible((PointF)point);

    /// <summary>Whether a point is in the clipping region.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y));

    /// <summary>Whether a point is in the clipping region.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public bool IsVisible(int x, int y) => IsVisible(new PointF(x, y));

    /// <summary>Whether a rectangle overlaps the clipping region.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public bool IsVisible(RectangleF rect) => ClipBounds.IntersectsWith(rect);

    /// <summary>Whether a rectangle overlaps the clipping region.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public bool IsVisible(Rectangle rect) => ClipBounds.IntersectsWith(rect);

    /// <summary>Saves the transformation, clip and rendering settings.</summary>
    public GraphicsState Save() => new(Capture());

    /// <summary>Restores what <see cref="Save"/> saved.</summary>
    /// <param name="gstate">The saved state.</param>
    public void Restore(GraphicsState gstate)
    {
        ArgumentNullException.ThrowIfNull(gstate);
        Restore((SavedState)gstate.State);
    }

    /// <summary>Opens a container: the state is saved until <see cref="EndContainer"/>.</summary>
    public GraphicsContainer BeginContainer() => new(Capture());

    /// <summary>Closes a container, restoring the state from when it was opened.</summary>
    /// <param name="container">The container.</param>
    public void EndContainer(GraphicsContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        Restore((SavedState)container.State);
    }

    /// <summary>Sends pending drawing on. Drawing is shown when the window is next drawn.</summary>
    public void Flush()
    {
        // Recorded drawing is shown with the window's next frame.
    }

    /// <summary>Sends pending drawing on. Drawing is shown when the window is next drawn.</summary>
    /// <param name="intention">Whether to wait.</param>
    public void Flush(FlushIntention intention)
    {
        // Recorded drawing is shown with the window's next frame.
    }

    /// <summary>The nearest color the surface can show: every color.</summary>
    /// <param name="color">The color.</param>
    public Color GetNearestColor(Color color) => color;

    /// <summary>Device contexts do not exist on this device.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IntPtr GetHdc() =>
        throw new NotSupportedException("Graphics.GetHdc is not supported by TabStudio's Forms layer: there is no GDI device context on this device.");

    /// <summary>Device contexts do not exist on this device.</summary>
    /// <param name="hdc">The device context.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void ReleaseHdc(IntPtr hdc) =>
        throw new NotSupportedException("Graphics.ReleaseHdc is not supported by TabStudio's Forms layer: there is no GDI device context on this device.");

    /// <summary>Releases the GDI device context. Not available on this device.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void ReleaseHdc() =>
        throw new NotSupportedException("Graphics.ReleaseHdc is not supported by TabStudio's Forms layer: there is no GDI device context on this device.");

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onDispose?.Invoke(this);
    }

    /// <summary>Records a command in the current state.</summary>
    private void Record(DrawCommand command)
    {
        ObjectDisposedException.ThrowIf(_disposed && _onDispose is not null, this);
        _builder.Add(Absolute, EffectiveClip, Antialias, command);
        _onRecord?.Invoke();
    }

    private void Apply(Matrix3x2 operation, MatrixOrder order) =>
        _transform = order == MatrixOrder.Prepend ? operation * _transform : _transform * operation;

    /// <summary>The offset that puts an odd-width stroke on pixel centers, so a one-pixel line is one pixel wide.</summary>
    private float StrokeOffset(Pen pen)
    {
        var width = Math.Max(1, (int)MathF.Round(pen.Width));
        var absolute = Absolute;
        var axisAligned = absolute.M12 == 0 && absolute.M21 == 0 && absolute.M11 == 1 && absolute.M22 == 1;
        return axisAligned && width % 2 == 1 ? 0.5f : 0f;
    }

    private RectangleF ToDevice(RectangleF rect) => Bounds(rect, Absolute);

    private RectangleF ToWorld(RectangleF rect) =>
        Matrix3x2.Invert(Absolute, out var inverse) ? Bounds(rect, inverse) : RectangleF.Empty;

    private static RectangleF Bounds(RectangleF rect, Matrix3x2 matrix)
    {
        if (rect.Width >= 4194304)
        {
            return rect;
        }

        Span<Vector2> corners =
        [
            Vector2.Transform(new Vector2(rect.Left, rect.Top), matrix),
            Vector2.Transform(new Vector2(rect.Right, rect.Top), matrix),
            Vector2.Transform(new Vector2(rect.Left, rect.Bottom), matrix),
            Vector2.Transform(new Vector2(rect.Right, rect.Bottom), matrix),
        ];
        float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
        foreach (var corner in corners)
        {
            left = Math.Min(left, corner.X);
            top = Math.Min(top, corner.Y);
            right = Math.Max(right, corner.X);
            bottom = Math.Max(bottom, corner.Y);
        }

        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    private SavedState Capture() => new(_transform, _clip, SmoothingMode, InterpolationMode, TextRenderingHint);

    private void Restore(SavedState state)
    {
        _transform = state.Transform;
        _clip = state.Clip;
        SmoothingMode = state.Smoothing;
        InterpolationMode = state.Interpolation;
        TextRenderingHint = state.TextHint;
    }

    private sealed record SavedState(Matrix3x2 Transform, RectangleF? Clip, SmoothingMode Smoothing, InterpolationMode Interpolation, TextRenderingHint TextHint);
}
