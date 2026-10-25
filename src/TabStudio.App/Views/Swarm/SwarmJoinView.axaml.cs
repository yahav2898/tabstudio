using Avalonia;
using Avalonia.Controls;

namespace TabStudio.App.Views.Swarm;

/// <summary>
/// The Join a swarm half of the TabStudio Link dialog. On a phone's narrow screen, the code goes under the address.
/// </summary>
public sealed partial class SwarmJoinView : UserControl
{
    /// <summary>Below this width, the address and the code are stacked.</summary>
    private const double NarrowWidth = 460;

    private bool? _narrow;

    /// <summary>Creates the view.</summary>
    public SwarmJoinView()
    {
        InitializeComponent();
        JoinGrid.SizeChanged += (_, e) => LayOut(e.NewSize.Width < NarrowWidth);
    }

    /// <summary>The address and the code in one row, or the code under the address.</summary>
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
        CodeLabel.Margin = narrow ? new Thickness(2, 12, 0, 5) : new Thickness(2, 0, 0, 5);
        Grid.SetRow(CodeBox, narrow ? 3 : 1);
        Grid.SetColumn(CodeBox, narrow ? 0 : 2);
        Grid.SetColumnSpan(CodeBox, span);
        Grid.SetRow(JoinButton, narrow ? 4 : 2);
    }
}
