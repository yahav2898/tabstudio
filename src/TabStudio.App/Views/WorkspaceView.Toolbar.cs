using System.Collections.Specialized;
using Avalonia.Controls;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The toolbar's "Run on" item: a tap chooses where and what F5 runs, as the Run button's menu does. Its menu holds
/// real menu items, so that when the item folds into the toolbar's More menu the choices come along as a submenu.
/// </summary>
public sealed partial class WorkspaceView
{
    private readonly MenuFlyout _runHostMenu = new() { Placement = PlacementMode.BottomEdgeAlignedLeft };

    /// <summary>Gives the Run on item its menu. Called by the constructor.</summary>
    private void InitializeToolbar() => RunHostItem.Flyout = _runHostMenu;

    private void AttachToolbar(WorkspaceViewModel viewModel)
    {
        viewModel.RunTargets.CollectionChanged += OnRunTargetsChanged;
        FillRunHostMenu();
    }

    private void DetachToolbar(WorkspaceViewModel viewModel) => viewModel.RunTargets.CollectionChanged -= OnRunTargetsChanged;

    private void OnRunTargetsChanged(object? sender, NotifyCollectionChangedEventArgs e) => FillRunHostMenu();

    /// <summary>Lists the startup projects and launch configurations F5 can run.</summary>
    private void FillRunHostMenu()
    {
        _runHostMenu.Items.Clear();
        if (_viewModel is null)
        {
            return;
        }

        foreach (var target in _viewModel.RunTargets)
        {
            _runHostMenu.Items.Add(new MenuItem { Header = target.Header, Command = target.Select });
        }

        RunHostItem.IsEnabled = _viewModel.RunTargets.Count > 0;
    }
}
