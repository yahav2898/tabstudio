using System.Xml.Linq;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>
/// Templates: a control template's triggers become styles that reach into the template (<c>/template/</c>), a data
/// template's data triggers become style classes its elements bind, and <c>{x:Type}</c> data types become plain
/// type names.
/// </summary>
internal sealed partial class WpfXamlConverter
{
    /// <summary>Controls whose template shows their content with a ContentPresenter, which Avalonia binds explicitly.</summary>
    private static readonly HashSet<string> ContentControls = new(StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "RepeatButton", "CheckBox", "RadioButton", "ContentControl", "Label", "ListBoxItem",
        "ComboBoxItem", "TreeViewItem", "TabItem", "Expander", "HeaderedContentControl", "ToolTip", "UserControl", "MenuItem",
    };

    /// <summary>
    /// Converts a control template in place and returns the styles its triggers became, which belong next to the
    /// style that sets the template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="defaultTarget">The Avalonia type it is for, when it does not say.</param>
    /// <param name="styleSelector">The selector of the style that sets it, or <see langword="null"/> when it is not in a style.</param>
    private List<XElement> ConvertControlTemplate(XElement template, string? defaultTarget, string? styleSelector)
    {
        var target = Value(template, "TargetType") is { } type ? MapTypeName(type) : defaultTarget;
        if (target is not null)
        {
            template.SetAttributeValue("TargetType", target);
        }

        var targetWpf = Value(template, "TargetType");
        foreach (var presenter in template.Descendants(XamlNamespaces.Avalonia + "ContentPresenter"))
        {
            ConvertContentPresenter(presenter, target);
        }

        var extra = new List<XElement>();
        if (template.Element(XamlNamespaces.Avalonia + "ControlTemplate.Triggers") is { } triggers)
        {
            foreach (var trigger in triggers.Elements().ToList())
            {
                if (styleSelector is null)
                {
                    Note(template, trigger.Name.LocalName + DescribeTrigger(trigger), "This template is not set by a style, so its triggers have no selector to start from; set the template in a style, and the trigger becomes a style of its own", Original(trigger));
                    continue;
                }

                ConvertTemplateTrigger(template, trigger, styleSelector, extra);
            }

            triggers.Remove();
        }

        _report.Converted("ControlTemplate");
        ConvertChildren(template, new Scope(TemplateTarget: target ?? targetWpf));
        return extra;
    }

    /// <summary>Avalonia's ContentPresenter shows nothing unless it is bound to the content.</summary>
    private void ConvertContentPresenter(XElement presenter, string? target)
    {
        var source = presenter.Attribute("ContentSource");
        source?.Remove();
        if (target is null || !ContentControls.Contains(target) || presenter.Attribute("Content") is not null)
        {
            return;
        }

        var property = source?.Value ?? "Content";
        presenter.SetAttributeValue("Content", $"{{TemplateBinding {property}}}");
        if (presenter.Attribute("ContentTemplate") is null)
        {
            presenter.SetAttributeValue("ContentTemplate", $"{{TemplateBinding {property}Template}}");
        }

        _report.Converted("ContentPresenter bound to the content, as Avalonia needs");
    }

    /// <summary>A template trigger becomes a style: on the control, or on a named part of its template.</summary>
    private void ConvertTemplateTrigger(XElement template, XElement trigger, string styleSelector, List<XElement> extra)
    {
        string? suffix = trigger.Name.LocalName switch
        {
            "Trigger" => TriggerSuffix(Value(trigger, "Property"), Value(trigger, "Value")),
            "MultiTrigger" => trigger.Elements(XamlNamespaces.Avalonia + "MultiTrigger.Conditions").Elements()
                .Select(c => TriggerSuffix(Value(c, "Property"), Value(c, "Value"))).ToList() is { Count: > 0 } all && all.All(c => c is not null)
                ? string.Concat(all) : null,
            _ => null,
        };

        if (suffix is null)
        {
            Note(template, trigger.Name.LocalName + DescribeTrigger(trigger), "Only property triggers with a pseudo-class or a property selector become styles of a template", Original(trigger));
            return;
        }

        var byPart = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var setter in trigger.Elements(XamlNamespaces.Avalonia + "Setter").ToList())
        {
            var selector = styleSelector + suffix;
            if (setter.Attribute("TargetName") is { } targetName)
            {
                var part = template.Descendants().FirstOrDefault(e => (e.Attribute(NameAttribute) ?? e.Attribute("Name"))?.Value == targetName.Value);
                selector += $" /template/ {part?.Name.LocalName ?? string.Empty}#{targetName.Value}";
                targetName.Remove();
            }

            if (!byPart.TryGetValue(selector, out var style))
            {
                style = new XElement(XamlNamespaces.Avalonia + "Style", new XAttribute("Selector", selector));
                byPart[selector] = style;
                extra.Add(style);
            }

            setter.Remove();
            style.Add(setter);
            ConvertSetter(setter, null, null, styleSelector, extra);
        }

        _report.Converted("Template trigger → style reaching into the template");
    }

    /// <summary>
    /// A data template: <c>{x:Type}</c> data types become type names, a hierarchical template a tree template, and
    /// data triggers style classes that the template's elements bind.
    /// </summary>
    private void ConvertDataTemplate(XElement template, Scope scope)
    {
        if (template.Attribute("DataType") is { } dataType)
        {
            dataType.Value = MapTypeName(dataType.Value);
            _report.Converted("DataTemplate DataType {x:Type} → type name");
        }

        if (template.Name.LocalName == "HierarchicalDataTemplate")
        {
            template.Name = XamlNamespaces.Avalonia + "TreeDataTemplate";
            _report.Converted("HierarchicalDataTemplate → TreeDataTemplate");
            if (template.Attribute("ItemTemplate") is { } itemTemplate)
            {
                Note(template, "HierarchicalDataTemplate ItemTemplate", "Avalonia's tree templates choose the next level's template by its DataType", Original(itemTemplate));
                itemTemplate.Remove();
            }
        }

        var triggers = template.Element(template.Name.Namespace + (template.Name.LocalName + ".Triggers"))
            ?? template.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Template.Triggers", StringComparison.Ordinal));
        triggers?.Remove();
        ConvertChildren(template, scope with { InDataTemplate = true });

        foreach (var trigger in triggers?.Elements().ToList() ?? [])
        {
            ConvertDataTemplateTrigger(template, trigger);
        }
    }

    /// <summary>A data trigger of a data template: its elements bind a style class, and a style sets the values.</summary>
    private void ConvertDataTemplateTrigger(XElement template, XElement trigger)
    {
        var root = template.Elements().FirstOrDefault(e => !e.Name.LocalName.Contains('.', StringComparison.Ordinal));
        List<ClassCondition?> conditions = trigger.Name.LocalName switch
        {
            "DataTrigger" => [WpfProject.Condition(Value(trigger, "Binding") ?? string.Empty, Value(trigger, "Value") ?? string.Empty)],
            "MultiDataTrigger" => trigger.Elements(XamlNamespaces.Avalonia + "MultiDataTrigger.Conditions").Elements()
                .Select(c => WpfProject.Condition(Value(c, "Binding") ?? string.Empty, Value(c, "Value") ?? string.Empty)).ToList(),
            _ => [],
        };

        if (root is null || conditions.Count == 0 || conditions.Any(c => c is null))
        {
            Note(template, trigger.Name.LocalName + DescribeTrigger(trigger), trigger.Name.LocalName is "DataTrigger" or "MultiDataTrigger"
                ? "Only a binding to a boolean, or a comparison with {x:Null}, becomes a style class; bind the property with a converter instead"
                : "Avalonia has no such trigger in data templates; bind the property, or use a style with a pseudo-class", Original(trigger));
            return;
        }

        var classes = string.Concat(conditions.Select(c => "." + c!.ClassName));
        foreach (var setter in trigger.Elements(XamlNamespaces.Avalonia + "Setter").ToList())
        {
            var element = setter.Attribute("TargetName") is { } targetName
                ? template.Descendants().FirstOrDefault(e => (e.Attribute(NameAttribute) ?? e.Attribute("Name"))?.Value == targetName.Value)
                : root;
            setter.Attribute("TargetName")?.Remove();
            if (element is null)
            {
                Note(template, "Setter TargetName", "The named element is not in the template", Original(setter));
                continue;
            }

            foreach (var condition in conditions)
            {
                element.SetAttributeValue("Classes." + condition!.ClassName, condition.Binding);
            }

            // A style in the element's own styles, matching the element by its type and the classes.
            var styles = PropertyElement(element, "Styles");
            var selector = element.Name.LocalName.Contains(':', StringComparison.Ordinal) ? classes : element.Name.LocalName + classes;
            var style = styles.Elements().FirstOrDefault(s => Value(s, "Selector") == selector);
            if (style is null)
            {
                style = new XElement(XamlNamespaces.Avalonia + "Style", new XAttribute("Selector", selector));
                styles.Add(style);
            }

            setter.Remove();
            style.Add(setter);
            ConvertSetter(setter, null, element.Name.LocalName, selector, []);
        }

        _report.Converted("Data trigger in a data template → style class bound to the data");
    }
}
