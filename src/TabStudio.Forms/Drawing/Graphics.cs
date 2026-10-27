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
/// Drawing is recorded and shown when the window is next drawn, which follows each event the app handles. On an
/// image (<see cref="FromImage"/>) drawing goes into the image's pixels, as soon as anything reads them. Shapes are
/// drawn without antialiasing unless <see cref="SmoothingMode"/> asks for it, as in GDI+.
/// </remarks>
public sealed partial class Graphics : MarshalByRefObject, IDeviceContext
{
    private readonly DisplayListBuilder _builder;
    private readonly Matrix3x2 _origin;
    private readonly RectangleF _bounds;
    private readonly Action<Graphics>? _onDispose;
    private readonly Action? _onRecord;
    private readonly ImageBuffer? _image;
    private readonly bool _replaceable;
    private Matrix3x2 _transform = Matrix3x2.Identity;
    private RectangleF? _clip;
    private IReadOnlyList<PathData>? _clipPaths;
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

    /// <summary>Creates a surface that draws into an image's pixels.</summary>
    /// <param name="image">The image's pixels.</param>
    private Graphics(ImageBuffer image)
        : this(image.Recorder, PointF.Empty, new RectangleF(0, 0, image.Width, image.Height), onDispose: _ => image.Flush(), onRecord: image.Recorded) =>
        _image = image;

    /// <summary>Creates a surface whose earlier drawing a later drawing covering all of it may replace.</summary>
    /// <param name="builder">Where drawing is recorded.</param>
    /// <param name="origin">Where the surface's origin is in the window's client area.</param>
    /// <param name="bounds">What the surface may draw on, in the window's client area.</param>
    /// <param name="onDispose">Called when the surface is disposed.</param>
    /// <param name="onRecord">Called after each drawing operation.</param>
    /// <param name="replaceable">Whether a clear or a copy of the whole surface forgets what was drawn before.</param>
    internal Graphics(DisplayListBuilder builder, PointF origin, RectangleF bounds, Action<Graphics>? onDispose, Action? onRecord, bool replaceable)
        : this(builder, origin, bounds, onDispose, onRecord) =>
        _replaceable = replaceable;

    /// <summary>Whether shapes are antialiased.</summary>
    public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.None;

    /// <summary>The quality of text rendering. Text is always antialiased on this device.</summary>
    public TextRenderingHint TextRenderingHint { get; set; }

    /// <summary>How images are scaled: <see cref="InterpolationMode.NearestNeighbor"/> keeps hard pixel edges.</summary>
    public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Bilinear;

    /// <summary>
    /// Where pixels are: by default, as in GDI+, a pixel's center is at whole coordinates, so a one-pixel line at
    /// y = 10 covers row 10 exactly; <see cref="PixelOffsetMode.Half"/> and <see cref="PixelOffsetMode.HighQuality"/>
    /// put a pixel's corner there instead.
    /// </summary>
    public PixelOffsetMode PixelOffsetMode { get; set; }

    /// <summary>The compositing quality. Kept for compatibility.</summary>
    public CompositingQuality CompositingQuality { get; set; }

    /// <summary>Whether shapes and images blend with what is under them, or replace it (<see cref="CompositingMode.SourceCopy"/>).</summary>
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
            SetClip(value, CombineMode.Replace);
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

    private bool SourceCopy => CompositingMode == CompositingMode.SourceCopy;

    private bool PixelCenters => PixelOffsetMode is not (PixelOffsetMode.Half or PixelOffsetMode.HighQuality);

    /// <summary>Whether drawing covers the whole surface, with nothing clipped away.</summary>
    private bool ClipIsWholeSurface => _clipPaths is null && (_clip is null || RectangleF.Intersect(_bounds, _clip.Value) == _bounds);

    /// <summary>Creates a surface that draws on an image, as GDI+ does: the image's pixels change.</summary>
    /// <param name="image">The image, a <see cref="Bitmap"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="Exception">The image has an indexed pixel format, which GDI+ cannot draw on.</exception>
    public static Graphics FromImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if ((image.PixelFormat & Imaging.PixelFormat.Indexed) != 0)
        {
#pragma warning disable CA2201 // GDI+ throws this exact type, and apps catch it.
            throw new Exception("A Graphics object cannot be created from an image that has an indexed pixel format.");
#pragma warning restore CA2201
        }

        return new Graphics(image.Buffer);
    }

    /// <summary>Creates a surface for a window handle: the control's surface, as <see cref="System.Windows.Forms.Control.CreateGraphics"/> returns.</summary>
    /// <param name="hwnd">The handle of a control, from <see cref="System.Windows.Forms.Control.Handle"/>.</param>
    /// <exception cref="ArgumentException">No control has the handle.</exception>
    public static Graphics FromHwnd(IntPtr hwnd) =>
        System.Windows.Forms.Control.FromHandle(hwnd)?.CreateGraphics()
            ?? throw new ArgumentException("Parameter is not valid: no window has this handle.", nameof(hwnd));

    /// <summary>Creates a surface for a window handle.</summary>
    /// <param name="hwnd">The handle of a control.</param>
    /// <exception cref="ArgumentException">No control has the handle.</exception>
    public static Graphics FromHwndInternal(IntPtr hwnd) => FromHwnd(hwnd);

    /// <summary>Fills the clipping region with a color. On an image, the pixels become that color, transparency included.</summary>
    /// <param name="color">The color.</param>
    public void Clear(Color color)
    {
        var clip = EffectiveClip;
        var argb = SystemColorValues.Argb(color);
        if (_image is not null)
        {
            _builder.Add(Matrix3x2.Identity, clip, antialias: false, new ClearCommand(argb), _clipPaths, sourceCopy: true);
        }
        else
        {
            if (_replaceable && ClipIsWholeSurface && (argb >> 24) == 255)
            {
                // What was drawn before is hidden from now on.
                _builder.Clear();
            }

            _builder.Add(Matrix3x2.Identity, clip, antialias: false, new RectangleCommand(clip, new SolidBrushSpec(argb), null), _clipPaths);
        }

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
        var axisAligned = _transform.M12 == 0 && _transform.M21 == 0;
        if (!axisAligned && combineMode is CombineMode.Replace or CombineMode.Intersect or CombineMode.Exclude)
        {
            // A rotated rectangle clips as the shape it is.
            using var path = new GraphicsPath();
            path.AddRectangle(rect);
            SetClip(path, combineMode);
            return;
        }

        var device = ToDevice(rect);
        switch (combineMode)
        {
            case CombineMode.Replace:
                _clip = device;
                _clipPaths = null;
                break;
            case CombineMode.Intersect:
                _clip = _clip is { } clip ? RectangleF.Intersect(clip, device) : device;
                break;
            case CombineMode.Union:
                _clip = _clip is { } current && _clipPaths is null ? RectangleF.Union(current, device) : null;
                _clipPaths = null;
                break;
            case CombineMode.Exclude:
                _clipPaths = [.. _clipPaths ?? [], Excluding(RectanglePath(device))];
                break;
            case CombineMode.Xor:
            case CombineMode.Complement:
                // Kept as the bounding rectangle.
                break;
        }
    }

    /// <summary>Sets the clipping region to a path's shape.</summary>
    /// <param name="path">The path.</param>
    public void SetClip(GraphicsPath path) => SetClip(path, CombineMode.Replace);

    /// <summary>Combines the clipping region with a path's shape.</summary>
    /// <param name="path">The path.</param>
    /// <param name="combineMode">How they combine; union, xor and complement keep the bounding rectangle.</param>
    public void SetClip(GraphicsPath path, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(path);
        var device = GeometryMath.Transform(path.ToData(), Absolute);
        var bounds = GeometryMath.Bounds(device);
        switch (combineMode)
        {
            case CombineMode.Replace:
                _clip = bounds;
                _clipPaths = [device];
                break;
            case CombineMode.Intersect:
                _clip = _clip is { } clip ? RectangleF.Intersect(clip, bounds) : bounds;
                _clipPaths = [.. _clipPaths ?? [], device];
                break;
            case CombineMode.Exclude:
                _clipPaths = [.. _clipPaths ?? [], Excluding(device)];
                break;
            default:
                SetClip(path.GetBounds(), combineMode);
                break;
        }
    }

    /// <summary>Sets the clipping region to a region.</summary>
    /// <param name="region">The region.</param>
    public void SetClip(Region region) => SetClip(region, CombineMode.Replace);

    /// <summary>Combines the clipping region with a region.</summary>
    /// <param name="region">The region.</param>
    /// <param name="combineMode">How they combine.</param>
    public void SetClip(Region region, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region.IsInfiniteRegion)
        {
            if (combineMode == CombineMode.Replace)
            {
                ResetClip();
            }

            return;
        }

        if (region.Path is { } path)
        {
            SetClip(path, combineMode);
            if (combineMode is CombineMode.Replace or CombineMode.Intersect && !region.Bounds.Contains(path.GetBounds()))
            {
                SetClip(region.Bounds, CombineMode.Intersect);
            }

            foreach (var excluded in region.Exclusions)
            {
                SetClip(excluded, CombineMode.Exclude);
            }

            return;
        }

        SetClip(region.Bounds, combineMode);
        foreach (var excluded in region.Exclusions)
        {
            SetClip(excluded, CombineMode.Exclude);
        }
    }

    /// <summary>Sets the clipping region to that of another surface.</summary>
    /// <param name="g">The other surface.</param>
    public void SetClip(Graphics g)
    {
        ArgumentNullException.ThrowIfNull(g);
        _clip = g.EffectiveClip;
        _clipPaths = g._clipPaths;
    }

    /// <summary>Sets the clipping region to that of another surface, combined with the current one.</summary>
    /// <param name="g">The other surface.</param>
    /// <param name="combineMode">How they combine.</param>
    public void SetClip(Graphics g, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (combineMode == CombineMode.Replace)
        {
            SetClip(g);
            return;
        }

        var bounds = g.EffectiveClip;
        if (combineMode == CombineMode.Intersect)
        {
            _clip = _clip is { } clip ? RectangleF.Intersect(clip, bounds) : bounds;
            _clipPaths = g._clipPaths is { } paths ? [.. _clipPaths ?? [], .. paths] : _clipPaths;
            return;
        }

        SetClip(ToWorld(bounds), combineMode);
    }

    /// <summary>Limits the clipping region to a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public void IntersectClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Intersect);

    /// <summary>Limits the clipping region to a rectangle.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public void IntersectClip(RectangleF rect) => SetClip(rect, CombineMode.Intersect);

    /// <summary>Limits the clipping region to a region.</summary>
    /// <param name="region">The region.</param>
    public void IntersectClip(Region region) => SetClip(region, CombineMode.Intersect);

    /// <summary>Excludes a rectangle from the clipping region.</summary>
    /// <param name="rect">The rectangle, in world coordinates.</param>
    public void ExcludeClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Exclude);

    /// <summary>Excludes a region from the clipping region.</summary>
    /// <param name="region">The region.</param>
    public void ExcludeClip(Region region) => SetClip(region, CombineMode.Exclude);

    /// <summary>Moves the clipping region.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void TranslateClip(float dx, float dy)
    {
        if (_clip is { } clip)
        {
            _clip = new RectangleF(clip.X + dx, clip.Y + dy, clip.Width, clip.Height);
        }

        if (_clipPaths is { } paths)
        {
            var offset = Matrix3x2.CreateTranslation(dx, dy);
            _clipPaths = [.. paths.Select(path => GeometryMath.Transform(path, offset))];
        }
    }

    /// <summary>Moves the clipping region.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void TranslateClip(int dx, int dy) => TranslateClip((float)dx, dy);

    /// <summary>Resets the clipping region to the whole surface.</summary>
    public void ResetClip()
    {
        _clip = null;
        _clipPaths = null;
    }

    /// <summary>Whether a point is in the clipping region.</summary>
    /// <param name="point">The point, in world coordinates.</param>
    public bool IsVisible(PointF point)
    {
        if (!ClipBounds.Contains(point))
        {
            return false;
        }

        if (_clipPaths is not { } paths)
        {
            return true;
        }

        var device = Vector2.Transform(new Vector2(point.X, point.Y), Absolute);
        return paths.All(path => GeometryMath.Contains(path, new PointF(device.X, device.Y)));
    }

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

    /// <summary>
    /// Sends pending drawing on: on an image, it goes into the pixels now; on a window, it is shown when the window
    /// is next drawn.
    /// </summary>
    public void Flush() => _image?.Flush();

    /// <summary>Sends pending drawing on, as <see cref="Flush()"/> does.</summary>
    /// <param name="intention">Whether to wait; drawing always completes before this returns.</param>
    public void Flush(FlushIntention intention) => Flush();

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
        _builder.Add(Absolute, EffectiveClip, Antialias, command, _clipPaths, SourceCopy, PixelCenters);
        _onRecord?.Invoke();
    }

    /// <summary>
    /// Draws an image that replaces what is under it, as <see cref="BufferedGraphics.Render()"/> copies its buffer:
    /// when it covers the whole surface, what was drawn before is forgotten.
    /// </summary>
    /// <param name="image">The pixels.</param>
    /// <param name="destination">Where they go, in world coordinates.</param>
    internal void CopyImage(ImageSnapshot image, RectangleF destination)
    {
        if (_replaceable && ClipIsWholeSurface && _transform.IsIdentity && destination.Contains(new RectangleF(0, 0, _bounds.Width, _bounds.Height)))
        {
            _builder.Clear();
        }

        ObjectDisposedException.ThrowIf(_disposed && _onDispose is not null, this);
        _builder.Add(Absolute, EffectiveClip, antialias: false, new ImageCommand(image, destination, null, Smooth: false), _clipPaths, sourceCopy: true);
        _onRecord?.Invoke();
    }

    /// <summary>A path that covers everything but a shape, for excluding it from the clip.</summary>
    private static PathData Excluding(PathData shape)
    {
        const float Far = 4194304;
        var outside = new PathFigureData(new PointF(-Far, -Far), [new LineSegmentData(new PointF(Far, -Far)), new LineSegmentData(new PointF(Far, Far)), new LineSegmentData(new PointF(-Far, Far))], Closed: true);
        return new PathData([outside, .. shape.Figures], FillMode.Alternate);
    }

    private static PathData RectanglePath(RectangleF rect) =>
        new([new PathFigureData(new PointF(rect.Left, rect.Top), [new LineSegmentData(new PointF(rect.Right, rect.Top)), new LineSegmentData(new PointF(rect.Right, rect.Bottom)), new LineSegmentData(new PointF(rect.Left, rect.Bottom))], Closed: true)], FillMode.Alternate);

    private void Apply(Matrix3x2 operation, MatrixOrder order) =>
        _transform = order == MatrixOrder.Prepend ? operation * _transform : _transform * operation;

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

    private SavedState Capture() => new(_transform, _clip, _clipPaths, SmoothingMode, InterpolationMode, TextRenderingHint, CompositingMode, PixelOffsetMode, CompositingQuality);

    private void Restore(SavedState state)
    {
        _transform = state.Transform;
        _clip = state.Clip;
        _clipPaths = state.ClipPaths;
        SmoothingMode = state.Smoothing;
        InterpolationMode = state.Interpolation;
        TextRenderingHint = state.TextHint;
        CompositingMode = state.Compositing;
        PixelOffsetMode = state.PixelOffset;
        CompositingQuality = state.Quality;
    }

    private sealed record SavedState(
        Matrix3x2 Transform,
        RectangleF? Clip,
        IReadOnlyList<PathData>? ClipPaths,
        SmoothingMode Smoothing,
        InterpolationMode Interpolation,
        TextRenderingHint TextHint,
        CompositingMode Compositing,
        PixelOffsetMode PixelOffset,
        CompositingQuality Quality);
}
