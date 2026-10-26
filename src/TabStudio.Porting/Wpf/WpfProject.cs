using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.Output;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>A class an element gets when a keyed style's data trigger holds, and the binding that sets it.</summary>
/// <param name="ClassName">The style class, such as <c>isFavorite</c>.</param>
/// <param name="Binding">The binding of <c>Classes.isFavorite</c>, such as <c>{Binding IsFavorite}</c>.</param>
internal sealed record ClassCondition(string ClassName, string Binding);

/// <summary>A keyed WPF style, which becomes a style class in Avalonia.</summary>
/// <param name="Key">The style's key.</param>
/// <param name="TargetType">Its WPF target type, without a prefix.</param>
/// <param name="ClassName">The Avalonia style class elements that used the style get.</param>
/// <param name="BasedOnKey">The keyed style it is based on, whose class the elements get too.</param>
/// <param name="Conditions">The classes its data triggers set.</param>
internal sealed record WpfStyle(string Key, string? TargetType, string ClassName, string? BasedOnKey, IReadOnlyList<ClassCondition> Conditions);

/// <summary>A tunneling (Preview) event handler from XAML, which code-behind adds with the Tunnel strategy.</summary>
/// <param name="Element">The named element, or <see langword="null"/> for the view itself.</param>
/// <param name="Event">The Avalonia routed event, such as <c>InputElement.KeyDownEvent</c>.</param>
/// <param name="Handler">The handler method.</param>
internal sealed record TunnelHandler(string? Element, string Event, string Handler);

/// <summary>What the converter learned about the class of a XAML file, for converting its code-behind.</summary>
/// <param name="ClassName">The class, with its namespace.</param>
/// <param name="RootType">The WPF type of the file's root, such as <c>Window</c>.</param>
internal sealed class XamlClassInfo(string ClassName, string RootType)
{
    /// <summary>The class, with its namespace.</summary>
    public string ClassName { get; } = ClassName;

    /// <summary>The WPF type of the file's root, such as <c>Window</c>.</summary>
    public string RootType { get; } = RootType;

    /// <summary>Named elements whose type changed: name to WPF type and Avalonia type.</summary>
    public Dictionary<string, (string Wpf, string Avalonia)> ChangedElements { get; } = new(StringComparer.Ordinal);

    /// <summary>Handlers of events whose arguments have another type in Avalonia: method to the arguments' type.</summary>
    public Dictionary<string, string> HandlerArguments { get; } = new(StringComparer.Ordinal);

    /// <summary>Preview event handlers the code-behind adds.</summary>
    public List<TunnelHandler> TunnelHandlers { get; } = [];

    /// <summary>For the application, the class of the window <c>StartupUri</c> named.</summary>
    public string? StartupWindow { get; set; }
}

/// <summary>
/// A WPF project as the converter sees it before converting any file: its XAML, its keyed styles, the dictionaries
/// that hold styles, and which names its code uses. Styles in one file are used in others, so this comes first.
/// </summary>
internal sealed class WpfProject
{
    /// <summary>Reads the project's XAML and code.</summary>
    public WpfProject(PortContext context)
    {
        Context = context;
        foreach (var (path, text) in context.Source.Files)
        {
            if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) && TryLoad(text) is { } document)
            {
                Xaml[path] = document;
            }
            else if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var root = CSharpSyntaxTree.ParseText(text, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)).GetRoot();
                foreach (var type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    DeclaredTypes.Add(type.Identifier.ValueText);
                }

                if (path.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
                {
                    CodeIdentifiers[path[..^3]] = [.. root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText)];
                }
            }
        }

        foreach (var (path, document) in Xaml)
        {
            Scan(path, document);
        }
    }

    /// <summary>The port.</summary>
    public PortContext Context { get; }

    /// <summary>The XAML files, by their path in the original project.</summary>
    public Dictionary<string, XDocument> Xaml { get; } = new(StringComparer.Ordinal);

    /// <summary>The keyed styles of the whole project, by key.</summary>
    public Dictionary<string, WpfStyle> Styles { get; } = new(StringComparer.Ordinal);

    /// <summary>Resource dictionary files that hold styles, which become Avalonia <c>Styles</c> files.</summary>
    public HashSet<string> StyleDictionaries { get; } = new(StringComparer.Ordinal);

    /// <summary>The WPF types that implicit styles (styles without a key) target anywhere in the project.</summary>
    public HashSet<string> ImplicitStyleTargets { get; } = new(StringComparer.Ordinal);

    /// <summary>Keys of <c>BooleanToVisibilityConverter</c> resources, which Avalonia's IsVisible does not need.</summary>
    public HashSet<string> VisibilityConverterKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Names of the types the project declares, which are never taken for WPF's.</summary>
    public HashSet<string> DeclaredTypes { get; } = new(StringComparer.Ordinal);

    /// <summary>The identifiers each XAML file's code-behind uses, by the XAML file's path.</summary>
    public Dictionary<string, HashSet<string>> CodeIdentifiers { get; } = new(StringComparer.Ordinal);

    /// <summary>What the converter learned about each XAML class, by class name.</summary>
    public Dictionary<string, XamlClassInfo> Classes { get; } = new(StringComparer.Ordinal);

    /// <summary>The XAML file of a path, in the new project: <c>.xaml</c> becomes <c>.axaml</c>.</summary>
    /// <param name="path">The path in the original project.</param>
    public static string TargetPath(string path) =>
        path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ? path[..^5] + ".axaml"
        : path.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase) ? path[..^8] + ".axaml.cs"
        : path;

    /// <summary>Parses XAML, keeping line numbers and whitespace, or <see langword="null"/> when it is not well-formed.</summary>
    /// <param name="text">The XAML.</param>
    public static XDocument? TryLoad(string text)
    {
        try
        {
            return XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>A style class name made from a resource key: letters, digits, <c>-</c> and <c>_</c>.</summary>
    /// <param name="key">The key.</param>
    public static string ClassName(string key)
    {
        var name = new string([.. key.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')]);
        return name.Length == 0 || char.IsDigit(name[0]) ? "style" + name : name;
    }

    /// <summary>The type name inside <c>{x:Type T}</c>, or the text itself.</summary>
    /// <param name="value">An attribute value such as <c>{x:Type Button}</c> or <c>Button</c>.</param>
    public static string TypeName(string value)
    {
        var extension = MarkupExtension.Parse(value);
        if (extension is { Name: "x:Type" })
        {
            return (extension.Positional(0)?.Text ?? extension.GetText("TypeName") ?? string.Empty).Trim();
        }

        return value.Trim();
    }

    /// <summary>The key of a <c>{StaticResource Key}</c> or <c>{DynamicResource Key}</c>, or <see langword="null"/>.</summary>
    /// <param name="value">The attribute value.</param>
    public static string? ResourceKey(string value) =>
        MarkupExtension.Parse(value) is { Name: "StaticResource" or "DynamicResource" } extension
        && (extension.Positional(0) ?? extension.Find("ResourceKey")?.Value) is { Extension: null } key
            ? key.Text
            : null;

    /// <summary>A class condition for a data trigger's binding and value, when the value is a boolean or null.</summary>
    /// <param name="binding">The trigger's binding.</param>
    /// <param name="value">The value it compares with.</param>
    /// <returns>The condition, or <see langword="null"/> when no style class can express it exactly.</returns>
    public static ClassCondition? Condition(string binding, string value)
    {
        if (MarkupExtension.Parse(binding) is not { Name: "Binding" } extension)
        {
            return null;
        }

        var path = (extension.Positional(0)?.Text ?? extension.GetText("Path") ?? string.Empty).Trim();
        var words = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Any(w => !w.All(c => char.IsLetterOrDigit(c) || c == '_')))
        {
            return null;
        }

        var pascal = string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        var hasConverter = extension.Find("Converter") is not null;
        switch (value.Trim())
        {
            case var v when v.Equals("True", StringComparison.OrdinalIgnoreCase):
                return new ClassCondition(char.ToLowerInvariant(pascal[0]) + pascal[1..], extension.ToString());
            case var v when v.Equals("False", StringComparison.OrdinalIgnoreCase) && !hasConverter:
                extension.Set("Converter", new MarkupValue(MarkupExtension.Parse("{x:Static BoolConverters.Not}")!));
                return new ClassCondition("not" + pascal, extension.ToString());
            case "{x:Null}" when !hasConverter:
                extension.Set("Converter", new MarkupValue(MarkupExtension.Parse("{x:Static ObjectConverters.IsNull}")!));
                return new ClassCondition("no" + pascal, extension.ToString());
            default:
                return null;
        }
    }

    private void Scan(string path, XDocument document)
    {
        var root = document.Root;
        if (root is null)
        {
            return;
        }

        var hasStyles = false;
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.Namespace != XamlNamespaces.Wpf)
            {
                continue;
            }

            switch (element.Name.LocalName)
            {
                case "Style":
                    hasStyles |= element.Ancestors().All(a => a.Name.LocalName is "ResourceDictionary" or "ResourceDictionary.MergedDictionaries");
                    if (element.Attribute(XamlNamespaces.Xaml + "Key")?.Value is { } key)
                    {
                        Styles[key] = ReadStyle(key, element);
                    }
                    else if (element.Attribute("TargetType")?.Value is { } target)
                    {
                        ImplicitStyleTargets.Add(TypeName(target));
                    }

                    break;
                case "BooleanToVisibilityConverter" when element.Attribute(XamlNamespaces.Xaml + "Key")?.Value is { } converter:
                    VisibilityConverterKeys.Add(converter);
                    break;
            }
        }

        if (root.Name == XamlNamespaces.Wpf + "ResourceDictionary" && hasStyles)
        {
            StyleDictionaries.Add(path);
        }
    }

    private static WpfStyle ReadStyle(string key, XElement style)
    {
        var target = style.Attribute("TargetType")?.Value is { } type ? TypeName(type) : null;
        var basedOn = style.Attribute("BasedOn")?.Value is { } based && ResourceKey(based) is { } baseKey && !baseKey.StartsWith('{') ? baseKey : null;
        var conditions = new List<ClassCondition>();
        foreach (var trigger in style.Elements(XamlNamespaces.Wpf + "Style.Triggers").Elements())
        {
            List<(string? Binding, string? Value)> pairs = trigger.Name.LocalName switch
            {
                "DataTrigger" => [(trigger.Attribute("Binding")?.Value, trigger.Attribute("Value")?.Value)],
                "MultiDataTrigger" => trigger.Elements(XamlNamespaces.Wpf + "MultiDataTrigger.Conditions").Elements()
                    .Select(c => (c.Attribute("Binding")?.Value, c.Attribute("Value")?.Value)).ToList(),
                _ => [],
            };

            foreach (var (binding, value) in pairs)
            {
                if (binding is not null && value is not null && Condition(binding, value) is { } condition
                    && !conditions.Exists(c => c.ClassName == condition.ClassName))
                {
                    conditions.Add(condition);
                }
            }
        }

        return new WpfStyle(key, target, ClassName(key), basedOn, conditions);
    }
}
