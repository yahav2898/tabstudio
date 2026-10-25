using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels.Swarm;

namespace TabStudio.App.Views.Swarm;

/// <summary>
/// Add a device: the swarm's QR code and code. Escape closes it. On a narrow screen, the instructions go under the code.
/// </summary>
public sealed partial class SwarmInviteView : UserControl
{
    /// <summary>Below this width, the instructions go under the QR code.</summary>
    private const double NarrowWidth = 520;

    private bool? _narrow;

    /// <summary>Creates the view.</summary>
    public SwarmInviteView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        CodeGrid.SizeChanged += (_, e) => LayOut(e.NewSize.Width < NarrowWidth);
    }

    private void LayOut(bool narrow)
    {
        if (_narrow == narrow)
        {
            return;
        }

        _narrow = narrow;
        Grid.SetRow(CodeText, narrow ? 1 : 0);
        Grid.SetColumn(CodeText, narrow ? 0 : 2);
        Grid.SetColumnSpan(CodeText, narrow ? 3 : 1);
        CodeText.Margin = narrow ? new Thickness(0, 16, 0, 0) : default;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is SwarmInviteViewModel { IsOpen: true } invite)
        {
            invite.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
