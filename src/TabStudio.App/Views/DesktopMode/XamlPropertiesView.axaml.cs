using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels.DesktopMode;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// The companion's Properties panel for the XAML element at the caret. A typed value is written to the XAML on
/// Enter, or when its field is left; Escape gives the edit up.
/// </summary>
public sealed partial class XamlPropertiesView : UserControl
{
    /// <summary>Creates the view.</summary>
    public XamlPropertiesView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(LostFocusEvent, OnLostFocus, RoutingStrategies.Bubble);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not TextBox box)
        {
            return;
        }

        if (box.DataContext is XamlPropertyRowViewModel row)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    row.Commit();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    row.Revert();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.Enter && (ReferenceEquals(box, NewNameBox) || ReferenceEquals(box, NewValueBox))
            && DataContext is XamlPropertiesViewModel properties)
        {
            properties.AddPropertyCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox { DataContext: XamlPropertyRowViewModel row })
        {
            row.CommitOnLeave();
        }
    }
}
