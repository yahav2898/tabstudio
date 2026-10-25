using System.Globalization;
using TabStudio.Porting.Output;

namespace TabStudio.Porting.Forms;

/// <summary>Connects designer events after XAML has created the controls.</summary>
internal sealed partial class FormsXamlBuilder
{
    private void AddEvents(FormsComponent control, string avaloniaType)
    {
        foreach (var subscription in control.Events)
        {
            var target = control.Name == "this" ? string.Empty : control.Name + ".";
            var mapped = subscription.Event switch
            {
                "Click" when avaloniaType is "Button" or "MenuItem" => "Click",
                "Click" => "Tapped",
                "DoubleClick" => "DoubleTapped",
                "CheckedChanged" => "IsCheckedChanged",
                "TextChanged" => "TextChanged",
                "SelectedIndexChanged" or "SelectedValueChanged" or "SelectionChanged" => "SelectionChanged",
                "ValueChanged" => "ValueChanged",
                "MouseDown" => "PointerPressed",
                "MouseUp" => "PointerReleased",
                "MouseMove" => "PointerMoved",
                "MouseEnter" => "PointerEntered",
                "MouseLeave" => "PointerExited",
                "KeyDown" or "KeyUp" => subscription.Event,
                "Load" when control.Name == "this" && avaloniaType == "UserControl" => "Loaded",
                "Load" or "Shown" or "FormClosing" or "FormClosed" when control.Name == "this" && avaloniaType == "AppForm" => subscription.Event,
                _ => null,
            };
            if (mapped is null)
            {
                Todo($"{target}{subscription.Event}", "Avalonia has no exact event mapping; connect an equivalent event and adapt the handler", $"{target}{subscription.Event} += {subscription.Handler};");
                continue;
            }

            var args = mapped switch
            {
                "PointerPressed" => "PointerPressedEventArgs",
                "PointerReleased" => "PointerReleasedEventArgs",
                "PointerMoved" or "PointerEntered" or "PointerExited" => "PointerEventArgs",
                "KeyDown" or "KeyUp" => "KeyEventArgs",
                _ => "EventArgs",
            };
            if (args != "EventArgs")
            {
                Info.HandlerArguments[subscription.Handler] = args;
                Info.Usings.Add("Avalonia.Input");
            }

            if (mapped == "FormClosing")
            {
                mapped = "FormClosingAsync";
                Info.AsyncClosingHandlers.Add(subscription.Handler);
            }

            Info.Statements.Add($"{target}{mapped} += {subscription.Handler};");
            _report.Converted($"{subscription.Event} → {mapped} event");
        }
    }

    private void AddComponent(FormsComponent component)
    {
        Info.Components[component.Name] = (component.Type, null);
        switch (component.Type)
        {
            case "Timer":
                Info.Usings.Add("Avalonia.Threading");
                Info.Fields.Add($"private readonly DispatcherTimer {component.Name} = new();");
                if (FormsValues.Number(component.Property("Interval")) is { } interval)
                {
                    Info.Statements.Add($"{component.Name}.Interval = TimeSpan.FromMilliseconds({interval.ToString(CultureInfo.InvariantCulture)});");
                }

                foreach (var handler in component.Events.Where(e => e.Event == "Tick"))
                {
                    Info.Statements.Add($"{component.Name}.Tick += {handler.Handler};");
                }

                if (FormsValues.Boolean(component.Property("Enabled")) is true)
                {
                    Info.Statements.Add($"{component.Name}.Start();");
                }

                Info.Statements.Add($"{(_design.BaseType == "Form" ? "Closed" : "DetachedFromVisualTree")} += (_, _) => {component.Name}.Stop();");
                _report.Converted("Timer → DispatcherTimer, stopped when the form closes");
                break;
            case "ToolTip":
            case "DataGridViewTextBoxColumn":
            case "DataGridViewCheckBoxColumn":
            case "ColumnHeader":
                break;
            default:
                Todo($"{component.Name}: {component.Type}", "This Windows Forms component has no exact Avalonia equivalent; recreate its behavior using a cross-platform service", null);
                break;
        }
    }
}
