using System.Collections.Frozen;

namespace TabStudio.Porting.Wpf;

/// <summary>
/// WPF's XAML types and what they are in Avalonia, after Avalonia's WPF migration guide: the same type, a type of
/// another name, or nothing that maps exactly.
/// </summary>
internal static class WpfElements
{
    /// <summary>Types Avalonia's XAML has under the same name, with the same meaning.</summary>
    public static readonly FrozenSet<string> SameInAvalonia = FrozenSet.Create(StringComparer.Ordinal,
    [
        // Controls and panels.
        "AccessText", "Border", "Button", "Calendar", "Canvas", "CheckBox", "ComboBox", "ComboBoxItem", "ContentControl",
        "ContentPresenter", "ContextMenu", "Decorator", "DockPanel", "Ellipse", "Expander", "Grid", "GridSplitter",
        "HeaderedContentControl", "HeaderedItemsControl", "Image", "ItemsControl", "ItemsPresenter", "Label", "Line", "ListBox",
        "ListBoxItem", "Menu", "MenuItem", "Panel", "Path", "Polygon", "Polyline", "Popup", "ProgressBar", "RadioButton",
        "Rectangle", "RepeatButton", "ScrollBar", "ScrollContentPresenter", "ScrollViewer", "Separator", "Slider",
        "StackPanel", "TabControl", "TabItem", "TextBlock", "TextBox", "Thumb", "TickBar", "ToggleButton", "ToolTip",
        "Track", "TreeView", "TreeViewItem", "UniformGrid", "UserControl", "Viewbox", "VirtualizingStackPanel", "WrapPanel",
        "ColumnDefinition", "RowDefinition", "Application",

        // The data grid, from the Avalonia.Controls.DataGrid package.
        "DataGrid", "DataGridTextColumn", "DataGridCheckBoxColumn", "DataGridTemplateColumn", "DataGridRow", "DataGridCell",
        "DataGridColumnHeader", "DataGridRowHeader",

        // Styles, templates and resources.
        "Style", "Setter", "ControlTemplate", "DataTemplate", "ItemsPanelTemplate", "ResourceDictionary",

        // Brushes, effects, transforms, geometry and drawings.
        "SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush", "GradientStop", "ImageBrush", "VisualBrush",
        "DrawingBrush", "RotateTransform", "ScaleTransform", "TranslateTransform", "SkewTransform", "TransformGroup",
        "MatrixTransform", "DropShadowEffect", "BlurEffect", "PathGeometry", "PathFigure", "LineSegment", "ArcSegment",
        "BezierSegment", "QuadraticBezierSegment", "PolyLineSegment", "PolyBezierSegment", "EllipseGeometry",
        "RectangleGeometry", "LineGeometry", "GeometryGroup", "StreamGeometry", "DrawingImage", "DrawingGroup",
        "GeometryDrawing", "ImageDrawing", "Pen", "DashStyle", "FontFamily", "Color", "Thickness", "CornerRadius",

        // Text inside a TextBlock.
        "Run", "Bold", "Italic", "Underline", "LineBreak", "Span", "InlineUIContainer",

        // Bindings and input.
        "Binding", "MultiBinding", "KeyBinding",
    ]);

    /// <summary>Types Avalonia names differently: WPF's name to Avalonia's.</summary>
    public static readonly FrozenDictionary<string, string> Renamed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DatePicker"] = "CalendarDatePicker",
        ["HierarchicalDataTemplate"] = "TreeDataTemplate",
        ["ListViewItem"] = "ListBoxItem",
        ["StatusBarItem"] = "ContentControl",
        ["AdornerDecorator"] = "Decorator",
        ["ToolBarPanel"] = "StackPanel",
        ["VirtualizingPanel"] = "VirtualizingStackPanel",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Types with nothing in Avalonia that maps exactly, and what to use instead.</summary>
    public static readonly FrozenDictionary<string, string> Unsupported = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["RichTextBox"] = "Avalonia has no rich text editor. Use a TextBox with AcceptsReturn, or a third-party editor such as AvaloniaEdit",
        ["FlowDocument"] = "Avalonia has no flow documents. Show the text with TextBlocks and inlines",
        ["FlowDocumentScrollViewer"] = "Avalonia has no flow documents. Show the text with TextBlocks in a ScrollViewer",
        ["FlowDocumentReader"] = "Avalonia has no flow documents. Show the text with TextBlocks in a ScrollViewer",
        ["FlowDocumentPageViewer"] = "Avalonia has no flow documents. Show the text with TextBlocks in a ScrollViewer",
        ["DocumentViewer"] = "Avalonia has no document viewer",
        ["WebBrowser"] = "Avalonia has no built-in web view. Use a NativeControlHost or a WebView package",
        ["Frame"] = "Avalonia has no page navigation. Use a ContentControl whose content the view model changes",
        ["NavigationWindow"] = "Avalonia has no page navigation. Use a ContentControl whose content the view model changes",
        ["MediaElement"] = "Avalonia has no media player. Use a package such as LibVLCSharp.Avalonia",
        ["InkCanvas"] = "Avalonia has no ink canvas. Draw the strokes with Polylines in a Canvas from pointer events",
        ["Viewport3D"] = "Avalonia has no 3D. Draw with OpenGL in an OpenGlControlBase",
        ["CollectionViewSource"] = "Avalonia has no collection views. Sort and filter the collection in the view model, or use DataGridCollectionView",
        ["ObjectDataProvider"] = "Avalonia has no data providers. Create the object in the view model",
        ["XmlDataProvider"] = "Avalonia has no XML data binding. Load the XML into objects in the view model",
        ["DataGridComboBoxColumn"] = "Avalonia's DataGrid has no combo box column. Use a DataGridTemplateColumn with a ComboBox",
        ["DataGridHyperlinkColumn"] = "Avalonia's DataGrid has no hyperlink column. Use a DataGridTemplateColumn with a HyperlinkButton",
        ["ResizeGrip"] = "Avalonia windows are resized from their edges",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>WPF's controls that the converter handles one by one, with their Avalonia counterparts.</summary>
    public static readonly FrozenSet<string> Special = FrozenSet.Create(StringComparer.Ordinal,
    [
        "Window", "Page", "GroupBox", "StatusBar", "ToolBar", "ToolBarTray", "ListView", "GridView", "GridViewColumn",
        "PasswordBox", "Hyperlink", "MouseBinding", "BooleanToVisibilityConverter", "Trigger", "DataTrigger", "MultiTrigger",
        "MultiDataTrigger", "EventTrigger", "Condition", "EventSetter", "BitmapImage", "CombinedGeometry",
    ]);

    /// <summary>
    /// The Avalonia name of a type named in an attribute, such as a style's <c>TargetType</c>, or
    /// <see langword="null"/> when Avalonia has no such type.
    /// </summary>
    /// <param name="wpfName">The type's WPF name, without a prefix.</param>
    public static string? AvaloniaName(string wpfName) => wpfName switch
    {
        _ when SameInAvalonia.Contains(wpfName) => wpfName,
        _ when Renamed.TryGetValue(wpfName, out var renamed) => renamed,
        "GroupBox" => "HeaderedContentControl",
        "PasswordBox" => "TextBox",
        "ListView" => "ListBox",
        "Page" => "UserControl",
        "Control" => "TemplatedControl",
        "FrameworkElement" or "UIElement" => "Control",
        "ButtonBase" => "Button",
        "TextBoxBase" => "TextBox",
        "Selector" => "SelectingItemsControl",
        "Shape" => "Shape",
        _ => null,
    };
}
