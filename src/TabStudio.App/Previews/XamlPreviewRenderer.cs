using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace TabStudio.App.Previews;

/// <summary>What the XAML designer shows for a XAML file.</summary>
/// <param name="View">The rendered view, or <see langword="null"/> when there is nothing to show.</param>
/// <param name="Error">Why the XAML could not be rendered, if it could not.</param>
/// <param name="ErrorLine">The 1-based line of <paramref name="Error"/>, when known.</param>
/// <param name="ErrorColumn">The 1-based column of <paramref name="Error"/>, when known.</param>
/// <param name="Note">How the preview differs from the app, when it does, such as a window shown as its content.</param>
/// <param name="DesignWidth">The width <c>d:DesignWidth</c> asks for, if any.</param>
/// <param name="DesignHeight">The height <c>d:DesignHeight</c> asks for, if any.</param>
internal sealed record XamlPreview(
    Control? View,
    string? Error = null,
    int? ErrorLine = null,
    int? ErrorColumn = null,
    string? Note = null,
    double? DesignWidth = null,
    double? DesignHeight = null);

/// <summary>
/// Renders the XAML of an Avalonia view as the designer shows it: in design mode, with the app's types from its
/// last build, <c>Design.DataContext</c> and <c>d:DesignWidth</c> applied, and the view's own class created
/// without loading the XAML compiled into it, so what appears is the XAML being typed.
/// </summary>
internal static class XamlPreviewRenderer
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Renders XAML. UI thread only.</summary>
    /// <param name="xaml">The XAML being edited.</param>
    /// <param name="path">The file's path in the app, such as <c>/Views/MainView.axaml</c>.</param>
    /// <param name="build">The app's last build, or <see langword="null"/> when it has none yet.</param>
    public static XamlPreview Render(string xaml, string path, XamlDesignBuild? build)
    {
        ArgumentNullException.ThrowIfNull(xaml);
        XElement root;
        try
        {
            root = XDocument.Parse(xaml, LoadOptions.SetLineInfo).Root!;
        }
        catch (XmlException ex)
        {
            return new XamlPreview(null, ex.Message, ex.LineNumber, ex.LinePosition);
        }

        var className = root.Attribute(XName.Get("Class", XamlNamespace))?.Value;
        var classType = className is null ? null : build?.FindType(className);
        switch (root.Name.LocalName)
        {
            case "Application":
                return new XamlPreview(null, Note: "App.axaml has no preview of its own: its styles and resources apply to the preview of every view.");
            case "Window":
                return RenderLoaded(
                    () => Load(AvaloniaAppHost.ContentOf(xaml), path, build),
                    root,
                    build,
                    "The window's content is shown; a window's own size, title and code apply only on a desktop.");
        }

        if (classType is not null && typeof(Control).IsAssignableFrom(classType) && classType.GetConstructor(Type.EmptyTypes) is not null)
        {
            return RenderLoaded(
                () =>
                {
                    object view;
                    using (AppXamlLoader.SuppressCompiledXaml())
                    {
                        view = Activator.CreateInstance(classType)!;
                    }

                    return AppXamlLoader.Load(xaml, path, build!.Assembly, view, designMode: true);
                },
                root,
                build,
                note: null);
        }

        // A class the last build does not have yet, or no build: the XAML is shown on its own.
        var note = className is null ? null : $"{className} is not in the last build of the app yet, so its code does not run in the preview. It will after the project builds.";
        return RenderLoaded(() => Load(WithoutClass(xaml, root), path, build), root, build, note);
    }

    private static object Load(string xaml, string path, XamlDesignBuild? build)
    {
        if (build is not null)
        {
            return AppXamlLoader.Load(xaml, path, build.Assembly, designMode: true);
        }

        RuntimeXamlTypes.CountLoad();
        return AvaloniaRuntimeXamlLoader.Load(xaml, designMode: true);
    }

    private static XamlPreview RenderLoaded(Func<object> load, XElement root, XamlDesignBuild? build, string? note)
    {
        object loaded;
        try
        {
            loaded = load();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var inner = ex switch
            {
                TargetInvocationException { InnerException: { } cause } => cause,
                AggregateException { InnerExceptions: [var first, ..] } => first,
                _ => ex,
            };
            return inner is XmlException xml
                ? new XamlPreview(null, inner.Message, xml.LineNumber > 0 ? xml.LineNumber : null, xml.LinePosition > 0 ? xml.LinePosition : null)
                : new XamlPreview(null, $"{inner.GetType().Name}: {inner.Message}");
        }

        return loaded switch
        {
            Control view => Preview(view, root, build, note),
            IStyle and AvaloniaObject styles when Design.GetPreviewWith(styles) is { } previewWith => StylePreview(previewWith, (IStyle)styles),
            ResourceDictionary resources when PreviewWith(resources) is { } previewWith => ResourcePreview(previewWith, resources),
            IStyle or ResourceDictionary => new XamlPreview(null, Note: "Add a Design.PreviewWith with a few controls to see these styles or resources applied."),
            _ => new XamlPreview(null, Note: $"A {loaded.GetType().Name} has no visual preview."),
        };
    }

    /// <summary>A view with its design-time properties: <c>Design.DataContext</c>, <c>d:DesignWidth</c> and <c>d:DesignHeight</c>.</summary>
    private static XamlPreview Preview(Control view, XElement root, XamlDesignBuild? build, string? note)
    {
        double? width = view.IsSet(Design.WidthProperty) ? Design.GetWidth(view) : null;
        double? height = view.IsSet(Design.HeightProperty) ? Design.GetHeight(view) : null;
        Design.ApplyDesignModeProperties(view, view);

        // Without design-time data, a new view model of the type the bindings are compiled against, when it has a
        // constructor without parameters: the preview then shows the view model's own values.
        if (view.DataContext is null && DataType(root, build) is { } dataType && dataType.GetConstructor(Type.EmptyTypes) is not null)
        {
            try
            {
                view.DataContext = Activator.CreateInstance(dataType);
                note ??= $"Showing a new {dataType.Name}. Set Design.DataContext to preview other data.";
            }
            catch (TargetInvocationException ex)
            {
                note ??= $"A new {dataType.Name} could not be created for the preview: {ex.InnerException?.Message}";
            }
        }

        return new XamlPreview(view, Note: note, DesignWidth: width, DesignHeight: height);
    }

    private static XamlPreview StylePreview(Control previewWith, IStyle styles)
    {
        var host = new Border { Child = previewWith, Padding = new Thickness(16) };
        host.Styles.Add(styles);
        return new XamlPreview(host);
    }

    /// <summary>A resource dictionary's <c>Design.PreviewWith</c>; Avalonia's lookup throws for dictionaries without one.</summary>
    private static Control? PreviewWith(ResourceDictionary resources)
    {
        try
        {
            return Design.GetPreviewWith(resources);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static XamlPreview ResourcePreview(Control previewWith, ResourceDictionary resources)
    {
        var host = new Border { Child = previewWith, Padding = new Thickness(16) };
        host.Resources.MergedDictionaries.Add(resources);
        return new XamlPreview(host);
    }

    /// <summary>The type named by the root's <c>x:DataType</c>, such as <c>vm:MainViewModel</c>, in the app's build.</summary>
    private static Type? DataType(XElement root, XamlDesignBuild? build)
    {
        if (build is null || root.Attribute(XName.Get("DataType", XamlNamespace))?.Value is not { } value)
        {
            return null;
        }

        var colon = value.IndexOf(':', StringComparison.Ordinal);
        var prefix = colon < 0 ? string.Empty : value[..colon];
        var name = colon < 0 ? value : value[(colon + 1)..];
        var xmlNamespace = (prefix.Length == 0 ? root.GetDefaultNamespace() : root.GetNamespaceOfPrefix(prefix))?.NamespaceName;
        var clrNamespace = xmlNamespace switch
        {
            null => null,
            _ when xmlNamespace.StartsWith("using:", StringComparison.Ordinal) => xmlNamespace["using:".Length..],
            _ when xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal) => xmlNamespace["clr-namespace:".Length..].Split(';')[0],
            _ => null,
        };
        return clrNamespace is null ? null : build.FindType($"{clrNamespace}.{name}");
    }

    /// <summary>
    /// The XAML without its <c>x:Class</c>, so it loads as the root element's own type. The attribute is blanked
    /// out rather than removed, so errors keep the lines and columns of the text being edited.
    /// </summary>
    private static string WithoutClass(string xaml, XElement root)
    {
        if (root.GetPrefixOfNamespace(XamlNamespace) is not { } prefix)
        {
            return xaml;
        }

        var match = Regex.Match(xaml, $@"\b{Regex.Escape(prefix)}:Class\s*=\s*(""[^""]*""|'[^']*')", RegexOptions.None, TimeSpan.FromSeconds(1));
        return match.Success ? string.Concat(xaml.AsSpan(0, match.Index), new string(' ', match.Length), xaml.AsSpan(match.Index + match.Length)) : xaml;
    }
}
