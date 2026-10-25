using Avalonia.Controls;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The exception helper shown at the line that threw an unhandled exception.</summary>
public sealed partial class ExceptionHelperView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ExceptionHelperView()
    {
        InitializeComponent();
        CopyDetails.Click += OnCopyDetailsClick;
    }

    private async void OnCopyDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExceptionHelperViewModel helper && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(helper.Details);
        }
    }
}
