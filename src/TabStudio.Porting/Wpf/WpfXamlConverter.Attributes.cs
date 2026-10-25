using System.Collections.Frozen;
using System.Xml.Linq;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>
/// Attributes: Visibility becomes IsVisible, styles by key become classes, events get Avalonia's names, and
/// bindings and markup extensions lose what only WPF has.
/// </summary>
internal sealed partial class WpfXamlConverter
{
    /// <summary>Binding settings Avalonia does without: it validates data and notifies by itself.</summary>
    private static readonly FrozenSet<string> WpfOnlyBindingSettings = FrozenSet.Create(StringComparer.Ordinal,
    [
        "ValidatesOnDataErrors", "ValidatesOnExceptions", "ValidatesOnNotifyDataErrors", "NotifyOnValidationError",
        "NotifyOnSourceUpdated", "NotifyOnTargetUpdated", "BindsDirectlyToSource", "BindingGroupName", "AsyncState",
        "UpdateSourceExceptionFilter",
    ]);

    /// <summary>Static classes of WPF whose members (system colors, routed commands) Avalonia does not have.</summary>
    private static readonly string[] SystemStatics =
    [
        "SystemColors.", "SystemParameters.", "SystemFonts.", "SystemCommands.", "ApplicationCommands.", "NavigationCommands.",
        "MediaCommands.", "EditingCommands.", "ComponentCommands.",
    ];

    /// <summary>WPF's tunneling events with Avalonia routed events to add their handlers to with the Tunnel strategy.</summary>
    private static readonly FrozenDictionary<string, string> TunnelEvents = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PreviewKeyDown"] = "InputElement.KeyDownEvent",
        ["PreviewKeyUp"] = "InputElement.KeyUpEvent",
        ["PreviewTextInput"] = "InputElement.TextInputEvent",
        ["PreviewMouseDown"] = "InputElement.PointerPressedEvent",
        ["PreviewMouseUp"] = "InputElement.PointerReleasedEvent",
        ["PreviewMouseLeftButtonDown"] = "InputElement.PointerPressedEvent",
        ["PreviewMouseLeftButtonUp"] = "InputElement.PointerReleasedEvent",
        ["PreviewMouseRightButtonDown"] = "InputElement.PointerPressedEvent",
        ["PreviewMouseRightButtonUp"] = "InputElement.PointerReleasedEvent",
        ["PreviewMouseMove"] = "InputElement.PointerMovedEvent",
        ["PreviewMouseWheel"] = "InputElement.PointerWheelChangedEvent",
        ["PreviewMouseDoubleClick"] = "InputElement.DoubleTappedEvent",
        ["PreviewGotKeyboardFocus"] = "InputElement.GotFocusEvent",
        ["PreviewLostKeyboardFocus"] = "InputElement.LostFocusEvent",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Range controls, whose ValueChanged handlers take Avalonia's RangeBaseValueChangedEventArgs.</summary>
    private static readonly FrozenSet<string> RangeControls = FrozenSet.Create(StringComparer.Ordinal, ["Slider", "ScrollBar", "ProgressBar"]);

    /// <summary>Converts an element's attributes.</summary>
    private void ConvertAttributes(XElement element, Scope scope)
    {
        var type = element.Annotation<WpfType>()?.Name ?? element.Name.LocalName;
        foreach (var attribute in element.Attributes().ToList())
        {
            if (attribute.Parent is null || attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            var ns = attribute.Name.Namespace;
            if (ns == XamlNamespaces.Xaml)
            {
                switch (attribute.Name.LocalName)
                {
                    case "Uid":
                        attribute.Remove();
                        break;
                    case "Shared":
                        Note(element, "x:Shared", WpfAttributes.Unsupported["x:Shared"], Original(attribute));
                        attribute.Remove();
                        break;
                }

                continue;
            }

            if (ns == XamlNamespaces.Blend)
            {
                if (attribute.Name.LocalName == "DataContext" && attribute.Value.Contains("DesignInstance", StringComparison.Ordinal))
                {
                    attribute.Remove();
                    _report.Converted("d:DesignInstance removed: Avalonia sets design data with Design.DataContext");
                }

                continue;
            }

            if (ns == XNamespace.None)
            {
                ConvertAttribute(element, attribute, type, scope);
            }
        }
    }

    private void ConvertAttribute(XElement element, XAttribute attribute, string type, Scope scope)
    {
        var name = attribute.Name.LocalName;
        if (name == "Visibility")
        {
            ConvertVisibility(element, attribute);
            return;
        }

        if (name == "Style")
        {
            ConvertStyleReference(element, attribute);
            return;
        }

        if (ConvertEvent(element, attribute, type))
        {
            return;
        }

        if (WpfAttributes.Dropped.Contains(name))
        {
            attribute.Remove();
            _report.Converted("WPF-only setting removed");
            return;
        }

        if (WpfAttributes.Unsupported.TryGetValue(name, out var why) && (name is not ("Left" or "Top" or "WindowStyle") || element == _root))
        {
            Note(element, Original(attribute), why);
            attribute.Remove();
            return;
        }

        if (name == "Command" && SystemStatics.Any(s => attribute.Value.StartsWith(s, StringComparison.Ordinal)))
        {
            Note(element, Original(attribute), "Avalonia has no built-in routed commands; bind the command to one of the view model, or handle Click");
            attribute.Remove();
            return;
        }

        if (type is "TextBox" or "PasswordBox" && name is "VerticalScrollBarVisibility" or "HorizontalScrollBarVisibility")
        {
            attribute = Rename(element, attribute, "ScrollViewer." + name);
            _report.Converted($"TextBox {name} → ScrollViewer.{name}");
        }
        else if (name is "DisplayMemberPath" or "SelectedValuePath")
        {
            var property = name == "DisplayMemberPath" ? "DisplayMemberBinding" : "SelectedValueBinding";
            Rename(element, attribute, property, $"{{Binding {attribute.Value}}}");
            _report.Converted($"{name} → {property}");
            return;
        }
        else if (name == "SelectionMode" && type is "ListBox" or "ListView" && element.Name.LocalName == "ListBox")
        {
            attribute.Value = attribute.Value switch
            {
                "Extended" => "Multiple",
                "Multiple" => "Multiple,Toggle",
                var other => other,
            };
            return;
        }
        else if (WpfAttributes.Renamed.TryGetValue(name, out var renamed))
        {
            attribute = Rename(element, attribute, renamed);
            _report.Converted($"{name} → {renamed}");
        }

        if (ConvertPropertyValue(element, attribute.Name.LocalName, attribute.Value) is { } value)
        {
            attribute.Value = value;
        }
        else
        {
            attribute.Remove();
        }
    }

    /// <summary>
    /// Converts a property value: markup extensions, sizes of Auto, transform origins, cursors and pack URIs.
    /// </summary>
    /// <returns>The Avalonia value, or <see langword="null"/> when the property should be left out.</returns>
    private string? ConvertPropertyValue(XElement element, string property, string value)
    {
        if (MarkupExtension.Parse(value) is { } markup)
        {
            return ConvertMarkup(element, property, markup);
        }

        switch (property)
        {
            case "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight" when value.Trim() == "Auto" && element.Name.LocalName is not ("RowDefinition" or "ColumnDefinition"):
                _report.Converted("Size of Auto removed: it is the default");
                return null;
            case "RenderTransformOrigin":
                return RelativePoint(value) is { } relative ? Counted(relative, "RenderTransformOrigin → relative point in percent") : value;
            case "Cursor" when WpfAttributes.Cursors.TryGetValue(value.Trim(), out var cursor):
                return cursor == value.Trim() ? value : Counted(cursor, "Cursor → Avalonia's cursor name");
            case "Source" or "ImageSource" or "Icon" or "NavigateUri" when value.Contains("component/", StringComparison.OrdinalIgnoreCase) || value.StartsWith("pack:", StringComparison.OrdinalIgnoreCase):
                return Counted(AppUri(value), "pack URI → app address");
            default:
                return value;
        }
    }

    private string Counted(string value, string description)
    {
        _report.Converted(description);
        return value;
    }

    /// <summary>
    /// Converts a markup extension: bindings lose WPF-only settings, types get Avalonia's names, and what refers to
    /// WPF's system colors or commands is left out with a note.
    /// </summary>
    /// <returns>The Avalonia markup, or <see langword="null"/> when the attribute should be left out.</returns>
    private string? ConvertMarkup(XElement element, string property, MarkupExtension markup)
    {
        switch (markup.Name)
        {
            case "Binding" or "MultiBinding":
                foreach (var setting in markup.Arguments.Where(a => a.Key is not null && WpfOnlyBindingSettings.Contains(a.Key)).ToList())
                {
                    markup.Arguments.Remove(setting);
                    _report.Converted("WPF-only binding setting removed");
                }

                if (markup.GetText("UpdateSourceTrigger") is "PropertyChanged" or "Default")
                {
                    markup.Remove("UpdateSourceTrigger");
                    _report.Converted("UpdateSourceTrigger=PropertyChanged removed: it is Avalonia's default");
                }

                if (markup.Find("IsAsync") is { } isAsync)
                {
                    Note(element, $"{property} binding with IsAsync", "Avalonia binds tasks with the ^ operator: make the property a Task and bind Path=Value^");
                    markup.Arguments.Remove(isAsync);
                }

                if (markup.Find("XPath") is not null)
                {
                    Note(element, $"{property}=\"{markup}\"", "Avalonia has no XML data binding; load the XML into objects in the view model");
                    return null;
                }

                break;
            case "TemplateBinding" when markup.Positional(0) is { Extension: null } templateProperty:
                if (templateProperty.Text == "Visibility")
                {
                    templateProperty.Text = "IsVisible";
                }

                break;
            case "RelativeSource":
                if (!ConvertRelativeSource(element, markup))
                {
                    return null;
                }

                break;
            case "x:Type" when markup.Positional(0) is { Extension: null } typeName:
                typeName.Text = MapTypeName(typeName.Text);
                break;
            case "x:Static" when markup.Positional(0) is { Extension: null } member && SystemStatics.Any(s => member.Text.StartsWith(s, StringComparison.Ordinal)):
                Note(element, $"{property}=\"{markup}\"", "Avalonia has no " + member.Text[..member.Text.IndexOf('.', StringComparison.Ordinal)] + "; use a theme resource such as {DynamicResource SystemControlForegroundBaseHighBrush}, or a value of your own");
                return null;
            case "x:Reference" or "x:Array" or "ComponentResourceKey" or "x:Code":
                Note(element, $"{property}=\"{markup}\"", markup.Name == "x:Reference"
                    ? "Avalonia refers to named elements in bindings: {Binding #Name} or ElementName=Name"
                    : $"Avalonia's XAML has no {markup.Name}");
                return null;
            case "d:DesignInstance":
                return null;
        }

        // Nested extensions: a converter's resource, a relative source, a static value.
        foreach (var argument in markup.Arguments.ToList())
        {
            if (argument.Value.Extension is { } nested)
            {
                if (ConvertMarkup(element, property, nested) is null)
                {
                    markup.Arguments.Remove(argument);
                    if (markup.Name is "StaticResource" or "DynamicResource")
                    {
                        return null;
                    }
                }
            }
        }

        return markup.ToString();
    }

    /// <summary>A relative source: an ancestor type gets its Avalonia name; WPF's PreviousData has no counterpart.</summary>
    /// <returns>Whether the relative source can stay.</returns>
    private bool ConvertRelativeSource(XElement element, MarkupExtension source)
    {
        if (source.Positional(0)?.Text == "PreviousData" || source.GetText("Mode") == "PreviousData")
        {
            Note(element, "RelativeSource PreviousData", "Avalonia has no PreviousData; give each item the value it needs from the previous one in the view model");
            return false;
        }

        if (source.Find("AncestorType") is { } ancestor)
        {
            if (ancestor.Value.Extension is { Name: "x:Type" } type && type.Positional(0) is { Extension: null } name)
            {
                name.Text = MapTypeName(name.Text);
            }
            else if (ancestor.Value.Extension is null)
            {
                ancestor.Value.Text = MapTypeName(ancestor.Value.Text);
            }

            _report.Converted("RelativeSource AncestorType → Avalonia type");
        }

        return true;
    }

    /// <summary>Visibility: Avalonia has IsVisible, a boolean; Hidden, which keeps the space, is transparent and does not take input.</summary>
    private void ConvertVisibility(XElement element, XAttribute attribute)
    {
        var value = attribute.Value.Trim();
        switch (value)
        {
            case "Visible":
                Rename(element, attribute, "IsVisible", "True");
                break;
            case "Collapsed":
                Rename(element, attribute, "IsVisible", "False");
                break;
            case "Hidden":
                ReplaceAttribute(element, attribute, new XAttribute("Opacity", "0"), new XAttribute("IsHitTestVisible", "False"));
                Note(element, "Visibility.Hidden", "Opacity preserves layout and suppresses pointer input; review keyboard focus and accessibility for hidden descendants");
                _report.Converted("Visibility Hidden → Opacity 0, without input");
                return;
            default:
                if (MarkupExtension.Parse(value) is { } markup && VisibilityBinding(markup) is { } binding)
                {
                    Rename(element, attribute, "IsVisible", binding);
                }
                else
                {
                    Note(element, Original(attribute), "Avalonia shows and hides elements with IsVisible, a boolean");
                    attribute.Remove();
                    return;
                }

                break;
        }

        _report.Converted("Visibility → IsVisible");
    }

    /// <summary>
    /// A binding of Visibility as a binding of IsVisible: a BooleanToVisibilityConverter is no longer needed, and any
    /// other converter returns a boolean once its code is ported.
    /// </summary>
    private string? VisibilityBinding(MarkupExtension markup)
    {
        if (markup.Name is "TemplateBinding" && markup.Positional(0) is { Extension: null } property && property.Text == "Visibility")
        {
            property.Text = "IsVisible";
        }

        if (markup.Name is not ("Binding" or "TemplateBinding" or "MultiBinding"))
        {
            return null;
        }

        if (markup.Find("Converter") is { Value.Extension: { Name: "StaticResource" or "DynamicResource" } converter } argument
            && converter.Positional(0) is { Extension: null } key && _project.VisibilityConverterKeys.Contains(key.Text))
        {
            markup.Arguments.Remove(argument);
            markup.Remove("ConverterParameter");
            _report.Converted("BooleanToVisibilityConverter removed: IsVisible takes the boolean");
        }

        return ConvertMarkup(_root, "IsVisible", markup);
    }

    /// <summary>
    /// Style="{StaticResource Key}": the element gets the classes of the style and of the keyed styles it is based on,
    /// and binds the classes the styles' data triggers use.
    /// </summary>
    private void ConvertStyleReference(XElement element, XAttribute attribute)
    {
        var key = WpfProject.ResourceKey(attribute.Value);
        if (key is null || !_project.Styles.TryGetValue(key, out var style))
        {
            var why = MarkupExtension.Parse(attribute.Value) is { Name: "x:Null" }
                ? "Avalonia has no way to turn implicit styles off for one element; give it a class and restyle it"
                : "The style is not in this project; give the element the class of its Avalonia version";
            Note(element, Original(attribute), why);
            attribute.Remove();
            return;
        }

        var chain = new List<WpfStyle>();
        for (var current = style; current is not null && !chain.Contains(current); current = current.BasedOnKey is { } basedOn && _project.Styles.TryGetValue(basedOn, out var next) ? next : null)
        {
            chain.Insert(0, current);
        }

        attribute.Remove();
        AddClasses(element, [.. chain.Select(s => s.ClassName)]);
        foreach (var condition in chain.SelectMany(s => s.Conditions))
        {
            element.SetAttributeValue("Classes." + condition.ClassName, condition.Binding);
        }

        _report.Converted("Style=\"{StaticResource}\" → Classes");
    }

    /// <summary>Converts an event attribute.</summary>
    /// <returns>Whether the attribute was an event.</returns>
    private bool ConvertEvent(XElement element, XAttribute attribute, string type)
    {
        var name = attribute.Name.LocalName;
        var handler = attribute.Value;
        if (WpfEvents.Renamed.TryGetValue(name, out var mapping))
        {
            Rename(element, attribute, mapping.Name);
            if (mapping.ArgsType is not null && _class is not null)
            {
                _class.HandlerArguments[handler] = mapping.ArgsType;
            }

            if (mapping.Note is not null)
            {
                Note(element, $"{name} → {mapping.Name}", mapping.Note);
            }

            _report.Converted($"{name} → {mapping.Name}");
            return true;
        }

        if (WpfEvents.Unsupported.TryGetValue(name, out var why))
        {
            Note(element, Original(attribute), why);
            attribute.Remove();
            return true;
        }

        if (WpfEvents.IsPreview(name))
        {
            if (TunnelEvents.TryGetValue(name, out var routedEvent) && _class is not null)
            {
                var target = element == _root ? null : EnsureName(element);
                _class.TunnelHandlers.Add(new TunnelHandler(target, routedEvent, handler));
                var bubbling = name["Preview".Length..];
                if (WpfEvents.Renamed.TryGetValue(bubbling, out var bubblingMapping) && bubblingMapping.ArgsType is not null)
                {
                    _class.HandlerArguments[handler] = bubblingMapping.ArgsType;
                }

                _report.Converted("Preview event → handler added with the Tunnel strategy in code-behind");
            }
            else
            {
                Note(element, Original(attribute), "Add the handler in code-behind with AddHandler(…, RoutingStrategies.Tunnel), Avalonia's way to see an event before the element does");
            }

            attribute.Remove();
            return true;
        }

        if (name == "ValueChanged" && RangeControls.Contains(type) && _class is not null)
        {
            _class.HandlerArguments[handler] = "RangeBaseValueChangedEventArgs";
            return true;
        }

        return false;
    }
}
