using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TabStudio.App.Views;

/// <summary>The start screen: recent workspaces and the ways to get code onto the tablet.</summary>
public sealed partial class StartView : UserControl
{
    /// <summary>Creates the view.</summary>
    public StartView()
    {
        InitializeComponent();
        TemplateList.AddHandler(Gestures.TappedEvent, OnTemplateTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// Brings the template's options, the project name and Create into view when a template is tapped, so creating it
    /// takes no scrolling. Only a tap does this, not the template chosen when the page opens. It waits for layout, since
    /// choosing a template can show options such as Blazor's render mode.
    /// </summary>
    private void OnTemplateTapped(object? sender, TappedEventArgs e) =>
        Dispatcher.UIThread.Post(() => CreateSection.BringIntoView(), DispatcherPriority.Background);
}
