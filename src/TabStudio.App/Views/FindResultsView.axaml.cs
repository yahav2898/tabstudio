using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The Find Results panel: the Find in Files form and its matches. Enter in the find box runs Find All. On a wide
/// panel the form is beside the matches; on a narrow one it is above them and scrolls rather than taking all the room.
/// </summary>
public sealed partial class FindResultsView : UserControl
{
    /// <summary>Narrower than this, the form goes above the matches.</summary>
    private const double SideBySideWidth = 720;

    /// <summary>The share of a narrow panel's height the form takes at most.</summary>
    private const double NarrowFormShare = 0.5;

    /// <summary>The least height of a narrow panel's form: its find box, a phone's touch target, and the margin around it.</summary>
    private const double NarrowFormMinimum = 64;

    private FindResultsViewModel? _viewModel;
    private bool? _isSideBySide;

    /// <summary>Creates the view.</summary>
    public FindResultsView()
    {
        InitializeComponent();

        // Tunnel, so Find All wins over the text box's own handling of Enter.
        FindBox.AddHandler(KeyDownEvent, OnFindBoxKeyDown, RoutingStrategies.Tunnel);
        SizeChanged += (_, e) => ArrangeForm(e.NewSize);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
        }

        _viewModel = DataContext as FindResultsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            FindBox.Focus(NavigationMethod.Tab);
            FindBox.SelectAll();
        }, DispatcherPriority.Background);

    private void OnFindBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && _viewModel is { } viewModel)
        {
            viewModel.FindAllCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>The form beside the matches on a wide panel, above them on a narrow one.</summary>
    private void ArrangeForm(Size size)
    {
        var sideBySide = size.Width >= SideBySideWidth;
        FormScroller.MaxHeight = sideBySide ? double.PositiveInfinity : Math.Max(NarrowFormMinimum, size.Height * NarrowFormShare);
        if (_isSideBySide == sideBySide)
        {
            return;
        }

        _isSideBySide = sideBySide;
        if (sideBySide)
        {
            LayoutRoot.RowDefinitions = new RowDefinitions("*");
            LayoutRoot.ColumnDefinitions = new ColumnDefinitions("360,*");
            Grid.SetRow(ResultsView, 0);
            Grid.SetColumn(ResultsView, 1);
            FormPane.BorderThickness = new Thickness(0, 0, 1, 0);
        }
        else
        {
            LayoutRoot.ColumnDefinitions = new ColumnDefinitions("*");
            LayoutRoot.RowDefinitions = new RowDefinitions("Auto,*");
            Grid.SetColumn(ResultsView, 0);
            Grid.SetRow(ResultsView, 1);
            FormPane.BorderThickness = new Thickness(0, 0, 0, 1);
        }
    }
}
