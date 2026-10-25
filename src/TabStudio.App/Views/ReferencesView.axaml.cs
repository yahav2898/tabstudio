using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The Find All References panel. Tapping a reference, or pressing Enter on it, opens it.</summary>
public sealed partial class ReferencesView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ReferencesView()
    {
        InitializeComponent();
        Rows.AddHandler(TappedEvent, OnRowTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Rows.KeyDown += OnRowsKeyDown;
    }

    private ReferencesViewModel? ViewModel => DataContext as ReferencesViewModel;

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is ReferenceLineRow row)
        {
            ViewModel?.NavigateCommand.Execute(row);
        }
    }

    private void OnRowsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Rows.SelectedItem is ReferenceLineRow row)
        {
            ViewModel?.NavigateCommand.Execute(row);
            e.Handled = true;
        }
    }
}
