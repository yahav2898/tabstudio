using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>A DataTip: the value of the expression under the pointer, expandable to its members.</summary>
public sealed partial class DataTipView : UserControl
{
    /// <summary>Creates the view.</summary>
    public DataTipView()
    {
        InitializeComponent();
        Rows.AddHandler(TappedEvent, OnRowTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is VariableTreeViewModel tree
            && (e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is VariableViewModel row)
        {
            tree.ToggleCommand.Execute(row);
        }
    }
}
