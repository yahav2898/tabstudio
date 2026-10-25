using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TabStudio.Forms.Design.Model;
using TabStudio.Forms.Design.Surface;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace TabStudio.Forms.Design.View;

/// <summary>A selection handle: the edge or corner it drags.</summary>
internal enum Handle
{
    /// <summary>The top-left corner.</summary>
    TopLeft,

    /// <summary>The top edge.</summary>
    Top,

    /// <summary>The top-right corner.</summary>
    TopRight,

    /// <summary>The right edge.</summary>
    Right,

    /// <summary>The bottom-right corner.</summary>
    BottomRight,

    /// <summary>The bottom edge.</summary>
    Bottom,

    /// <summary>The bottom-left corner.</summary>
    BottomLeft,

    /// <summary>The left edge.</summary>
    Left,
}

/// <summary>
/// Touch and the mouse on the designer: a tap selects (with Ctrl or Shift, adds to the selection), a drag moves the
/// selection on the 8-pixel grid, possibly into another container, a handle resizes, a double-tap asks for the default
/// event's handler, and a long press or a right-click asks for the menu. With a tool armed, a tap adds it there.
/// Delete, the arrows, Escape and the usual shortcuts work from a keyboard.
/// </summary>
internal sealed partial class DesignCanvas
{
    /// <summary>How far the mouse moves before a press becomes a drag.</summary>
    private const double MouseSlop = 3;

    /// <summary>How far a finger moves before a press becomes a drag.</summary>
    private const double TouchSlop = 10;

    /// <summary>How near a handle a finger must press to grab it, on screen.</summary>
    private const double TouchHandleReach = 18;

    /// <summary>How near a handle the mouse must press to grab it.</summary>
    private const double MouseHandleReach = 6;

    private readonly DispatcherTimer _longPress = new() { Interval = TimeSpan.FromMilliseconds(550) };
    private Gesture? _gesture;

    private enum GestureKind
    {
        Pending,
        Move,
        Resize,
    }

    /// <summary>The handles of a component, with their centers on the canvas: none for a control that sizes itself.</summary>
    /// <param name="component">The component.</param>
    public IReadOnlyList<(Handle Handle, Point Center)> Handles(DesignComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var bounds = BoundsOnCanvas(component);
        if (component.IsRoot)
        {
            // A form is resized from its right and bottom edges, at its frame.
            var frame = _surface?.Frame(_content)?.Frame;
            if (frame is { } f)
            {
                var origin = ToCanvas(DrawingPoint.Empty);
                bounds = new Rect(origin.X + (f.X * _scale), origin.Y + (f.Y * _scale), f.Width * _scale, f.Height * _scale);
            }

            return [(Handle.Right, new Point(bounds.Right, bounds.Center.Y)), (Handle.BottomRight, bounds.BottomRight), (Handle.Bottom, new Point(bounds.Center.X, bounds.Bottom))];
        }

        if (component.Instance is not System.Windows.Forms.Control control || control.AutoSize && control is System.Windows.Forms.Label or System.Windows.Forms.CheckBox or System.Windows.Forms.RadioButton)
        {
            return [];
        }

        return
        [
            (Handle.TopLeft, bounds.TopLeft), (Handle.Top, new Point(bounds.Center.X, bounds.Top)), (Handle.TopRight, bounds.TopRight),
            (Handle.Right, new Point(bounds.Right, bounds.Center.Y)), (Handle.BottomRight, bounds.BottomRight),
            (Handle.Bottom, new Point(bounds.Center.X, bounds.Bottom)), (Handle.BottomLeft, bounds.BottomLeft), (Handle.Left, new Point(bounds.Left, bounds.Center.Y)),
        ];
    }

    /// <summary>Adds a tool where it was dropped from the Toolbox.</summary>
    /// <param name="type">The tool's type.</param>
    /// <param name="point">Where it was dropped, on the canvas.</param>
    /// <returns>Whether it was added: a drop off the form adds nothing.</returns>
    public bool DropTool(Type type, Point point)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (_surface?.Root is not { } root)
        {
            return false;
        }

        var design = ToDesign(point);
        var visual = typeof(System.Windows.Forms.Control).IsAssignableFrom(type);
        if (visual && !new DrawingRectangle(DrawingPoint.Empty, root.ClientSize).Contains(design))
        {
            return false;
        }

        var name = _surface.Add(type, visual ? design : DrawingPoint.Empty);
        Select([name]);
        Focus();
        return true;
    }

    /// <summary>Moves the selection by a distance, as the arrow keys do.</summary>
    /// <param name="dx">The horizontal distance.</param>
    /// <param name="dy">The vertical distance.</param>
    public void Nudge(int dx, int dy)
    {
        if (_surface is null)
        {
            return;
        }

        var moved = Selection.Where(c => !c.IsRoot && c.Instance is System.Windows.Forms.Control).Select(c =>
        {
            var bounds = DesignSurface.LocalBoundsOf(c);
            bounds.Offset(dx, dy);
            return (c, bounds);
        }).ToList();
        if (moved.Count > 0)
        {
            _surface.SetBounds(moved);
        }
    }

    /// <summary>Selects the container of the selected component, as Escape does.</summary>
    public void SelectParent()
    {
        if (_surface is not null && PrimarySelection is { IsRoot: false } selected)
        {
            Select([_surface.ParentOf(selected)?.Owner.Name ?? _surface.ClassName]);
        }
    }

    private void InitializeInput()
    {
        _longPress.Tick += (_, _) => OnLongPress();
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => CancelGesture();
        KeyDown += OnKeyDown;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_surface?.Document is not { } document || _gesture is not null)
        {
            return;
        }

        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        var point = e.GetPosition(this);
        var touch = e.Pointer.Type != PointerType.Mouse;
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            SelectAt(point, extend: false);
            MenuRequested?.Invoke(this, point);
            return;
        }

        // A handle of the selected component resizes it.
        if (Selection is [.., var primary] && HandleAt(primary, point, touch) is { } handle)
        {
            Begin(e, new Gesture(GestureKind.Resize, point, touch) { Handle = handle, Components = [(primary, DesignSurface.LocalBoundsOf(primary))] });
            return;
        }

        var extend = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) != 0;
        var hit = SelectAt(point, extend);
        if (hit is null)
        {
            return;
        }

        if (e.ClickCount == 2 && ArmedTool is null && !extend)
        {
            HandlerRequested?.Invoke(this, hit);
            return;
        }

        var movable = Selection.Where(c => !c.IsRoot && c.Instance is System.Windows.Forms.Control control && control.Dock == System.Windows.Forms.DockStyle.None).ToList();
        Begin(e, new Gesture(GestureKind.Pending, point, touch) { Hit = hit, Components = [.. movable.Select(c => (c, DesignSurface.LocalBoundsOf(c)))] });
        if (touch)
        {
            _longPress.Start();
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_gesture is not { } gesture || _surface is null || e.Pointer != gesture.Pointer)
        {
            return;
        }

        var point = e.GetPosition(this);
        var dx = (point.X - gesture.Start.X) / _scale;
        var dy = (point.Y - gesture.Start.Y) / _scale;
        if (gesture.Kind == GestureKind.Pending)
        {
            if (Math.Abs(point.X - gesture.Start.X) < (gesture.Touch ? TouchSlop : MouseSlop) && Math.Abs(point.Y - gesture.Start.Y) < (gesture.Touch ? TouchSlop : MouseSlop))
            {
                return;
            }

            _longPress.Stop();
            if (gesture.Components.Count == 0 || ArmedTool is not null)
            {
                return;
            }

            gesture.Kind = GestureKind.Move;
        }

        gesture.Current = gesture.Kind switch
        {
            GestureKind.Move => [.. gesture.Components.Select(c => (c.Component, Moved(c.Bounds, dx, dy)))],
            GestureKind.Resize => [.. gesture.Components.Select(c => (c.Component, Resized(c.Component, c.Bounds, gesture.Handle, dx, dy)))],
            _ => gesture.Current,
        };

        if (gesture.Kind == GestureKind.Move && gesture.Components.Count == 1)
        {
            gesture.Target = _surface.ContainerAt(ToDesign(point), [gesture.Components[0].Component]);
        }

        _surface.Preview(gesture.Current);
        _content = _surface.Paint();
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_gesture is not { } gesture || e.Pointer != gesture.Pointer || _surface is null)
        {
            return;
        }

        e.Handled = true;
        _gesture = null;
        _longPress.Stop();
        e.Pointer.Capture(null);
        switch (gesture.Kind)
        {
            case GestureKind.Pending when ArmedTool is { } tool && !gesture.LongPressed:
                ArmedTool = null;
                DropTool(tool, gesture.Start);
                ToolPlaced?.Invoke(this, EventArgs.Empty);
                break;
            case GestureKind.Move when gesture.Current.Count > 0:
                CommitMove(gesture);
                break;
            case GestureKind.Resize when gesture.Current.Count > 0:
                _surface.SetBounds(gesture.Current);
                break;
        }

        InvalidateVisual();
    }

    private void CommitMove(Gesture gesture)
    {
        var surface = _surface!;
        if (gesture.Target is var (owner, collection, origin) && gesture.Current.Count == 1)
        {
            var (component, bounds) = gesture.Current[0];
            var parent = surface.ParentOf(component);
            if (parent is { } current && (current.Owner.Name != owner.Name || current.Collection != collection))
            {
                // Dropped in another container: the control moves into it, where it was dropped.
                var oldOrigin = component.Instance is System.Windows.Forms.Control { Parent: { } oldParent } ? oldParent.ClientOriginInWindow() : DrawingPoint.Empty;
                var location = new DrawingPoint(
                    DesignSurface.Snap(oldOrigin.X + bounds.X - origin.X),
                    DesignSurface.Snap(oldOrigin.Y + bounds.Y - origin.Y));
                var name = component.Name;
                surface.Reparent(component, owner, collection, location);
                Select([name]);
                return;
            }
        }

        surface.SetBounds(gesture.Current);
    }

    private void OnLongPress()
    {
        _longPress.Stop();
        if (_gesture is { Kind: GestureKind.Pending } gesture)
        {
            gesture.LongPressed = true;
            CancelGesture();
            MenuRequested?.Invoke(this, gesture.Start);
        }
    }

    private void CancelGesture()
    {
        if (_gesture is { } gesture && gesture.Kind is GestureKind.Move or GestureKind.Resize && _surface is not null)
        {
            // Back to what the file says.
            _surface.Preview(gesture.Components);
            _content = _surface.Paint();
        }

        _gesture = null;
        _longPress.Stop();
        InvalidateVisual();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_surface?.Document is null)
        {
            return;
        }

        var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var step = control ? 1 : DesignSurface.GridSize;
        switch (e.Key)
        {
            case Key.Delete or Key.Back:
                _surface.Delete(Selection);
                break;
            case Key.Left:
                Nudge(-step, 0);
                break;
            case Key.Right:
                Nudge(step, 0);
                break;
            case Key.Up:
                Nudge(0, -step);
                break;
            case Key.Down:
                Nudge(0, step);
                break;
            case Key.Escape when ArmedTool is not null:
                ArmedTool = null;
                ToolPlaced?.Invoke(this, EventArgs.Empty);
                break;
            case Key.Escape:
                SelectParent();
                break;
            case Key.Z when control:
                _surface.Undo();
                break;
            case Key.Y when control:
                _surface.Redo();
                break;
            case Key.C when control:
                Clipboard = _surface.Copy(Selection);
                break;
            case Key.X when control:
                Clipboard = _surface.Copy(Selection);
                _surface.Delete(Selection);
                break;
            case Key.V when control && Clipboard is { } clip:
                Select(_surface.Paste(clip, PasteTarget()));
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>What was copied, shared by every form's designer, as the designer's clipboard is in Visual Studio.</summary>
    public static DesignClip? Clipboard { get; set; }

    /// <summary>Where a paste goes: into the selected container, or the selected control's container.</summary>
    public DrawingPoint? PasteTarget()
    {
        if (_surface is null || PrimarySelection is not { IsRoot: false } selected || selected.Instance is not System.Windows.Forms.Control control)
        {
            return null;
        }

        var bounds = DesignSurface.BoundsOf(selected);
        return DesignSurface.IsContainer(control) ? new DrawingPoint(bounds.X + 1, bounds.Y + 1) : null;
    }

    /// <summary>Selects what is at a point; with extend, adds it to or takes it from the selection.</summary>
    private DesignComponent? SelectAt(Point point, bool extend)
    {
        var hit = TrayItems().FirstOrDefault(t => t.Bounds.Contains(point)).Component
            ?? _surface?.HitTest(ToDesign(point));
        if (hit is null)
        {
            return null;
        }

        if (extend && !hit.IsRoot)
        {
            Select(_selection.Contains(hit.Name) ? _selection.Where(n => n != hit.Name) : [.. _selection.Where(n => n != _surface!.ClassName), hit.Name]);
        }
        else if (!_selection.Contains(hit.Name))
        {
            Select([hit.Name]);
        }

        return hit;
    }

    private Handle? HandleAt(DesignComponent component, Point point, bool touch)
    {
        var reach = touch ? TouchHandleReach : MouseHandleReach;
        foreach (var (handle, center) in Handles(component))
        {
            if (Math.Abs(point.X - center.X) <= reach && Math.Abs(point.Y - center.Y) <= reach)
            {
                return handle;
            }
        }

        return null;
    }

    private void Begin(PointerPressedEventArgs e, Gesture gesture)
    {
        gesture.Pointer = e.Pointer;
        _gesture = gesture;
        e.Pointer.Capture(this);
    }

    /// <summary>Bounds moved by a distance, snapped to the grid.</summary>
    private static DrawingRectangle Moved(DrawingRectangle bounds, double dx, double dy) =>
        new(DesignSurface.Snap(bounds.X + (int)Math.Round(dx)), DesignSurface.Snap(bounds.Y + (int)Math.Round(dy)), bounds.Width, bounds.Height);

    /// <summary>Bounds with an edge or a corner dragged by a distance, the dragged edges snapped to the grid.</summary>
    private static DrawingRectangle Resized(DesignComponent component, DrawingRectangle bounds, Handle handle, double dx, double dy)
    {
        var left = bounds.Left;
        var top = bounds.Top;
        var right = bounds.Right;
        var bottom = bounds.Bottom;
        var x = (int)Math.Round(dx);
        var y = (int)Math.Round(dy);
        if (handle is Handle.TopLeft or Handle.Left or Handle.BottomLeft)
        {
            left = Math.Min(DesignSurface.Snap(left + x), right - DesignSurface.GridSize);
        }

        if (handle is Handle.TopRight or Handle.Right or Handle.BottomRight)
        {
            right = Math.Max(component.IsRoot ? right + x : DesignSurface.Snap(right + x), left + DesignSurface.GridSize);
        }

        if (handle is Handle.TopLeft or Handle.Top or Handle.TopRight)
        {
            top = Math.Min(DesignSurface.Snap(top + y), bottom - DesignSurface.GridSize);
        }

        if (handle is Handle.BottomLeft or Handle.Bottom or Handle.BottomRight)
        {
            bottom = Math.Max(component.IsRoot ? bottom + y : DesignSurface.Snap(bottom + y), top + DesignSurface.GridSize);
        }

        return DrawingRectangle.FromLTRB(left, top, right, bottom);
    }

    /// <summary>While a control is dragged: the grid of its container, and the container it would go into.</summary>
    private void DrawGestureFeedback(DrawingContext context)
    {
        if (_gesture is not { Kind: GestureKind.Move or GestureKind.Resize } gesture || _surface is null)
        {
            return;
        }

        var (owner, collection, origin) = gesture.Target ?? (_surface.Document!.Root, "Controls", DrawingPoint.Empty);
        var container = owner.Instance as System.Windows.Forms.Control;
        if (collection.StartsWith("Panel", StringComparison.Ordinal) && container is System.Windows.Forms.SplitContainer split)
        {
            container = collection.StartsWith("Panel1", StringComparison.Ordinal) ? split.Panel1 : split.Panel2;
        }

        if (container is null)
        {
            return;
        }

        var size = container.ClientSize;
        var dot = new ImmutableSolidColorBrush(Color.FromArgb(0x60, 0x60, 0x60, 0x60));
        for (var gx = 0; gx < size.Width; gx += DesignSurface.GridSize)
        {
            for (var gy = 0; gy < size.Height; gy += DesignSurface.GridSize)
            {
                var at = ToCanvas(new DrawingPoint(origin.X + gx, origin.Y + gy));
                context.FillRectangle(dot, new Rect(at.X, at.Y, 1, 1));
            }
        }

        if (gesture.Target is not null && !owner.IsRoot)
        {
            var bounds = new Rect(ToCanvas(origin), new Size(size.Width * _scale, size.Height * _scale));
            context.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Accent), 2), bounds.Deflate(1));
        }
    }

    /// <summary>A press on the designer, followed until it is released.</summary>
    private sealed class Gesture(GestureKind kind, Point start, bool touch)
    {
        public GestureKind Kind { get; set; } = kind;

        public Point Start { get; } = start;

        public bool Touch { get; } = touch;

        public IPointer? Pointer { get; set; }

        public DesignComponent? Hit { get; init; }

        public Handle Handle { get; init; }

        /// <summary>The components dragged, with their bounds when the press began.</summary>
        public IReadOnlyList<(DesignComponent Component, DrawingRectangle Bounds)> Components { get; init; } = [];

        /// <summary>Their bounds now.</summary>
        public IReadOnlyList<(DesignComponent Component, DrawingRectangle Bounds)> Current { get; set; } = [];

        /// <summary>The container a single dragged control is over.</summary>
        public (DesignComponent Owner, string Collection, DrawingPoint Origin)? Target { get; set; }

        public bool LongPressed { get; set; }
    }
}
