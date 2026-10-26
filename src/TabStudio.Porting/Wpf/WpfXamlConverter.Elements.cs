using System.Globalization;
using System.Xml.Linq;
using TabStudio.Porting.Output;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>Elements: WPF's controls become Avalonia's, one by one where they differ.</summary>
internal sealed partial class WpfXamlConverter
{
    /// <summary>What a list view that became a DataGrid shows, as the list view did.</summary>
    private static readonly (string Name, string Value)[] GridDefaults = [("AutoGenerateColumns", "False"), ("IsReadOnly", "True"), ("HeadersVisibility", "Column")];

    /// <summary>The separators of WPF's key modifiers, as in <c>Ctrl+Shift</c>.</summary>
    private static readonly char[] ModifierSeparators = ['+', ','];

    /// <summary>The attributes a placeholder for an unsupported control keeps: its name and its place in the layout.</summary>
    private static readonly HashSet<string> LayoutAttributes = new(StringComparer.Ordinal)
    {
        "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan", "DockPanel.Dock", "Canvas.Left", "Canvas.Top",
        "Canvas.Right", "Canvas.Bottom", "Margin", "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
        "HorizontalAlignment", "VerticalAlignment", "Name",
    };

    private void ConvertElement(XElement element, Scope scope)
    {
        if (element.Name.Namespace != XamlNamespaces.Avalonia)
        {
            // A type of the app or of a library, such as local:RatingBar or sys:String: its properties are WPF's.
            ConvertAttributes(element, scope);
            ConvertChildren(element, scope);
            return;
        }

        var local = element.Name.LocalName;
        if (local.Contains('.', StringComparison.Ordinal))
        {
            ConvertPropertyElement(element, scope);
            return;
        }

        if (element.Annotation<WpfType>() is null)
        {
            element.AddAnnotation(new WpfType(local));
        }

        switch (local)
        {
            case "Style":
            {
                var extra = ConvertStyle(element);
                element.AddAfterSelf(extra.Skip(1));
                return;
            }

            case "ControlTemplate":
                ConvertControlTemplate(element, scope.TemplateTarget, null);
                return;
            case "DataTemplate" or "HierarchicalDataTemplate" or "ItemsPanelTemplate":
                if (local != "ItemsPanelTemplate")
                {
                    ConvertDataTemplate(element, scope);
                    ConvertAttributes(element, scope);
                    return;
                }

                break;
            case "Label":
                ConvertLabel(element);
                break;
            case "GroupBox":
                element.Name = XamlNamespaces.Avalonia + "HeaderedContentControl";
                AddClasses(element, "groupBox");
                _project.Context.Needs(SupportNeeds.Styles);
                ChangedElement(element, local, "HeaderedContentControl");
                _report.Converted("GroupBox → HeaderedContentControl with the port's group box style");
                break;
            case "StatusBar":
                ConvertStatusBar(element, scope);
                return;
            case "ToolBarTray" or "ToolBar":
                ConvertToolBar(element, scope);
                return;
            case "ListView":
                ConvertListView(element);
                break;
            case "PasswordBox":
                element.Name = XamlNamespaces.Avalonia + "TextBox";
                if (element.Attribute("PasswordChar") is null)
                {
                    element.Add(new XAttribute("PasswordChar", "●"));
                }

                if (element.Attribute("Password") is { } password)
                {
                    Rename(element, password, "Text");
                }

                ChangedElement(element, local, "TextBox");
                _report.Converted("PasswordBox → TextBox with a PasswordChar");
                break;
            case "Line":
                ConvertLine(element);
                break;
            case "LinearGradientBrush" or "RadialGradientBrush":
                ConvertGradient(element);
                break;
            case "DropShadowEffect":
                ConvertDropShadow(element);
                break;
            case "ImageBrush":
                ConvertImageBrush(element);
                break;
            case "TextBlock":
                ConvertHyperlinkText(element);
                break;
            case "Hyperlink":
                Note(element, "Hyperlink", "Avalonia has no hyperlinks inside text; the link became plain text. Put a HyperlinkButton next to the text", Original(element));
                element.Name = XamlNamespaces.Avalonia + "Run";
                element.ReplaceAttributes(new XAttribute("Text", element.Value));
                element.RemoveNodes();
                return;
            case "KeyBinding":
                ConvertKeyBinding(element);
                break;
            case "MouseBinding":
                Note(element, "MouseBinding", "Avalonia has no mouse bindings; handle the gesture's event, such as DoubleTapped, and run the command there", Original(element));
                element.Remove();
                return;
            case "Popup":
                ConvertPopup(element);
                break;
            case "MenuItem":
                ConvertMenuItem(element);
                break;
            case "DataGrid":
                _project.Context.UsesDataGrid = true;
                ConvertDataGrid(element);
                break;
            case "DataGridComboBoxColumn" or "DataGridHyperlinkColumn":
                ConvertSpecialColumn(element);
                return;
            case "DataGridTextColumn" or "DataGridCheckBoxColumn" or "DataGridTemplateColumn":
                foreach (var style in (string[])["ElementStyle", "EditingElementStyle"])
                {
                    if (element.Attribute(style) is { } attribute)
                    {
                        Note(element, $"{local} {style}", "Avalonia's DataGrid columns have no element styles; style the cells with DataGridCell selectors in the grid's Styles", Original(attribute));
                        attribute.Remove();
                    }
                }

                break;
            case "BitmapImage" or "CombinedGeometry":
                Note(element, local, local == "BitmapImage"
                    ? "Avalonia loads images with Bitmap, from an avares:// address, in code; in XAML, set the Image's Source to the address"
                    : "Avalonia has no CombinedGeometry; use a CombinedGeometry-free PathGeometry or a GeometryGroup", Original(element));
                element.Remove();
                return;
            default:
                if (WpfElements.Unsupported.TryGetValue(local, out var why))
                {
                    ReplaceWithPlaceholder(element, local, why);
                    return;
                }

                if (WpfElements.Renamed.TryGetValue(local, out var renamed))
                {
                    element.Name = XamlNamespaces.Avalonia + renamed;
                    ChangedElement(element, local, renamed);
                    _report.Converted($"{local} → {renamed}");
                }
                else if (!WpfElements.SameInAvalonia.Contains(local) && !WpfElements.Special.Contains(local) && local is not ("Styles" or "StyleInclude" or "ResourceInclude" or "FluentTheme"))
                {
                    ReplaceWithPlaceholder(element, local, $"Avalonia has no {local}");
                    return;
                }

                break;
        }

        if (element.Element(element.Name.Namespace + (element.Name.LocalName + ".LayoutTransform")) is { } layoutTransform)
        {
            WrapInLayoutTransform(element, layoutTransform);
        }

        ConvertAttributes(element, scope);
        ConvertChildren(element, scope);
    }

    /// <summary>A property element, <c>Owner.Property</c>: its owner gets the Avalonia type, and its property Avalonia's name.</summary>
    private void ConvertPropertyElement(XElement element, Scope scope)
    {
        var local = element.Name.LocalName;
        var dot = local.IndexOf('.', StringComparison.Ordinal);
        var owner = local[..dot];
        var property = local[(dot + 1)..];
        var parent = element.Parent!;
        var parentType = parent.Annotation<WpfType>()?.Name ?? parent.Name.LocalName;
        if (owner != parentType && owner != parent.Name.LocalName)
        {
            // An attached property set with element syntax, such as <ToolTipService.ToolTip>.
            var attached = owner + "." + property;
            if (WpfAttributes.Renamed.TryGetValue(attached, out var renamedAttached))
            {
                element.Name = XamlNamespaces.Avalonia + renamedAttached;
                _report.Converted($"{attached} → {renamedAttached}");
            }
            else if (WpfAttributes.Unsupported.TryGetValue(attached, out var unsupported))
            {
                Note(element, attached, unsupported, Original(element));
                element.Remove();
                return;
            }

            ConvertChildren(element, scope);
            return;
        }

        XName Own(string name) => parent.Name.Namespace + (parent.Name.LocalName + "." + name);
        switch (property)
        {
            case "Resources":
                element.Name = Own("Resources");
                ConvertResources(element, scope);
                return;
            case "Triggers":
                foreach (var trigger in element.Elements().ToList())
                {
                    Note(element, trigger.Name.LocalName + DescribeTrigger(trigger), "Avalonia controls have no triggers; animations go in a style, as Animation and KeyFrame, started by a pseudo-class or a class", Original(trigger));
                }

                MoveCommentsBefore(element);
                element.Remove();
                return;
            case "InputBindings":
                element.Name = Own("KeyBindings");
                _report.Converted("InputBindings → KeyBindings");
                ConvertChildren(element, scope);
                RemoveIfEmpty(element);
                return;
            case "CommandBindings":
                Note(element, "CommandBindings", "Avalonia has no routed commands; bind each button to a command of the view model instead", Original(element));
                element.Remove();
                return;
            case "ToolTip":
                element.Name = XamlNamespaces.Avalonia + "ToolTip.Tip";
                _report.Converted("ToolTip → ToolTip.Tip");
                ConvertChildren(element, scope);
                return;
            case "Style":
                ConvertInlineStyle(element, parent, null, isContainerStyle: false);
                return;
            case "ItemContainerStyle":
                ConvertInlineStyle(element, parent, ContainerOf(parent.Name.LocalName) ?? "ContentPresenter", isContainerStyle: true);
                return;
            case "RowStyle" or "CellStyle" or "ColumnHeaderStyle" or "RowHeaderStyle":
                ConvertInlineStyle(element, parent, "DataGrid" + property[..^"Style".Length], isContainerStyle: true);
                return;
            case "FocusVisualStyle":
                element.Remove();
                _report.Converted("WPF-only setting removed");
                return;
            case "TaskbarItemInfo" or "ElementStyle" or "EditingElementStyle" or "ItemTemplateSelector" or "ContentTemplateSelector" or "GroupStyle":
                Note(element, local, property switch
                {
                    "TaskbarItemInfo" => "Avalonia windows have no taskbar item information",
                    "GroupStyle" => "Avalonia's ItemsControl has no groups; group the items in the view model, or use DataGridCollectionView's grouping",
                    "ElementStyle" or "EditingElementStyle" => "Avalonia's DataGrid columns have no element styles; style the cells with DataGridCell selectors in the grid's Styles",
                    _ => "Avalonia has no template selectors; give each item type a DataTemplate with its DataType",
                }, Original(element));
                element.Remove();
                return;
            case "Template":
                element.Name = Own("Template");
                foreach (var template in element.Elements(XamlNamespaces.Avalonia + "ControlTemplate").ToList())
                {
                    ConvertControlTemplate(template, parent.Name.LocalName, null);
                }

                return;
            case "Visibility":
                element.Name = Own("IsVisible");
                ConvertChildren(element, scope);
                return;
            default:
                if (WpfAttributes.Unsupported.TryGetValue(property, out var why))
                {
                    Note(element, local, why, Original(element));
                    element.Remove();
                    return;
                }

                element.Name = WpfAttributes.Renamed.TryGetValue(property, out var renamed) && renamed.Contains('.', StringComparison.Ordinal)
                    ? XamlNamespaces.Avalonia + renamed
                    : Own(renamed ?? property);
                ConvertChildren(element, scope);
                return;
        }
    }

    /// <summary>The containers an items control creates, which its item container style is for.</summary>
    private static string? ContainerOf(string itemsControl) => itemsControl switch
    {
        "ListBox" => "ListBoxItem",
        "ComboBox" => "ComboBoxItem",
        "TreeView" or "TreeViewItem" => "TreeViewItem",
        "TabControl" => "TabItem",
        "Menu" or "MenuItem" or "ContextMenu" => "MenuItem",
        "DataGrid" => "DataGridRow",
        "ItemsControl" => "ContentPresenter",
        _ => null,
    };

    /// <summary>
    /// A style set on one element (<c>&lt;Button.Style&gt;</c>), or on its item containers, goes in the element's own
    /// styles with a selector for the element's type or its containers.
    /// </summary>
    private void ConvertInlineStyle(XElement property, XElement owner, string? selector, bool isContainerStyle)
    {
        if (property.Element(XamlNamespaces.Avalonia + "Style") is not { } style)
        {
            property.Remove();
            return;
        }

        selector ??= SelectorType(owner.Name);
        if (!isContainerStyle && owner.Descendants(owner.Name).Any())
        {
            // The style applies to this element only, not to elements of the same type inside it.
            var only = "style" + (++_generatedNames).ToString(CultureInfo.InvariantCulture);
            AddClasses(owner, only);
            selector += "." + only;
        }

        var before = style.PreviousNode;
        var converted = ConvertStyle(style, selector, isContainerStyle);
        var styles = PropertyElement(owner, "Styles", property);
        foreach (var node in Detach(style, before, converted.Skip(1)))
        {
            styles.Add(node);
        }

        MoveCommentsBefore(property);
        property.Remove();
        _report.Converted(isContainerStyle ? "Item container style → style of the containers" : "Style of one element → the element's Styles");
    }

    /// <summary>A type as a style selector names it: <c>local|RatingBar</c> for a type of another namespace.</summary>
    private string SelectorType(XName name)
    {
        if (name.Namespace == XamlNamespaces.Avalonia)
        {
            return name.LocalName;
        }

        var prefix = _root.GetPrefixOfNamespace(name.Namespace);
        return prefix is null ? name.LocalName : $"{prefix}|{name.LocalName}";
    }

    /// <summary>Records that a named element changed type, for its code-behind.</summary>
    private void ChangedElement(XElement element, string wpf, string avalonia)
    {
        var name = element.Attribute(NameAttribute)?.Value ?? element.Attribute("Name")?.Value;
        if (name is not null && _class is not null)
        {
            _class.ChangedElements[name] = (wpf, avalonia);
        }
    }

    /// <summary>Adds style classes to an element's <c>Classes</c>.</summary>
    private static void AddClasses(XElement element, params string[] classes)
    {
        var existing = (Value(element, "Classes") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var name in classes)
        {
            if (!existing.Contains(name, StringComparer.Ordinal))
            {
                existing.Add(name);
            }
        }

        Set(element, "Classes", string.Join(' ', existing));
    }

    /// <summary>Moves the TODO comments inside an element in front of it, before it is removed.</summary>
    private static void MoveCommentsBefore(XElement element)
    {
        foreach (var comment in element.Nodes().OfType<XComment>().ToList())
        {
            comment.Remove();
            element.AddBeforeSelf(comment);
        }
    }

    /// <summary>A control Avalonia does not have: a placeholder keeps its place and name in the layout, and a note says what it was.</summary>
    private void ReplaceWithPlaceholder(XElement element, string type, string why)
    {
        Note(element, type, why, Original(element));
        if (element.Parent is { } parent && parent.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal))
        {
            element.Remove();
            return;
        }

        var placeholder = new XElement(XamlNamespaces.Avalonia + "Border",
            element.Attributes().Where(a => a.Name == NameAttribute || LayoutAttributes.Contains(a.Name.LocalName)).Select(a => new XAttribute(a)));
        ChangedElement(element, type, "Border");
        element.ReplaceWith(placeholder);
    }

    /// <summary>
    /// A label of plain text becomes a TextBlock, as Avalonia's guide suggests; one with a Target (an access key for
    /// a field), a frame, a style or code that uses it stays an Avalonia Label.
    /// </summary>
    private void ConvertLabel(XElement label)
    {
        if (label.Attribute("Target") is { } target)
        {
            if (MarkupExtension.Parse(target.Value) is { Name: "Binding" } binding && binding.GetText("ElementName") is { } elementName)
            {
                target.Value = elementName;
                _report.Converted("Label Target binding → element name");
            }

            return;
        }

        var name = label.Attribute(NameAttribute)?.Value ?? label.Attribute("Name")?.Value;
        var usedInCode = name is not null && _project.CodeIdentifiers.TryGetValue(_sourcePath, out var identifiers) && identifiers.Contains(name);
        var hasContentElements = label.Elements().Any(e => !e.Name.LocalName.Contains('.', StringComparison.Ordinal));
        var styled = label.Attribute("Style") is not null || label.Attribute("Template") is not null || _project.ImplicitStyleTargets.Contains("Label");
        var framed = label.Attribute("BorderBrush") is not null || label.Attribute("BorderThickness") is not null;
        if (usedInCode || hasContentElements || styled || framed || label.Elements().Any())
        {
            return;
        }

        label.Name = XamlNamespaces.Avalonia + "TextBlock";
        if (label.Attribute("Content") is { } content)
        {
            Rename(label, content, "Text", MarkupExtension.IsExtension(content.Value) ? content.Value : WithoutAccessKey(content.Value));
        }
        else if (label.Value.Trim().Length > 0)
        {
            var text = WithoutAccessKey(label.Value.Trim());
            label.RemoveNodes();
            label.Add(new XAttribute("Text", text));
        }

        // A Label pads its text by 5; a TextBlock does not.
        if (label.Attribute("Padding") is null)
        {
            label.Add(new XAttribute("Padding", "5"));
        }

        if (label.Attribute("HorizontalContentAlignment") is { } horizontal)
        {
            Rename(label, horizontal, "TextAlignment", horizontal.Value == "Stretch" ? "Left" : horizontal.Value);
        }

        label.Attribute("VerticalContentAlignment")?.Remove();
        _report.Converted("Label of text → TextBlock");
    }

    /// <summary>Text without its access key marker: <c>_Name</c> is shown as <c>Name</c>.</summary>
    private static string WithoutAccessKey(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '_' && i + 1 < text.Length)
            {
                i++;
            }

            result.Append(text[i]);
        }

        return result.ToString();
    }

    /// <summary>A status bar becomes a strip with a DockPanel, as Avalonia's guide shows; its items keep their docking.</summary>
    private void ConvertStatusBar(XElement bar, Scope scope)
    {
        var panel = new XElement(XamlNamespaces.Avalonia + "DockPanel");
        foreach (var node in bar.Nodes().ToList())
        {
            if (node is XElement { Name.LocalName: var name } && name.StartsWith("StatusBar.", StringComparison.Ordinal))
            {
                continue;
            }

            node.Remove();
            panel.Add(node);
        }

        foreach (var separator in panel.Elements(XamlNamespaces.Avalonia + "Separator").ToList())
        {
            separator.ReplaceWith(new XElement(XamlNamespaces.Avalonia + "Border", new XAttribute("Width", "1"), new XAttribute("Margin", "6,2"),
                new XAttribute("Background", "{DynamicResource SystemControlForegroundBaseLowBrush}")));
        }

        foreach (var items in (string[])["ItemsSource", "ItemTemplate"])
        {
            if (bar.Attribute(items) is { } attribute)
            {
                Note(bar, "StatusBar " + items, "The status bar became a DockPanel; show the items with an ItemsControl inside it", Original(attribute));
                attribute.Remove();
            }
        }

        bar.Name = XamlNamespaces.Avalonia + "Border";
        AddClasses(bar, "statusBar");
        bar.Add(panel);
        _project.Context.Needs(SupportNeeds.Styles);
        ChangedElement(bar, "StatusBar", "Border");
        _report.Converted("StatusBar → Border and DockPanel (Avalonia has no status bar)");
        ConvertAttributes(bar, scope);
        ConvertChildren(panel, scope);
    }

    /// <summary>A toolbar becomes a strip with a row of its buttons; a toolbar tray, a row of those.</summary>
    private void ConvertToolBar(XElement element, Scope scope)
    {
        var isTray = element.Name.LocalName == "ToolBarTray";
        var inTray = element.Parent?.Annotation<WpfType>()?.Name == "ToolBarTray";
        var orientation = Value(element, "Orientation") ?? "Horizontal";
        var row = new XElement(XamlNamespaces.Avalonia + "StackPanel", new XAttribute("Orientation", orientation), new XAttribute("Spacing", isTray ? "8" : "2"));
        foreach (var node in element.Nodes().Where(n => n is not XElement e || !e.Name.LocalName.Contains('.', StringComparison.Ordinal)).ToList())
        {
            node.Remove();
            row.Add(node);
        }

        foreach (var name in (string[])["Band", "BandIndex", "Header", "IsLocked", "Orientation", "OverflowMode"])
        {
            element.Attribute(name)?.Remove();
        }

        if (inTray)
        {
            element.Name = XamlNamespaces.Avalonia + "StackPanel";
            element.SetAttributeValue("Orientation", orientation);
            element.SetAttributeValue("Spacing", "2");
            foreach (var node in row.Nodes().ToList())
            {
                node.Remove();
                element.Add(node);
            }
        }
        else
        {
            element.Name = XamlNamespaces.Avalonia + "Border";
            AddClasses(element, "toolBar");
            element.Add(row);
            _project.Context.Needs(SupportNeeds.Styles);
        }

        _report.Converted(isTray ? "ToolBarTray → a row of toolbars" : "ToolBar → a row of buttons (Avalonia has no toolbar)");
        ConvertAttributes(element, scope);
        foreach (var child in (inTray ? element : row).Elements().ToList())
        {
            ConvertElement(child, scope);
        }
    }

    /// <summary>A list view with a GridView becomes a DataGrid; any other list view, a ListBox.</summary>
    private void ConvertListView(XElement list)
    {
        var viewProperty = list.Element(list.Name.Namespace + "ListView.View");
        var view = viewProperty?.Elements().FirstOrDefault();
        if (view?.Name.LocalName != "GridView")
        {
            list.Name = XamlNamespaces.Avalonia + "ListBox";
            ChangedElement(list, "ListView", "ListBox");
            _report.Converted("ListView → ListBox");
            if (viewProperty is not null)
            {
                Note(list, "ListView.View", "Avalonia's ListBox has no views; lay the items out with an ItemsPanel and an ItemTemplate", Original(viewProperty));
                viewProperty.Remove();
            }

            return;
        }

        list.Name = XamlNamespaces.Avalonia + "DataGrid";
        _project.Context.UsesDataGrid = true;
        var columns = new XElement(XamlNamespaces.Avalonia + "DataGrid.Columns");
        foreach (var column in view.Elements().Where(e => e.Name.LocalName == "GridViewColumn"))
        {
            var template = column.Attribute("CellTemplate")?.Value;
            var templateElement = column.Element(column.Name.Namespace + "GridViewColumn.CellTemplate")?.Elements().FirstOrDefault();
            var converted = new XElement(XamlNamespaces.Avalonia + (template is null && templateElement is null ? "DataGridTextColumn" : "DataGridTemplateColumn"));
            foreach (var name in (string[])["Header", "Width"])
            {
                if (Value(column, name) is { } value)
                {
                    converted.Add(new XAttribute(name, value));
                }
            }

            if (Value(column, "DisplayMemberBinding") is { } binding)
            {
                converted.Add(new XAttribute("Binding", binding));
            }

            if (template is not null)
            {
                converted.Add(new XAttribute("CellTemplate", template));
            }
            else if (templateElement is not null)
            {
                templateElement.Remove();
                converted.Add(new XElement(XamlNamespaces.Avalonia + "DataGridTemplateColumn.CellTemplate", templateElement));
            }

            columns.Add(converted);
        }

        viewProperty!.ReplaceWith(columns);
        foreach (var (name, value) in GridDefaults)
        {
            if (list.Attribute(name) is null)
            {
                list.Add(new XAttribute(name, value));
            }
        }

        if (list.Attribute("SelectionMode") is { } mode && mode.Value is "Multiple")
        {
            mode.Value = "Extended";
        }

        ChangedElement(list, "ListView", "DataGrid");
        _report.Converted("ListView with a GridView → DataGrid");
    }

    /// <summary>A DataGrid: Avalonia's has no new-item row and no alternating row brush.</summary>
    private void ConvertDataGrid(XElement grid)
    {
        if (grid.Attribute("CanUserAddRows") is { } add)
        {
            if (IsTrue(add.Value))
            {
                Note(grid, "CanUserAddRows=\"True\"", "Avalonia's DataGrid has no row for new items; add a button that adds an item to the collection", Original(add));
            }

            add.Remove();
            _report.Converted("CanUserAddRows removed: Avalonia's DataGrid never shows a new-item row");
        }

        grid.Attribute("CanUserDeleteRows")?.Remove();
        if (grid.Attribute("AlternatingRowBackground") is { } alternating)
        {
            Note(grid, "AlternatingRowBackground", "Style alternate rows with <Style Selector=\"DataGridRow:nth-child(2n)\"> in the grid's Styles", Original(alternating));
            alternating.Remove();
        }
    }

    /// <summary>Columns Avalonia's DataGrid does not have become text columns showing the same value.</summary>
    private void ConvertSpecialColumn(XElement column)
    {
        var local = column.Name.LocalName;
        var binding = Value(column, "SelectedItemBinding") ?? Value(column, "SelectedValueBinding") ?? Value(column, "TextBinding") ?? Value(column, "Binding");
        Note(column, local, local == "DataGridComboBoxColumn"
            ? "Avalonia's DataGrid has no combo box column; it shows the value as text now. Edit it with a DataGridTemplateColumn whose CellEditingTemplate has a ComboBox"
            : "Avalonia's DataGrid has no hyperlink column; it shows the address as text now. Use a DataGridTemplateColumn with a HyperlinkButton", Original(column));
        var replacement = new XElement(XamlNamespaces.Avalonia + "DataGridTextColumn", new XAttribute("IsReadOnly", "True"));
        foreach (var name in (string[])["Header", "Width", "SortMemberPath"])
        {
            if (Value(column, name) is { } value)
            {
                replacement.Add(new XAttribute(name, value));
            }
        }

        if (binding is not null)
        {
            replacement.Add(new XAttribute("Binding", binding));
        }

        column.ReplaceWith(replacement);
    }

    /// <summary>A line's ends: Avalonia has StartPoint and EndPoint instead of X1, Y1, X2 and Y2.</summary>
    private void ConvertLine(XElement line)
    {
        string Take(string name)
        {
            var value = Value(line, name) ?? "0";
            line.Attribute(name)?.Remove();
            return value;
        }

        if (line.Attribute("X1") is null && line.Attribute("X2") is null && line.Attribute("Y1") is null && line.Attribute("Y2") is null)
        {
            return;
        }

        var anchor = line.Attribute("X1") ?? line.Attribute("Y1") ?? line.Attribute("X2") ?? line.Attribute("Y2");
        var marker = new XAttribute("StartPoint", string.Empty);
        ReplaceAttribute(line, anchor!, marker, new XAttribute("EndPoint", string.Empty));
        var x1 = Take("X1");
        var y1 = Take("Y1");
        var x2 = Take("X2");
        var y2 = Take("Y2");
        line.SetAttributeValue("StartPoint", $"{x1},{y1}");
        line.SetAttributeValue("EndPoint", $"{x2},{y2}");
        _report.Converted("Line X1/Y1/X2/Y2 → StartPoint/EndPoint");
    }

    /// <summary>Gradient points: WPF's are fractions of the box (0.5,1), Avalonia's relative points need percents (50%,100%).</summary>
    private void ConvertGradient(XElement brush)
    {
        if (brush.Attribute("MappingMode") is { } mapping)
        {
            mapping.Remove();
            if (mapping.Value == "Absolute")
            {
                return;
            }
        }

        foreach (var name in (string[])["StartPoint", "EndPoint", "Center", "GradientOrigin"])
        {
            if (brush.Attribute(name) is { } point && RelativePoint(point.Value) is { } relative)
            {
                point.Value = relative;
                _report.Converted("Gradient point → relative point in percent");
            }
        }

        foreach (var name in (string[])["RadiusX", "RadiusY"])
        {
            if (brush.Attribute(name) is { } radius && TryNumber(radius.Value, out var fraction))
            {
                radius.Value = string.Create(CultureInfo.InvariantCulture, $"{fraction * 100:0.##}%");
            }
        }

        brush.Attribute("ColorInterpolationMode")?.Remove();
    }

    /// <summary>A drop shadow: WPF's depth and direction are Avalonia's X and Y offsets.</summary>
    private void ConvertDropShadow(XElement effect)
    {
        effect.Attribute("RenderingBias")?.Remove();
        var depthAttribute = effect.Attribute("ShadowDepth");
        var directionAttribute = effect.Attribute("Direction");
        if (depthAttribute is null && directionAttribute is null)
        {
            return;
        }

        var depth = depthAttribute is not null && TryNumber(depthAttribute.Value, out var d) ? d : 5;
        var direction = directionAttribute is not null && TryNumber(directionAttribute.Value, out var a) ? a : 315;
        var radians = direction * Math.PI / 180;
        var x = new XAttribute("OffsetX", Math.Round(depth * Math.Cos(radians), 2).ToString(CultureInfo.InvariantCulture));
        var y = new XAttribute("OffsetY", Math.Round(-depth * Math.Sin(radians), 2).ToString(CultureInfo.InvariantCulture));
        ReplaceAttribute(effect, (depthAttribute ?? directionAttribute)!, x, y);
        effect.Attribute("ShadowDepth")?.Remove();
        effect.Attribute("Direction")?.Remove();
        _report.Converted("DropShadowEffect ShadowDepth/Direction → OffsetX/OffsetY");
    }

    /// <summary>An image brush: Avalonia names the image Source and the tiling rectangles differently.</summary>
    private void ConvertImageBrush(XElement brush)
    {
        if (brush.Attribute("ImageSource") is { } source)
        {
            Rename(brush, source, "Source", AppUri(source.Value));
            _report.Converted("ImageBrush ImageSource → Source");
        }

        foreach (var name in (string[])["Viewport", "Viewbox", "ViewportUnits", "ViewboxUnits"])
        {
            if (brush.Attribute(name) is { } attribute)
            {
                Note(brush, "ImageBrush " + name, "Avalonia's tile brushes use DestinationRect and SourceRect, as relative rectangles in percent", Original(attribute));
                attribute.Remove();
            }
        }
    }

    /// <summary>A TextBlock holding only a hyperlink becomes a HyperlinkButton, which opens its address itself.</summary>
    /// <returns>Whether it did.</returns>
    private bool ConvertHyperlinkText(XElement block)
    {
        var meaningful = block.Nodes().Where(n => n is not XText text || !string.IsNullOrWhiteSpace(text.Value)).ToList();
        if (meaningful is not [XElement { Name.LocalName: "Hyperlink" } link] || link.Elements().Any())
        {
            return false;
        }

        var text = link.Value.Trim();
        block.Name = XamlNamespaces.Avalonia + "HyperlinkButton";
        block.RemoveNodes();
        foreach (var name in (string[])["TextWrapping", "TextTrimming", "TextAlignment", "LineHeight"])
        {
            block.Attribute(name)?.Remove();
        }

        foreach (var attribute in link.Attributes().Where(a => !a.IsNamespaceDeclaration))
        {
            if (attribute.Name.LocalName == "RequestNavigate")
            {
                _report.Converted("Hyperlink RequestNavigate removed: HyperlinkButton opens its NavigateUri");
                continue;
            }

            block.SetAttributeValue(attribute.Name, attribute.Name.LocalName == "NavigateUri" ? AppUri(attribute.Value) : attribute.Value);
        }

        block.SetAttributeValue("Content", text);
        _report.Converted("TextBlock with a Hyperlink → HyperlinkButton");
        return true;
    }

    /// <summary>A key binding: Avalonia's takes the whole gesture, such as <c>Ctrl+N</c>.</summary>
    private void ConvertKeyBinding(XElement binding)
    {
        if (binding.Attribute("Key") is not { } key)
        {
            return;
        }

        var modifiers = binding.Attribute("Modifiers");
        var parts = (modifiers?.Value ?? string.Empty).Split(ModifierSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m switch
            {
                "Control" => "Ctrl",
                "Windows" => "Meta",
                _ => m,
            })
            .Append(key.Value.Trim());
        Rename(binding, key, "Gesture", string.Join('+', parts));
        modifiers?.Remove();
        _report.Converted("KeyBinding Key/Modifiers → Gesture");
    }

    /// <summary>A popup: Avalonia dismisses it on an outside click when IsLightDismissEnabled is set, the opposite of StaysOpen.</summary>
    private void ConvertPopup(XElement popup)
    {
        if (popup.Attribute("StaysOpen") is { } staysOpen)
        {
            if (IsTrue(staysOpen.Value))
            {
                staysOpen.Remove();
            }
            else
            {
                Rename(popup, staysOpen, "IsLightDismissEnabled", "True");
            }

            _report.Converted("Popup StaysOpen → IsLightDismissEnabled");
        }

        if (popup.Attribute("Placement") is { } placement)
        {
            switch (placement.Value)
            {
                case "Mouse" or "MousePoint":
                    placement.Value = "Pointer";
                    break;
                case "Bottom" or "Top" or "Left" or "Right" or "Center":
                    break;
                default:
                    Note(popup, $"Popup Placement=\"{placement.Value}\"", "Avalonia places popups with Placement, PlacementAnchor and PlacementGravity", Original(placement));
                    placement.Remove();
                    break;
            }
        }
    }

    /// <summary>A menu item: its shortcut text is a gesture in Avalonia, and checkable items have a toggle type.</summary>
    private void ConvertMenuItem(XElement item)
    {
        if (item.Attribute("InputGestureText") is { } gesture)
        {
            if (KeyGestureText(gesture.Value) is { } parsed)
            {
                Rename(item, gesture, "InputGesture", parsed);
                _report.Converted("InputGestureText → InputGesture");
            }
            else
            {
                Note(item, $"InputGestureText=\"{gesture.Value}\"", "Avalonia shows a menu item's shortcut from InputGesture, a key gesture such as Ctrl+S", Original(gesture));
                gesture.Remove();
            }
        }

        if (item.Attribute("IsCheckable") is { } checkable)
        {
            if (IsTrue(checkable.Value))
            {
                Rename(item, checkable, "ToggleType", "CheckBox");
                _report.Converted("IsCheckable → ToggleType=\"CheckBox\"");
            }
            else
            {
                checkable.Remove();
            }
        }
    }

    /// <summary>A shortcut as Avalonia's key gestures write it, or <see langword="null"/> when it is not one.</summary>
    internal static string? KeyGestureText(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(p => p.Length == 0))
        {
            return null;
        }

        var result = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var last = i == parts.Length - 1;
            var mapped = part switch
            {
                "Ctrl" or "Control" => last ? null : "Ctrl",
                "Shift" => last ? null : "Shift",
                "Alt" => last ? null : "Alt",
                "Win" or "Windows" => last ? null : "Meta",
                "Del" when last => "Delete",
                "Ins" when last => "Insert",
                "Esc" when last => "Escape",
                "PgUp" when last => "PageUp",
                "PgDn" when last => "PageDown",
                "Enter" or "Return" when last => "Enter",
                _ when last && part.Length == 1 && char.IsLetter(part[0]) => part.ToUpperInvariant(),
                _ when last && part.Length == 1 && char.IsDigit(part[0]) => "D" + part,
                _ when last && part.Length is 2 or 3 && part[0] == 'F' && int.TryParse(part[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 24 => part,
                _ when last && part is "Delete" or "Insert" or "Home" or "End" or "PageUp" or "PageDown" or "Escape" or "Tab" or "Space" or "Back" or "Up" or "Down" or "Left" or "Right" => part,
                _ => null,
            };

            if (mapped is null)
            {
                return null;
            }

            result.Add(mapped);
        }

        return string.Join('+', result);
    }

    /// <summary>A layout transform: Avalonia applies it with a LayoutTransformControl around the element.</summary>
    private void WrapInLayoutTransform(XElement element, XElement property)
    {
        var transform = new XElement(XamlNamespaces.Avalonia + "LayoutTransformControl.LayoutTransform", property.Elements());
        property.Remove();
        var wrapper = new XElement(XamlNamespaces.Avalonia + "LayoutTransformControl", transform);
        foreach (var attached in element.Attributes().Where(a => a.Name.LocalName is "Grid.Row" or "Grid.Column" or "Grid.RowSpan" or "Grid.ColumnSpan" or "DockPanel.Dock" or "Canvas.Left" or "Canvas.Top").ToList())
        {
            wrapper.Add(new XAttribute(attached));
            attached.Remove();
        }

        element.ReplaceWith(wrapper);
        wrapper.Add(element);
        _report.Converted("LayoutTransform → LayoutTransformControl");
    }
}
