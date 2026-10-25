using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Toolbox;
using TabStudio.App.ViewModels.DesktopMode;
using TabStudio.Testing;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>When desktop mode starts and ends: the offer, "Always", a display going away, and the size on the display.</summary>
public sealed class DesktopModeViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeExternalDisplay _display = new();
    private readonly AppPaths _paths;

    public DesktopModeViewModelTests()
    {
        _paths = new AppPaths(_temp.Path);
    }

    [Fact]
    public void Offers_desktop_mode_for_a_connected_display_and_ends_it_when_the_display_goes()
    {
        using var desktop = Create();
        Assert.True(desktop.IsSupported);
        Assert.False(desktop.HasDisplay);
        Assert.False(desktop.ToggleCommand.CanExecute(null));

        _display.Connect();
        Assert.True(desktop.IsOfferVisible);
        Assert.Equal("Use HDMI Screen", desktop.StatusText);
        Assert.Equal("HDMI Screen · 1920 × 1080 · Automatic · 100%", desktop.DisplayDescription);

        desktop.EnterCommand.Execute(null);
        Assert.True(desktop.IsActive);
        Assert.False(desktop.IsOfferVisible);
        Assert.Equal("Desktop mode", desktop.StatusText);
        Assert.False(desktop.EnterCommand.CanExecute(null));

        _display.Disconnect();
        Assert.False(desktop.IsActive);
        Assert.False(desktop.HasDisplay);

        // Without a display, turning it on does nothing.
        desktop.IsActive = true;
        Assert.False(desktop.IsActive);
    }

    [Fact]
    public void Not_now_hides_the_offer_and_the_menu_still_starts_desktop_mode()
    {
        using var desktop = Create();
        _display.Connect();

        desktop.DismissOfferCommand.Execute(null);
        Assert.False(desktop.IsOfferVisible);
        Assert.False(desktop.IsActive);

        desktop.IsActive = true;
        Assert.True(desktop.IsActive);
        desktop.ToggleCommand.Execute(null);
        Assert.False(desktop.IsActive);
    }

    [Fact]
    public void Always_moves_the_IDE_to_every_display_connected_later_even_after_a_restart()
    {
        using (var first = Create())
        {
            _display.Connect();
            first.UseAlwaysCommand.Execute(null);
            Assert.True(first.IsActive);
            Assert.True(first.UseDisplayAutomatically);
            _display.Disconnect();
        }

        using var second = Create();
        Assert.True(second.UseDisplayAutomatically);
        _display.Connect(name: "Wireless display");
        Assert.True(second.IsActive);
        Assert.False(second.IsOfferVisible);
        Assert.Equal("Wireless display", second.DisplayName);
    }

    [Fact]
    public void Picks_a_size_for_the_display_or_the_one_the_user_chose()
    {
        // A 24-inch 1080p monitor gets every pixel; Android's simulated 240 dpi display would leave only 720 units of
        // height at its own 1.5, so it gets 1.35 and 800; a 4K television keeps its 2.
        Assert.Equal(1, DesktopModeViewModel.ScaleFor(new ExternalDisplay(2, "Monitor", 1920, 1080, 1), DesktopScale.Automatic));
        Assert.Equal(1.35, DesktopModeViewModel.ScaleFor(new ExternalDisplay(2, "Overlay #1", 1920, 1080, 1.5), DesktopScale.Automatic), 6);
        Assert.Equal(2, DesktopModeViewModel.ScaleFor(new ExternalDisplay(2, "Television", 3840, 2160, 2), DesktopScale.Automatic));
        Assert.Equal(1.25, DesktopModeViewModel.ScaleFor(null, DesktopScale.Percent125));

        using var desktop = Create();
        _display.Connect(systemScale: 1.5);
        Assert.Equal("Automatic · 135%", desktop.ScaleLabel);

        desktop.SetScaleCommand.Execute(DesktopScale.Percent100);
        Assert.Equal(1, desktop.EffectiveScale);
        Assert.Equal("100%", desktop.ScaleLabel);
        Assert.True(desktop.ScaleOptions.Single(o => o.Scale == DesktopScale.Percent100).IsSelected);
        Assert.False(desktop.ScaleOptions.Single(o => o.Scale == DesktopScale.Automatic).IsSelected);
        Assert.Equal(DesktopScale.Percent100, new DesktopModeSettingsStore(_paths).Load().Scale);
    }

    public void Dispose() => _temp.Dispose();

    private DesktopModeViewModel Create() => new(_display, new DesktopModeSettingsStore(_paths), ToolboxCatalog.LoadEmbedded());
}
