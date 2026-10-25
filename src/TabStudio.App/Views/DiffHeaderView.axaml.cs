using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TabStudio.App.Views;

/// <summary>
/// The header of a comparison tab: the file, the versions compared, the added and removed line counts, and
/// buttons that move to the previous or next change.
/// </summary>
public sealed partial class DiffHeaderView : UserControl
{
    /// <summary>Creates the view.</summary>
    public DiffHeaderView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when a button asks to move to another change: -1 for the previous one, 1 for the next.</summary>
    public event EventHandler<int>? MoveRequested;

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => MoveRequested?.Invoke(this, -1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => MoveRequested?.Invoke(this, 1);
}
