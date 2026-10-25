using System.Xml.Linq;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Forms;

/// <summary>
/// Layout: Windows Forms places controls at a location with a size, docks some to the edges, and anchors others
/// to the sides of their container. Docked controls go in a DockPanel; the others in a Canvas, or in a Grid with
/// margins and alignments when some are anchored to the right or the bottom, so they stretch and move the same way.
/// </summary>
internal sealed partial class FormsXamlBuilder
{
    /// <summary>Controls a container docks to its top unless the designer says otherwise.</summary>
    private static readonly HashSet<string> DockedTop = new(StringComparer.Ordinal) { "MenuStrip", "ToolStrip" };

    /// <summary>The bounds a control has in the designer: location and size.</summary>
    private static (double X, double Y, double Width, double Height)? Bounds(FormsComponent control)
    {
        var location = FormsValues.Pair(control.Property("Location")) ?? (0, 0);
        return FormsValues.Pair(control.Property("Size")) is { } size ? (location.A, location.B, size.A, size.B) : null;
    }

    /// <summary>How a control docks: Top, Bottom, Left, Right, Fill or None.</summary>
    private static string DockOf(FormsComponent control) =>
        FormsValues.EnumMember(control.Property("Dock")) ?? (DockedTop.Contains(control.Type) ? "Top" : control.Type == "StatusStrip" ? "Bottom" : "None");

    /// <summary>The sides a control is anchored to; Top and Left unless the designer says otherwise.</summary>
    private static HashSet<string> AnchorsOf(FormsComponent control)
    {
        var anchors = FormsValues.Flags(control.Property("Anchor"));
        return anchors.Count == 0 && control.Property("Anchor") is null ? ["Top", "Left"] : anchors;
    }

    /// <summary>Whether a control is one line tall, so Avalonia's control may take its own height.</summary>
    private static bool IsSingleLine(FormsComponent control) => control.Type switch
    {
        "TextBox" or "MaskedTextBox" => FormsValues.Boolean(control.Property("Multiline")) is not true,
        "ComboBox" or "NumericUpDown" or "DateTimePicker" or "CheckBox" or "RadioButton" or "LinkLabel" => true,
        "Button" => (Bounds(control)?.Height ?? 23) <= 36,
        "Label" => FormsValues.Boolean(control.Property("AutoSize")) is true || (Bounds(control)?.Height ?? 15) <= 30,
        _ => false,
    };

    /// <summary>
    /// The layout of a container's controls, or <see langword="null"/> when it has none.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="children">Its controls, in the order the designer added them.</param>
    /// <param name="width">Its client width.</param>
    /// <param name="height">Its client height.</param>
    /// <param name="offsetX">What to subtract from the controls' X, such as a group box's frame.</param>
    /// <param name="offsetY">What to subtract from the controls' Y, such as a group box's caption.</param>
    private XElement? Layout(FormsComponent container, List<FormsComponent> children, double width, double height, double offsetX, double offsetY)
    {
        var visual = children.Where(c => IsVisual(c)).ToList();
        if (visual.Count == 0)
        {
            return null;
        }

        // Windows Forms docks the control added last first, so the DockPanel takes them in reverse.
        var docked = visual.Where(c => DockOf(c) != "None").Reverse().ToList();
        var placed = visual.Where(c => DockOf(c) == "None").ToList();
        if (docked.Count == 0)
        {
            return Placed(placed, width, height, offsetX, offsetY);
        }

        var panel = new XElement(XamlNamespaces.Avalonia + "DockPanel");
        double top = 0, bottom = 0, left = 0, right = 0;
        XElement? fill = null;
        foreach (var control in docked)
        {
            var dock = DockOf(control);
            var element = Element(control);
            var bounds = Bounds(control);
            if (dock == "Fill")
            {
                fill = element;
                continue;
            }

            element.Add(new XAttribute("DockPanel.Dock", dock));
            switch (dock)
            {
                case "Top":
                    top += bounds?.Height ?? 0;
                    SetSize(element, control, width: false, height: !IsStrip(control));
                    break;
                case "Bottom":
                    bottom += bounds?.Height ?? 0;
                    SetSize(element, control, width: false, height: !IsStrip(control));
                    break;
                case "Left":
                    left += bounds?.Width ?? 0;
                    SetSize(element, control, width: true, height: false);
                    break;
                case "Right":
                    right += bounds?.Width ?? 0;
                    SetSize(element, control, width: true, height: false);
                    break;
            }

            panel.Add(element);
        }

        _report.Converted("Docked controls → DockPanel");
        var rest = placed.Count > 0 ? Placed(placed, width - left - right, height - top - bottom, offsetX + left, offsetY + top) : null;
        if (fill is not null && rest is not null)
        {
            // A filling control with controls placed over it: both in one cell.
            panel.Add(new XElement(XamlNamespaces.Avalonia + "Panel", fill, rest));
        }
        else if ((fill ?? rest) is { } last)
        {
            panel.Add(last);
        }
        else
        {
            panel.SetAttributeValue("LastChildFill", "False");
        }

        return panel;
    }

    private static bool IsStrip(FormsComponent control) => control.Type is "MenuStrip" or "ToolStrip" or "StatusStrip";

    /// <summary>
    /// Controls at fixed places: a Canvas when all keep to the top and left, else a Grid in which each control's
    /// margins and alignments keep it where its anchors do.
    /// </summary>
    private XElement Placed(List<FormsComponent> controls, double width, double height, double offsetX, double offsetY)
    {
        var anchored = controls.Any(c => AnchorsOf(c) is var anchors && (anchors.Contains("Right") || anchors.Contains("Bottom")));
        var panel = new XElement(XamlNamespaces.Avalonia + (anchored ? "Grid" : "Canvas"));
        _report.Converted(anchored ? "Anchored controls → Grid with margins" : "Controls at fixed places → Canvas");
        foreach (var control in controls)
        {
            var element = Element(control);
            var bounds = Bounds(control) ?? (0, 0, 0, 0);
            var x = bounds.X - offsetX;
            var y = (bounds.Y - offsetY) * _scale;
            var w = bounds.Width;
            var h = bounds.Height * _scale;
            if (!anchored)
            {
                element.Add(new XAttribute("Canvas.Left", Round(x)), new XAttribute("Canvas.Top", Round(y)));
                SetSize(element, control, width: true, height: true);
                panel.Add(element);
                continue;
            }

            var anchors = AnchorsOf(control);
            var horizontal = (anchors.Contains("Left"), anchors.Contains("Right")) switch
            {
                (true, true) => "Stretch",
                (false, true) => "Right",
                _ => "Left",
            };
            var vertical = (anchors.Contains("Top"), anchors.Contains("Bottom")) switch
            {
                (true, true) => "Stretch",
                (false, true) => "Bottom",
                _ => "Top",
            };

            var rightMargin = horizontal is "Stretch" or "Right" ? width - (bounds.X - offsetX + w) : 0;
            var bottomMargin = vertical is "Stretch" or "Bottom" ? (height - (bounds.Y - offsetY + bounds.Height)) * _scale : 0;
            var leftMargin = horizontal is "Right" ? 0 : x;
            var topMargin = vertical is "Bottom" ? 0 : y;
            element.Add(new XAttribute("Margin", $"{Round(leftMargin)},{Round(topMargin)},{Round(Math.Max(0, rightMargin))},{Round(Math.Max(0, bottomMargin))}"));
            if (horizontal != "Left")
            {
                element.Add(new XAttribute("HorizontalAlignment", horizontal));
            }
            else
            {
                element.Add(new XAttribute("HorizontalAlignment", "Left"));
            }

            element.Add(new XAttribute("VerticalAlignment", vertical));
            SetSize(element, control, width: horizontal != "Stretch", height: vertical != "Stretch");
            panel.Add(element);
        }

        return panel;
    }

    /// <summary>Gives an element its designer size, except where Avalonia's control should size itself.</summary>
    private void SetSize(XElement element, FormsComponent control, bool width, bool height)
    {
        if (Bounds(control) is not { } bounds)
        {
            return;
        }

        var autoSize = FormsValues.Boolean(control.Property("AutoSize")) is true || (control.Type is "Label" or "CheckBox" or "RadioButton" or "LinkLabel" && control.Property("AutoSize") is null);
        if (width && !autoSize && bounds.Width > 0)
        {
            element.SetAttributeValue("Width", Round(bounds.Width));
        }

        if (height && !autoSize && !IsSingleLine(control) && bounds.Height > 0)
        {
            element.SetAttributeValue("Height", Round(bounds.Height * _scale));
        }
    }
}
