using System.Globalization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.Output;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Forms;

/// <summary>Windows Forms' controls as Avalonia's, with their properties.</summary>
internal sealed partial class FormsXamlBuilder
{
    /// <summary>Components the designer creates that are not controls; the form's code gets them as fields.</summary>
    private static readonly HashSet<string> NonVisual = new(StringComparer.Ordinal)
    {
        "Timer", "ToolTip", "ErrorProvider", "BackgroundWorker", "OpenFileDialog", "SaveFileDialog", "FolderBrowserDialog",
        "ColorDialog", "FontDialog", "PrintDialog", "PrintDocument", "PageSetupDialog", "PrintPreviewDialog", "BindingSource",
        "ImageList", "NotifyIcon", "HelpProvider", "FileSystemWatcher", "SerialPort", "ContextMenuStrip", "EventLog",
        "DataGridViewTextBoxColumn", "DataGridViewCheckBoxColumn", "DataGridViewComboBoxColumn", "DataGridViewButtonColumn",
        "DataGridViewLinkColumn", "DataGridViewImageColumn", "ColumnHeader", "Container",
    };

    /// <summary>Properties that only tune Windows Forms' own drawing and layout passes; Avalonia needs nothing for them.</summary>
    private static readonly HashSet<string> Dropped = new(StringComparer.Ordinal)
    {
        "Name", "Location", "Size", "Anchor", "Dock", "TabIndex", "TabStop", "Text", "AutoSize", "Margin", "UseVisualStyleBackColor",
        "FormattingEnabled", "ImeMode", "UseCompatibleTextRendering", "CausesValidation", "UseMnemonic", "FlatStyle", "TextImageRelation",
        "ImageAlign", "AutoEllipsis", "AutoSizeMode", "AutoScaleDimensions", "AutoScaleMode", "ClientSize", "MainMenuStrip",
        "DoubleBuffered", "Enabled", "Visible", "BackColor", "ForeColor", "Font", "Cursor", "Padding", "MinimumSize", "MaximumSize",
        "AccessibleName", "AccessibleDescription", "RightToLeft", "ContextMenuStrip", "StartPosition", "FormBorderStyle",
        "MaximizeBox", "MinimizeBox", "ShowInTaskbar", "TopMost", "WindowState", "Icon", "AcceptButton", "CancelButton",
        "KeyPreview", "ShowIcon", "SizeGripStyle", "HelpButton", "ControlBox", "RenderMode", "GripStyle", "LayoutStyle",
        "ImageScalingSize", "Stretch", "ColumnHeadersHeightSizeMode", "RowHeadersWidthSizeMode", "BorderStyle", "ShortcutKeyDisplayString",
        "HideSelection", "ShowShortcutKeys", "CheckAlign", "TextAlign", "DisplayStyle", "ImageTransparentColor", "AllowUserToAddRows",
        "AllowUserToDeleteRows", "AllowUserToResizeRows", "AllowUserToOrderColumns", "EnableHeadersVisualStyles", "UseWaitCursor",
        "AutoScroll", "AutoScrollMargin", "AutoScrollMinSize", "TabStopsEnabled", "LinkBehavior", "LinkColor", "ActiveLinkColor",
        "VisitedLinkColor", "MultiSelect", "SelectionMode", "RowHeadersVisible", "ColumnHeadersVisible", "ReadOnly", "Image",
        "FlatAppearance.BorderSize", "FlatAppearance.BorderColor", "FlatAppearance.MouseOverBackColor", "FlatAppearance.MouseDownBackColor",
        "BackgroundImageLayout", "Spring", "ToolTipText", "CheckOnClick", "Checked", "CheckState", "ShortcutKeys", "Value",
        "Minimum", "Maximum", "Increment", "DecimalPlaces", "ThousandsSeparator", "DropDownStyle", "PlaceholderText", "Multiline",
        "PasswordChar", "UseSystemPasswordChar", "MaxLength", "ScrollBars", "WordWrap", "Mask", "Format", "CustomFormat",
        "ShowUpDown", "SizeMode", "Style", "TickFrequency", "TickStyle", "SmallChange", "LargeChange", "Orientation",
        "SplitterDistance", "FixedPanel", "IsSplitterFixed", "SplitterWidth", "FlowDirection", "WrapContents", "ColumnCount",
        "RowCount", "Alignment", "SelectedIndex", "DialogResult", "ThreeState", "MarqueeAnimationSpeed", "BackgroundColor",
        "GridColor", "CellBorderStyle", "AlternatingRowsDefaultCellStyle", "DefaultCellStyle", "ColumnHeadersDefaultCellStyle",
        "AutoSizeColumnsMode", "AutoSizeRowsMode", "RowTemplate", "HeaderText", "Width", "AutoSizeMode", "DataPropertyName",
        "SortMode", "FillWeight", "MinimumWidth", "Frozen", "Resizable", "View", "FullRowSelect", "GridLines", "UseTabStops",
        "IntegralHeight", "ItemHeight", "Sorted", "MaxDropDownItems", "DropDownHeight", "DropDownWidth", "AutoCompleteMode",
        "AutoCompleteSource", "TextAlignment", "AcceptsReturn", "AcceptsTab", "ShortcutsEnabled", "Lines", "SelectionStart",
    };

    /// <summary>Whether a component is a control, which the XAML shows, rather than a component the code holds.</summary>
    private bool IsVisual(FormsComponent component)
    {
        if (!NonVisual.Contains(component.Type))
        {
            return true;
        }

        if (!Info.Components.ContainsKey(component.Name))
        {
            AddComponent(component);
        }

        return false;
    }

    /// <summary>The Avalonia element of a control, with its properties, its content and its children.</summary>
    private XElement Element(FormsComponent control)
    {
        var element = control.Type switch
        {
            "Button" => Button(control),
            "Label" => Text(control, "TextBlock"),
            "LinkLabel" => Link(control),
            "TextBox" or "RichTextBox" or "MaskedTextBox" => TextBox(control),
            "CheckBox" or "RadioButton" => Check(control),
            "ComboBox" or "ListBox" or "CheckedListBox" => List(control),
            "NumericUpDown" => UpDown(control),
            "DateTimePicker" => Date(control),
            "MonthCalendar" => Simple(control, "Calendar"),
            "PictureBox" => Picture(control),
            "ProgressBar" => Progress(control),
            "TrackBar" => Slider(control),
            "GroupBox" => Group(control),
            "Panel" => Panel(control),
            "FlowLayoutPanel" => Flow(control),
            "TableLayoutPanel" => Table(control),
            "SplitContainer" => Split(control),
            "TabControl" => Tabs(control),
            "MenuStrip" => Menu(control),
            "ToolStrip" => ToolBar(control),
            "StatusStrip" => StatusBar(control),
            "DataGridView" => Grid(control),
            "ListView" => ListView(control),
            "TreeView" => Simple(control, "TreeView"),
            _ when _projectTypes.Contains(control.Type) => new XElement(_local + control.Type),
            _ => Placeholder(control),
        };

        if (control.Name != "this")
        {
            element.Add(new XAttribute(Name, control.Name));
        }

        Info.Components[control.Name] = (control.Type, element.Name.LocalName);
        AddCommon(element, control, isRoot: false);
        AddEvents(control, element.Name.LocalName);
        NoteUnported(element, control);
        return element;
    }

    /// <summary>What every control has: enabled, visible, colors, font, cursor, tooltip and accessibility.</summary>
    private void AddCommon(XElement element, FormsComponent control, bool isRoot)
    {
        if (FormsValues.Boolean(Get(control, "Enabled")) is false)
        {
            element.Add(new XAttribute("IsEnabled", "False"));
        }

        if (FormsValues.Boolean(Get(control, "Visible")) is false)
        {
            element.Add(new XAttribute("IsVisible", "False"));
        }

        if (FormsValues.Boolean(Get(control, "TabStop")) is false && !isRoot)
        {
            element.Add(new XAttribute("IsTabStop", "False"));
        }

        if (FormsValues.Color(Get(control, "BackColor")) is { } background)
        {
            element.Add(new XAttribute("Background", background));
        }

        if (FormsValues.Color(Get(control, "ForeColor")) is { } foreground && element.Name.LocalName is not ("Border" or "Canvas" or "Grid" or "DockPanel"))
        {
            element.Add(new XAttribute("Foreground", foreground));
        }

        if (FormsValues.Font(Get(control, "Font")) is { } font)
        {
            if (!font.Family.Equals(FormsValues.DefaultFamily, StringComparison.OrdinalIgnoreCase))
            {
                element.Add(new XAttribute("FontFamily", font.Family));
            }

            if (Math.Abs(font.Points - FormsValues.DefaultPoints) > 0.01)
            {
                element.Add(new XAttribute("FontSize", Math.Round(font.Points * 96 / 72, 1).ToString(CultureInfo.InvariantCulture)));
            }

            if (font.Styles.Contains("Bold"))
            {
                element.Add(new XAttribute("FontWeight", "Bold"));
            }

            if (font.Styles.Contains("Italic"))
            {
                element.Add(new XAttribute("FontStyle", "Italic"));
            }

            if (font.Styles.Contains("Underline") && element.Name.LocalName == "TextBlock")
            {
                element.Add(new XAttribute("TextDecorations", "Underline"));
            }

            _report.Converted("Font → FontFamily, FontSize and FontWeight");
        }

        if (FormsValues.EnumMember(Get(control, "Cursor")) is { } cursor)
        {
            element.Add(new XAttribute("Cursor", cursor switch
            {
                "Default" => "Arrow",
                "WaitCursor" => "Wait",
                "IBeam" => "Ibeam",
                "SizeNS" => "SizeNorthSouth",
                "SizeWE" => "SizeWestEast",
                var other => other,
            }));
        }

        if (FormsValues.EnumMember(Get(control, "RightToLeft")) is "Yes")
        {
            element.Add(new XAttribute("FlowDirection", "RightToLeft"));
        }

        if (control.ToolTip is { } tip)
        {
            element.Add(new XAttribute("ToolTip.Tip", tip));
            _report.Converted("ToolTip.SetToolTip → ToolTip.Tip");
        }

        if (FormsValues.String(Get(control, "AccessibleName")) is { } accessible)
        {
            element.Add(new XAttribute("AutomationProperties.Name", accessible));
        }

        if (!isRoot && FormsValues.Pair(Get(control, "MinimumSize")) is { } minimum)
        {
            AddIfPositive(element, "MinWidth", minimum.A);
            AddIfPositive(element, "MinHeight", minimum.B * _scale);
        }

        if (!isRoot && FormsValues.Pair(Get(control, "MaximumSize")) is { } maximum)
        {
            AddIfPositive(element, "MaxWidth", maximum.A);
            AddIfPositive(element, "MaxHeight", maximum.B * _scale);
        }

        if (Get(control, "ContextMenuStrip") is IdentifierNameSyntax or MemberAccessExpressionSyntax
            && DesignerName(Get(control, "ContextMenuStrip")!) is { } menuName && _design.Components.TryGetValue(menuName, out var menu))
        {
            var context = new XElement(XamlNamespaces.Avalonia + "ContextMenu", menu.Items.Select(MenuEntry));
            element.Add(new XElement(element.Name + ".ContextMenu", context));
            _report.Converted("ContextMenuStrip → ContextMenu");
        }
    }

    private static readonly HashSet<string> ApproximateProperties = new(StringComparer.Ordinal)
    {
        "CausesValidation", "ImeMode", "AutoEllipsis", "ImageAlign", "TextImageRelation", "RightToLeft",
        "MaximizeBox", "MinimizeBox", "KeyPreview", "ShowIcon", "ControlBox", "SizeGripStyle", "HelpButton",
        "ColumnHeadersVisible", "AllowUserToAddRows", "AllowUserToDeleteRows", "AllowUserToResizeRows",
        "AllowUserToOrderColumns", "EnableHeadersVisualStyles", "GridColor", "CellBorderStyle", "BackgroundColor",
        "AlternatingRowsDefaultCellStyle", "DefaultCellStyle", "ColumnHeadersDefaultCellStyle", "RowTemplate",
        "AutoSizeRowsMode", "FixedPanel", "IsSplitterFixed", "SplitterWidth", "BackgroundImageLayout",
        "IntegralHeight", "ItemHeight", "Sorted", "AutoCompleteMode", "AutoCompleteSource", "DropDownHeight",
        "DropDownWidth", "MaxDropDownItems", "FlatAppearance.BorderSize", "FlatAppearance.BorderColor",
        "FlatAppearance.MouseOverBackColor", "FlatAppearance.MouseDownBackColor", "HideSelection", "FullRowSelect",
        "CheckAlign", "LinkBehavior", "LinkColor", "ActiveLinkColor", "VisitedLinkColor", "ShortcutsEnabled",
    };

    /// <summary>Notes for properties the converter set aside that change how the control looks or works.</summary>
    private void NoteUnported(XElement element, FormsComponent control)
    {
        foreach (var (property, value) in control.Properties)
        {
            if (Dropped.Contains(property) && !ApproximateProperties.Contains(property))
            {
                continue;
            }

            Note(element, $"{control.Name}.{property} = {value}", $"The converter does not port {control.Type}.{property}; set the Avalonia {element.Name.LocalName}'s equivalent, if it has one");
        }

        if (Get(control, "Image") is { } image)
        {
            Note(element, $"{control.Name}.Image", "The image is in the form's .resx; save it in Assets and show it with an Image whose Source is \"/Assets/name.png\"");
            _ = image;
        }
    }

    private static string? DesignerName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };

    private XElement Simple(FormsComponent control, string type)
    {
        _report.Converted($"{control.Type} → {type}");
        return new XElement(XamlNamespaces.Avalonia + type);
    }

    private XElement Button(FormsComponent control)
    {
        var button = new XElement(XamlNamespaces.Avalonia + "Button", new XAttribute("Content", FormsValues.AccessKeyText(FormsValues.String(Get(control, "Text")) ?? string.Empty)));
        if (DesignerName(Get(_design.Form, "AcceptButton") ?? SyntaxFactoryNull) == control.Name)
        {
            button.Add(new XAttribute("IsDefault", "True"));
            _report.Converted("AcceptButton → IsDefault");
        }

        if (DesignerName(Get(_design.Form, "CancelButton") ?? SyntaxFactoryNull) == control.Name)
        {
            button.Add(new XAttribute("IsCancel", "True"));
            _report.Converted("CancelButton → IsCancel");
        }

        if (FormsValues.EnumMember(Get(control, "DialogResult")) is { } result and not "None")
        {
            // A button with a DialogResult closes its dialog with that answer.
            Info.Statements.Add($"{control.Name}.Click += (_, _) => DialogResult = DialogResult.{result};");
            _report.Converted("Button DialogResult → closes the dialog with it");
        }

        if (FormsValues.EnumMember(Get(control, "TextAlign")) is { } align)
        {
            button.Add(new XAttribute("HorizontalContentAlignment", align.EndsWith("Left", StringComparison.Ordinal) ? "Left" : align.EndsWith("Right", StringComparison.Ordinal) ? "Right" : "Center"));
        }

        _report.Converted("Button → Button");
        return button;
    }

    private XElement Text(FormsComponent control, string type)
    {
        var text = new XElement(XamlNamespaces.Avalonia + type, new XAttribute("Text", (FormsValues.String(Get(control, "Text")) ?? string.Empty).Replace("&&", "&", StringComparison.Ordinal)));
        if (FormsValues.EnumMember(Get(control, "TextAlign")) is { } align && FormsValues.Boolean(Get(control, "AutoSize")) is not true)
        {
            text.Add(new XAttribute("TextAlignment", align.EndsWith("Left", StringComparison.Ordinal) ? "Left" : align.EndsWith("Right", StringComparison.Ordinal) ? "Right" : "Center"));
            text.Add(new XAttribute("VerticalAlignment", align.StartsWith("Top", StringComparison.Ordinal) ? "Top" : align.StartsWith("Bottom", StringComparison.Ordinal) ? "Bottom" : "Center"));
        }

        _report.Converted("Label → TextBlock");
        return text;
    }

    private XElement Link(FormsComponent control)
    {
        _report.Converted("LinkLabel → HyperlinkButton");
        return new XElement(XamlNamespaces.Avalonia + "HyperlinkButton", new XAttribute("Content", FormsValues.String(Get(control, "Text")) ?? string.Empty));
    }

    private XElement TextBox(FormsComponent control)
    {
        var box = new XElement(XamlNamespaces.Avalonia + (control.Type == "MaskedTextBox" ? "MaskedTextBox" : "TextBox"));
        if (FormsValues.String(Get(control, "Text")) is { Length: > 0 } text)
        {
            box.Add(new XAttribute("Text", text));
        }

        var multiline = control.Type == "RichTextBox" || FormsValues.Boolean(Get(control, "Multiline")) is true;
        if (multiline)
        {
            box.Add(new XAttribute("AcceptsReturn", "True"));
            if (FormsValues.Boolean(Get(control, "WordWrap")) is not false)
            {
                box.Add(new XAttribute("TextWrapping", "Wrap"));
            }
        }

        if (FormsValues.Boolean(Get(control, "ReadOnly")) is true)
        {
            box.Add(new XAttribute("IsReadOnly", "True"));
        }

        if (Get(control, "PasswordChar") is LiteralExpressionSyntax { Token.ValueText: { Length: 1 } character })
        {
            box.Add(new XAttribute("PasswordChar", character));
        }
        else if (FormsValues.Boolean(Get(control, "UseSystemPasswordChar")) is true)
        {
            box.Add(new XAttribute("PasswordChar", "●"));
        }

        if (FormsValues.Number(Get(control, "MaxLength")) is { } maxLength and > 0 and < 32767)
        {
            box.Add(new XAttribute("MaxLength", Round(maxLength)));
        }

        if (FormsValues.String(Get(control, "PlaceholderText")) is { } placeholder)
        {
            box.Add(new XAttribute("Watermark", placeholder));
            _report.Converted("PlaceholderText → Watermark");
        }

        if (FormsValues.String(Get(control, "Mask")) is { } mask)
        {
            box.Add(new XAttribute("Mask", mask));
        }

        switch (FormsValues.EnumMember(Get(control, "ScrollBars")))
        {
            case "Vertical" or "Both":
                box.Add(new XAttribute("ScrollViewer.VerticalScrollBarVisibility", "Auto"));
                break;
        }

        if (FormsValues.EnumMember(Get(control, "TextAlign")) is var align && align is "Right" or "Center")
        {
            box.Add(new XAttribute("TextAlignment", align));
        }

        if (control.Type == "RichTextBox")
        {
            Note(box, $"{control.Name}: RichTextBox", "Avalonia has no rich text editor; it is a plain TextBox now. Use AvaloniaEdit for formatted text");
        }

        _report.Converted($"{control.Type} → {box.Name.LocalName}");
        return box;
    }

    private XElement Check(FormsComponent control)
    {
        var check = new XElement(XamlNamespaces.Avalonia + control.Type, new XAttribute("Content", FormsValues.AccessKeyText(FormsValues.String(Get(control, "Text")) ?? string.Empty)));
        var state = FormsValues.EnumMember(Get(control, "CheckState"));
        if (FormsValues.Boolean(Get(control, "Checked")) is true || state == "Checked")
        {
            check.Add(new XAttribute("IsChecked", "True"));
        }
        else if (state == "Indeterminate")
        {
            check.Add(new XAttribute("IsChecked", "{x:Null}"));
        }

        if (FormsValues.Boolean(Get(control, "ThreeState")) is true)
        {
            check.Add(new XAttribute("IsThreeState", "True"));
        }

        _report.Converted($"{control.Type} → {control.Type}");
        return check;
    }

    private XElement List(FormsComponent control)
    {
        var list = new XElement(XamlNamespaces.Avalonia + (control.Type == "ComboBox" ? "ComboBox" : "ListBox"));
        if (control.Type == "ComboBox" && FormsValues.EnumMember(Get(control, "DropDownStyle")) is "DropDown" or null && control.Property("DropDownStyle") is null)
        {
            // The designer's default is an editable combo box.
            list.Add(new XAttribute("IsEditable", "True"));
        }

        switch (FormsValues.EnumMember(Get(control, "SelectionMode")))
        {
            case "MultiSimple":
                list.Add(new XAttribute("SelectionMode", "Multiple,Toggle"));
                break;
            case "MultiExtended":
                list.Add(new XAttribute("SelectionMode", "Multiple"));
                break;
        }

        if (control.ListItems.Count > 0)
        {
            var system = XamlNamespaces.Using("System");
            if (_root.GetPrefixOfNamespace(system) is null)
            {
                _root.Add(new XAttribute(XNamespace.Xmlns + "sys", system.NamespaceName));
            }

            foreach (var item in control.ListItems)
            {
                list.Add(FormsValues.String(item) is { } text
                    ? new XElement(system + "String", text)
                    : new XElement(system + "String", item.ToString()));
            }

            _report.Converted("Items.AddRange → items in XAML");
        }

        if (control.Type == "CheckedListBox")
        {
            Note(list, $"{control.Name}: CheckedListBox", "Avalonia's ListBox has no check boxes; give it an ItemTemplate with a CheckBox bound to the item");
        }

        _report.Converted($"{control.Type} → {list.Name.LocalName}");
        return list;
    }

    private XElement UpDown(FormsComponent control)
    {
        var upDown = new XElement(XamlNamespaces.Avalonia + "NumericUpDown");
        foreach (var name in (string[])["Minimum", "Maximum", "Increment", "Value"])
        {
            var value = FormsValues.Number(Get(control, name)) ?? name switch
            {
                "Maximum" => 100,
                "Increment" => 1,
                _ => 0,
            };
            upDown.Add(new XAttribute(name, value.ToString(CultureInfo.InvariantCulture)));
        }

        if (FormsValues.Number(Get(control, "DecimalPlaces")) is { } places and > 0)
        {
            upDown.Add(new XAttribute("FormatString", "F" + Round(places)));
        }
        else
        {
            upDown.Add(new XAttribute("FormatString", "0"));
        }

        _report.Converted("NumericUpDown → NumericUpDown");
        return upDown;
    }

    private XElement Date(FormsComponent control)
    {
        var format = FormsValues.EnumMember(Get(control, "Format"));
        if (format == "Time")
        {
            _report.Converted("DateTimePicker with time → TimePicker");
            return new XElement(XamlNamespaces.Avalonia + "TimePicker");
        }

        var picker = new XElement(XamlNamespaces.Avalonia + "CalendarDatePicker");
        if (format == "Long")
        {
            picker.Add(new XAttribute("SelectedDateFormat", "Long"));
        }
        else if (format == "Custom" && FormsValues.String(Get(control, "CustomFormat")) is { } custom)
        {
            picker.Add(new XAttribute("SelectedDateFormat", "Custom"), new XAttribute("CustomDateFormatString", custom));
        }

        _report.Converted("DateTimePicker → CalendarDatePicker");
        return picker;
    }

    private XElement Picture(FormsComponent control)
    {
        var image = new XElement(XamlNamespaces.Avalonia + "Image", new XAttribute("Stretch", FormsValues.EnumMember(Get(control, "SizeMode")) switch
        {
            "StretchImage" => "Fill",
            "Zoom" => "Uniform",
            _ => "None",
        }));
        _report.Converted("PictureBox → Image");
        return image;
    }

    private XElement Progress(FormsComponent control)
    {
        var bar = new XElement(XamlNamespaces.Avalonia + "ProgressBar");
        foreach (var name in (string[])["Minimum", "Maximum", "Value"])
        {
            if (FormsValues.Number(Get(control, name)) is { } value)
            {
                bar.Add(new XAttribute(name, value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (FormsValues.EnumMember(Get(control, "Style")) is "Marquee")
        {
            bar.Add(new XAttribute("IsIndeterminate", "True"));
        }

        _report.Converted("ProgressBar → ProgressBar");
        return bar;
    }

    private XElement Slider(FormsComponent control)
    {
        var slider = new XElement(XamlNamespaces.Avalonia + "Slider");
        foreach (var (forms, avalonia) in (ReadOnlySpan<(string, string)>)[("Minimum", "Minimum"), ("Maximum", "Maximum"), ("Value", "Value"), ("TickFrequency", "TickFrequency"), ("SmallChange", "SmallChange"), ("LargeChange", "LargeChange")])
        {
            if (FormsValues.Number(Get(control, forms)) is { } value)
            {
                slider.Add(new XAttribute(avalonia, value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (FormsValues.Number(Get(control, "Maximum")) is null)
        {
            slider.Add(new XAttribute("Maximum", "10"));
        }

        if (FormsValues.EnumMember(Get(control, "Orientation")) is "Vertical")
        {
            slider.Add(new XAttribute("Orientation", "Vertical"));
        }

        slider.Add(new XAttribute("TickPlacement", FormsValues.EnumMember(Get(control, "TickStyle")) switch
        {
            "None" => "None",
            "TopLeft" => "TopLeft",
            "Both" => "Outside",
            _ => "BottomRight",
        }));
        slider.Add(new XAttribute("IsSnapToTickEnabled", "True"));
        _report.Converted("TrackBar → Slider");
        return slider;
    }

    private XElement Group(FormsComponent control)
    {
        var group = new XElement(XamlNamespaces.Avalonia + "HeaderedContentControl", new XAttribute("Classes", "groupBox"),
            new XAttribute("Header", FormsValues.String(Get(control, "Text")) ?? string.Empty), new XAttribute("Padding", "0"));
        var (width, height) = Size(control);
        group.Add(new XAttribute("Width", Round(width)), new XAttribute("Height", Round(height * _scale)));
        if (Layout(control, control.Children, width, height - 18, 0, 18) is { } content)
        {
            group.Add(content);
        }

        _context.Needs(SupportNeeds.Styles);
        _report.Converted("GroupBox → HeaderedContentControl with the port's group box style");
        return group;
    }

    private XElement Panel(FormsComponent control)
    {
        var (width, height) = Size(control);
        var content = Layout(control, control.Children, width, height, 0, 0) ?? new XElement(XamlNamespaces.Avalonia + "Panel");
        var bordered = FormsValues.EnumMember(Get(control, "BorderStyle")) is "FixedSingle" or "Fixed3D";
        var scrolls = FormsValues.Boolean(Get(control, "AutoScroll")) is true;
        if (scrolls)
        {
            content = new XElement(XamlNamespaces.Avalonia + "ScrollViewer", new XAttribute("HorizontalScrollBarVisibility", "Auto"), content);
        }

        if (bordered)
        {
            content = new XElement(XamlNamespaces.Avalonia + "Border", new XAttribute("BorderThickness", "1"),
                new XAttribute("BorderBrush", "{DynamicResource SystemControlForegroundBaseLowBrush}"), content);
        }

        _report.Converted("Panel → " + content.Name.LocalName);
        return content;
    }

    private XElement Flow(FormsComponent control)
    {
        var vertical = FormsValues.EnumMember(Get(control, "FlowDirection")) is "TopDown" or "BottomUp";
        var wraps = FormsValues.Boolean(Get(control, "WrapContents")) is not false;
        var panel = new XElement(XamlNamespaces.Avalonia + (wraps ? "WrapPanel" : "StackPanel"));
        if (vertical)
        {
            panel.Add(new XAttribute("Orientation", "Vertical"));
        }
        else if (!wraps)
        {
            panel.Add(new XAttribute("Orientation", "Horizontal"));
        }

        foreach (var child in control.Children.Where(IsVisual))
        {
            var element = Element(child);
            element.Add(new XAttribute("Margin", FormsValues.Thickness(Get(child, "Margin")) ?? "3"));
            SetSize(element, child, width: true, height: true);
            panel.Add(element);
        }

        _report.Converted($"FlowLayoutPanel → {panel.Name.LocalName}");
        return panel;
    }

    private XElement Table(FormsComponent control)
    {
        var grid = new XElement(XamlNamespaces.Avalonia + "Grid");
        static string Track(FormsTrack track) => track.SizeType switch
        {
            "Percent" => track.Value.ToString(CultureInfo.InvariantCulture) + "*",
            "Absolute" => track.Value.ToString(CultureInfo.InvariantCulture),
            _ => "Auto",
        };

        var columns = (int)(FormsValues.Number(Get(control, "ColumnCount")) ?? Math.Max(1, control.ColumnStyles.Count));
        var rows = (int)(FormsValues.Number(Get(control, "RowCount")) ?? Math.Max(1, control.RowStyles.Count));
        grid.Add(new XAttribute("ColumnDefinitions", string.Join(',', Enumerable.Range(0, columns).Select(i => i < control.ColumnStyles.Count ? Track(control.ColumnStyles[i]) : "*"))));
        grid.Add(new XAttribute("RowDefinitions", string.Join(',', Enumerable.Range(0, rows).Select(i => i < control.RowStyles.Count ? Track(control.RowStyles[i]) : "Auto"))));
        var next = 0;
        foreach (var child in control.Children.Where(IsVisual))
        {
            var element = Element(child);
            var (column, row) = child.Cell ?? (next % Math.Max(1, columns), next / Math.Max(1, columns));
            next++;
            if (column > 0)
            {
                element.Add(new XAttribute("Grid.Column", column));
            }

            if (row > 0)
            {
                element.Add(new XAttribute("Grid.Row", row));
            }

            if (child.ColumnSpan > 1)
            {
                element.Add(new XAttribute("Grid.ColumnSpan", child.ColumnSpan));
            }

            if (child.RowSpan > 1)
            {
                element.Add(new XAttribute("Grid.RowSpan", child.RowSpan));
            }

            element.Add(new XAttribute("Margin", FormsValues.Thickness(Get(child, "Margin")) ?? "3"));
            if (DockOf(child) == "Fill")
            {
                element.Add(new XAttribute("HorizontalAlignment", "Stretch"), new XAttribute("VerticalAlignment", "Stretch"));
            }
            else
            {
                var anchors = AnchorsOf(child);
                element.Add(new XAttribute("HorizontalAlignment", anchors.Contains("Left") && anchors.Contains("Right") ? "Stretch" : anchors.Contains("Right") ? "Right" : anchors.Contains("Left") ? "Left" : "Center"));
                element.Add(new XAttribute("VerticalAlignment", anchors.Contains("Top") && anchors.Contains("Bottom") ? "Stretch" : anchors.Contains("Bottom") ? "Bottom" : anchors.Contains("Top") ? "Top" : "Center"));
                SetSize(element, child, width: !(anchors.Contains("Left") && anchors.Contains("Right")), height: !(anchors.Contains("Top") && anchors.Contains("Bottom")));
            }

            grid.Add(element);
        }

        _report.Converted("TableLayoutPanel → Grid");
        return grid;
    }

    private XElement Split(FormsComponent control)
    {
        var horizontal = FormsValues.EnumMember(Get(control, "Orientation")) is "Horizontal";
        var distance = FormsValues.Number(Get(control, "SplitterDistance")) ?? 100;
        var (width, height) = Size(control);
        var grid = new XElement(XamlNamespaces.Avalonia + "Grid", new XAttribute(horizontal ? "RowDefinitions" : "ColumnDefinitions", $"{Round(horizontal ? distance * _scale : distance)},4,*"));
        var first = Layout(control, control.Children, horizontal ? width : distance, horizontal ? distance : height, 0, 0) ?? new XElement(XamlNamespaces.Avalonia + "Panel");
        var second = Layout(control, control.SecondPanel, horizontal ? width : width - distance - 4, horizontal ? height - distance - 4 : height, 0, 0) ?? new XElement(XamlNamespaces.Avalonia + "Panel");
        var position = horizontal ? "Grid.Row" : "Grid.Column";
        grid.Add(first);
        grid.Add(new XElement(XamlNamespaces.Avalonia + "GridSplitter", new XAttribute(position, "1"), new XAttribute("ResizeDirection", horizontal ? "Rows" : "Columns")));
        second.Add(new XAttribute(position, "2"));
        grid.Add(second);
        _report.Converted("SplitContainer → Grid with a GridSplitter");
        return grid;
    }

    private XElement Tabs(FormsComponent control)
    {
        var tabs = new XElement(XamlNamespaces.Avalonia + "TabControl");
        var (width, height) = Size(control);
        foreach (var page in control.Children.Where(c => c.Type == "TabPage"))
        {
            var item = new XElement(XamlNamespaces.Avalonia + "TabItem", new XAttribute(Name, page.Name), new XAttribute("Header", FormsValues.String(Get(page, "Text")) ?? page.Name));
            Info.Components[page.Name] = ("TabPage", "TabItem");
            if (Layout(page, page.Children, width - 8, height - 30, 0, 0) is { } content)
            {
                item.Add(content);
            }

            AddEvents(page, "TabItem");
            tabs.Add(item);
        }

        _report.Converted("TabControl and TabPages → TabControl and TabItems");
        return tabs;
    }

    private XElement Menu(FormsComponent control)
    {
        var menu = new XElement(XamlNamespaces.Avalonia + "Menu", control.Items.Select(MenuEntry));
        _report.Converted("MenuStrip → Menu");
        return menu;
    }

    /// <summary>A menu's item: a menu item with its access key and shortcut, a separator, or its drop-down items.</summary>
    private XElement MenuEntry(FormsComponent item)
    {
        if (item.Type == "ToolStripSeparator")
        {
            return new XElement(XamlNamespaces.Avalonia + "Separator");
        }

        var entry = new XElement(XamlNamespaces.Avalonia + "MenuItem", new XAttribute(Name, item.Name),
            new XAttribute("Header", FormsValues.AccessKeyText(FormsValues.String(Get(item, "Text")) ?? item.Name)));
        Info.Components[item.Name] = (item.Type, "MenuItem");
        if (FormsValues.KeyGesture(Get(item, "ShortcutKeys")) is { } gesture)
        {
            entry.Add(new XAttribute("InputGesture", gesture), new XAttribute("HotKey", gesture));
            _report.Converted("ShortcutKeys → InputGesture and HotKey");
        }

        if (FormsValues.Boolean(Get(item, "CheckOnClick")) is true || FormsValues.Boolean(Get(item, "Checked")) is true)
        {
            entry.Add(new XAttribute("ToggleType", "CheckBox"));
            if (FormsValues.Boolean(Get(item, "Checked")) is true)
            {
                entry.Add(new XAttribute("IsChecked", "True"));
            }
        }

        if (FormsValues.Boolean(Get(item, "Enabled")) is false)
        {
            entry.Add(new XAttribute("IsEnabled", "False"));
        }

        foreach (var child in item.Items)
        {
            entry.Add(MenuEntry(child));
        }

        AddEvents(item, "MenuItem");
        _report.Converted("ToolStripMenuItem → MenuItem");
        return entry;
    }

    private XElement ToolBar(FormsComponent control)
    {
        var row = new XElement(XamlNamespaces.Avalonia + "StackPanel", new XAttribute("Orientation", "Horizontal"), new XAttribute("Spacing", "2"));
        foreach (var item in control.Items)
        {
            row.Add(StripItem(item));
        }

        _context.Needs(SupportNeeds.Styles);
        _report.Converted("ToolStrip → a row of buttons (Avalonia has no toolbar)");
        return new XElement(XamlNamespaces.Avalonia + "Border", new XAttribute("Classes", "toolBar"), row);
    }

    private XElement StatusBar(FormsComponent control)
    {
        var panel = new XElement(XamlNamespaces.Avalonia + "DockPanel");
        var spring = control.Items.FirstOrDefault(i => FormsValues.Boolean(Get(i, "Spring")) is true);
        foreach (var item in control.Items.Where(i => i != spring))
        {
            var element = StripItem(item);
            element.Add(new XAttribute("DockPanel.Dock", "Left"), new XAttribute("Margin", "0,0,12,0"));
            panel.Add(element);
        }

        if (spring is not null)
        {
            panel.Add(StripItem(spring));
        }
        else
        {
            panel.Add(new XAttribute("LastChildFill", "False"));
        }

        _context.Needs(SupportNeeds.Styles);
        _report.Converted("StatusStrip → Border and DockPanel (Avalonia has no status bar)");
        return new XElement(XamlNamespaces.Avalonia + "Border", new XAttribute("Classes", "statusBar"), panel);
    }

    /// <summary>An item of a toolbar or status strip.</summary>
    private XElement StripItem(FormsComponent item)
    {
        var text = FormsValues.String(Get(item, "Text")) ?? string.Empty;
        var element = item.Type switch
        {
            "ToolStripStatusLabel" or "ToolStripLabel" => new XElement(XamlNamespaces.Avalonia + "TextBlock", new XAttribute("Text", text), new XAttribute("VerticalAlignment", "Center")),
            "ToolStripButton" => new XElement(XamlNamespaces.Avalonia + "Button", new XAttribute("Content", FormsValues.AccessKeyText(text))),
            "ToolStripComboBox" => new XElement(XamlNamespaces.Avalonia + "ComboBox"),
            "ToolStripTextBox" => new XElement(XamlNamespaces.Avalonia + "TextBox", new XAttribute("Text", text)),
            "ToolStripProgressBar" => new XElement(XamlNamespaces.Avalonia + "ProgressBar", new XAttribute("Width", "100")),
            "ToolStripSeparator" => new XElement(XamlNamespaces.Avalonia + "Border", new XAttribute("Width", "1"), new XAttribute("Margin", "4,2"),
                new XAttribute("Background", "{DynamicResource SystemControlForegroundBaseLowBrush}")),
            _ => new XElement(XamlNamespaces.Avalonia + "Button", new XAttribute("Content", FormsValues.AccessKeyText(text))),
        };

        if (item.Type != "ToolStripSeparator")
        {
            element.Add(new XAttribute(Name, item.Name));
            Info.Components[item.Name] = (item.Type, element.Name.LocalName);
            AddEvents(item, element.Name.LocalName);
        }

        if (FormsValues.String(Get(item, "ToolTipText")) is { } tip)
        {
            element.Add(new XAttribute("ToolTip.Tip", tip));
        }

        if (item.Type is "ToolStripDropDownButton" or "ToolStripSplitButton")
        {
            Note(element, $"{item.Name}: {item.Type}", "It became a Button; give it a Flyout with a MenuFlyout of its items, or use a DropDownButton or SplitButton");
        }

        return element;
    }

    private XElement Grid(FormsComponent control)
    {
        _context.UsesDataGrid = true;
        var grid = new XElement(XamlNamespaces.Avalonia + "DataGrid", new XAttribute("AutoGenerateColumns", "False"));
        if (FormsValues.Boolean(Get(control, "ReadOnly")) is true)
        {
            grid.Add(new XAttribute("IsReadOnly", "True"));
        }

        if (FormsValues.Boolean(Get(control, "MultiSelect")) is false)
        {
            grid.Add(new XAttribute("SelectionMode", "Single"));
        }

        if (FormsValues.Boolean(Get(control, "RowHeadersVisible")) is false)
        {
            grid.Add(new XAttribute("HeadersVisibility", "Column"));
        }

        var fillAll = FormsValues.EnumMember(Get(control, "AutoSizeColumnsMode")) is "Fill";
        var bound = control.Columns.Any(c => Get(c, "DataPropertyName") is not null);
        var columns = new XElement(XamlNamespaces.Avalonia + "DataGrid.Columns");
        for (var i = 0; i < control.Columns.Count; i++)
        {
            var column = control.Columns[i];
            var binding = FormsValues.String(Get(column, "DataPropertyName")) is { } path ? $"{{Binding {path}}}" : $"{{Binding Cells[{i}].Value}}";
            var type = column.Type switch
            {
                "DataGridViewCheckBoxColumn" => "DataGridCheckBoxColumn",
                _ => "DataGridTextColumn",
            };
            var element = new XElement(XamlNamespaces.Avalonia + type, new XAttribute("Header", FormsValues.String(Get(column, "HeaderText")) ?? column.Name), new XAttribute("Binding", binding));
            if (fillAll || FormsValues.EnumMember(Get(column, "AutoSizeMode")) is "Fill")
            {
                element.Add(new XAttribute("Width", "*"));
            }
            else
            {
                element.Add(new XAttribute("Width", Round(FormsValues.Number(Get(column, "Width")) ?? 100)));
            }

            if (FormsValues.Boolean(Get(column, "ReadOnly")) is true)
            {
                element.Add(new XAttribute("IsReadOnly", "True"));
            }

            if (column.Type is "DataGridViewComboBoxColumn" or "DataGridViewButtonColumn" or "DataGridViewLinkColumn" or "DataGridViewImageColumn")
            {
                Note(element, $"{column.Name}: {column.Type}", "Avalonia's DataGrid shows it as text now; use a DataGridTemplateColumn with a ComboBox, a Button, a HyperlinkButton or an Image");
            }

            columns.Add(element);
        }

        if (columns.HasElements)
        {
            grid.Add(columns);
            _report.Converted("DataGridView columns → DataGrid columns", control.Columns.Count);
        }

        if (!bound)
        {
            // Rows added in code: the grid shows a GridRows collection, whose cells the columns bind.
            var rows = control.Name + "Rows";
            _context.Needs(SupportNeeds.GridRows);
            Info.GridRows[control.Name] = rows;
            Info.Fields.Add($"private readonly GridRows {rows} = new({string.Join(", ", control.Columns.Select(c => "\"" + c.Name + "\""))});");
            Info.Statements.Add($"{control.Name}.ItemsSource = {rows};");
        }

        _report.Converted("DataGridView → DataGrid");
        return grid;
    }

    private XElement ListView(FormsComponent control)
    {
        if (FormsValues.EnumMember(Get(control, "View")) is "Details" && control.Columns.Count > 0)
        {
            _context.UsesDataGrid = true;
            var grid = new XElement(XamlNamespaces.Avalonia + "DataGrid", new XAttribute("AutoGenerateColumns", "False"), new XAttribute("IsReadOnly", "True"), new XAttribute("HeadersVisibility", "Column"),
                new XElement(XamlNamespaces.Avalonia + "DataGrid.Columns", control.Columns.Select((c, i) => new XElement(XamlNamespaces.Avalonia + "DataGridTextColumn",
                    new XAttribute("Header", FormsValues.String(Get(c, "Text")) ?? c.Name), new XAttribute("Binding", $"{{Binding [{i}]}}"), new XAttribute("Width", Round(FormsValues.Number(Get(c, "Width")) ?? 100))))));
            Note(grid, $"{control.Name}: ListView", "It became a DataGrid whose columns show the items' values by position; fill its ItemsSource with arrays or with objects and bindings by name, in place of ListViewItems");
            _report.Converted("ListView in details view → DataGrid");
            return grid;
        }

        Note(control.Name == "this" ? _root : new XElement("x"), $"{control.Name}: ListView", "It became a ListBox; give it an ItemTemplate and an ItemsPanel for its view");
        _report.Converted("ListView → ListBox");
        return new XElement(XamlNamespaces.Avalonia + "ListBox");
    }

    private XElement Placeholder(FormsComponent control)
    {
        var placeholder = new XElement(XamlNamespaces.Avalonia + "Border");
        Note(placeholder, $"{control.Name}: {control.Type}", $"Avalonia has no {control.Type}, so a placeholder keeps its place; use an Avalonia control or package that does what it did");
        return placeholder;
    }

    /// <summary>A control's client size in the designer.</summary>
    private static (double Width, double Height) Size(FormsComponent control) =>
        Bounds(control) is { } bounds ? (bounds.Width, bounds.Height) : (200, 100);

    private static ExpressionSyntax SyntaxFactoryNull => Microsoft.CodeAnalysis.CSharp.SyntaxFactory.IdentifierName("null");
}
