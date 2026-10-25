using Avalonia.Controls;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Swarm;
using TabStudio.App.Views;
using TabStudio.App.Views.Swarm;
using TabStudio.Link.Networking;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// A swarm inside the test, made of the app's own pieces: the tablet, "Yahav's tablet", with a workspace and its swarm
/// on the loopback address, and devices that join it over real TLS, each with its own identity, cache, screen and
/// window, as separate phones would.
/// </summary>
internal sealed class SwarmAppFixture : IAsyncDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly List<SwarmAppDevice> _devices = [];

    public SwarmAppFixture()
    {
        Paths = new AppPaths(Path.Combine(_temp.Path, "tablet"));
        var status = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var profile = DeviceProfile.For(status.Device);
        var ui = new AvaloniaUiDispatcher();
        var compiler = new CompilerServices(Paths);
        Link = new LinkService(Paths, compiler, status, profile, new TestDeviceIdentity("Yahav's tablet", LinkDeviceKinds.Tablet), ui);
        Registry = new AppScreenRegistry();
        Mirror = new SwarmInputMirror(Registry, ui);
        Swarm = new SwarmCoordinatorService(Paths, Link, ui, Registry, new SwarmSettingsStore(Paths), Mirror);
        Services = new SwarmServices(Swarm, Registry, Mirror, Paths);
        Workspace = new WorkspaceViewModel(compiler, status, profile, ui, new SessionStore(Paths), Paths, link: Link, swarm: Services);
    }

    public AppPaths Paths { get; }

    public LinkService Link { get; }

    public AppScreenRegistry Registry { get; }

    public SwarmInputMirror Mirror { get; }

    public SwarmCoordinatorService Swarm { get; }

    public SwarmServices Services { get; }

    public WorkspaceViewModel Workspace { get; }

    /// <summary>The Swarm window.</summary>
    public SwarmViewModel SwarmWindow => Workspace.Swarm ?? throw new InvalidOperationException("The workspace has no swarm.");

    /// <summary>Shows the IDE in a tablet-sized window.</summary>
    public Window ShowIde()
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = Workspace } };
        window.Show();
        return window;
    }

    /// <summary>Starts the swarm on the loopback address.</summary>
    public async Task StartAsync()
    {
        Assert.True(await Swarm.StartAsync(port: 0, loopbackOnly: true), Swarm.Error);
    }

    /// <summary>A phone that scans the swarm's code and joins it.</summary>
    public async Task<SwarmAppDevice> JoinAsync(string name)
    {
        var device = new SwarmAppDevice(Path.Combine(_temp.Path, name.Replace(' ', '-').Replace("'", string.Empty, StringComparison.Ordinal)), name);
        _devices.Add(device);
        var invitation = Swarm.Coordinator?.Invitation ?? throw new InvalidOperationException("The swarm shows no QR code.");
        Assert.True(await device.Member.JoinAsync(LinkTarget.FromInvitation(invitation)), device.Member.Member.Error);
        await UiTestHelpers.WaitUntilAsync(() => Swarm.Devices.Any(d => d.Name == name && d.IsConnected), because: $"{name} to join");
        return device;
    }

    /// <summary>The tablet's screen of a device, in its registry.</summary>
    public DeviceAppScreen ScreenOf(SwarmAppDevice device) =>
        Swarm.Devices.FirstOrDefault(d => d.Name == device.Name) is { } joined && Swarm.ScreenOf(joined) is { } screen
            ? screen
            : throw new InvalidOperationException($"{device.Name} has no screen on the tablet.");

    public async ValueTask DisposeAsync()
    {
        foreach (var device in _devices)
        {
            await device.DisposeAsync();
        }

        Workspace.Dispose();
        await Swarm.DisposeAsync();
        Mirror.Dispose();
        Link.Dispose();
        _temp.Dispose();
    }
}

/// <summary>A phone of a <see cref="SwarmAppFixture"/>: its own data, Link identity, screen and window.</summary>
internal sealed class SwarmAppDevice : IAsyncDisposable
{
    public SwarmAppDevice(string folder, string name)
    {
        Name = name;
        Paths = new AppPaths(folder);
        var status = new DefaultDeviceStatus(new DeviceInfo(6L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var profile = DeviceProfile.For(status.Device);
        var ui = new AvaloniaUiDispatcher();
        Link = new LinkService(Paths, new CompilerServices(Paths), status, profile, new TestDeviceIdentity(name, LinkDeviceKinds.Phone), ui);
        Registry = new AppScreenRegistry();
        Host = new SwarmDeviceHost(ui, Registry, new NoWebViewFactory(), new TopLevelStoragePicker(), profile);
        Preview = new FakePreviewScreen(Host.Preview, name);
        Registry.Add(Preview);
        Member = new SwarmMemberService(Paths, Link, ui, Host);
        Page = new SwarmDevicePageViewModel(Member);
        Window = new Window { Width = 412, Height = 915, Content = new SwarmDevicePageView { DataContext = Page } };
        Window.Show();
    }

    public string Name { get; }

    public AppPaths Paths { get; }

    public LinkService Link { get; }

    public AppScreenRegistry Registry { get; }

    public SwarmDeviceHost Host { get; }

    /// <summary>The device's app preview as a screen, as the Swarm screens feature provides it.</summary>
    public FakePreviewScreen Preview { get; }

    public SwarmMemberService Member { get; }

    public SwarmDevicePageViewModel Page { get; }

    public Window Window { get; }

    /// <summary>The texts the device's app shows.</summary>
    public IReadOnlyList<string> Texts() =>
        Host.Preview.View is { } view
            ? [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<TextBlock>().Select(t => t.Text ?? string.Empty)]
            : [];

    public async ValueTask DisposeAsync()
    {
        Window.Close();
        Page.Dispose();
        await Member.DisposeAsync();
        Link.Dispose();
    }
}
