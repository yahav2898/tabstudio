using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Link.Networking;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// TabStudio Link on screen: pairing a tablet by typing the host's code, the host's QR code and the tablet it serves,
/// and the IDE's status bar while linked and while the host is away. The hosts run in the test, on the loopback address.
/// </summary>
public sealed class LinkScreenshotTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    [AvaloniaFact]
    public async Task Pairing_by_typing_the_code_links_and_remembers_the_host()
    {
        var (main, window, _) = Start("Galaxy Tab S9", LinkDeviceKinds.Tablet, 1280, 800);
        var link = main.Link;
        await using var host = link.Service.CreateLoopbackHost("Pixel 9", LinkDeviceKinds.Phone);
        var invitation = host.Invitation;

        link.Open(LinkTab.Connect);
        link.AddressText = $"127.0.0.1:{invitation.Port.ToString(CultureInfo.InvariantCulture)}";
        link.CodeText = invitation.Code!.DisplayText;
        await UiTestHelpers.WaitUntilAsync(() => link.PairCommand.CanExecute(null));
        UiTestHelpers.SaveScreenshot(window, "link-01-pair-by-code");

        await link.PairCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => link.IsLinked && link.Service.Latency is not null && link.ClientDetail?.Contains(" ms", StringComparison.Ordinal) == true, because: "the link and its first ping");
        UiTestHelpers.SaveScreenshot(window, "link-02-linked");

        Assert.Equal("Linked to Pixel 9", link.ClientTitle);
        Assert.Null(link.ClientError);
        Assert.Contains("end-to-end encrypted (TLS 1.", link.ClientDetail, StringComparison.Ordinal);
        Assert.Equal("Pixel 9", Assert.Single(link.PairedHosts).Name);
        Assert.Equal(string.Empty, link.CodeText);

        link.CloseCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !link.IsOpen);
        UiTestHelpers.SaveScreenshot(window, "link-03-start-linked");
        Assert.StartsWith("Pixel 9 · ", link.StatusText, StringComparison.Ordinal);

        link.Open();
        await link.DisconnectCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => link.IsNotLinked);
        UiTestHelpers.SaveScreenshot(window, "link-04-paired-host");
        Assert.True(Assert.Single(link.PairedHosts).CanConnect);
    }

    [AvaloniaFact]
    public async Task A_host_shows_its_QR_code_and_the_tablet_it_serves()
    {
        var (main, window, _) = Start("Pixel 9", LinkDeviceKinds.Phone, 1280, 800);
        var link = main.Link;
        Assert.True(await link.Service.StartHostingAsync(port: 0, loopbackOnly: true), link.Service.HostError);
        link.Open(LinkTab.Host);
        await UiTestHelpers.WaitUntilAsync(() => link.IsHosting && link.HostQrCode is not null, because: "the QR code");
        UiTestHelpers.SaveScreenshot(window, "link-05-host-qr");

        Assert.Equal("Waiting for a tablet", link.HostTitle);

        // A cool device says only its cores: the design keeps quiet about what is fine ("Thermal OK" was shown).
        Assert.DoesNotContain("Thermal", link.HostDeviceText, StringComparison.Ordinal);
        Assert.EndsWith("cores", link.HostDeviceText, StringComparison.Ordinal);
        Assert.Matches("^[0-9A-Z]{4}-[0-9A-Z]{4}$", link.PairingCodeText);

        var tablet = Tablet("Galaxy Tab S9");
        Assert.True(await tablet.PairAsync(LinkTarget.FromInvitation(link.Service.Invitation!)), tablet.ClientError);
        await UiTestHelpers.WaitUntilAsync(() => link.IsServing, because: "the tablet");
        UiTestHelpers.SaveScreenshot(window, "link-06-host-serving");

        Assert.Equal("Serving Galaxy Tab S9", link.HostTitle);
        Assert.Equal("Galaxy Tab S9", Assert.Single(link.PairedTablets).Name);
    }

    [AvaloniaFact]
    public async Task On_a_phone_the_QR_code_and_the_pairing_fields_are_stacked()
    {
        var (main, _, _) = Start("Pixel 9", LinkDeviceKinds.Phone, 1280, 800);
        var link = main.Link;
        var phone = new Window { Width = 412, Height = 915, Content = new LinkView { DataContext = link } };
        phone.Show();
        Assert.True(await link.Service.StartHostingAsync(port: 0, loopbackOnly: true), link.Service.HostError);
        link.Open(LinkTab.Host);
        await UiTestHelpers.WaitUntilAsync(() => link.HostQrCode is not null, because: "the QR code");
        UiTestHelpers.SaveScreenshot(phone, "link-07-host-phone");

        Assert.Equal(1, Grid.GetRow(Named<StackPanel>(phone, "HostCodeText")));

        link.SelectTabCommand.Execute(LinkTab.Connect);
        await UiTestHelpers.WaitUntilAsync(() => Grid.GetRow(Named<TextBox>(phone, "CodeBox")) == 3, because: "the code under the address");
        UiTestHelpers.SaveScreenshot(phone, "link-08-connect-phone");

        Assert.True(Named<TextBlock>(phone, "AddressLabel").Bounds.Width > 40, "The Address label is readable.");
        Assert.True(Named<TextBlock>(phone, "CodeLabel").Bounds.Width > 30, "The Code label is readable.");
    }

    [AvaloniaFact]
    public async Task The_status_bar_shows_the_linked_host_and_the_fallback_when_it_goes_away()
    {
        var (main, window, services) = Start("Galaxy Tab S9", LinkDeviceKinds.Tablet, 1280, 800);
        var workspace = await OpenSampleAsync(main, services);
        await using var host = main.Link.Service.CreateLoopbackHost("Pixel 9", LinkDeviceKinds.Phone);

        Assert.True(await main.Link.Service.PairAsync(host.Invitation), main.Link.Service.ClientError);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsLinkedToHost && workspace.LinkStatusText.EndsWith(" ms", StringComparison.Ordinal), because: "the link and its first ping");
        await workspace.ActiveDocument!.AnalyzeNowAsync();
        UiTestHelpers.SaveScreenshot(window, "link-09-ide-linked");
        Assert.StartsWith("Pixel 9 · ", workspace.LinkStatusText, StringComparison.Ordinal);

        await host.StopAsync();
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsLinkReconnecting, because: "the fallback");
        UiTestHelpers.SaveScreenshot(window, "link-10-ide-reconnecting");

        Assert.Equal("Reconnecting to Pixel 9", workspace.LinkStatusText);
        Assert.Equal("Lost Pixel 9 · compiling on this device", workspace.StatusText);
    }

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }
    }

    private static T Named<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static async Task<WorkspaceViewModel> OpenSampleAsync(MainViewModel main, ServiceProvider services)
    {
        var solution = await services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await main.OpenWorkspaceAsync(solution);
        var workspace = main.Workspace!;
        await workspace.ActiveDocument!.AnalyzeNowAsync();
        return workspace;
    }

    /// <summary>The app on a device with the given name, in a window of the given size.</summary>
    private (MainViewModel Main, MainWindow Window, ServiceProvider Services) Start(string name, string kind, double width, double height)
    {
        var temp = Track(new TempDirectory());
        var services = Track(AppServices.Create(new AppPaths(temp.Path), s => s.AddSingleton<IDeviceIdentity>(new TestDeviceIdentity(name, kind))));
        var main = services.GetRequiredService<MainViewModel>();
        Track(new Closing(() => main.Workspace?.Dispose()));
        var window = new MainWindow { DataContext = main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (main, window, services);
    }

    /// <summary>A second device, a tablet, with its own identity and storage.</summary>
    private LinkService Tablet(string name)
    {
        var temp = Track(new TempDirectory());
        var paths = new AppPaths(temp.Path);
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        return Track(new LinkService(paths, new CompilerServices(paths), device, DeviceProfile.For(device.Device), new TestDeviceIdentity(name, LinkDeviceKinds.Tablet), new AvaloniaUiDispatcher()));
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
