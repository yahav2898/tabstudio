using System.Collections.Specialized;
using System.Windows.Input;
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
/// and <see cref="IsPinnedProperty"/>. An <see cref="AdaptiveBarSpacer"/> takes the room left, so the items after it sit
/// at the bar's end, as a status bar's caret position does.
/// </summary>
public sealed class AdaptiveBar : Panel
{
    /// <summary>The importance of a child that is not an <see cref="AdaptiveBarItem"/>.</summary>
    public static readonly AttachedProperty<int> PriorityProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, int>("Priority");

    /// <summary>Whether a child that is not an <see cref="AdaptiveBarItem"/> always stays in the bar; such children are pinned unless they say otherwise.</summary>
    public static readonly AttachedProperty<bool> IsPinnedProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, bool>("IsPinned", true);

    /// <summary>
    /// What the More menu calls a child: an <see cref="AdaptiveBarItem"/>'s label by default, and for any other child,
    /// such as a status bar item of its own kind, the only way into the menu; a child without one is left out of it.
    /// </summary>
    public static readonly AttachedProperty<string?> MenuHeaderProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, string?>("MenuHeader");

    /// <summary>What the More menu runs for a child that is not an <see cref="AdaptiveBarItem"/>.</summary>
    public static readonly AttachedProperty<ICommand?> MenuCommandProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, ICommand?>("MenuCommand");

    /// <summary>The More menu's icon for a child that is not an <see cref="AdaptiveBarItem"/>.</summary>
    public static readonly AttachedProperty<Geometry?> MenuIconProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, Geometry?>("MenuIcon");

    /// <summary>
    /// The shortest a child that is not an <see cref="AdaptiveBarItem"/> may become, for one whose text trims, such as
    /// the status bar's message: it stays in the bar, shrinks before the items after it are pushed out of the bar, and
    /// takes the room they leave, up to its full length. Unset (<see cref="double.NaN"/>), a child keeps its length.
    /// </summary>
    public static readonly AttachedProperty<double> MinLengthProperty =
        AvaloniaProperty.RegisterAttached<AdaptiveBar, Control, double>("MinLength", double.NaN);

    /// <summary>The space between two items.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AdaptiveBar, double>(nameof(Spacing), 2);

    /// <summary>The look of the items and the More button, such as <c>ToolButton</c> or <c>StatusBarButton</c>.</summary>
    public static readonly StyledProperty<ControlTheme?> ItemThemeProperty =
        AvaloniaProperty.Register<AdaptiveBar, ControlTheme?>(nameof(ItemTheme));

    private readonly Button _more;
    private AdaptiveMode[] _modes = [];
    private List<Control> _slotted = [];

    // The children's lengths as they measured with all the room they want, before a trimming child was measured
    // again at the length it gets.
    private List<AdaptiveSlot> _slots = [];

    static AdaptiveBar()
    {
        AffectsMeasure<AdaptiveBar>(SpacingProperty);
        AffectsParentMeasure<AdaptiveBar>(PriorityProperty, IsPinnedProperty, MinLengthProperty);
    }

    /// <summary>Creates the bar, with its More button.</summary>
    public AdaptiveBar()
    {
        _more = new AdaptiveBarItem { Icon = MoreIcon, IsIconFilled = true, Label = "More", ShowsLabel = false, Name = "MoreButton" };
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        menu.Opening += (_, _) => FillMoreMenu();
        _more.Flyout = menu;
        VisualChildren.Add(_more);
        LogicalChildren.Add(_more);

        // Items in More are parked outside the bar, which must not draw them.
        ClipToBounds = true;
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

    /// <summary>Whether the More button shows, because something did not fit.</summary>
    public bool ShowsMore { get; private set; }

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

    /// <summary>Gets the shortest a trimming child may become.</summary>
    /// <param name="control">The child.</param>
    public static double GetMinLength(Control control) => control.GetValue(MinLengthProperty);

    /// <summary>Sets the shortest a trimming child may become.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">Its shortest length, such as its icon and a few letters.</param>
    public static void SetMinLength(Control control, double value) => control.SetValue(MinLengthProperty, value);

    /// <summary>Gets what the More menu calls a child.</summary>
    /// <param name="control">The child.</param>
    public static string? GetMenuHeader(Control control) => control.GetValue(MenuHeaderProperty);

    /// <summary>Sets what the More menu calls a child.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">The header, such as "CI: 1 failed".</param>
    public static void SetMenuHeader(Control control, string? value) => control.SetValue(MenuHeaderProperty, value);

    /// <summary>Gets what the More menu runs for a child.</summary>
    /// <param name="control">The child.</param>
    public static ICommand? GetMenuCommand(Control control) => control.GetValue(MenuCommandProperty);

    /// <summary>Sets what the More menu runs for a child.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">The command.</param>
    public static void SetMenuCommand(Control control, ICommand? value) => control.SetValue(MenuCommandProperty, value);

    /// <summary>Gets the More menu's icon for a child.</summary>
    /// <param name="control">The child.</param>
    public static Geometry? GetMenuIcon(Control control) => control.GetValue(MenuIconProperty);

    /// <summary>Sets the More menu's icon for a child.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">The icon.</param>
    public static void SetMenuIcon(Control control, Geometry? value) => control.SetValue(MenuIconProperty, value);

    private static Geometry? MoreIcon =>
        Application.Current?.TryGetResource("Icon.Overflow", null, out var icon) == true ? icon as Geometry : null;

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

        _slots = slots;
        _modes = AdaptiveLayout.Fit(slots, availableSize.Width, Spacing, _more.DesiredSize.Width);
        var length = AdaptiveLayout.Length(slots, _modes, Spacing, _more.DesiredSize.Width);
        if (!double.IsInfinity(availableSize.Width))
        {
            // A trimming child measures again at the length it gets, so its text trims rather than overflowing it.
            var trimmed = TrimmedLengths(slots, _modes, Math.Max(0, availableSize.Width - length));
            for (var i = 0; i < _slotted.Count; i++)
            {
                if (trimmed[i] is { } trim)
                {
                    _slotted[i].Measure(new Size(trim, availableSize.Height));
                    length += trim - slots[i].Compact;
                }
            }
        }

        return new Size(length, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var slots = _slots.Count == _slotted.Count ? _slots : _slotted.Select(SlotOf).ToList();
        _modes = AdaptiveLayout.Fit(slots, finalSize.Width, Spacing, _more.DesiredSize.Width);
        var overflows = _slotted.Where((child, i) => _modes[i] == AdaptiveMode.Overflow && child is not AdaptiveBarSeparator).Any();
        var spacers = _slotted.Count(child => child is AdaptiveBarSpacer);
        var room = Math.Max(0, finalSize.Width - AdaptiveLayout.Length(slots, _modes, Spacing, _more.DesiredSize.Width));
        var trimmed = TrimmedLengths(slots, _modes, room);
        room = Math.Max(0, room - trimmed.Select((trim, i) => trim is { } length ? length - slots[i].Compact : 0).Sum());
        var x = 0.0;
        for (var i = 0; i < _slotted.Count; i++)
        {
            var child = _slotted[i];
            if (_modes[i] == AdaptiveMode.Overflow)
            {
                child.Arrange(AdaptiveLayout.Parked);
                continue;
            }

            var width = child is AdaptiveBarSpacer ? room / spacers
                : trimmed[i] ?? (_modes[i] == AdaptiveMode.Compact ? slots[i].Compact : slots[i].Full);
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }

        ShowsMore = overflows;
        _more.Arrange(overflows ? new Rect(x, 0, _more.DesiredSize.Width, finalSize.Height) : AdaptiveLayout.Parked);
        return finalSize;
    }

    private static AdaptiveSlot SlotOf(Control child) => child switch
    {
        AdaptiveBarSeparator separator => new AdaptiveSlot(separator.DesiredSize.Width, separator.DesiredSize.Width, int.MaxValue, IsSeparator: true),
        AdaptiveBarSpacer => new AdaptiveSlot(0, 0, int.MaxValue, IsPinned: true),
        AdaptiveBarItem item => new AdaptiveSlot(item.DesiredSize.Width, item.CanCompact ? Math.Min(item.DesiredSize.Width, item.CompactWidth) : item.DesiredSize.Width, item.Priority, item.IsPinned),
        _ when Trims(child) => new AdaptiveSlot(child.DesiredSize.Width, Math.Min(child.DesiredSize.Width, GetMinLength(child)), GetPriority(child), GetIsPinned(child)),
        _ => new AdaptiveSlot(child.DesiredSize.Width, child.DesiredSize.Width, GetPriority(child), GetIsPinned(child)),
    };

    private static bool Trims(Control child) => child is not AdaptiveBarItem && !double.IsNaN(GetMinLength(child));

    /// <summary>
    /// The length of each trimming child left at its shortest: that, and as much of the room the bar has left as it
    /// still wants, in the bar's order; <see langword="null"/> for every other child.
    /// </summary>
    private double?[] TrimmedLengths(List<AdaptiveSlot> slots, AdaptiveMode[] modes, double room)
    {
        var lengths = new double?[slots.Count];
        for (var i = 0; i < slots.Count && i < _slotted.Count; i++)
        {
            if (modes[i] == AdaptiveMode.Compact && Trims(_slotted[i]))
            {
                var extra = Math.Min(room, slots[i].Full - slots[i].Compact);
                lengths[i] = slots[i].Compact + extra;
                room -= extra;
            }
        }

        return lengths;
    }

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
                // A child of its own kind is in the menu when it says what it is called there.
                if (GetMenuHeader(child) is { Length: > 0 } header)
                {
                    menu.Items.Add(new MenuItem
                    {
                        Header = header,
                        Command = GetMenuCommand(child),
                        Icon = GetMenuIcon(child) is { } icon ? new StrokeIcon { Data = icon, Size = 16 } : null,
                    });
                }

                continue;
            }

            var entry = new MenuItem
            {
                Header = GetMenuHeader(item) ?? item.Label,
                Command = item.Command,
                CommandParameter = item.CommandParameter,
                IsEnabled = item.IsEffectivelyEnabled,
                Icon = item.Icon is null ? null : MenuIcon(item),
            };
            if (item.Gesture is { } gesture)
            {
                entry.InputGesture = TryParse(gesture);
            }

            // An item that opens a menu, such as the configuration, becomes a submenu.
            if (item.Flyout is MenuFlyout { Items.Count: > 0 } flyout)
            {
                AddChoices(entry, flyout, item);
            }

            menu.Items.Add(entry);
        }
    }

    /// <summary>An item's icon for its entry in the More menu, in the item's icon color when it has one.</summary>
    private static StrokeIcon MenuIcon(AdaptiveBarItem item)
    {
        var icon = new StrokeIcon { Data = item.Icon, Size = 16, IsFilled = item.IsIconFilled };
        if (item.IconBrush is { } brush)
        {
            icon.Foreground = brush;
        }

        return icon;
    }

    /// <summary>Copies the choices of an item's menu into its entry in the More menu, as they are now.</summary>
    private static void AddChoices(MenuItem entry, MenuFlyout flyout, AdaptiveBarItem item)
    {
        foreach (var choice in flyout.Items)
        {
            switch (choice)
            {
                case MenuItem menuItem:
                    // A menu that never opened has not given its items the item's data context, so their bindings
                    // have nothing to read yet.
                    menuItem.DataContext ??= item.DataContext;
                    if (menuItem.IsVisible)
                    {
                        entry.Items.Add(new MenuItem
                        {
                            Header = menuItem.Header,
                            Command = menuItem.Command,
                            CommandParameter = menuItem.CommandParameter,
                            InputGesture = menuItem.InputGesture,
                        });
                    }

                    break;
                case Separator when entry.Items.Count > 0 && entry.Items[^1] is not Separator:
                    entry.Items.Add(new Separator());
                    break;
            }
        }

        if (entry.Items.Count > 0 && entry.Items[^1] is Separator)
        {
            entry.Items.RemoveAt(entry.Items.Count - 1);
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

/// <summary>
/// Empty space in an <see cref="AdaptiveBar"/> that takes the room the items leave, so the items after it sit at the
/// bar's end.
/// </summary>
public sealed class AdaptiveBarSpacer : Control
{
    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => default;
}
