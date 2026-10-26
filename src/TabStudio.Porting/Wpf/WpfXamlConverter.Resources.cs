using System.Xml.Linq;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>
/// Resources: Avalonia keeps styles in <c>Styles</c> and implicit data templates in <c>DataTemplates</c>, not in
/// resources, and includes dictionaries with <c>ResourceInclude</c> and <c>StyleInclude</c>.
/// </summary>
internal sealed partial class WpfXamlConverter
{
    /// <summary>Converts an element's <c>Resources</c>, moving its styles and implicit data templates out.</summary>
    /// <param name="resources">The <c>Owner.Resources</c> property element.</param>
    /// <param name="scope">Where it is.</param>
    private void ConvertResources(XElement resources, Scope scope)
    {
        var owner = resources.Parent!;
        ConvertResourceItems(resources, out var styles, out var templates, scope);
        if (styles.Count > 0)
        {
            var target = PropertyElement(owner, "Styles", resources);
            foreach (var style in styles)
            {
                target.Add(style);
            }

            _report.Converted("Styles in Resources → Styles", styles.OfType<XElement>().Count(s => s.Name.LocalName == "Style"));
        }

        if (templates.Count > 0)
        {
            var target = PropertyElement(owner, "DataTemplates", resources);
            foreach (var template in templates)
            {
                target.Add(template);
            }

            _report.Converted("Implicit DataTemplate in Resources → DataTemplates", templates.OfType<XElement>().Count());
        }

        RemoveIfEmpty(resources);
    }

    /// <summary>
    /// Converts the items of a resource dictionary in place. Styles (with the includes of dictionaries that hold
    /// styles) and implicit data templates are taken out, converted, for the caller to place.
    /// </summary>
    private void ConvertResourceItems(XElement container, out List<XNode> styles, out List<XNode> templates, Scope? scope = null)
    {
        styles = [];
        templates = [];
        scope ??= Scope.Root;
        foreach (var item in container.Elements().ToList())
        {
            var local = item.Name.LocalName;
            if (item.Name.Namespace != XamlNamespaces.Avalonia)
            {
                ConvertElement(item, scope);
                continue;
            }

            switch (local)
            {
                case "ResourceDictionary.MergedDictionaries":
                    ConvertMergedDictionaries(item, styles);
                    break;
                case "ResourceDictionary" when item.Attribute(KeyAttribute) is null && item.Attribute("Source") is null:
                    ConvertResourceItems(item, out var nestedStyles, out var nestedTemplates, scope);
                    styles.AddRange(nestedStyles);
                    templates.AddRange(nestedTemplates);
                    RemoveIfEmpty(item);
                    break;
                case "Style":
                {
                    var before = item.PreviousNode;
                    var converted = ConvertStyle(item);
                    styles.AddRange(Detach(item, before, converted.Skip(1)));
                    break;
                }

                case "DataTemplate" or "HierarchicalDataTemplate" when item.Attribute(KeyAttribute) is null && item.Attribute("DataType") is not null:
                {
                    var before = item.PreviousNode;
                    ConvertElement(item, scope);
                    templates.AddRange(Detach(item, before, []));
                    break;
                }

                case "BooleanToVisibilityConverter" when item.Attribute(KeyAttribute)?.Value is { } key && _project.VisibilityConverterKeys.Contains(key):
                    item.Remove();
                    _report.Converted("BooleanToVisibilityConverter removed: IsVisible takes the boolean");
                    break;
                default:
                    ConvertElement(item, scope);
                    break;
            }
        }
    }

    /// <summary>
    /// Merged dictionaries: one with styles is included in the styles (<c>StyleInclude</c>), any other stays a merged
    /// dictionary (<c>ResourceInclude</c>), each with its file's new address.
    /// </summary>
    private void ConvertMergedDictionaries(XElement merged, List<XNode> styles)
    {
        foreach (var include in merged.Elements().ToList())
        {
            if (include.Name != XamlNamespaces.Avalonia + "ResourceDictionary" || Value(include, "Source") is not { } source)
            {
                ConvertElement(include, Scope.Root);
                continue;
            }

            var path = ResolvePath(source);
            var address = AppUri(source).StartsWith("avares://", StringComparison.Ordinal) ? WpfProject.TargetPath(AppUri(source)) : "/" + WpfProject.TargetPath(path);
            if (!_project.Xaml.ContainsKey(path) && !address.StartsWith("avares://", StringComparison.Ordinal))
            {
                Note(include, $"ResourceDictionary Source=\"{source}\"", "The dictionary is not in this project; include its Avalonia version with ResourceInclude or StyleInclude", Original(include));
                include.Remove();
                continue;
            }

            if (_project.StyleDictionaries.Contains(path))
            {
                include.Remove();
                styles.Add(new XElement(XamlNamespaces.Avalonia + "StyleInclude", new XAttribute("Source", address)));
                _report.Converted("Merged dictionary with styles → StyleInclude");
            }
            else
            {
                include.Name = XamlNamespaces.Avalonia + "ResourceInclude";
                include.SetAttributeValue("Source", address);
                _report.Converted("Merged dictionary → ResourceInclude");
            }
        }

        RemoveIfEmpty(merged);
    }

    /// <summary>
    /// Takes a converted element out of the tree with the TODO comments the conversion put in front of it (the
    /// nodes after <paramref name="before"/>), followed by elements the conversion made from it.
    /// </summary>
    private static List<XNode> Detach(XElement element, XNode? before, IEnumerable<XElement> extras)
    {
        var nodes = new List<XNode>();
        var node = before is null ? element.Parent?.FirstNode ?? element : before.NextNode;
        while (node is not null && !ReferenceEquals(node, element))
        {
            if (node is XComment)
            {
                nodes.Add(node);
            }

            node = node.NextNode;
        }

        foreach (var comment in nodes)
        {
            comment.Remove();
        }

        element.Remove();
        nodes.Add(element);
        nodes.AddRange(extras);
        return nodes;
    }

    /// <summary>Removes an element that has nothing left in it, such as resources whose styles moved.</summary>
    private static void RemoveIfEmpty(XElement element)
    {
        if (!element.Elements().Any() && element.Attributes().All(a => a.IsNamespaceDeclaration) && string.IsNullOrWhiteSpace(element.Value))
        {
            // Keep a comment that explains something with the parent.
            foreach (var comment in element.Nodes().OfType<XComment>().ToList())
            {
                comment.Remove();
                element.AddBeforeSelf(comment);
            }

            element.Remove();
        }
    }
}
