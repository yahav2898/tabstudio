using System.Globalization;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Link.Networking;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// Joining a swarm on screen, with the app composed as it ships: the tablet's Add a device dialog with its QR code, a
/// phone that joins by typing the tablet's address and code, the phone waiting full screen for the tablet's app, and
/// the tablet's Swarm window and status bar with the phone in it.
/// </summary>
[Collection(SwarmAppFixture.Collection)]
public sealed class SwarmScreenshotTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    [AvaloniaFact]
    public async Task A_phone_joins_the_tablets_swarm_by_typing_its_code_and_waits_full_screen_for_its_app()
    {
        var (tablet, tabletWindow, tabletServices) = Start("Yahav's tablet", LinkDeviceKinds.Tablet, 1280, 800);
        await tablet.OpenWorkspaceAsync(await new SampleLibrary(tabletServices.GetRequiredService<AppPaths>()).EnsureExtractedAsync());
        var workspace = tablet.Workspace!;
        var swarmWindow = workspace.Swarm!;
        var swarm = tabletServices.GetRequiredService<SwarmCoordinatorService>();
        Assert.True(await swarm.StartAsync(port: 0, loopbackOnly: true), swarm.Error);
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Swarm);
        await swarmWindow.AddDeviceCommand.ExecuteAsync(null);
        Assert.True(swarmWindow.Invite.IsOpen);
        Assert.NotNull(swarmWindow.Invite.Code);
        Assert.Equal("No device has joined yet.", swarmWindow.Invite.JoinedText);
        UiTestHelpers.SaveScreenshot(tabletWindow, "swarm-00-add-a-device");

        // The phone: Swarm on the start screen, then the tablet's address and code.
        var (phone, phoneWindow, _) = Start("Pixel 7", LinkDeviceKinds.Phone, 412, 915);
        Assert.True(phone.Link.HasSwarm);
        phone.Link.ShowSwarmCommand.Execute(null);
        Assert.True(phone.Link.IsSwarmTab);
        var join = phone.Link.Swarm!;
        var invitation = swarm.Coordinator!.Invitation!;
        join.AddressText = "127.0.0.1:" + invitation.Port.ToString(CultureInfo.InvariantCulture);
        join.CodeText = invitation.Code!.DisplayText;
        await UiTestHelpers.WaitUntilAsync(() => join.JoinCommand.CanExecute(null));
        UiTestHelpers.SaveScreenshot(phoneWindow, "swarm-08-join-a-swarm");

        await join.JoinCommand.ExecuteAsync(null);
        Assert.Null(join.Error);
        await UiTestHelpers.WaitUntilAsync(() => phone.SwarmDevice!.IsVisible && !phone.Link.IsOpen, because: "the phone's swarm page");
        await UiTestHelpers.WaitUntilAsync(() => phone.SwarmDevice!.DetailText?.Contains(" ms", StringComparison.Ordinal) == true, because: "the first ping");
        Assert.Equal("Yahav's tablet", phone.SwarmDevice!.CoordinatorName);
        Assert.Equal("Waiting for Yahav's tablet to run an app", phone.SwarmDevice.StateText);
        UiTestHelpers.SaveScreenshot(phoneWindow, "swarm-09-device-waiting");

        // The tablet: the phone in the dialog, the Swarm window and the status bar; a new code for the next device.
        await UiTestHelpers.WaitUntilAsync(() => swarmWindow.Invite.JoinedText == "1 device is in the swarm.", because: "the dialog to count the phone");
        Assert.NotEqual(invitation.Code.DisplayText, swarmWindow.Invite.CodeText);
        swarmWindow.Invite.CloseCommand.Execute(null);
        var size = string.Create(CultureInfo.InvariantCulture, $"{phoneWindow.ClientSize.Width:0} × {phoneWindow.ClientSize.Height:0}");
        await UiTestHelpers.WaitUntilAsync(() => swarmWindow.Devices.Count == 1 && swarmWindow.Devices[0].Detail.StartsWith(size, StringComparison.Ordinal), because: "the phone's screen in the Swarm window");
        var row = swarmWindow.Devices[0];
        Assert.Equal("Pixel 7", row.Name);
        Assert.Equal("Ready", row.StateLabel);
        Assert.Contains("Large phone", row.Detail, StringComparison.Ordinal);
        Assert.Equal("Swarm: 1 device", swarmWindow.StatusText);
        Assert.Contains(workspace.RunTargets, t => t.Name == "Swarm");
        UiTestHelpers.SaveScreenshot(tabletWindow, "swarm-10-window-with-a-waiting-phone");

        // Leaving from the phone's overlay: it is the IDE again, and the tablet lists it offline until it rejoins.
        phone.SwarmDevice.ToggleExpandedCommand.Execute(null);
        await phone.SwarmDevice.LeaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => !phone.SwarmDevice.IsVisible, because: "the phone to leave");
        await UiTestHelpers.WaitUntilAsync(() => swarmWindow.Devices.Count == 0 || swarmWindow.Devices[0].IsOffline, because: "the tablet to see it leave");
    }

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }
    }

    private (MainViewModel Main, MainWindow Window, ServiceProvider Services) Start(string name, string kind, double width, double height)
    {
        var temp = Track(new TempDirectory());
        var services = Track(AppServices.Create(new AppPaths(temp.Path), s => s.AddSingleton<IDeviceIdentity>(new TestDeviceIdentity(name, kind))));
        var main = services.GetRequiredService<MainViewModel>();
        Track(new Closing(() => main.Workspace?.Dispose()));
        // A phone's screen is narrower than the desktop window's least width.
        var window = new MainWindow { DataContext = main, MinWidth = 0, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (main, window, services);
    }

    private T Track<T>(T disposable)
        where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    private sealed class Closing(Action close) : IDisposable
    {
        public void Dispose() => close();
    }
}
