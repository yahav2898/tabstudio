using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels.WindowsBridge;

namespace TabStudio.App.Views.WindowsBridge;

/// <summary>The Windows Bridge tab of the TabStudio Link dialog. Enter in the code box pairs with the PC.</summary>
public sealed partial class WindowsBridgeTabView : UserControl
{
    /// <summary>Creates the view.</summary>
    public WindowsBridgeTabView()
    {
        InitializeComponent();
        CodeBox.AddHandler(KeyDownEvent, OnCodeKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnCodeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is WindowsBridgeViewModel { PairCommand: { } pair } && pair.CanExecute(null))
        {
            pair.Execute(null);
            e.Handled = true;
        }
    }
}
