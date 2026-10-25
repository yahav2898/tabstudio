using TabStudio.App.ViewModels;
using TabStudio.App.Views.DesktopMode;

namespace TabStudio.App.Views;

/// <summary>Desktop mode in the root view: the IDE moves to the external display, and the companion takes its place.</summary>
public sealed partial class MainView
{
    private DesktopScreenPresenter? _desktop;

    /// <summary>The external display's view while desktop mode shows the IDE there, for tests and the self test.</summary>
    internal DesktopScreenView? DesktopScreen => _desktop?.Screen;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _desktop?.Dispose();
        _desktop = (DataContext as MainViewModel)?.DesktopMode is { } desktop
            ? new DesktopScreenPresenter(Root, Ide, desktop)
            : null;
    }
}
