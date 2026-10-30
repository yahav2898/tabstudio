using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TabStudio.App.Controls;

/// <summary>
/// Draws a line icon designed on a 24 by 24 grid, scaled to fit, in the inherited foreground color.
/// </summary>
/// <remarks>
/// All icons in TabStudio are simple stroke paths (see <c>Theme/Icons.axaml</c>), so they stay crisp at any
/// size and take the color of the button or text around them, exactly like a font glyph would.
/// <para>
/// In a right-to-left layout only the icons that point a way (back, forward, the left and right arrows and chevrons,
/// undo and redo) mirror, as Android's auto-mirrored icons do; Run's triangle, a check or a bug keep their drawing.
/// </para>
/// </remarks>
public sealed class StrokeIcon : Control
{
    /// <summary>The icon geometry, in a 24 by 24 coordinate space.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<StrokeIcon, Geometry?>(nameof(Data));

    /// <summary>The icon color. Inherited from the surrounding control by default.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<StrokeIcon>();

    /// <summary>The line width, in grid units.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<StrokeIcon, double>(nameof(StrokeThickness), 1.7);

    /// <summary>Whether the geometry is filled instead of stroked, for solid icons such as Play.</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<StrokeIcon, bool>(nameof(IsFilled));

    /// <summary>The icon size used when no explicit width or height is set.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<StrokeIcon, double>(nameof(Size), 18);

    private const double GridSize = 24;

    /// <summary>The icons that point a way, and mirror with a right-to-left layout.</summary>
    private static readonly string[] DirectionalKeys =
        ["Icon.ArrowLeft", "Icon.ArrowRight", "Icon.Back", "Icon.Forward", "Icon.ChevronLeft", "Icon.ChevronRight", "Icon.Undo", "Icon.Redo"];

    private static HashSet<Geometry>? _directional;

    static StrokeIcon()
    {
        AffectsRender<StrokeIcon>(DataProperty, ForegroundProperty, StrokeThicknessProperty, IsFilledProperty);
        AffectsMeasure<StrokeIcon>(SizeProperty);
    }

    /// <summary>Creates an icon.</summary>
    public StrokeIcon()
    {
        // A spinner's "spinning" class is often bound; one that starts turning while hidden must not run its animation.
        Classes.CollectionChanged += (_, _) => Theming.HiddenAnimations.Watch(this);
    }

    /// <inheritdoc cref="DataProperty"/>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <inheritdoc cref="StrokeThicknessProperty"/>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <inheritdoc cref="IsFilledProperty"/>
    public bool IsFilled
    {
        get => GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Whether the icon points a way, and so mirrors in a right-to-left layout.</summary>
    public bool IsDirectional => Data is { } data && Directional().Contains(data);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
        {
            UpdateMirroring();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMirroring();
    }

    /// <summary>
    /// Keeps an icon that does not point a way drawn left to right, so it does not mirror with its layout; one that
    /// does takes its layout's direction.
    /// </summary>
    private void UpdateMirroring()
    {
        if (IsDirectional)
        {
            ClearValue(FlowDirectionProperty);
        }
        else
        {
            FlowDirection = FlowDirection.LeftToRight;
        }
    }

    private static HashSet<Geometry> Directional()
    {
        if (_directional is { Count: > 0 } known)
        {
            return known;
        }

        var found = new HashSet<Geometry>(ReferenceEqualityComparer.Instance);
        if (Application.Current is { } application)
        {
            foreach (var key in DirectionalKeys)
            {
                if (application.TryGetResource(key, null, out var value) && value is Geometry geometry)
                {
                    found.Add(geometry);
                }
            }
        }

        return _directional = found;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var data = Data;
        var brush = Foreground;
        if (data is null || brush is null)
        {
            return;
        }

        var scale = Math.Min(Bounds.Width, Bounds.Height) / GridSize;
        var offsetX = (Bounds.Width - (GridSize * scale)) / 2;
        var offsetY = (Bounds.Height - (GridSize * scale)) / 2;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            if (IsFilled)
            {
                context.DrawGeometry(brush, null, data);
            }
            else
            {
                var pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                context.DrawGeometry(null, pen, data);
            }
        }
    }
}
