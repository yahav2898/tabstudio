using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The TabStudio Link dialog. Enter in the code box pairs; Escape closes. On a phone's narrow screen, the QR code's
/// instructions go under it and the code under the address.
/// </summary>
public sealed partial class LinkView : UserControl
{
    /// <summary>Below this width, rows that sit side by side on a tablet are stacked.</summary>
    private const double NarrowWidth = 520;

    private bool? _hostCodeNarrow;
    private bool? _pairingNarrow;

    /// <summary>Creates the view.</summary>
    public LinkView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        HostCodeGrid.SizeChanged += (_, e) => LayOutHostCode(e.NewSize.Width < NarrowWidth);
        PairGrid.SizeChanged += (_, e) => LayOutPairing(e.NewSize.Width < NarrowWidth);
    }

    /// <summary>The QR code's instructions beside it, or under it.</summary>
    private void LayOutHostCode(bool narrow)
    {
        if (_hostCodeNarrow == narrow)
        {
            return;
        }

        _hostCodeNarrow = narrow;
        Grid.SetRow(HostCodeText, narrow ? 1 : 0);
        Grid.SetColumn(HostCodeText, narrow ? 0 : 2);
        Grid.SetColumnSpan(HostCodeText, narrow ? 3 : 1);
        HostCodeText.Margin = narrow ? new Thickness(0, 16, 0, 0) : default;
    }

    /// <summary>The address, the code and Pair in one row, or the code and Pair under the address.</summary>
    private void LayOutPairing(bool narrow)
    {
        if (_pairingNarrow == narrow)
        {
            return;
        }

        _pairingNarrow = narrow;
        Grid.SetColumnSpan(AddressBox, narrow ? 5 : 1);
        Grid.SetRow(CodeLabel, narrow ? 2 : 0);
        Grid.SetColumn(CodeLabel, narrow ? 0 : 2);
        CodeLabel.Margin = narrow ? new Thickness(2, 12, 0, 5) : new Thickness(2, 0, 0, 5);
        Grid.SetRow(CodeBox, narrow ? 3 : 1);
        Grid.SetColumn(CodeBox, narrow ? 0 : 2);
        Grid.SetColumnSpan(CodeBox, narrow ? 3 : 1);
        Grid.SetRow(PairButton, narrow ? 3 : 1);
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
