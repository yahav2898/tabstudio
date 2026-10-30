using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using TabStudio.App.ViewModels.Shell;

namespace TabStudio.App.Controls;

/// <summary>Which side of the activity bar faces the screen's edge, where the selection indicator is drawn.</summary>
public enum ActivityBarEdge
{
    /// <summary>The bar is on the screen's left edge.</summary>
    Left,

    /// <summary>The bar is on the screen's right edge, beside a side bar on the right, as TabStudio's is.</summary>
    Right,
}

/// <summary>
/// The activity bar (<see cref="ActivityBarViewModel"/>): a strip of 48-pixel icon buttons, one per tool window, with
/// the shown one marked by a bar on the screen's edge, badges for counts, states and work in progress, tooltips that
/// name each window with its key gesture, and a long press that shows the name on a touch screen. Items that do not fit
/// the height move into a More menu at the end of the top group; the selected one always stays.
/// </summary>
public sealed class ActivityBar : Control
{
    /// <summary>The bar's items.</summary>
    public static readonly StyledProperty<ActivityBarViewModel?> BarProperty =
        AvaloniaProperty.Register<ActivityBar, ActivityBarViewModel?>(nameof(Bar));

    /// <summary>Which side faces the screen's edge.</summary>
    public static readonly StyledProperty<ActivityBarEdge> EdgeProperty =
        AvaloniaProperty.Register<ActivityBar, ActivityBarEdge>(nameof(Edge), ActivityBarEdge.Right);

    /// <summary>The bar's width and each button's height.</summary>
    public const double ItemSize = 48;

    private readonly List<ActivityButton> _buttons = [];
    private readonly ActivityButton _more;
    private AdaptiveMode[] _modes = [];

    /// <summary>Creates the bar.</summary>
    public ActivityBar()
    {
        _more = new ActivityButton(null) { Name = "ActivityMore" };
        _more.SetIcon(Resource<Geometry>("Icon.Overflow"), filled: true);
        ToolTip.SetTip(_more, "More tool windows");
        var menu = new MenuFlyout { Placement = PlacementMode.LeftEdgeAlignedBottom };
        menu.Opening += (_, _) => FillMoreMenu();
        _more.Flyout = menu;
        AddChild(_more);
        ClipToBounds = true;
    }

    /// <inheritdoc cref="BarProperty"/>
    public ActivityBarViewModel? Bar
    {
        get => GetValue(BarProperty);
        set => SetValue(BarProperty, value);
    }

    /// <inheritdoc cref="EdgeProperty"/>
    public ActivityBarEdge Edge
    {
        get => GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    /// <summary>The items that moved into the More menu.</summary>
    public IReadOnlyList<ActivityItem> Overflowed =>
        [.. TopButtons().Where((button, index) => index < _modes.Length && _modes[index] == AdaptiveMode.Overflow).Select(button => button.Item!)];

    /// <summary>The More button, shown when items overflow.</summary>
    public Button MoreButton => _more;

    /// <summary>Whether the More button shows, because items did not fit.</summary>
    public bool ShowsMore { get; private set; }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BarProperty)
        {
            if (change.OldValue is ActivityBarViewModel old)
            {
                old.Items.CollectionChanged -= OnItemsChanged;
            }

            if (change.NewValue is ActivityBarViewModel bar)
            {
                bar.Items.CollectionChanged += OnItemsChanged;
            }

            Rebuild();
        }
        else if (change.Property == EdgeProperty)
        {
            foreach (var button in _buttons.Append(_more))
            {
                button.Edge = Edge;
            }
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var item = new Size(ItemSize, ItemSize);
        foreach (var button in _buttons.Append(_more))
        {
            button.Measure(item);
        }

        var top = TopButtons().ToList();
        var bottom = BottomButtons().Count();
        var room = availableSize.Height - (bottom * ItemSize);
        var slots = top.Select((button, index) => new AdaptiveSlot(ItemSize, ItemSize, -index, IsPinned: button.Item!.IsSelected)).ToList();
        _modes = AdaptiveLayout.Fit(slots, room, 0, ItemSize);
        var height = double.IsInfinity(availableSize.Height)
            ? (top.Count + bottom) * ItemSize
            : availableSize.Height;
        return new Size(ItemSize, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = TopButtons().ToList();
        var bottom = BottomButtons().ToList();
        var slots = top.Select((button, index) => new AdaptiveSlot(ItemSize, ItemSize, -index, IsPinned: button.Item!.IsSelected)).ToList();
        _modes = AdaptiveLayout.Fit(slots, finalSize.Height - (bottom.Count * ItemSize), 0, ItemSize);

        var y = 0.0;
        var overflows = false;
        for (var i = 0; i < top.Count; i++)
        {
            if (_modes[i] == AdaptiveMode.Overflow)
            {
                overflows = true;
                top[i].Arrange(AdaptiveLayout.Parked);
                continue;
            }

            top[i].Arrange(new Rect(0, y, ItemSize, ItemSize));
            y += ItemSize;
        }

        ShowsMore = overflows;
        _more.Arrange(overflows ? new Rect(0, y, ItemSize, ItemSize) : AdaptiveLayout.Parked);

        var bottomY = finalSize.Height - (bottom.Count * ItemSize);
        foreach (var button in bottom)
        {
            button.Arrange(new Rect(0, bottomY, ItemSize, ItemSize));
            bottomY += ItemSize;
        }

        foreach (var hidden in _buttons.Where(button => !button.Item!.IsAvailable))
        {
            hidden.Arrange(AdaptiveLayout.Parked);
        }

        return finalSize;
    }

    private static T? Resource<T>(string key)
        where T : class =>
        Application.Current?.TryGetResource(key, null, out var value) == true ? value as T : null;

    private IEnumerable<ActivityButton> TopButtons() => _buttons.Where(button => button.Item!.IsAvailable && !button.Item.IsAtBottom);

    private IEnumerable<ActivityButton> BottomButtons() => _buttons.Where(button => button.Item!.IsAvailable && button.Item.IsAtBottom);

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ActivityItem.IsAvailable) or nameof(ActivityItem.IsAtBottom) or nameof(ActivityItem.IsSelected))
        {
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    private void Rebuild()
    {
        foreach (var button in _buttons)
        {
            button.Item!.PropertyChanged -= OnItemChanged;
            button.Detach();
            VisualChildren.Remove(button);
            LogicalChildren.Remove(button);
        }

        _buttons.Clear();
        if (Bar is not { } bar)
        {
            InvalidateMeasure();
            return;
        }

        foreach (var item in bar.Items)
        {
            var button = new ActivityButton(item) { Edge = Edge, Command = bar.ActivateCommand, CommandParameter = item };
            item.PropertyChanged += OnItemChanged;
            _buttons.Add(button);
            AddChild(button);
        }

        InvalidateMeasure();
    }

    private void AddChild(Control child)
    {
        VisualChildren.Add(child);
        LogicalChildren.Add(child);
    }

    private void FillMoreMenu()
    {
        var menu = (MenuFlyout)_more.Flyout!;
        menu.Items.Clear();
        foreach (var item in Overflowed)
        {
            menu.Items.Add(new MenuItem
            {
                Header = item.BadgeDescription is { Length: > 0 } badge ? $"{item.Title} ({badge})" : item.Title,
                Icon = new StrokeIcon { Data = item.Icon, Size = 16 },
                Command = Bar?.ActivateCommand,
                CommandParameter = item,
                InputGesture = Parse(item.Gesture),
            });
        }
    }

    private static KeyGesture? Parse(string? gesture)
    {
        if (gesture is null)
        {
            return null;
        }

        try
        {
            return KeyGesture.Parse(gesture);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// One item of the bar: its icon, the selection mark on the screen's edge, and its badge. A long press shows the
    /// item's name instead of opening it.
    /// </summary>
    private sealed class ActivityButton : Button
    {
        private readonly StrokeIcon _icon = new() { Size = 22, StrokeThickness = 1.8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        private readonly Border _indicator = new() { Width = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 10), IsVisible = false };
        private readonly Border _count = new() { MinWidth = 18, MinHeight = 18, CornerRadius = new CornerRadius(9), Padding = new Thickness(5, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(25, 6, 0, 0), IsVisible = false };
        private readonly TextBlock _countText = new() { FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        private readonly Border _dot = new() { Width = 11, Height = 11, CornerRadius = new CornerRadius(5.5), BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(29, 10, 0, 0), IsVisible = false };
        private readonly StrokeIcon _activity = new() { Size = 12, StrokeThickness = 2.6, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(29, 27, 0, 0), IsVisible = false };
        private bool _heldOpen;

        public ActivityButton(ActivityItem? item)
        {
            Item = item;
            _count.Child = _countText;
            TabStudio.App.Accessibility.TypeScale.SetBaseFontSize(_countText, 10.5);
            _activity.Classes.Add("spinning");
            _activity.Data = Resource<Geometry>("Icon.Spinner");
            Content = new Panel { Children = { _indicator, _icon, _count, _dot, _activity } };
            Width = ItemSize;
            Height = ItemSize;
            Padding = default;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            ToolTip.SetPlacement(this, PlacementMode.Left);
            ToolTip.SetShowDelay(this, 250);
            AddHandler(Gestures.HoldingEvent, OnHolding);
            BindToken(_indicator, Border.BackgroundProperty, "Brush.ActivityIndicator");
            BindToken(_dot, Border.BorderBrushProperty, "Brush.ActivityBar");
            if (item is not null)
            {
                item.PropertyChanged += OnItemChanged;
                Update();
            }
        }

        public ActivityItem? Item { get; }

        public ActivityBarEdge Edge
        {
            get => _indicator.HorizontalAlignment == HorizontalAlignment.Left ? ActivityBarEdge.Left : ActivityBarEdge.Right;
            set => _indicator.HorizontalAlignment = value == ActivityBarEdge.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }

        protected override Type StyleKeyOverride => typeof(Button);

        public void SetIcon(Geometry? icon, bool filled = false)
        {
            _icon.Data = icon;
            _icon.IsFilled = filled;
        }

        public void Detach()
        {
            if (Item is not null)
            {
                Item.PropertyChanged -= OnItemChanged;
            }
        }

        protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);
            if (Theme is null && Resource<ControlTheme>("ActivityButton") is { } theme)
            {
                Theme = theme;
            }
        }

        protected override void OnClick()
        {
            if (_heldOpen)
            {
                _heldOpen = false;
                ToolTip.SetIsOpen(this, false);
                return;
            }

            base.OnClick();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            if (_heldOpen)
            {
                _heldOpen = false;
                ToolTip.SetIsOpen(this, false);
            }
        }

        /// <summary>Gives a property a palette token, following the theme.</summary>
        private static void BindToken(StyledElement target, AvaloniaProperty property, string token) =>
            target.Bind(property, target.GetResourceObservable(token));

        private void OnHolding(object? sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                _heldOpen = true;
                ToolTip.SetIsOpen(this, true);
                e.Handled = true;
            }
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => Update();

        private void Update()
        {
            if (Item is not { } item)
            {
                return;
            }

            _icon.Data = item.Icon;
            _indicator.IsVisible = item.IsSelected;
            Classes.Set("selected", item.IsSelected);
            ToolTip.SetTip(this, item.ToolTip);
            AutomationProperties.SetName(this, item.ToolTip);

            _count.IsVisible = item.BadgeKind == ActivityBadgeKind.Count;
            _countText.Text = item.BadgeText;
            _dot.IsVisible = item.BadgeKind == ActivityBadgeKind.Dot;
            _activity.IsVisible = item.BadgeKind == ActivityBadgeKind.Activity;
            var (background, foreground) = item.BadgeTone switch
            {
                ActivityTone.Error => ("Brush.Badge.Count", "Brush.OnAccent"),
                ActivityTone.Accent => ("Brush.Accent", "Brush.OnAccent"),
                ActivityTone.Success => ("Brush.Success", "Brush.Window"),
                ActivityTone.Warning => ("Brush.Warning", "Brush.Window"),
                ActivityTone.Running => ("Brush.Running", "Brush.Window"),
                _ => ("Brush.TextMuted", "Brush.Window"),
            };
            BindToken(_count, Border.BackgroundProperty, background);
            BindToken(_countText, TextBlock.ForegroundProperty, foreground);
            BindToken(_dot, Border.BackgroundProperty, background);
            BindToken(_activity, StrokeIcon.ForegroundProperty, item.BadgeTone == ActivityTone.Running ? "Brush.AccentText" : background);
        }
    }
}
