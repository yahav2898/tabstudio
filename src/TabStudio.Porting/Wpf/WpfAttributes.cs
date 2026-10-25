using System.Collections.Frozen;

namespace TabStudio.Porting.Wpf;

/// <summary>WPF's XAML properties that Avalonia names differently, does not need, or does not have.</summary>
internal static class WpfAttributes
{
    /// <summary>Properties Avalonia has under another name (attached properties mostly).</summary>
    public static readonly FrozenDictionary<string, string> Renamed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ToolTip"] = "ToolTip.Tip",
        ["ToolTipService.ToolTip"] = "ToolTip.Tip",
        ["ToolTipService.Placement"] = "ToolTip.Placement",
        ["ToolTipService.HorizontalOffset"] = "ToolTip.HorizontalOffset",
        ["ToolTipService.VerticalOffset"] = "ToolTip.VerticalOffset",
        ["ToolTipService.InitialShowDelay"] = "ToolTip.ShowDelay",
        ["ToolTipService.BetweenShowDelay"] = "ToolTip.BetweenShowDelay",
        ["ToolTipService.ShowOnDisabled"] = "ToolTip.ShowOnDisabled",
        ["ToolTipService.IsEnabled"] = "ToolTip.ServiceEnabled",
        ["Panel.ZIndex"] = "ZIndex",
        ["RenderOptions.BitmapScalingMode"] = "RenderOptions.BitmapInterpolationMode",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Properties that only tune WPF's own rendering or virtualization; Avalonia needs nothing for them.</summary>
    public static readonly FrozenSet<string> Dropped = FrozenSet.Create(StringComparer.Ordinal,
    [
        "SnapsToDevicePixels", "FocusVisualStyle", "TextOptions.TextFormattingMode", "TextOptions.TextRenderingMode",
        "TextOptions.TextHintingMode", "RenderOptions.ClearTypeHint", "RenderOptions.EdgeMode", "RenderOptions.CachingHint",
        "ScrollViewer.CanContentScroll", "ScrollViewer.PanningMode", "ScrollViewer.IsDeferredScrollingEnabled",
        "VirtualizingStackPanel.IsVirtualizing", "VirtualizingStackPanel.VirtualizationMode", "VirtualizingPanel.IsVirtualizing",
        "VirtualizingPanel.VirtualizationMode", "VirtualizingPanel.ScrollUnit", "VirtualizingPanel.IsVirtualizingWhenGrouping",
        "VirtualizingPanel.CacheLength", "VirtualizingPanel.CacheLengthUnit", "EnableRowVirtualization", "EnableColumnVirtualization",
        "SpellCheck.IsEnabled", "IsSynchronizedWithCurrentItem", "KeyboardNavigation.DirectionalNavigation",
        "KeyboardNavigation.ControlTabNavigation", "Stylus.IsPressAndHoldEnabled", "Stylus.IsFlicksEnabled",
        "Stylus.IsTapFeedbackEnabled", "Stylus.IsTouchFeedbackEnabled", "AutoToolTipPlacement", "AutoToolTipPrecision",
        "StaysOpenOnEdit", "ForceCursor", "IsManipulationEnabled", "ToolTipService.ShowDuration", "PopupAnimation",
        "AllowsTransparency", "MinRowHeight", "IsReadOnlyCaretVisible", "ShowsPreview",
        "Typography.Capitals", "Typography.NumeralStyle", "IsTextSearchEnabled", "TextSearch.TextPath", "x:Uid",
    ]);

    /// <summary>Properties with nothing in Avalonia that maps exactly, and what to do instead.</summary>
    public static readonly FrozenDictionary<string, string> Unsupported = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["OverridesDefaultStyle"] = "Avalonia has no OverridesDefaultStyle; give the control a ControlTheme of its own, or Theme=\"{x:Null}\" and a Template",
        ["CharacterCasing"] = "Avalonia's TextBox has no CharacterCasing; change the case in the view model's property setter",
        ["FocusManager.FocusedElement"] = "Avalonia has no FocusManager.FocusedElement; call Focus() on the element in the view's Loaded event",
        ["Validation.ErrorTemplate"] = "Avalonia shows validation errors with DataValidationErrors; restyle that control to change how errors look",
        ["WindowStyle"] = "Avalonia windows set their frame with SystemDecorations and ExtendClientAreaToDecorationsHint, on the desktop only",
        ["Left"] = "Avalonia windows are placed with Position, on the desktop only",
        ["Top"] = "Avalonia windows are placed with Position, on the desktop only",
        ["AlternationCount"] = "Avalonia has no AlternationCount; style alternating rows with :nth-child(2n) selectors",
        ["SelectionUnit"] = "Avalonia's DataGrid selects whole rows",
        ["ItemTemplateSelector"] = "Avalonia has no template selectors; give each item type its own DataTemplate with a DataType",
        ["ContentTemplateSelector"] = "Avalonia has no template selectors; give each content type its own DataTemplate with a DataType",
        ["CellTemplateSelector"] = "Avalonia has no template selectors; give each item type its own DataTemplate with a DataType",
        ["x:Shared"] = "Avalonia's resources are always shared; create a new object in code where one per use is needed",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>WPF's cursor names to Avalonia's.</summary>
    public static readonly FrozenDictionary<string, string> Cursors = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Arrow"] = "Arrow",
        ["Hand"] = "Hand",
        ["IBeam"] = "Ibeam",
        ["Wait"] = "Wait",
        ["AppStarting"] = "AppStarting",
        ["Cross"] = "Cross",
        ["Help"] = "Help",
        ["No"] = "No",
        ["SizeAll"] = "SizeAll",
        ["SizeNS"] = "SizeNorthSouth",
        ["SizeWE"] = "SizeWestEast",
        ["SizeNWSE"] = "TopLeftCorner",
        ["SizeNESW"] = "TopRightCorner",
        ["UpArrow"] = "UpArrow",
        ["None"] = "None",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Property triggers that are pseudo-classes in Avalonia: property and value to the pseudo-class.</summary>
    public static readonly FrozenDictionary<string, string> PseudoClasses = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["IsMouseOver=True"] = ":pointerover",
        ["IsMouseOver=False"] = ":not(:pointerover)",
        ["IsPressed=True"] = ":pressed",
        ["IsPressed=False"] = ":not(:pressed)",
        ["IsEnabled=False"] = ":disabled",
        ["IsEnabled=True"] = ":not(:disabled)",
        ["IsFocused=True"] = ":focus",
        ["IsFocused=False"] = ":not(:focus)",
        ["IsKeyboardFocused=True"] = ":focus-visible",
        ["IsKeyboardFocusWithin=True"] = ":focus-within",
        ["IsChecked=True"] = ":checked",
        ["IsChecked=False"] = ":unchecked",
        ["IsChecked={x:Null}"] = ":indeterminate",
        ["IsSelected=True"] = ":selected",
        ["IsSelected=False"] = ":not(:selected)",
        ["IsExpanded=True"] = ":expanded",
        ["IsExpanded=False"] = ":not(:expanded)",
        ["Validation.HasError=True"] = ":error",
        ["IsDropDownOpen=True"] = ":dropdownopen",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Controls whose Fluent theme sets the background, border and text color of a template part in its pointer-over,
    /// pressed and disabled states, so a style's setters for those states target that part, as Avalonia's guide shows.
    /// </summary>
    public static readonly FrozenDictionary<string, string> StatePart = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Button"] = "ContentPresenter#PART_ContentPresenter",
        ["ToggleButton"] = "ContentPresenter#PART_ContentPresenter",
        ["RepeatButton"] = "ContentPresenter#PART_ContentPresenter",
        ["ListBoxItem"] = "ContentPresenter#PART_ContentPresenter",
        ["ComboBoxItem"] = "ContentPresenter#PART_ContentPresenter",
        ["TextBox"] = "Border#PART_BorderElement",
    }.ToFrozenDictionary(StringComparer.Ordinal);
}
