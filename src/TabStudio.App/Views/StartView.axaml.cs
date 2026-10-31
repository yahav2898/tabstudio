using Avalonia;
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
        SizeChanged += (_, e) => ArrangeColumns(e.NewSize.Width);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TabStudio.App.Accessibility.TypeScale.Changed += OnTextSizeChanged;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        TabStudio.App.Accessibility.TypeScale.Changed -= OnTextSizeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTextSizeChanged(object? sender, EventArgs e) => ArrangeColumns(Bounds.Width);

    /// <summary>Below this width the two columns stack, Open recent above Get started, so neither is squeezed.</summary>
    private const double Stacked = 860;

    /// <summary>The width four cards need in one row; with less, they take two rows, so their labels have room.</summary>
    private const double CardRow = 640;

    /// <summary>
    /// Lays the page out for its width: Open recent beside Get started on a wide screen, one above the other on a
    /// narrow one or at a large text size, with the four cards in two rows when one row leaves their labels no room.
    /// </summary>
    private void ArrangeColumns(double width)
    {
        var factor = TabStudio.App.Accessibility.TypeScale.Factor;
        var stacked = width < Stacked * factor;
        Columns.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("360,40,*");
        Columns.Margin = stacked ? new Thickness(16, 20, 16, 20) : new Thickness(40, 28, 40, 28);
        Grid.SetColumn(RecentColumn, 0);
        Grid.SetRow(RecentColumn, 0);
        Grid.SetColumn(StartedColumn, stacked ? 0 : 2);
        Grid.SetRow(StartedColumn, stacked ? 2 : 0);
        StartedColumn.Margin = stacked ? new Thickness(0, 28, 0, 0) : default;

        var cards = stacked ? width - 32 : width - 480;
        var columns = cards < CardRow * factor ? 2 : 4;
        ActionCards.Columns = columns;
        ActionCards.Rows = 4 / columns;
        for (var i = 0; i < ActionCards.Children.Count; i++)
        {
            var last = (i + 1) % columns == 0;
            var bottom = i < ActionCards.Children.Count - columns;
            ActionCards.Children[i].Margin = new Thickness(0, 0, last ? 0 : 10, bottom ? 10 : 0);
        }
    }

    /// <summary>
    /// Brings the template's options, the project name and Create into view when a template is tapped, so creating it
    /// takes no scrolling. Only a tap does this, not the template chosen when the page opens. It waits for layout, since
    /// choosing a template can show options such as Blazor's render mode.
    /// </summary>
    private void OnTemplateTapped(object? sender, TappedEventArgs e) =>
        Dispatcher.UIThread.Post(() => CreateSection.BringIntoView(), DispatcherPriority.Background);
}
