using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels.Live;

namespace TabStudio.App.Views.Live;

/// <summary>
/// Join Live Session. Escape closes it; on a phone's narrow screen, the code goes under the address.
/// </summary>
public sealed partial class LiveJoinView : UserControl
{
    /// <summary>Below this width, the address and the code are stacked.</summary>
    private const double NarrowWidth = 460;

    private bool? _narrow;

    /// <summary>Creates the view.</summary>
    public LiveJoinView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        JoinGrid.SizeChanged += (_, e) => LayOut(e.NewSize.Width < NarrowWidth);
    }

    private void LayOut(bool narrow)
    {
        if (_narrow == narrow)
        {
            return;
        }

        _narrow = narrow;
        var span = narrow ? 3 : 1;
        Grid.SetColumnSpan(AddressLabel, span);
        Grid.SetColumnSpan(AddressBox, span);
        Grid.SetRow(CodeLabel, narrow ? 2 : 0);
        Grid.SetColumn(CodeLabel, narrow ? 0 : 2);
        Grid.SetColumnSpan(CodeLabel, span);
        CodeLabel.Margin = narrow ? new Thickness(2, 12, 0, 6) : new Thickness(2, 0, 0, 6);
        Grid.SetRow(CodeBox, narrow ? 3 : 1);
        Grid.SetColumn(CodeBox, narrow ? 0 : 2);
        Grid.SetColumnSpan(CodeBox, span);
        Grid.SetRow(JoinButton, narrow ? 4 : 2);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is LiveJoinViewModel { IsOpen: true } join)
        {
            join.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
