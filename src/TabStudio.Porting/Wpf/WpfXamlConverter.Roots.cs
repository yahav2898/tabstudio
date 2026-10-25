using System.Xml.Linq;
using TabStudio.Porting.Output;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>The roots of WPF's XAML files: windows, the application and resource dictionaries.</summary>
internal sealed partial class WpfXamlConverter
{
    /// <summary>The theme of Avalonia's data grid, which App.axaml includes when the app has a grid.</summary>
    public const string DataGridTheme = "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml";

    /// <summary>The port's styles for group boxes, status bars and toolbars.</summary>
    public const string PortingStyles = "/Porting/PortingStyles.axaml";

    /// <summary>A window becomes the port's AppWindow: a window on the desktop, the screen on phones.</summary>
    private void ConvertWindowRoot()
    {
        _root.AddAnnotation(new WpfType("Window"));
        _root.Name = Support + "AppWindow";
        _project.Context.Needs(SupportNeeds.WpfWindow);
        _report.Converted("Window → AppWindow, a window on the desktop and the screen on phones");
        if (_root.Attribute("ResizeMode") is { } resize)
        {
            if (resize.Value is "NoResize" or "CanMinimize")
            {
                Rename(_root, resize, "CanResize", "False");
                _report.Converted("ResizeMode → CanResize");
            }
            else
            {
                resize.Remove();
            }
        }

        foreach (var name in (string[])["Activated", "Deactivated"])
        {
            if (_root.Attribute(name) is { } handler)
            {
                Note(_root, $"{name}=\"{handler.Value}\"", "The port's AppWindow has no activation events; on the desktop, handle DesktopWindow's " + name + " event in code", Original(handler));
                handler.Remove();
            }
        }
    }

    /// <summary>The application: StartupUri moves to code, and the app gets Avalonia's Fluent theme in WPF's light look.</summary>
    private void ConvertApplicationRoot()
    {
        if (_root.Attribute("StartupUri") is { } startup)
        {
            var path = ResolvePath(startup.Value);
            if (_project.Xaml.TryGetValue(path, out var window) && window.Root?.Attribute(ClassAttribute)?.Value is { } windowClass && _class is not null)
            {
                _class.StartupWindow = windowClass;
                _report.Converted("StartupUri → App.axaml.cs opens the window");
            }
            else
            {
                Note(_root, $"StartupUri=\"{startup.Value}\"", "The window it names was not found; open the first window in App.axaml.cs", Original(startup));
            }

            startup.Remove();
        }

        foreach (var name in (string[])["Startup", "Exit", "SessionEnding", "DispatcherUnhandledException", "Activated", "Deactivated", "LoadCompleted", "Navigated"])
        {
            if (_root.Attribute(name) is { } handler)
            {
                var why = name == "Startup"
                    ? "Avalonia's Application has no Startup event; call the handler from OnFrameworkInitializationCompleted in App.axaml.cs"
                    : $"Avalonia's Application has no {name} event; on the desktop, IClassicDesktopStyleApplicationLifetime has Startup, Exit and ShutdownRequested";
                Note(_root, $"{name}=\"{handler.Value}\"", why, Original(handler));
                handler.Remove();
            }
        }

        if (_root.Attribute("ShutdownMode") is { } shutdown)
        {
            Note(_root, $"ShutdownMode=\"{shutdown.Value}\"", "Set ShutdownMode on IClassicDesktopStyleApplicationLifetime in App.axaml.cs", Original(shutdown));
            shutdown.Remove();
        }

        Set(_root, "RequestedThemeVariant", "Light");
        _report.Converted("WPF's look → Fluent theme in light");
    }

    /// <summary>
    /// Completes App.axaml once every file is converted: Avalonia's Fluent theme first, then the data grid's theme
    /// and the port's styles when the app uses them, then the styles the WPF resources had.
    /// </summary>
    /// <param name="app">The converted application.</param>
    /// <param name="context">The port.</param>
    public static void FinishApplication(XDocument app, PortContext context)
    {
        var root = app.Root!;
        var styles = PropertyElement(root, "Styles", root.Element(root.Name.Namespace + "Application.Resources"));
        var first = new List<XElement> { new(XamlNamespaces.Avalonia + "FluentTheme") };
        if (context.UsesDataGrid)
        {
            first.Add(new XElement(XamlNamespaces.Avalonia + "StyleInclude", new XAttribute("Source", DataGridTheme)));
        }

        if ((context.Support & SupportNeeds.Styles) != 0)
        {
            first.Add(new XElement(XamlNamespaces.Avalonia + "StyleInclude", new XAttribute("Source", PortingStyles)));
        }

        styles.AddFirst(first);
    }

    /// <summary>
    /// A resource dictionary file: one with styles becomes an Avalonia <c>Styles</c> file, its other resources in
    /// <c>Styles.Resources</c>; one without stays a <c>ResourceDictionary</c>.
    /// </summary>
    private void ConvertDictionaryRoot()
    {
        if (!_project.StyleDictionaries.Contains(_sourcePath))
        {
            ConvertResourceItems(_root, out var styles, out var templates);
            foreach (var orphan in styles.Concat(templates).OfType<XElement>())
            {
                Note(_root, "Implicit " + orphan.Name.LocalName, "An Avalonia resource dictionary cannot hold implicit data templates; move it to Application.DataTemplates in App.axaml", Original(orphan));
            }

            ConvertAttributes(_root, Scope.Root);
            return;
        }

        _root.Name = XamlNamespaces.Avalonia + "Styles";
        _report.Converted("ResourceDictionary with styles → Styles file");
        ConvertResourceItems(_root, out var ownStyles, out var keyless);
        foreach (var template in keyless.OfType<XElement>())
        {
            Note(_root, "DataTemplate DataType=\"" + Value(template, "DataType") + "\"", "An Avalonia Styles file cannot hold data templates; move it to Application.DataTemplates in App.axaml", Original(template));
        }

        // What remains are resources: they go to Styles.Resources, ahead of the styles.
        var resources = _root.Elements().ToList();
        if (resources.Count > 0)
        {
            foreach (var resource in resources)
            {
                resource.Remove();
            }

            var dictionary = new XElement(XamlNamespaces.Avalonia + "ResourceDictionary", resources);
            _root.AddFirst(new XElement(XamlNamespaces.Avalonia + "Styles.Resources", dictionary));
        }

        foreach (var style in ownStyles)
        {
            _root.Add(style);
        }

        ConvertAttributes(_root, Scope.Root);
    }

    /// <summary>Remembers what type an element had in WPF, for its property elements.</summary>
    /// <param name="Name">The WPF type.</param>
    private sealed record WpfType(string Name);
}
