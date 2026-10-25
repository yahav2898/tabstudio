using Avalonia.Controls;
using Avalonia.Input;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The Watch panel: expressions evaluated whenever the program pauses, and a box to add one.</summary>
public sealed partial class WatchView : UserControl
{
    /// <summary>Creates the view.</summary>
    public WatchView()
    {
        InitializeComponent();
        NewExpressionBox.KeyDown += OnNewExpressionKeyDown;
    }

    private void OnNewExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is WatchViewModel viewModel && viewModel.AddNewExpressionCommand.CanExecute(null))
        {
            viewModel.AddNewExpressionCommand.Execute(null);
            e.Handled = true;
        }
    }
}
