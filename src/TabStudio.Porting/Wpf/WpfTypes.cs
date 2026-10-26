using System.Collections.Frozen;

namespace TabStudio.Porting.Wpf;

/// <summary>A WPF type in code and what it is in Avalonia.</summary>
/// <param name="Name">The Avalonia type, such as <c>Control</c> or <c>EventHandler&lt;RoutedEventArgs&gt;</c>.</param>
/// <param name="Namespace">Its namespace, which the file imports; <see langword="null"/> for types of the port or of .NET.</param>
internal sealed record TypeMapping(string Name, string? Namespace);

/// <summary>WPF's types as code uses them, and their Avalonia counterparts, after Avalonia's WPF migration guide.</summary>
internal static class WpfTypes
{
    /// <summary>The namespaces of WPF, which ported files no longer import.</summary>
    public static readonly FrozenSet<string> Namespaces = FrozenSet.Create(StringComparer.Ordinal,
    [
        "System.Windows", "System.Windows.Controls", "System.Windows.Controls.Primitives", "System.Windows.Data",
        "System.Windows.Documents", "System.Windows.Input", "System.Windows.Media", "System.Windows.Media.Imaging",
        "System.Windows.Media.Animation", "System.Windows.Media.Effects", "System.Windows.Media.Media3D",
        "System.Windows.Navigation", "System.Windows.Shapes", "System.Windows.Threading", "System.Windows.Markup",
        "System.Windows.Interop", "System.Windows.Automation", "System.Windows.Shell", "Microsoft.Win32",
    ]);

    private const string Av = "Avalonia";
    private const string Controls = "Avalonia.Controls";
    private const string Primitives = "Avalonia.Controls.Primitives";
    private const string Media = "Avalonia.Media";
    private const string Input = "Avalonia.Input";
    private const string Interactivity = "Avalonia.Interactivity";
    private const string Layout = "Avalonia.Layout";
    private const string Threading = "Avalonia.Threading";
    private const string Data = "Avalonia.Data";
    private const string Converters = "Avalonia.Data.Converters";
    private const string Documents = "Avalonia.Controls.Documents";
    private const string Shapes = "Avalonia.Controls.Shapes";
    private const string Templates = "Avalonia.Markup.Xaml.Templates";
    private const string Imaging = "Avalonia.Media.Imaging";

    /// <summary>WPF's types that Avalonia has, by their WPF name.</summary>
    public static readonly FrozenDictionary<string, TypeMapping> Mapped = Build();

    /// <summary>WPF's types that have no Avalonia counterpart, with what to use instead.</summary>
    public static readonly FrozenDictionary<string, string> Unsupported = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CollectionViewSource"] = "Avalonia has no collection views. Filter and sort a collection of the view model, or use DataGridCollectionView from Avalonia.Controls.DataGrid",
        ["ICollectionView"] = "Avalonia has no collection views. Filter and sort a collection of the view model, or use DataGridCollectionView from Avalonia.Controls.DataGrid",
        ["ListCollectionView"] = "Avalonia has no collection views. Use DataGridCollectionView from Avalonia.Controls.DataGrid",
        ["CollectionView"] = "Avalonia has no collection views. Use DataGridCollectionView from Avalonia.Controls.DataGrid",
        ["RoutedCommand"] = "Avalonia has no routed commands; use an ICommand of the view model",
        ["RoutedUICommand"] = "Avalonia has no routed commands; use an ICommand of the view model",
        ["ApplicationCommands"] = "Avalonia has no built-in commands; use an ICommand of the view model",
        ["NavigationCommands"] = "Avalonia has no built-in commands; use an ICommand of the view model",
        ["EditingCommands"] = "Avalonia has no built-in commands; use an ICommand of the view model",
        ["CommandBinding"] = "Avalonia has no command bindings; bind controls to commands of the view model",
        ["ExecutedRoutedEventArgs"] = "Avalonia has no command bindings; bind controls to commands of the view model",
        ["CanExecuteRoutedEventArgs"] = "Avalonia has no command bindings; bind controls to commands of the view model",
        ["NavigationService"] = "Avalonia has no page navigation; change the content of a ContentControl",
        ["NavigationWindow"] = "Avalonia has no page navigation; change the content of a ContentControl",
        ["FlowDocument"] = "Avalonia has no flow documents; build the text from TextBlocks and inlines",
        ["Paragraph"] = "Avalonia has no flow documents; build the text from TextBlocks and inlines",
        ["TextRange"] = "Avalonia has no rich text; keep the text as a string",
        ["TextPointer"] = "Avalonia has no rich text; keep the text as a string",
        ["Hyperlink"] = "Avalonia has no hyperlinks inside text; use a HyperlinkButton",
        ["RichTextBox"] = "Avalonia has no rich text editor; use a TextBox, or AvaloniaEdit",
        ["WebBrowser"] = "Avalonia has no built-in web view",
        ["Frame"] = "Avalonia has no page navigation; change the content of a ContentControl",
        ["MediaElement"] = "Avalonia has no media player; use a package such as LibVLCSharp.Avalonia",
        ["InkCanvas"] = "Avalonia has no ink canvas; draw strokes with Polylines from pointer events",
        ["Storyboard"] = "Avalonia animates with Animation and KeyFrame, started with RunAsync",
        ["DoubleAnimation"] = "Avalonia animates with Animation and KeyFrame, started with RunAsync",
        ["ColorAnimation"] = "Avalonia animates with Animation and KeyFrame, started with RunAsync",
        ["ThicknessAnimation"] = "Avalonia animates with Animation and KeyFrame, started with RunAsync",
        ["SystemParameters"] = "Avalonia reads the screen from TopLevel.GetTopLevel(this).Screens",
        ["SystemColors"] = "Use a theme resource, such as this.FindResource(\"SystemControlForegroundBaseHighBrush\")",
        ["SystemFonts"] = "Use the theme's font from FontFamily.Default",
        ["Mouse"] = "Avalonia has no global mouse state; use the pointer event's arguments",
        ["Keyboard"] = "Avalonia has no global keyboard state; use the key event's KeyModifiers",
        ["FocusManager"] = "Call Focus() on the element; TopLevel.GetTopLevel(this).FocusManager has the focused element",
        ["PresentationSource"] = "Avalonia has no Win32 presentation sources",
        ["HwndSource"] = "Avalonia has no Win32 windows; DesktopWindow.TryGetPlatformHandle() gives the handle on Windows",
        ["WindowInteropHelper"] = "Avalonia has no Win32 windows; DesktopWindow.TryGetPlatformHandle() gives the handle on Windows",
        ["OpenFileDialog"] = "Avalonia picks files with TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync, on every platform",
        ["SaveFileDialog"] = "Avalonia saves files with TopLevel.GetTopLevel(this).StorageProvider.SaveFilePickerAsync, on every platform",
        ["OpenFolderDialog"] = "Avalonia picks folders with TopLevel.GetTopLevel(this).StorageProvider.OpenFolderPickerAsync, on every platform",
        ["PrintDialog"] = "Avalonia does not print",
        ["Registry"] = "The Windows registry does not exist on other platforms; keep settings in a file",
        ["StartupEventArgs"] = "Avalonia's application starts in OnFrameworkInitializationCompleted; the desktop lifetime has Args",
        ["ExitEventArgs"] = "On the desktop, IClassicDesktopStyleApplicationLifetime raises Exit",
        ["DataTemplateSelector"] = "Avalonia has no template selectors; give each type a DataTemplate with its DataType",
        ["StyleSelector"] = "Avalonia has no style selectors; use classes",
        ["Adorner"] = "Avalonia's adorners are controls on the AdornerLayer: AdornerLayer.SetAdorner(element, adorner)",
        ["DragDrop"] = "Avalonia's DragDrop.DoDragDrop takes a DataTransfer and the pointer event",
        ["DataObject"] = "Avalonia's drag and drop and clipboard take a DataTransfer",
        ["Int32Rect"] = "Use Avalonia's PixelRect",
        ["FormattedText"] = "Avalonia's FormattedText takes other arguments: text, culture, flow direction, typeface, size and brush",
        ["WriteableBitmap"] = "Avalonia's WriteableBitmap is written through Lock()",
        ["RenderTargetBitmap"] = "Avalonia's RenderTargetBitmap takes a PixelSize and renders with Render(visual)",
        ["BitmapSource"] = "Use Avalonia's Bitmap, loaded from a stream or an avares:// address",
        ["CompositionTarget"] = "Use TopLevel.GetTopLevel(this).RequestAnimationFrame",
        ["SoundPlayer"] = "Play sounds with a cross-platform audio package",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The Avalonia delegate a WPF event handler type became, for the handler's arguments.</summary>
    public static readonly FrozenDictionary<string, string> HandlerArguments = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["RoutedEventHandler"] = "RoutedEventArgs",
        ["KeyEventHandler"] = "KeyEventArgs",
        ["MouseButtonEventHandler"] = "PointerPressedEventArgs",
        ["MouseEventHandler"] = "PointerEventArgs",
        ["MouseWheelEventHandler"] = "PointerWheelEventArgs",
        ["SelectionChangedEventHandler"] = "SelectionChangedEventArgs",
        ["TextChangedEventHandler"] = "TextChangedEventArgs",
        ["SizeChangedEventHandler"] = "SizeChangedEventArgs",
        ["TextCompositionEventHandler"] = "TextInputEventArgs",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static FrozenDictionary<string, TypeMapping> Build()
    {
        var map = new Dictionary<string, TypeMapping>(StringComparer.Ordinal);
        void Same(string ns, params string[] names)
        {
            foreach (var name in names)
            {
                map[name] = new TypeMapping(name, ns);
            }
        }

        void As(string wpf, string avalonia, string? ns) => map[wpf] = new TypeMapping(avalonia, ns);

        // System.Windows.
        Same(Av, "Application", "Thickness", "CornerRadius", "Size", "Point", "Rect", "Vector", "Matrix", "Visual", "StyledElement");
        As("DependencyObject", "AvaloniaObject", Av);
        As("DependencyProperty", "AvaloniaProperty", Av);
        As("DependencyPropertyChangedEventArgs", "AvaloniaPropertyChangedEventArgs", Av);
        As("UIElement", "Control", Controls);
        As("FrameworkElement", "Control", Controls);
        As("ContentElement", "StyledElement", Av);
        As("FrameworkContentElement", "StyledElement", Av);
        As("Visibility", "bool", null);
        As("Window", "AppWindow", null);
        Same(Layout, "HorizontalAlignment", "VerticalAlignment", "Orientation");
        Same(Media, "TextAlignment", "TextWrapping", "TextTrimming", "TextDecorations", "TextDecorationCollection", "FontStyle", "FontWeight", "FontStretch");
        As("FontWeights", "FontWeight", Media);
        As("FontStyles", "FontStyle", Media);
        As("FontStretches", "FontStretch", Media);
        Same(Controls, "GridLength", "GridUnitType", "ResourceDictionary", "SizeChangedEventArgs", "SizeToContent", "WindowStartupLocation", "WindowState");
        Same(Interactivity, "RoutedEventArgs", "RoutedEvent");
        As("RoutingStrategy", "RoutingStrategies", Interactivity);
        As("RoutedEventHandler", "EventHandler<RoutedEventArgs>", Interactivity);
        As("Style", "Style", "Avalonia.Styling");
        Same(Templates, "DataTemplate", "ControlTemplate", "ItemsPanelTemplate");

        // System.Windows.Controls and Primitives.
        Same(Controls, "Button", "TextBox", "TextBlock", "CheckBox", "ComboBox", "ComboBoxItem", "ListBox", "ListBoxItem", "Grid",
            "StackPanel", "DockPanel", "Canvas", "Border", "ScrollViewer", "Image", "Slider", "ProgressBar", "TabControl", "TabItem",
            "Menu", "MenuItem", "ContextMenu", "ToolTip", "Expander", "Label", "UserControl", "ContentControl", "ItemsControl",
            "Panel", "WrapPanel", "Viewbox", "RadioButton", "Separator", "TreeView", "TreeViewItem", "DataGrid", "DataGridTextColumn",
            "DataGridCheckBoxColumn", "DataGridTemplateColumn", "DataGridRow", "DataGridCell", "DataGridColumn", "Calendar",
            "GridSplitter", "ColumnDefinition", "RowDefinition", "SelectionChangedEventArgs", "TextChangedEventArgs", "SelectionMode",
            "Dock", "ScrollChangedEventArgs", "Decorator", "HeaderedContentControl", "HeaderedItemsControl", "RepeatButton",
            "ContentPresenter", "ItemsPresenter", "Control");
        As("Control", "TemplatedControl", Primitives);
        Same(Primitives, "ToggleButton", "Popup", "RangeBase", "Thumb", "UniformGrid", "ScrollBar", "ScrollBarVisibility", "TemplatedControl");
        As("ButtonBase", "Button", Controls);
        As("TextBoxBase", "TextBox", Controls);
        As("Selector", "SelectingItemsControl", Primitives);
        As("GroupBox", "HeaderedContentControl", Controls);
        As("DatePicker", "CalendarDatePicker", Controls);
        As("PasswordBox", "TextBox", Controls);
        As("ListView", "ListBox", Controls);
        As("ListViewItem", "ListBoxItem", Controls);
        As("Page", "UserControl", Controls);
        As("StatusBar", "Border", Controls);
        As("ToolBar", "StackPanel", Controls);
        As("RoutedPropertyChangedEventArgs", "RangeBaseValueChangedEventArgs", Primitives);

        // System.Windows.Media.
        As("Brush", "IBrush", Media);
        Same(Media, "SolidColorBrush", "Brushes", "Color", "Colors", "FontFamily", "Geometry", "PathGeometry", "Transform",
            "RotateTransform", "ScaleTransform", "TranslateTransform", "SkewTransform", "TransformGroup", "MatrixTransform", "Pen",
            "DrawingContext", "Stretch", "LinearGradientBrush", "RadialGradientBrush", "GradientStop", "Typeface", "DropShadowEffect",
            "BlurEffect", "StreamGeometry", "EllipseGeometry", "RectangleGeometry", "LineGeometry", "GeometryGroup", "ImageBrush");
        As("GradientStopCollection", "GradientStops", Media);
        As("ImageSource", "IImage", Media);
        As("BitmapImage", "Bitmap", Imaging);
        As("VisualTreeHelper", "VisualTreeHelper", null);

        // System.Windows.Input.
        Same(Input, "KeyEventArgs", "Key", "KeyBinding", "KeyGesture", "MouseButton", "Cursor");
        As("ModifierKeys", "KeyModifiers", Input);
        As("MouseEventArgs", "PointerEventArgs", Input);
        As("MouseButtonEventArgs", "PointerPressedEventArgs", Input);
        As("MouseWheelEventArgs", "PointerWheelEventArgs", Input);
        As("TextCompositionEventArgs", "TextInputEventArgs", Input);
        As("KeyEventHandler", "EventHandler<KeyEventArgs>", Input);
        As("MouseButtonEventHandler", "EventHandler<PointerPressedEventArgs>", Input);
        As("MouseEventHandler", "EventHandler<PointerEventArgs>", Input);
        As("MouseWheelEventHandler", "EventHandler<PointerWheelEventArgs>", Input);
        As("TextCompositionEventHandler", "EventHandler<TextInputEventArgs>", Input);
        As("SelectionChangedEventHandler", "EventHandler<SelectionChangedEventArgs>", Controls);
        As("TextChangedEventHandler", "EventHandler<TextChangedEventArgs>", Controls);
        As("SizeChangedEventHandler", "EventHandler<SizeChangedEventArgs>", Controls);
        As("Cursors", "Cursors", null);
        As("CommandManager", "CommandManager", null);

        // System.Windows.Threading.
        Same(Threading, "Dispatcher", "DispatcherTimer", "DispatcherPriority", "DispatcherOperation");

        // System.Windows.Data.
        Same(Converters, "IValueConverter", "IMultiValueConverter");
        Same(Data, "Binding", "BindingMode", "BindingOperations", "RelativeSource", "UpdateSourceTrigger", "MultiBinding");

        // System.Windows.Documents and Shapes.
        Same(Documents, "Run", "Bold", "Italic", "Underline", "Span", "LineBreak", "Inline", "InlineUIContainer");
        Same(Shapes, "Rectangle", "Ellipse", "Line", "Path", "Polygon", "Polyline", "Shape");

        // System.Windows.Markup.
        Same("Avalonia.Markup.Xaml", "MarkupExtension");

        // The port's own: message boxes and the window of the app.
        foreach (var name in (string[])["MessageBox", "MessageBoxButton", "MessageBoxImage", "MessageBoxResult"])
        {
            map[name] = new TypeMapping(name, null);
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
