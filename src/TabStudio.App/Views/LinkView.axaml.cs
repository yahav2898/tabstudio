using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The TabStudio Link dialog. Enter in the code box pairs; Escape closes.</summary>
public sealed partial class LinkView : UserControl
{
    /// <summary>Creates the view.</summary>
    public LinkView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not LinkViewModel { IsOpen: true } viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.CloseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (CodeBox.IsFocused || AddressBox.IsFocused) && viewModel.PairCommand.CanExecute(null))
        {
            viewModel.PairCommand.Execute(null);
            e.Handled = true;
        }
    }
}
