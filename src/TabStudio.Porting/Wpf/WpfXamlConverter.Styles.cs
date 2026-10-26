using System.Text.RegularExpressions;
using System.Xml.Linq;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>
/// Styles: WPF's <c>TargetType</c> and <c>x:Key</c> become an Avalonia selector (a keyed style becomes a style class
/// that the elements using it get), and triggers become styles of their own: property triggers with a pseudo-class
/// or a property selector, data triggers of keyed styles with a class the element binds.
/// </summary>
internal sealed partial class WpfXamlConverter
{
    /// <summary>Properties the Fluent theme sets on a template part in the pointer-over, pressed and disabled states.</summary>
    private static readonly HashSet<string> StateProperties = new(StringComparer.Ordinal) { "Background", "BorderBrush", "Foreground" };

    /// <summary>The attributes that describe a trigger in a note.</summary>
    private static readonly string[] TriggerAttributes = ["Property", "Binding", "Value", "RoutedEvent"];

    /// <summary>
    /// Converts a style in place to an Avalonia style, and returns it followed by the styles its triggers became.
    /// </summary>
    /// <param name="style">The style, still in the tree.</param>
    /// <param name="selectorOverride">The selector to use, for item container and inline styles.</param>
    /// <param name="isContainerStyle">Whether the style is for containers the control creates, such as rows.</param>
    private List<XElement> ConvertStyle(XElement style, string? selectorOverride = null, bool isContainerStyle = false)
    {
        var key = style.Attribute(KeyAttribute)?.Value;
        var targetWpf = Value(style, "TargetType") is { } type ? WpfProject.TypeName(type) : null;
        var target = targetWpf is null ? null : MapTypeName(targetWpf);
        var selector = selectorOverride ?? StyleSelector(key, targetWpf, target);
        if (selectorOverride is null && key is null && target is null)
        {
            Note(style, "Style without TargetType or x:Key", "Give the style a Selector");
        }

        // The selector takes the place of TargetType, where the reader expects it.
        var anchor = style.Attribute("TargetType") ?? style.Attribute(KeyAttribute);
        if (anchor is not null)
        {
            Rename(style, anchor, "Selector", selector);
        }
        else
        {
            style.SetAttributeValue("Selector", selector);
        }

        style.Attribute(KeyAttribute)?.Remove();
        style.Attribute("TargetType")?.Remove();
        if (style.Attribute("BasedOn") is { } basedOn)
        {
            // An element using the style gets the classes of every keyed style it is based on.
            basedOn.Remove();
            _report.Converted("BasedOn → the base style's class on the element");
        }

        _report.Converted(key is null ? "Style TargetType → selector" : "Keyed style → style class");

        var hasTemplate = style.Elements(XamlNamespaces.Avalonia + "Setter").Any(s => Value(s, "Property") == "Template");
        var extra = new List<XElement>();
        foreach (var setter in style.Elements().Where(e => e.Name.LocalName is "Setter" or "EventSetter").ToList())
        {
            ConvertSetter(setter, targetWpf, target, selector, extra);
        }

        if (style.Element(XamlNamespaces.Avalonia + "Style.Resources") is { } resources)
        {
            ConvertChildren(resources, Scope.Root);
        }

        if (style.Element(XamlNamespaces.Avalonia + "Style.Triggers") is { } triggers)
        {
            foreach (var trigger in triggers.Elements().ToList())
            {
                ConvertStyleTrigger(style, trigger, selector, key, target, hasTemplate, isContainerStyle, extra);
            }

            foreach (var comment in triggers.Nodes().OfType<XComment>().ToList())
            {
                comment.Remove();
                style.AddBeforeSelf(comment);
            }

            triggers.Remove();
        }

        return [style, .. extra];
    }

    /// <summary>The selector of a style: its type, and its class when it has a key.</summary>
    private string StyleSelector(string? key, string? targetWpf, string? target)
    {
        if (key is null)
        {
            return target ?? "Control";
        }

        var className = _project.Styles.TryGetValue(key, out var info) ? info.ClassName : WpfProject.ClassName(key);
        return targetWpf switch
        {
            null => "." + className,
            "FrameworkElement" or "UIElement" or "Control" => $":is({target}).{className}",
            _ => $"{target}.{className}",
        };
    }

    /// <summary>Converts a setter: its property, its value, and a control template it sets.</summary>
    private void ConvertSetter(XElement setter, string? targetWpf, string? target, string selector, List<XElement> extra)
    {
        if (setter.Name.LocalName == "EventSetter")
        {
            Note(setter, $"EventSetter {Value(setter, "Event")}", "Avalonia styles cannot set events; subscribe in code-behind, or add a class handler with AddClassHandler", Original(setter));
            setter.Remove();
            return;
        }

        if (setter.Attribute("Property") is not { } property)
        {
            return;
        }

        // WPF allows the owner in the name (Control.Background); Avalonia resolves the property on the target.
        var name = property.Value;
        if (name.StartsWith("Control.", StringComparison.Ordinal) || name.StartsWith("FrameworkElement.", StringComparison.Ordinal)
            || (targetWpf is not null && name.StartsWith(targetWpf + ".", StringComparison.Ordinal)))
        {
            name = name[(name.IndexOf('.', StringComparison.Ordinal) + 1)..];
        }

        if (WpfAttributes.Dropped.Contains(name))
        {
            setter.Remove();
            _report.Converted("WPF-only setting removed");
            return;
        }

        if (WpfAttributes.Unsupported.TryGetValue(name, out var why))
        {
            Note(setter, $"Setter {name}", why, Original(setter));
            setter.Remove();
            return;
        }

        if (setter.Attribute("TargetName") is { } targetName)
        {
            Note(setter, $"Setter TargetName=\"{targetName.Value}\"", "Only a template's triggers can set a property of a named part; use a selector with /template/ #" + targetName.Value, Original(setter));
            setter.Remove();
            return;
        }

        if (name == "Visibility")
        {
            ConvertVisibilitySetter(setter);
            return;
        }

        if (WpfAttributes.Renamed.TryGetValue(name, out var renamed))
        {
            name = renamed;
            _report.Converted($"{property.Value} → {renamed}");
        }

        property.Value = name;
        if (setter.Attribute("Value") is { } value)
        {
            if (ConvertPropertyValue(setter, name, value.Value) is { } converted)
            {
                value.Value = converted;
            }
            else
            {
                setter.Remove();
                return;
            }
        }

        // A template, or any other object, set as the setter's value.
        foreach (var child in setter.Elements().ToList())
        {
            var content = child.Name.LocalName == "Setter.Value" ? child.Elements().ToList() : [child];
            foreach (var item in content)
            {
                if (item.Name == XamlNamespaces.Avalonia + "ControlTemplate")
                {
                    extra.AddRange(ConvertControlTemplate(item, target, selector));
                }
                else
                {
                    ConvertElement(item, Scope.Root);
                }
            }
        }
    }

    /// <summary>A setter of Visibility sets IsVisible, or the Opacity for Hidden.</summary>
    private void ConvertVisibilitySetter(XElement setter)
    {
        var value = setter.Attribute("Value");
        switch (value?.Value.Trim())
        {
            case "Visible":
                setter.SetAttributeValue("Property", "IsVisible");
                value.Value = "True";
                break;
            case "Collapsed":
                setter.SetAttributeValue("Property", "IsVisible");
                value.Value = "False";
                break;
            case "Hidden":
                Note(setter, "Visibility.Hidden setter", "Opacity preserves layout but not hidden input or focus semantics; add IsHitTestVisible and focus rules to this selector");
                setter.SetAttributeValue("Property", "Opacity");
                value.Value = "0";
                break;
            default:
                setter.SetAttributeValue("Property", "IsVisible");
                if (value is not null && MarkupExtension.Parse(value.Value) is { } markup)
                {
                    value.Value = VisibilityBinding(markup) ?? value.Value;
                }

                break;
        }

        _report.Converted("Visibility → IsVisible");
    }

    /// <summary>Turns a style trigger into styles of its own.</summary>
    private void ConvertStyleTrigger(XElement style, XElement trigger, string selector, string? key, string? target, bool hasTemplate, bool isContainerStyle, List<XElement> extra)
    {
        var kind = trigger.Name.LocalName;
        string? suffix = null;
        string? why = null;
        switch (kind)
        {
            case "Trigger":
                suffix = TriggerSuffix(Value(trigger, "Property"), Value(trigger, "Value"));
                why = "There is no pseudo-class or property selector for this property and value";
                break;
            case "MultiTrigger":
                var conditions = trigger.Elements(XamlNamespaces.Avalonia + "MultiTrigger.Conditions").Elements()
                    .Select(c => TriggerSuffix(Value(c, "Property"), Value(c, "Value"))).ToList();
                suffix = conditions.Count > 0 && conditions.All(c => c is not null) ? string.Concat(conditions) : null;
                why = "There is no pseudo-class or property selector for one of its conditions";
                break;
            case "DataTrigger" when key is not null && !isContainerStyle:
                suffix = DataTriggerSuffix(key, Value(trigger, "Binding"), Value(trigger, "Value"));
                why = "Only a binding to a boolean, or a comparison with {x:Null}, becomes a style class";
                break;
            case "MultiDataTrigger" when key is not null && !isContainerStyle:
                var classes = trigger.Elements(XamlNamespaces.Avalonia + "MultiDataTrigger.Conditions").Elements()
                    .Select(c => DataTriggerSuffix(key, Value(c, "Binding"), Value(c, "Value"))).ToList();
                suffix = classes.Count > 0 && classes.All(c => c is not null) ? string.Concat(classes) : null;
                why = "Only bindings to booleans, or comparisons with {x:Null}, become style classes";
                break;
            case "DataTrigger" or "MultiDataTrigger":
                why = isContainerStyle
                    ? "The control creates these containers, so they cannot bind a style class; bind the property in the container's style with a converter instead, such as <Setter Property=\"FontWeight\" Value=\"{Binding IsFavorite, Converter=...}\"/>"
                    : "A style without a key applies to elements the converter cannot see; give it a key, or bind the property with a converter";
                break;
            case "EventTrigger":
                why = "Avalonia animates with Animation and KeyFrame in a style, started by a pseudo-class or a class";
                break;
        }

        if (suffix is null)
        {
            Note(style, kind + DescribeTrigger(trigger), why ?? "Avalonia has no such trigger", Original(trigger));
            return;
        }

        if (trigger.Elements().Any(e => e.Name.LocalName.EndsWith(".EnterActions", StringComparison.Ordinal) || e.Name.LocalName.EndsWith(".ExitActions", StringComparison.Ordinal)))
        {
            Note(style, kind + " animations" + DescribeTrigger(trigger), "Avalonia animates with Animation and KeyFrame in the trigger's style", Original(trigger));
        }

        var setters = trigger.Elements(XamlNamespaces.Avalonia + "Setter").ToList();
        var onControl = new XElement(XamlNamespaces.Avalonia + "Style", new XAttribute("Selector", selector + suffix));
        var onPart = target is not null && !hasTemplate && WpfAttributes.StatePart.TryGetValue(target, out var part) && suffix.StartsWith(':')
            ? new XElement(XamlNamespaces.Avalonia + "Style", new XAttribute("Selector", $"{selector}{suffix} /template/ {part}"))
            : null;
        foreach (var setter in setters)
        {
            setter.Remove();
            (onPart is not null && StateProperties.Contains(Value(setter, "Property") ?? string.Empty) ? onPart : onControl).Add(setter);
        }

        foreach (var created in (XElement?[])[onControl, onPart])
        {
            if (created is null || !created.Elements().Any())
            {
                continue;
            }

            // The setters are converted in their new style, so their notes stay with them.
            foreach (var setter in created.Elements().ToList())
            {
                ConvertSetter(setter, null, target, selector, extra);
            }

            extra.Add(created);
        }

        _report.Converted(kind switch
        {
            "Trigger" or "MultiTrigger" => "Property trigger → style with a pseudo-class or property selector",
            _ => "Data trigger → style class bound to the data",
        });
    }

    /// <summary>The selector suffix of a property trigger: a pseudo-class, or a property selector.</summary>
    private static string? TriggerSuffix(string? property, string? value)
    {
        if (property is null || value is null)
        {
            return null;
        }

        var normalized = value.Trim() switch
        {
            var v when v.Equals("true", StringComparison.OrdinalIgnoreCase) => "True",
            var v when v.Equals("false", StringComparison.OrdinalIgnoreCase) => "False",
            var v => v,
        };

        if (WpfAttributes.PseudoClasses.TryGetValue($"{property}={normalized}", out var pseudo))
        {
            return pseudo;
        }

        if (property == "Visibility")
        {
            return normalized switch
            {
                "Visible" => "[IsVisible=True]",
                "Collapsed" => "[IsVisible=False]",
                _ => null,
            };
        }

        return !property.Contains('.', StringComparison.Ordinal) && SimpleValue().IsMatch(normalized) ? $"[{property}={normalized}]" : null;
    }

    /// <summary>The class a keyed style's data trigger condition sets on its elements.</summary>
    private string? DataTriggerSuffix(string key, string? binding, string? value)
    {
        if (binding is null || value is null || !_project.Styles.TryGetValue(key, out var style))
        {
            return null;
        }

        var condition = WpfProject.Condition(binding, value);
        return condition is not null && style.Conditions.Any(c => c.ClassName == condition.ClassName) ? "." + condition.ClassName : null;
    }

    private static string DescribeTrigger(XElement trigger)
    {
        var parts = TriggerAttributes
            .Select(n => Value(trigger, n) is { } v ? $"{n}=\"{v}\"" : null)
            .Where(p => p is not null);
        var text = string.Join(' ', parts);
        return text.Length == 0 ? string.Empty : " " + text;
    }

    [GeneratedRegex(@"^[\w.#+-]+$")]
    private static partial Regex SimpleValue();
}
