using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The Error List. Tapping a row opens the file at the diagnostic's location.</summary>
public sealed partial class ErrorListView : UserControl
{
    /// <summary>Whether the list is too narrow for its Project and File columns, as on a phone.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ErrorListView, bool>(nameof(IsCompact));

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        private set => SetValue(IsCompactProperty, value);
    }

    /// <summary>Below this height the panel keeps its filters and search on one row.</summary>
    private const double ShortHeight = 220;

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        IsCompact = e.NewSize.Width < TabStudio.App.Controls.ResponsiveColumns.CompactWidth;

        // A narrow panel puts the search under the filters, the width of the panel; a narrow and short one, as in a
        // pop-up window, keeps one row with a shorter search, or the two rows left the list no room for a single error.
        var narrow = e.NewSize.Width < 640;
        var below = narrow && e.NewSize.Height >= ShortHeight;
        Grid.SetRow(SearchBox, below ? 1 : 0);
        Grid.SetColumn(SearchBox, below ? 0 : 1);
        Grid.SetColumnSpan(SearchBox, below ? 2 : 1);
        SearchBox.Width = below ? double.NaN : narrow ? Math.Max(120, Math.Floor(e.NewSize.Width * 0.35)) : 220;
        SearchBox.Margin = below ? new Thickness(0, 0, 0, 6) : narrow ? new Thickness(6, 0, 0, 0) : default;
    }

    /// <summary>Creates the view.</summary>
    public ErrorListView()
    {
        InitializeComponent();
        Rows.AddHandler(TappedEvent, OnRowTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Rows.KeyDown += OnRowsKeyDown;
    }

    private ErrorListViewModel? ViewModel => DataContext as ErrorListViewModel;

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        // The screens badge of a layout finding opens its pictures, not the file.
        if ((e.Source as Control)?.FindAncestorOfType<Button>(includeSelf: true) is { Tag: "screens" })
        {
            return;
        }

        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is ErrorItemViewModel item)
        {
            // A code with a page, as language servers give, opens the page; the rest of the row opens the file.
            if (e.Source is TextBlock { Tag: "code" } && item.HasHelpLink)
            {
                ViewModel?.OpenHelpLinkCommand.Execute(item);
                return;
            }

            ViewModel?.NavigateCommand.Execute(item);
        }
    }

    private void OnRowsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Rows.SelectedItem is ErrorItemViewModel item)
        {
            ViewModel?.NavigateCommand.Execute(item);
            e.Handled = true;
        }
    }
}
