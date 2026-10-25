using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The rows of the Locals and Watch panels: name, value and type. Tapping a row with members expands it; on a
/// keyboard, Enter and the arrow keys do the same. A long press or right-click offers Add Watch.
/// </summary>
public sealed partial class VariableListView : UserControl
{
    /// <summary>Creates the view.</summary>
    public VariableListView()
    {
        InitializeComponent();
        Rows.AddHandler(TappedEvent, OnRowTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Rows.KeyDown += OnRowsKeyDown;
    }

    private VariableTreeViewModel? ViewModel => DataContext as VariableTreeViewModel;

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        // The remove button handles its own taps.
        if (e.Source is Control source && source.FindAncestorOfType<Button>(includeSelf: true) is null
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is VariableViewModel row)
        {
            ViewModel?.ToggleCommand.Execute(row);
        }
    }

    private void OnRowsKeyDown(object? sender, KeyEventArgs e)
    {
        if (Rows.SelectedItem is not VariableViewModel row || ViewModel is not { } viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Right when row is { IsExpandable: true, IsExpanded: false }:
            case Key.Left when row.IsExpanded:
                viewModel.ToggleCommand.Execute(row);
                e.Handled = true;
                break;
        }
    }
}
