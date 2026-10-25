using Avalonia.Controls;

namespace TabStudio.App.Views;

/// <summary>The desktop window. On Android the <see cref="MainView"/> is shown directly.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
