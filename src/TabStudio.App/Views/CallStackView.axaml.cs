using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The Call Stack panel. Tapping a frame, or Enter, shows its code in the editor.</summary>
public sealed partial class CallStackView : UserControl
{
    /// <summary>Creates the view.</summary>
    public CallStackView()
    {
        InitializeComponent();
        Frames.AddHandler(TappedEvent, OnFrameTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Frames.KeyDown += OnFramesKeyDown;
    }

    private CallStackViewModel? ViewModel => DataContext as CallStackViewModel;

    private void OnFrameTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is StackFrameViewModel frame)
        {
            ViewModel?.Activate(frame);
        }
    }

    private void OnFramesKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Frames.SelectedItem is StackFrameViewModel frame)
        {
            ViewModel?.Activate(frame);
            e.Handled = true;
        }
    }
}
