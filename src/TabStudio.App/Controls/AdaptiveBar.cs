using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace TabStudio.App.Controls;

/// <summary>
/// A toolbar or status bar that never wraps, scrolls or clips: when it runs out of room its least important items
/// shrink to their icons, and then move, least important first, into a More menu at its end (see
/// <see cref="AdaptiveLayout"/>). Children are <see cref="AdaptiveBarItem"/>s, <see cref="AdaptiveBarSeparator"/>s,
/// or any other control, which keeps its size and sets its importance with the attached <see cref="PriorityProperty"/>
/// and <see cref="IsPinnedProperty"/>.
/// </summary>
public sealed class AdaptiveBar : Panel
{
    /// <summary>The importance of a child that is not an <see cref="AdaptiveBarItem"/>.</summary>
    public static readonly AttachedProperty<int> PriorityProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, int>("Priority");

    /// <summary>Whether a child that is not an <see cref="AdaptiveBarItem"/> always stays in the bar; such children are pinned unless they say otherwise.</summary>
    public static readonly AttachedProperty<bool> IsPinnedProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, bool>("IsPinned", true);

    /// <summary>The space between two items.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AdaptiveBar, double>(nameof(Spacing), 2);

    /// <summary>The look of the items and the More button, such as <c>ToolButton</c> or <c>StatusBarButton</c>.</summary>
    public static readonly StyledProperty<ControlTheme?> ItemThemeProperty =
        AvaloniaProperty.Register<AdaptiveBar, ControlTheme?>(nameof(ItemTheme));

    private readonly Button _more;
    private AdaptiveMode[] _modes = [];
    private List<Control> _slotted = [];

    static AdaptiveBar()
    {
        AffectsMeasure<AdaptiveBar>(SpacingProperty);
        AffectsParentMeasure<AdaptiveBar>(PriorityProperty, IsPinnedProperty);
    }

    /// <summary>Creates the bar, with its More button.</summary>
    public AdaptiveBar()
    {
        _more = new AdaptiveBarItem { Icon = MoreIcon, Label = "More", ShowsLabel = false, Name = "MoreButton" };
        _more.Flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        _more.Flyout.Opening += (_, _) => FillMoreMenu();
        VisualChildren.Add(_more);
        LogicalChildren.Add(_more);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc cref="ItemThemeProperty"/>
    public ControlTheme? ItemTheme
    {
        get => GetValue(ItemThemeProperty);
        set => SetValue(ItemThemeProperty, value);
    }

    /// <summary>Where the More menu opens: below the bar for a toolbar, above it for a status bar.</summary>
    public PlacementMode MorePlacement
    {
        get => ((MenuFlyout)_more.Flyout!).Placement;
        set => ((MenuFlyout)_more.Flyout!).Placement = value;
    }

    /// <summary>The More button, shown when something overflows.</summary>
    public Button MoreButton => _more;

    /// <summary>The children in the More menu now.</summary>
    public IReadOnlyList<Control> Overflowed =>
        [.. _slotted.Where((child, index) => index < _modes.Length && _modes[index] == AdaptiveMode.Overflow && child is not AdaptiveBarSeparator)];

    /// <summary>How a child shows now, or <see langword="null"/> when it is hidden or not laid out yet.</summary>
    /// <param name="child">A child of the bar.</param>
    public AdaptiveMode? ModeOf(Control child)
    {
        var index = _slotted.IndexOf(child);
        return index >= 0 && index < _modes.Length ? _modes[index] : null;
    }

    /// <summary>Gets a child's importance.</summary>
    /// <param name="control">The child.</param>
    public static int GetPriority(Control control) => control.GetValue(PriorityProperty);

    /// <summary>Sets a child's importance.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">Its priority; lower leaves the bar first.</param>
    public static void SetPriority(Control control, int value) => control.SetValue(PriorityProperty, value);

    /// <summary>Gets whether a child always stays in the bar.</summary>
    /// <param name="control">The child.</param>
    public static bool GetIsPinned(Control control) => control.GetValue(IsPinnedProperty);

    /// <summary>Sets whether a child always stays in the bar.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">Whether it does.</param>
    public static void SetIsPinned(Control control, bool value) => control.SetValue(IsPinnedProperty, value);

    private static Geometry? MoreIcon =>
        Application.Current?.TryGetResource("Icon.More", null, out var icon) == true ? icon as Geometry : null;

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // Toolbar buttons unless the bar asks for another look, such as the status bar's.
        if (ItemTheme is null && Application.Current?.TryGetResource("ToolButton", null, out var theme) == true && theme is ControlTheme toolButton)
        {
            ItemTheme = toolButton;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemThemeProperty)
        {
            ApplyItemTheme(Children.Append(_more));
        }
    }

    /// <inheritdoc />
    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);

        // A reset clears the visual children; the More button stays the last one.
        if (!VisualChildren.Contains(_more))
        {
            VisualChildren.Add(_more);
            LogicalChildren.Add(_more);
        }
        else if (VisualChildren[^1] != _more)
        {
            VisualChildren.Remove(_more);
            VisualChildren.Add(_more);
        }

        if (e.NewItems is { } added)
        {
            ApplyItemTheme(added.OfType<Control>());
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
        _more.Measure(unbounded);
        _slotted = [.. Children.Where(child => child.IsVisible)];
        var height = _more.DesiredSize.Height;
        var slots = new List<AdaptiveSlot>(_slotted.Count);
        foreach (var child in _slotted)
        {
            child.Measure(unbounded);
            height = Math.Max(height, child.DesiredSize.Height);
            slots.Add(SlotOf(child));
        }

        _modes = AdaptiveLayout.Fit(slots, availableSize.Width, Spacing, _more.DesiredSize.Width);
        return new Size(AdaptiveLayout.Length(slots, _modes, Spacing, _more.DesiredSize.Width), height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var slots = _slotted.Select(SlotOf).ToList();
        _modes = AdaptiveLayout.Fit(slots, finalSize.Width, Spacing, _more.DesiredSize.Width);
        var x = 0.0;
        var overflows = false;
        for (var i = 0; i < _slotted.Count; i++)
        {
            var child = _slotted[i];
            if (_modes[i] == AdaptiveMode.Overflow)
            {
                overflows |= child is not AdaptiveBarSeparator;
                child.Arrange(default);
                continue;
            }

            var width = _modes[i] == AdaptiveMode.Compact ? slots[i].Compact : slots[i].Full;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }

        _more.Arrange(overflows ? new Rect(x, 0, _more.DesiredSize.Width, finalSize.Height) : default);
        return finalSize;
    }

    private static AdaptiveSlot SlotOf(Control child) => child switch
    {
        AdaptiveBarSeparator separator => new AdaptiveSlot(separator.DesiredSize.Width, separator.DesiredSize.Width, int.MaxValue, IsSeparator: true),
        AdaptiveBarItem item => new AdaptiveSlot(item.DesiredSize.Width, item.CanCompact ? Math.Min(item.DesiredSize.Width, item.CompactWidth) : item.DesiredSize.Width, item.Priority, item.IsPinned),
        _ => new AdaptiveSlot(child.DesiredSize.Width, child.DesiredSize.Width, GetPriority(child), GetIsPinned(child)),
    };

    private void ApplyItemTheme(IEnumerable<Control> controls)
    {
        if (ItemTheme is not { } theme)
        {
            return;
        }

        foreach (var item in controls.OfType<AdaptiveBarItem>().Where(item => item.Theme is null || item == _more))
        {
            item.Theme = theme;
        }
    }

    /// <summary>Lists what overflowed, with icons and key gestures, as menu items that run the same commands.</summary>
    private void FillMoreMenu()
    {
        var menu = (MenuFlyout)_more.Flyout!;
        menu.Items.Clear();
        foreach (var child in Overflowed)
        {
            if (child is not AdaptiveBarItem item)
            {
                continue;
            }

            var entry = new MenuItem
            {
                Header = item.Label,
                Command = item.Command,
                CommandParameter = item.CommandParameter,
                IsEnabled = item.IsEffectivelyEnabled,
                Icon = item.Icon is null ? null : new StrokeIcon { Data = item.Icon, Size = 16, IsFilled = item.IsIconFilled },
            };
            if (item.Gesture is { } gesture)
            {
                entry.InputGesture = TryParse(gesture);
            }

            menu.Items.Add(entry);
        }
    }

    private static KeyGesture? TryParse(string gesture)
    {
        try
        {
            return KeyGesture.Parse(gesture);
        }
        catch (ArgumentException)
        {
            // A chord such as "Ctrl+R, A" has no KeyGesture; the tooltip still names it.
            return null;
        }
    }
}

/// <summary>A thin line between groups of an <see cref="AdaptiveBar"/>, shown only between two items that stay in the bar.</summary>
public sealed class AdaptiveBarSeparator : Control
{
    /// <summary>The line's brush.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<AdaptiveBarSeparator, IBrush?>(nameof(Brush));

    static AdaptiveBarSeparator()
    {
        AffectsRender<AdaptiveBarSeparator>(BrushProperty);
    }

    /// <summary>Creates the separator.</summary>
    public AdaptiveBarSeparator()
    {
        Margin = new Thickness(5, 0);
        VerticalAlignment = VerticalAlignment.Center;
        this.Bind(BrushProperty, this.GetResourceObservable("Brush.BorderStrong"));
    }

    /// <inheritdoc cref="BrushProperty"/>
    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(1, 24);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Brush is { } brush)
        {
            context.FillRectangle(brush, new Rect(0, (Bounds.Height - 24) / 2, 1, 24));
        }
    }
}
