using System.Xml.Linq;
using TabStudio.Porting.Output;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>A XAML file of the new project, converted, before it is written.</summary>
/// <param name="Path">The file in the new project, relative with <c>/</c>.</param>
/// <param name="SourcePath">The original file.</param>
/// <param name="Document">The converted XAML.</param>
/// <param name="Format">The original file's indentation.</param>
/// <param name="Report">What changed in it.</param>
internal sealed record ConvertedXaml(string Path, string SourcePath, XDocument Document, XamlFormat Format, FileReport Report);

/// <summary>
/// Converts one WPF XAML file into Avalonia XAML: namespaces, controls and properties, styles and triggers,
/// resources and templates, bindings and markup extensions. Whatever has no exact Avalonia counterpart stays out
/// of the XAML and gets a <c>TODO(port)</c> comment and a port note, so the file still compiles.
/// </summary>
/// <remarks>The parts of the conversion are in <c>WpfXamlConverter.*.cs</c>.</remarks>
internal sealed partial class WpfXamlConverter
{
    /// <summary>The prefix of the namespace of the port's support code, such as <c>AppWindow</c>.</summary>
    private const string SupportPrefix = "app";

    private static readonly XName ClassAttribute = XamlNamespaces.Xaml + "Class";
    private static readonly XName KeyAttribute = XamlNamespaces.Xaml + "Key";
    private static readonly XName NameAttribute = XamlNamespaces.Xaml + "Name";

    private readonly WpfProject _project;
    private readonly string _sourcePath;
    private readonly FileReport _report;
    private readonly XElement _root;
    private readonly string _rootType;
    private readonly XamlClassInfo? _class;
    private XNamespace? _supportNamespace;
    private int _generatedNames;

    private WpfXamlConverter(WpfProject project, string sourcePath, XDocument document)
    {
        _project = project;
        _sourcePath = sourcePath;
        _root = document.Root!;
        _rootType = _root.Name.LocalName;
        _report = project.Context.Report(WpfProject.TargetPath(sourcePath));
        if (_root.Attribute(ClassAttribute)?.Value is { } className)
        {
            _class = new XamlClassInfo(className, _rootType);
            project.Classes[className] = _class;
        }
    }

    /// <summary>The Avalonia namespace of the port's support code, declared on the root when first needed.</summary>
    private XNamespace Support
    {
        get
        {
            if (_supportNamespace is null)
            {
                _supportNamespace = XamlNamespaces.Using(_project.Context.SupportNamespace);
                var prefix = SupportPrefix;
                while (_root.GetNamespaceOfPrefix(prefix) is { } taken && taken != _supportNamespace)
                {
                    prefix += "_";
                }

                if (_root.GetNamespaceOfPrefix(prefix) is null)
                {
                    _root.Add(new XAttribute(XNamespace.Xmlns + prefix, _supportNamespace.NamespaceName));
                }
            }

            return _supportNamespace;
        }
    }

    /// <summary>Converts a XAML file of the project.</summary>
    /// <param name="project">The project.</param>
    /// <param name="sourcePath">The file's path in the original project.</param>
    public static ConvertedXaml Convert(WpfProject project, string sourcePath)
    {
        var document = project.Xaml[sourcePath];
        var converter = new WpfXamlConverter(project, sourcePath, document);
        converter.ConvertDocument();
        return new ConvertedXaml(converter._report.Path, sourcePath, document, XamlFormat.Of(project.Context.Source.Files[sourcePath]), converter._report);
    }

    private void ConvertDocument()
    {
        ConvertNamespaces();
        switch (_rootType)
        {
            case "Window":
                ConvertWindowRoot();
                break;
            case "Page":
                _root.Name = XamlNamespaces.Avalonia + "UserControl";
                Note(_root, "Page", "Avalonia has no pages or page navigation; the page became a UserControl. Show it in a ContentControl whose content the view model changes");
                break;
            case "Application":
                ConvertApplicationRoot();
                break;
            case "ResourceDictionary":
                ConvertDictionaryRoot();
                return;
        }

        ConvertAttributes(_root, Scope.Root);
        ConvertChildren(_root, Scope.Root);
    }

    /// <summary>Avalonia's namespaces: its own for WPF's controls, and <c>using:</c> for CLR namespaces.</summary>
    private void ConvertNamespaces()
    {
        var renames = new Dictionary<XNamespace, XNamespace>();
        foreach (var declaration in _root.Attributes().Where(a => a.IsNamespaceDeclaration).ToList())
        {
            var value = declaration.Value;
            XNamespace? replacement = null;
            if (value == XamlNamespaces.Wpf.NamespaceName)
            {
                replacement = XamlNamespaces.Avalonia;
            }
            else if (XamlNamespaces.ClrNamespace(value) is { } clr && value.StartsWith("clr-namespace:", StringComparison.Ordinal))
            {
                replacement = XamlNamespaces.Using(clr);
                if (XamlNamespaces.ClrAssembly(value) is { } assembly && !IsBaseLibrary(assembly) && assembly != _project.Context.Source.Name)
                {
                    Note(_root, $"xmlns:{declaration.Name.LocalName} from {assembly}", $"The types come from {assembly}, which must work on Avalonia too: port that project or reference its Avalonia version");
                }
            }
            else if (value.StartsWith("http://schemas.microsoft.com/xaml/behaviors", StringComparison.Ordinal))
            {
                Note(_root, "Microsoft.Xaml.Behaviors", "Use the Avalonia.Xaml.Behaviors package, whose interactions and behaviors have the same names, with its own xmlns");
            }

            if (replacement is not null && replacement.NamespaceName != value)
            {
                renames[XNamespace.Get(value)] = replacement;
                declaration.Value = replacement.NamespaceName;
                _report.Converted(value == XamlNamespaces.Wpf.NamespaceName ? "WPF xmlns → Avalonia xmlns" : "clr-namespace → using:");
            }
        }

        foreach (var element in _root.DescendantsAndSelf())
        {
            if (renames.TryGetValue(element.Name.Namespace, out var renamed))
            {
                element.Name = renamed + element.Name.LocalName;
            }

            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && renames.ContainsKey(a.Name.Namespace)).ToList())
            {
                ReplaceAttribute(element, attribute, new XAttribute(renames[attribute.Name.Namespace] + attribute.Name.LocalName, attribute.Value));
            }
        }
    }

    private static bool IsBaseLibrary(string assembly) =>
        assembly is "mscorlib" or "System" or "System.Runtime" or "System.Core" or "netstandard" or "System.Collections" or "System.ObjectModel";

    private void ConvertChildren(XElement element, Scope scope)
    {
        foreach (var child in element.Elements().ToList())
        {
            if (child.Parent is not null)
            {
                ConvertElement(child, scope);
            }
        }
    }

    /// <summary>The context an element is converted in: inside a template, and of which control.</summary>
    /// <param name="TemplateTarget">The Avalonia type a surrounding control template is for.</param>
    /// <param name="InDataTemplate">Whether the element is inside a data template.</param>
    private sealed record Scope(string? TemplateTarget = null, bool InDataTemplate = false)
    {
        /// <summary>The scope of the file's root.</summary>
        public static Scope Root { get; } = new();
    }
}
