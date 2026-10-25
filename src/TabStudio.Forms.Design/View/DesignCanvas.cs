using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TabStudio.Forms.Design.Catalog;
using TabStudio.Forms.Design.Model;
using TabStudio.Forms.Design.Surface;
using TabStudio.Forms.Hosting;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace TabStudio.Forms.Design.View;

/// <summary>
/// The Forms designer's surface on screen: the form in its Windows 11 frame, as the app preview shows it running, with
/// the selected controls' handles, outlines for panels that have no border, and the component tray of timers and
/// other components below. Fingers and the mouse select, move and resize; see <c>DesignCanvas.Input.cs</c>.
/// </summary>
internal sealed partial class DesignCanvas : Control
{
    /// <summary>The background around the form.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<DesignCanvas, IBrush?>(nameof(Background));

    /// <summary>The color of selection outlines and handles.</summary>
    public static readonly StyledProperty<Color> AccentProperty = AvaloniaProperty.Register<DesignCanvas, Color>(nameof(Accent), Color.FromRgb(0x00, 0x78, 0xD7));

    /// <summary>The space around the form.</summary>
    private const double FormMargin = 28;

    /// <summary>A handle's size on screen.</summary>
    private const double HandleSize = 8;

    /// <summary>The height of the component tray's chips.</summary>
    private const double TrayHeight = 30;

    private static readonly IBrush TrayBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x14, 0x80, 0x80, 0x80));
    private static readonly IPen ContainerOutline = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x90, 0x80, 0x80, 0x80)), 1, new ImmutableDashStyle([3, 3], 0));

    private readonly DisplayListRenderer _renderer = new();
    private readonly List<string> _selection = [];
    private DesignSurface? _surface;
    private DisplayList _content = DisplayList.Empty;
    private double _scale = 1;

    static DesignCanvas()
    {
        AffectsRender<DesignCanvas>(BackgroundProperty, AccentProperty);
        FocusableProperty.OverrideDefaultValue<DesignCanvas>(true);
    }

    /// <summary>Creates the canvas.</summary>
    public DesignCanvas()
    {
        ClipToBounds = true;
        InitializeInput();
    }

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised on a double-tap on a component: the host creates its default event's handler and shows it.</summary>
    public event EventHandler<DesignComponent>? HandlerRequested;

    /// <summary>Raised on a long press or a right-click: the host shows the designer's menu at the point.</summary>
    public event EventHandler<Point>? MenuRequested;

    /// <summary>Raised when the armed tool was placed on the form, or put away.</summary>
    public event EventHandler? ToolPlaced;

    /// <summary>The background around the form.</summary>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>The color of selection outlines and handles.</summary>
    public Color Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>The surface shown.</summary>
    public DesignSurface? Surface
    {
        get => _surface;
        set
        {
            if (_surface is not null)
            {
                _surface.Changed -= OnSurfaceChanged;
            }

            _surface = value;
            if (_surface is not null)
            {
                _surface.Changed += OnSurfaceChanged;
            }

            _selection.Clear();
            OnSurfaceChanged(this, EventArgs.Empty);
        }
    }

    /// <summary>The tool a tap on the form adds, as after tapping it in the Toolbox, or <see langword="null"/>.</summary>
    public Type? ArmedTool { get; set; }

    /// <summary>The scale the form is drawn at: 1, or less when it does not fit.</summary>
    public double Scale => _scale;

    /// <summary>The selected components, the last selected last.</summary>
    public IReadOnlyList<DesignComponent> Selection =>
        _surface?.Document is { } document ? [.. _selection.Select(document.Find).OfType<DesignComponent>()] : [];

    /// <summary>The selected component the Properties panel shows: the last selected, or the form.</summary>
    public DesignComponent? PrimarySelection => Selection is [.., var last] ? last : _surface?.Document?.Root;

    /// <summary>Selects components by name.</summary>
    /// <param name="names">The names; none selects nothing.</param>
    public void Select(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var next = names.Distinct(StringComparer.Ordinal).ToList();
        if (next.SequenceEqual(_selection, StringComparer.Ordinal))
        {
            return;
        }

        _selection.Clear();
        _selection.AddRange(next);
        InvalidateVisual();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Where a point of the form's client area is on the canvas.</summary>
    /// <param name="point">The point, in the form's client coordinates.</param>
    public Point ToCanvas(DrawingPoint point)
    {
        var origin = ClientOrigin();
        return new Point(origin.X + (point.X * _scale), origin.Y + (point.Y * _scale));
    }

    /// <summary>Where a point of the canvas is in the form's client area.</summary>
    /// <param name="point">The point on the canvas.</param>
    public DrawingPoint ToDesign(Point point)
    {
        var origin = ClientOrigin();
        return new DrawingPoint((int)Math.Floor((point.X - origin.X) / _scale), (int)Math.Floor((point.Y - origin.Y) / _scale));
    }

    /// <summary>A component's bounds on the canvas; for a tray component, its chip's.</summary>
    /// <param name="component">The component.</param>
    public Rect BoundsOnCanvas(DesignComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_surface is null)
        {
            return default;
        }

        var bounds = DesignSurface.BoundsOf(component);
        if (bounds.IsEmpty && TrayItems().FirstOrDefault(t => t.Component.Name == component.Name) is { Component: not null } tray)
        {
            return tray.Bounds;
        }

        var topLeft = ToCanvas(bounds.Location);
        return new Rect(topLeft, new Size(bounds.Width * _scale, bounds.Height * _scale));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background ?? Brushes.Transparent, new Rect(Bounds.Size));
        if (_surface?.Root is not { } root || _surface.Document is not { } document)
        {
            return;
        }

        UpdateScale();
        var origin = ClientOrigin();
        var frame = _surface.Frame(_content);
        using (context.PushTransform(Matrix.CreateScale(_scale, _scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            if (frame is not null)
            {
                ChromePainter.DrawShadow(context, frame, active: true);
                using (context.PushClip(ChromePainter.Outline(frame, ChromePainter.ToRect(frame.Frame))))
                {
                    ChromePainter.DrawTitleBar(context, frame, active: true, WindowPart.None, ButtonState.Normal);
                    DrawContent(context, root);
                }

                ChromePainter.DrawBorder(context, frame, active: true);
            }
            else
            {
                DrawContent(context, root);
                context.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)), 1 / _scale), new Rect(-0.5, -0.5, root.Width + 1, root.Height + 1));
            }
        }

        DrawContainerOutlines(context, document);
        DrawTray(context);
        DrawSelection(context);
        DrawGestureFeedback(context);
    }

    private void DrawContent(DrawingContext context, System.Windows.Forms.Control root)
    {
        using (context.PushClip(new Rect(0, 0, root.ClientSize.Width, root.ClientSize.Height)))
        {
            _renderer.Render(context, _content);
        }
    }

    /// <summary>Dashed outlines for containers that draw no border of their own, as Visual Studio shows them at design time.</summary>
    private void DrawContainerOutlines(DrawingContext context, DesignDocument document)
    {
        foreach (var component in document.Components)
        {
            if (component.Instance is System.Windows.Forms.Control control
                && control is System.Windows.Forms.Panel { BorderStyle: System.Windows.Forms.BorderStyle.None } or System.Windows.Forms.SplitContainer or PlaceholderControl)
            {
                var rect = BoundsOnCanvas(component).Deflate(0.5);
                context.DrawRectangle(null, ContainerOutline, rect);
                if (control is System.Windows.Forms.SplitContainer split)
                {
                    var splitter = split.SplitterRectangle;
                    var at = ToCanvas(new DrawingPoint(control.BoundsInWindow().X + splitter.X, control.BoundsInWindow().Y + splitter.Y));
                    context.DrawRectangle(null, ContainerOutline, new Rect(at, new Size(splitter.Width * _scale, splitter.Height * _scale)).Deflate(0.5));
                }
            }
        }
    }

    /// <summary>The component tray: the components that are not drawn on the form, as chips under it.</summary>
    private void DrawTray(DrawingContext context)
    {
        var items = TrayItems();
        if (items.Count == 0)
        {
            return;
        }

        var foreground = this.FindResource("Brush.Foreground") as IBrush ?? Brushes.Gray;
        foreach (var (component, bounds) in items)
        {
            context.DrawRectangle(TrayBackground, null, new RoundedRect(bounds, 6));
            if (component.Type is { } type && Toolbox.Items.FirstOrDefault(t => t.Type.IsAssignableFrom(type)) is { } tool)
            {
                using (context.PushTransform(Matrix.CreateScale(14 / 24.0, 14 / 24.0) * Matrix.CreateTranslation(bounds.X + 8, bounds.Y + 8)))
                {
                    context.DrawGeometry(null, new ImmutablePen(foreground.ToImmutable(), 1.7 * 24 / 14.0), StreamGeometry.Parse(tool.Icon));
                }
            }

            var text = new FormattedText(component.Name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 12, foreground);
            context.DrawText(text, new Point(bounds.X + 28, bounds.Y + ((bounds.Height - text.Height) / 2)));
        }
    }

    /// <summary>The tray's chips: one per component that is not drawn on the form.</summary>
    private List<(DesignComponent Component, Rect Bounds)> TrayItems()
    {
        var items = new List<(DesignComponent, Rect)>();
        if (_surface?.Document is not { } document || _surface.Root is not { } root)
        {
            return items;
        }

        var frameBottom = ToCanvas(new DrawingPoint(0, root.ClientSize.Height)).Y + (_surface.Frame(_content) is { } frame ? (frame.Frame.Bottom - root.ClientSize.Height) * _scale : 0);
        var x = FormMargin;
        var y = frameBottom + 16;
        foreach (var component in document.Components.Where(c => c.Instance is not System.Windows.Forms.Control and not System.Windows.Forms.ToolStripItem))
        {
            var width = 40 + (component.Name.Length * 7.2);
            if (x + width > Bounds.Width - FormMargin && x > FormMargin)
            {
                x = FormMargin;
                y += TrayHeight + 8;
            }

            items.Add((component, new Rect(x, y, width, TrayHeight)));
            x += width + 8;
        }

        return items;
    }

    /// <summary>The selection: an outline around each selected component, and the handles of the last selected.</summary>
    private void DrawSelection(DrawingContext context)
    {
        var accent = new ImmutableSolidColorBrush(Accent);
        var outline = new ImmutablePen(accent, 1, new ImmutableDashStyle([2, 2], 0));
        var handleFill = Brushes.White;
        var handlePen = new ImmutablePen(accent, 1);
        var selection = Selection;
        foreach (var component in selection)
        {
            var bounds = BoundsOnCanvas(component);
            if (bounds.Width <= 0 && bounds.Height <= 0)
            {
                continue;
            }

            context.DrawRectangle(null, outline, component.IsRoot ? bounds.Inflate(1.5) : bounds.Inflate(0.5));
            if (component != selection[^1])
            {
                continue;
            }

            foreach (var (_, handle) in Handles(component))
            {
                context.DrawRectangle(handleFill, handlePen, new Rect(handle.X - (HandleSize / 2) + 0.5, handle.Y - (HandleSize / 2) + 0.5, HandleSize - 1, HandleSize - 1));
            }
        }
    }

    private void OnSurfaceChanged(object? sender, EventArgs e)
    {
        _content = _surface?.Paint() ?? DisplayList.Empty;

        // Components that are gone leave the selection; renamed ones are reselected by the edit that renamed them.
        if (_surface?.Document is { } document && _selection.RemoveAll(name => document.Find(name) is null) > 0)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        InvalidateVisual();
    }

    /// <summary>The scale that fits the form and its frame in the canvas, at most 1.</summary>
    private void UpdateScale()
    {
        if (_surface?.Root is not { } root || Bounds.Width <= 0)
        {
            return;
        }

        var frame = _surface.Frame(_content)?.Frame ?? new DrawingRectangle(DrawingPoint.Empty, root.Size);
        var available = Math.Max(1, Bounds.Width - (2 * FormMargin));
        _scale = Math.Clamp(Math.Floor(available / frame.Width * 100) / 100, 0.3, 1);
    }

    /// <summary>Where the form's client area's origin is on the canvas.</summary>
    private Point ClientOrigin()
    {
        var frame = _surface?.Frame(_content)?.Frame ?? DrawingRectangle.Empty;
        return new Point(FormMargin - (frame.X * _scale), FormMargin - (frame.Y * _scale));
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // A new width can change the scale, and with it the height the form needs.
        var scale = _scale;
        UpdateScale();
        if (scale != _scale)
        {
            InvalidateMeasure();
        }
    }

    /// <summary>The size the form needs on the canvas.</summary>
    /// <param name="availableSize">The space available.</param>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_surface?.Root is not { } root)
        {
            return default;
        }

        UpdateScale();
        var frame = _surface.Frame(_content)?.Frame ?? new DrawingRectangle(DrawingPoint.Empty, root.Size);
        var trayRows = TrayItems().Select(t => t.Bounds.Bottom).DefaultIfEmpty(0).Max();
        var height = Math.Max((frame.Height * _scale) + (2 * FormMargin), trayRows + FormMargin);
        return new Size(double.IsInfinity(availableSize.Width) ? (frame.Width * _scale) + (2 * FormMargin) : availableSize.Width, height);
    }

    private DrawingSize ClientSize => _surface?.Root?.ClientSize ?? DrawingSize.Empty;
}
