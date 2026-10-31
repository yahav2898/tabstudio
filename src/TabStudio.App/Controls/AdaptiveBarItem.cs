using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TabStudio.App.Accessibility;

namespace TabStudio.App.Controls;

/// <summary>
/// A command in an <see cref="AdaptiveBar"/>: an icon and a label that, when the bar runs out of room, shrinks to its
/// icon and then moves into the bar's More menu. Its tooltip names it with its key gesture; a long press on a touch
/// screen shows the tooltip instead of running the command.
/// </summary>
public class AdaptiveBarItem : Button
{
    /// <summary>The icon, a stroke geometry on the 24×24 grid of Icons.axaml.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, Geometry?>(nameof(Icon));

    /// <summary>The label, such as "Save All".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, string?>(nameof(Label));

    /// <summary>The command's key gesture as Visual Studio writes it, such as "Ctrl+Shift+S", shown in the tooltip and the More menu.</summary>
    public static readonly StyledProperty<string?> GestureProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, string?>(nameof(Gesture));

    /// <summary>How much the item matters: lower priorities shrink, and then leave the bar, first.</summary>
    public static readonly StyledProperty<int> PriorityProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, int>(nameof(Priority));

    /// <summary>Whether the item always stays in the bar.</summary>
    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, bool>(nameof(IsPinned));

    /// <summary>Whether the label shows when there is room; an item without it is always an icon.</summary>
    public static readonly StyledProperty<bool> ShowsLabelProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, bool>(nameof(ShowsLabel), true);

    /// <summary>The icon's size.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, double>(nameof(IconSize), 18);

    /// <summary>Whether the icon is drawn filled, as the Run triangle is.</summary>
    public static readonly StyledProperty<bool> IsIconFilledProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, bool>(nameof(IsIconFilled));

    /// <summary>The icon's color when it says something, as Stop's red does; the text's color when <see langword="null"/>.</summary>
    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<AdaptiveBarItem, IBrush?>(nameof(IconBrush));

    private const double BodyFontSize = 12.5;

    private readonly AdaptiveFace _face = new();
    private bool _heldOpen;

    static AdaptiveBarItem()
    {
        AffectsMeasure<AdaptiveBarItem>(IconProperty, LabelProperty, ShowsLabelProperty, IconSizeProperty);
    }

    /// <summary>Creates the item.</summary>
    public AdaptiveBarItem()
    {
        Content = _face;
        AddHandler(Gestures.HoldingEvent, OnHolding);
        UpdateFace();
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="GestureProperty"/>
    public string? Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    /// <inheritdoc cref="PriorityProperty"/>
    public int Priority
    {
        get => GetValue(PriorityProperty);
        set => SetValue(PriorityProperty, value);
    }

    /// <inheritdoc cref="IsPinnedProperty"/>
    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <inheritdoc cref="ShowsLabelProperty"/>
    public bool ShowsLabel
    {
        get => GetValue(ShowsLabelProperty);
        set => SetValue(ShowsLabelProperty, value);
    }

    /// <inheritdoc cref="IconSizeProperty"/>
    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <inheritdoc cref="IsIconFilledProperty"/>
    public bool IsIconFilled
    {
        get => GetValue(IsIconFilledProperty);
        set => SetValue(IsIconFilledProperty, value);
    }

    /// <inheritdoc cref="IconBrushProperty"/>
    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    /// <summary>Whether the item can shrink to its icon: it has an icon and a label it shows.</summary>
    public bool CanCompact => Icon is not null && ShowsLabel && !string.IsNullOrEmpty(Label);

    /// <summary>
    /// The item's width as an icon alone, after it was measured: never under its minimum width, which a theme such as
    /// ToolButton's sets, or folded buttons were laid out narrower than they draw and overlapped their neighbours.
    /// </summary>
    public double CompactWidth => Math.Max(MinWidth + Margin.Left + Margin.Right, DesiredSize.Width - (_face.FullWidth - _face.CompactWidth));

    /// <summary>What the tooltip and the More menu call the item: its label and key gesture.</summary>
    public string Title => string.IsNullOrEmpty(Gesture) ? Label ?? string.Empty : $"{Label} ({Gesture})";

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Button);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == LabelProperty || change.Property == GestureProperty
            || change.Property == ShowsLabelProperty || change.Property == IconSizeProperty || change.Property == IsIconFilledProperty
            || change.Property == IconBrushProperty)
        {
            UpdateFace();
        }
        else if (change.Property == FontSizeProperty)
        {
            // The label is body text unless the item or its theme sets a size, as the status bar's 12 does.
            if (IsSet(FontSizeProperty))
            {
                TypeScale.SetBaseFontSize(_face.Label, double.NaN);
                _face.Label.FontSize = FontSize;
            }
            else
            {
                TypeScale.SetBaseFontSize(_face.Label, BodyFontSize);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        // A long press showed the name; lifting the finger does not run the command too.
        if (_heldOpen)
        {
            _heldOpen = false;
            ToolTip.SetIsOpen(this, false);
            return;
        }

        base.OnClick();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_heldOpen)
        {
            _heldOpen = false;
            ToolTip.SetIsOpen(this, false);
        }
    }

    private void OnHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState == HoldingState.Started && !string.IsNullOrEmpty(Label))
        {
            _heldOpen = true;
            ToolTip.SetIsOpen(this, true);
            e.Handled = true;
        }
    }

    private void UpdateFace()
    {
        _face.Icon.Data = Icon;
        _face.Icon.Size = IconSize;
        _face.Icon.IsFilled = IsIconFilled;
        if (IconBrush is { } brush)
        {
            _face.Icon.Foreground = brush;
        }
        else
        {
            _face.Icon.ClearValue(StrokeIcon.ForegroundProperty);
        }

        _face.Icon.IsVisible = Icon is not null;
        _face.Label.Text = ShowsLabel ? Label : null;
        _face.Label.IsVisible = ShowsLabel && !string.IsNullOrEmpty(Label);
        ToolTip.SetTip(this, string.IsNullOrEmpty(Label) ? null : Title);
    }

    /// <summary>
    /// The icon and label of an item. It asks for room for both, and when it is given less, as the bar does to a
    /// compact item, it centers the icon and leaves the label out, so shrinking changes no property during layout.
    /// </summary>
    private sealed class AdaptiveFace : Panel
    {
        private const double Gap = 7;

        public AdaptiveFace()
        {
            Children.Add(Icon);
            Children.Add(Label);
            TypeScale.SetBaseFontSize(Label, BodyFontSize);
        }

        public StrokeIcon Icon { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

        public TextBlock Label { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

        public double FullWidth { get; private set; }

        public double CompactWidth { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
            Icon.Measure(unbounded);
            Label.Measure(unbounded);
            var icon = Icon.IsVisible ? Icon.DesiredSize : default;
            var label = Label.IsVisible ? Label.DesiredSize : default;
            FullWidth = icon.Width + label.Width + (icon.Width > 0 && label.Width > 0 ? Gap : 0);
            CompactWidth = icon.Width > 0 ? icon.Width : FullWidth;
            return new Size(FullWidth, Math.Max(icon.Height, label.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var compact = Icon.IsVisible && finalSize.Width + 0.5 < FullWidth;
            if (compact)
            {
                var icon = Icon.DesiredSize;
                Icon.Arrange(new Rect((finalSize.Width - icon.Width) / 2, (finalSize.Height - icon.Height) / 2, icon.Width, icon.Height));
                Label.Arrange(default);
                return finalSize;
            }

            var x = Math.Max(0, (finalSize.Width - FullWidth) / 2);
            if (Icon.IsVisible)
            {
                var icon = Icon.DesiredSize;
                Icon.Arrange(new Rect(x, (finalSize.Height - icon.Height) / 2, icon.Width, icon.Height));
                x += icon.Width + Gap;
            }

            var label = Label.DesiredSize;
            Label.Arrange(new Rect(x, (finalSize.Height - label.Height) / 2, label.Width, label.Height));
            return finalSize;
        }
    }
}
