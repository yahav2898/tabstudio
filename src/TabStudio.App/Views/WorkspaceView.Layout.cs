using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using TabStudio.App.Controls;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The shell's layout for the screen it is on (docs/design/README.md, "Portrait"). On a wide landscape screen the side
/// bar docks beside the editor. On a portrait screen, or one narrower than 900 pixels, the activity bar stays and the
/// side bar opens as a drawer over the editor, which keeps the whole width; the bottom dock takes at most 40% of the
/// height and Properties folds. Below 1000 pixels the Search button is its icon; when the menus do not fit, as on a
/// phone, they fold into one Menu button. The toolbar, the status bar and the touch bar fold their own items.
/// </summary>
public sealed partial class WorkspaceView
{
    /// <summary>The side bar's width, docked or as a drawer.</summary>
    private const double SideBarWidth = 330;

    /// <summary>The bottom dock's height until the user drags its splitter.</summary>
    private const double BottomPanelHeight = 200;

    /// <summary>The open Properties panel's height until the user drags its splitter.</summary>
    private const double PropertiesHeight = 214;

    /// <summary>Below this width, or taller than wide, the side bar is a drawer.</summary>
    private const double NarrowWidth = 900;

    /// <summary>Below this width the Search button shows only its icon.</summary>
    private const double CompactSearchWidth = 1000;

    /// <summary>The least the menu bar keeps for the solution's name beside the menus before they fold into the Menu button.</summary>
    private const double SolutionChipRoom = 120;

    /// <summary>How much of the editor's width the drawer leaves uncovered, at least.</summary>
    private const double DrawerGap = 32;

    /// <summary>The most of a portrait editor's height the bottom dock takes.</summary>
    private const double PortraitDockShare = 0.4;

    private GridLength _sideBarWidth = new(SideBarWidth);
    private GridLength _bottomPanelHeight = new(BottomPanelHeight);
    private GridLength _propertiesHeight = new(PropertiesHeight);
    private bool _isWide = true;
    private bool _laidOut;
    private bool _menusFolded;
    private double _menusWidth;
    private IDisposable? _drawerShadow;

    /// <summary>Whether the side bar docks beside the editor, rather than opening as a drawer over it; for tests.</summary>
    internal bool IsSideBarDocked => _isWide;

    /// <summary>Whether the menus are folded into the Menu button, as on a phone; for tests.</summary>
    internal bool AreMenusFolded => _menusFolded;

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var size = e.NewSize;
        var wide = size.Width >= NarrowWidth && size.Width >= size.Height;
        if (!_laidOut || wide != _isWide)
        {
            _laidOut = true;
            _isWide = wide;
            if (_viewModel is not null)
            {
                // A narrow screen gives the editor the width: the side bar waits behind its activity, Properties folds.
                _viewModel.IsSideBarVisible = wide;
                if (!wide)
                {
                    _viewModel.Properties.IsExpanded = false;
                }
            }

            UpdateSideBar();
            UpdateEditorArea();
        }

        SearchButton.Classes.Set("compact", size.Width < CompactSearchWidth);
        UpdateMenus(size.Width);
        StatusMessageText.MaxWidth = Math.Clamp(size.Width * 0.38, 140, 520);
    }

    /// <summary>Follows the editor area's size. Called by the constructor.</summary>
    private void InitializeLayout()
    {
        DrawerDismiss.PointerPressed += OnDrawerDismissPressed;
        AttachedToVisualTree += (_, _) => Accessibility.TypeScale.Changed += OnTextSizeChanged;
        DetachedFromVisualTree += (_, _) => Accessibility.TypeScale.Changed -= OnTextSizeChanged;
        EditorArea.SizeChanged += (_, _) => UpdateEditorArea();
        BottomDock.SizeChanged += (_, e) => UpdateBottomDock(e.NewSize.Width);
    }

    /// <summary>
    /// Fits the bottom panel's strip and debug layout to its width. On a phone the tabs had no room left beside
    /// "Show output from:" and its 170-pixel picker, and Hide fell off the end; the Call Stack's fixed 380 pixels
    /// beside Locals left Locals none.
    /// </summary>
    private void UpdateBottomDock(double width)
    {
        var wide = width >= 700;
        OutputSourceLabelHost.IsVisible = wide;
        OutputSourceBox.MinWidth = wide ? 170 : 110;
        CallStackBeside.Width = width >= 760 ? 380 : width >= 600 ? Math.Floor(width * 0.45) : 0;
        CallStackBeside.BorderThickness = new Thickness(CallStackBeside.Width > 0 ? 1 : 0, 0, 0, 0);
        // With no room beside Locals the Call Stack is its own tab; hidden, not squeezed to nothing, so the keyboard
        // and TalkBack do not reach a panel no one can see.
        CallStackBeside.Classes.Set("folded", CallStackBeside.Width <= 0);
        BringSelectedTabIntoView();
    }

    /// <summary>Scrolls the bottom panel's tabs so the selected one shows, as when Locals is chosen on a phone.</summary>
    private void BringSelectedTabIntoView() => Avalonia.Threading.Dispatcher.UIThread.Post(
        () => BottomTabs.Children.OfType<Button>().FirstOrDefault(tab => tab.IsVisible && tab.Classes.Contains("selected"))?.BringIntoView(),
        Avalonia.Threading.DispatcherPriority.Loaded);

    /// <summary>The text size changed: the bottom panel keeps room for its rows.</summary>
    private void OnTextSizeChanged(object? sender, EventArgs e) => UpdateBottomPanel();

    private void AttachLayout(WorkspaceViewModel viewModel)
    {
        viewModel.Properties.PropertyChanged += OnPropertiesChanged;
        viewModel.PropertyChanged += OnWorkspacePanelChanged;
        UpdateSideBar();
        UpdateProperties();
    }

    private void DetachLayout(WorkspaceViewModel viewModel)
    {
        viewModel.Properties.PropertyChanged -= OnPropertiesChanged;
        viewModel.PropertyChanged -= OnWorkspacePanelChanged;
    }

    private void OnWorkspacePanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.SelectedPanel))
        {
            BringSelectedTabIntoView();
        }
    }

    private void OnPropertiesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PropertiesViewModel.IsExpanded))
        {
            UpdateProperties();
        }
    }

    /// <summary>Docks the side bar beside the editor on a wide screen, or makes it a drawer over the editor on a narrow one.</summary>
    private void UpdateSideBar()
    {
        var visible = _viewModel?.IsSideBarVisible ?? true;
        var column = MainArea.ColumnDefinitions[2];
        if (column.Width.Value > 0)
        {
            // The docked width, as the user may have dragged it.
            _sideBarWidth = column.Width;
        }

        if (_isWide)
        {
            Grid.SetColumn(SideBarFrame, 2);
            Grid.SetColumnSpan(SideBarFrame, 1);
            SideBarFrame.HorizontalAlignment = HorizontalAlignment.Stretch;
            SideBarFrame.Width = double.NaN;
            SideBarFrame.BorderThickness = default;
            _drawerShadow?.Dispose();
            _drawerShadow = null;
            column.Width = visible ? _sideBarWidth : new GridLength(0);
            SideSplitter.IsVisible = visible;
            DrawerDismiss.IsVisible = false;
        }
        else
        {
            column.Width = new GridLength(0);
            SideSplitter.IsVisible = false;
            Grid.SetColumn(SideBarFrame, 0);
            Grid.SetColumnSpan(SideBarFrame, 3);
            SideBarFrame.HorizontalAlignment = HorizontalAlignment.Right;
            SideBarFrame.Width = DrawerWidth();
            SideBarFrame.BorderThickness = new Thickness(1, 0, 0, 0);
            _drawerShadow ??= SideBarFrame.Bind(Border.BoxShadowProperty, SideBarFrame.GetResourceObservable("Shadow.Dialog"));
            DrawerDismiss.IsVisible = visible;
        }

        SideBarFrame.IsVisible = visible;
    }

    /// <summary>The drawer's width: the side bar's, or less on a phone, leaving some of the editor in view.</summary>
    private double DrawerWidth()
    {
        var room = MainArea.Bounds.Width - ActivityBar.ItemSize - DrawerGap;
        return room > 0 ? Math.Clamp(room, 240, SideBarWidth) : SideBarWidth;
    }

    /// <summary>A tap beside the drawer closes it.</summary>
    private void OnDrawerDismissPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.IsSideBarVisible = false;
        }

        e.Handled = true;
    }

    /// <summary>A document opened from the drawer, as from the Solution Explorer, closes it, so the document shows.</summary>
    private void OnActiveDocumentShown()
    {
        if (!_isWide && _viewModel is { IsSideBarVisible: true, ActiveDocument: not null })
        {
            _viewModel.IsSideBarVisible = false;
        }
    }

    /// <summary>
    /// Opens Properties at its last height, or folds it into its 40-pixel header; it hides while a designer shows
    /// Properties of its own. Its splitter shows only while it is open.
    /// </summary>
    private void UpdateProperties()
    {
        if (_viewModel is null)
        {
            return;
        }

        var row = SideBar.RowDefinitions[2];
        if (row.Height.IsAbsolute && row.Height.Value > 0)
        {
            _propertiesHeight = row.Height;
        }

        var open = _viewModel.Properties.IsExpanded && _viewModel.ShowsSideBarProperties;
        row.Height = open ? _propertiesHeight : GridLength.Auto;
        PropertiesSplitter.IsVisible = open;
    }

    /// <summary>
    /// Fits the editor area: in portrait the bottom dock takes at most 40% of its height, and the floating touch bars
    /// leave most of the editor's width to the code, folding their least used buttons to icons.
    /// </summary>
    private void UpdateEditorArea()
    {
        var height = EditorArea.Bounds.Height;
        EditorArea.RowDefinitions[4].MaxHeight = _isWide || height <= 0 ? double.PositiveInfinity : Math.Max(120, height * PortraitDockShare);

        var width = EditorPanel.Bounds.Width;
        if (width > 0)
        {
            var share = width * (_isWide ? 0.75 : 0.5);
            TouchBarHost.MaxWidth = Math.Max(share, Math.Min(width - 32, 300));
        }

        if (!_isWide && SideBarFrame.IsVisible)
        {
            SideBarFrame.Width = DrawerWidth();
        }
    }

    /// <summary>The menu bar's menus, wherever they are now: in the menu bar, or in the Menu button's menu.</summary>
    private IEnumerable<MenuItem> TopMenuItems() =>
        _menusFolded && MenuButton.Flyout is MenuFlyout folded ? [.. folded.Items.OfType<MenuItem>()] : [.. MainMenu.Items.OfType<MenuItem>()];

    /// <summary>
    /// Folds the menus into the Menu button when the menu bar is too narrow for them beside the Search button and the
    /// solution's name, and puts them back when there is room.
    /// </summary>
    private void UpdateMenus(double width)
    {
        if (!_menusFolded && MainMenu.DesiredSize.Width > 0)
        {
            _menusWidth = MainMenu.DesiredSize.Width;
        }

        if (_menusWidth <= 0)
        {
            return;
        }

        var search = width < CompactSearchWidth ? 50 : 250;
        var needed = MenuBar.Margin.Left + MenuBar.Margin.Right + 32 + _menusWidth + search + SolutionChipRoom;
        var fold = width < needed;
        if (fold == _menusFolded || MenuButton.Flyout is not MenuFlyout flyout)
        {
            return;
        }

        _menusFolded = fold;
        if (fold)
        {
            // In a menu that has not opened, the menus would have no data context: they keep the shell's.
            var items = MainMenu.Items.OfType<MenuItem>().ToList();
            MainMenu.Items.Clear();
            foreach (var item in items)
            {
                item.DataContext = DataContext;
                flyout.Items.Add(item);
            }
        }
        else
        {
            var items = flyout.Items.OfType<MenuItem>().ToList();
            flyout.Items.Clear();
            foreach (var item in items)
            {
                item.ClearValue(DataContextProperty);
                MainMenu.Items.Add(item);
            }
        }

        MainMenu.IsVisible = !fold;
        MenuButton.IsVisible = fold;
    }
}
