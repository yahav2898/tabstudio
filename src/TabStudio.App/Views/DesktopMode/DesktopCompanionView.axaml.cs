using System.ComponentModel;
using Avalonia.Controls;
using TabStudio.App.ViewModels.DesktopMode;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// The tablet's companion in desktop mode. Code here only lays the panes out: side by side on a wide tablet, one at a
/// time under tabs on a narrow one.
/// </summary>
public sealed partial class DesktopCompanionView : UserControl
{
    /// <summary>The width from which the Toolbox and the Properties panel are shown side by side.</summary>
    private const double WideWidth = 900;

    private DesktopCompanionViewModel? _companion;
    private bool _wide = true;

    /// <summary>Creates the view.</summary>
    public DesktopCompanionView()
    {
        InitializeComponent();
        ArrangePanes();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_companion is not null)
        {
            _companion.PropertyChanged -= OnCompanionPropertyChanged;
        }

        _companion = (DataContext as DesktopModeViewModel)?.Companion;
        if (_companion is not null)
        {
            _companion.PropertyChanged += OnCompanionPropertyChanged;
        }

        ArrangePanes();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var wide = e.NewSize.Width >= WideWidth;
        if (wide != _wide)
        {
            _wide = wide;
            ArrangePanes();
        }
    }

    private void OnCompanionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopCompanionViewModel.Pane))
        {
            ArrangePanes();
        }
    }

    private void ArrangePanes()
    {
        PaneTabs.IsVisible = !_wide;
        Panes.Classes.Set("tabbed", !_wide);
        PaneDivider.IsVisible = _wide;
        Grid.SetColumn(PropertiesPane, _wide ? 2 : 0);
        Grid.SetColumnSpan(ToolboxPane, _wide ? 1 : 3);
        Grid.SetColumnSpan(PropertiesPane, _wide ? 1 : 3);
        var pane = _companion?.Pane ?? CompanionPane.Toolbox;
        ToolboxPane.IsVisible = _wide || pane == CompanionPane.Toolbox;
        PropertiesPane.IsVisible = _wide || pane == CompanionPane.Properties;
    }
}
