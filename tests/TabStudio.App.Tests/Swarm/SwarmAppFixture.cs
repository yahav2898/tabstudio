using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
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
/// window, as separate phones would. The windows draw frames on their own, as screens do, since Avalonia's controls
/// take a tap by what was last drawn under it.
/// </summary>
internal sealed class SwarmAppFixture : IAsyncDisposable
{
    /// <summary>The test collection of the tests that build and run apps on a swarm, which run one at a time.</summary>
    public const string Collection = "Swarm";

    /// <summary>How often the headless windows draw a frame.</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(40);

    private readonly TempDirectory _temp = new();
    private readonly List<SwarmAppDevice> _devices = [];
    private readonly DispatcherTimer _frames = new(DispatcherPriority.Background) { Interval = FrameInterval };

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
        _frames.Tick += (_, _) => AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        _frames.Start();
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

    /// <summary>
    /// Notes how far a swarm test got, with the time, in <c>swarm-tests.log</c> beside the screenshots CI publishes, so a
    /// test that hangs says where.
    /// </summary>
    public static void Progress(string step)
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "swarm-tests.log"), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{DateTime.UtcNow:HH:mm:ss.fff} {step}\n"));
        }
    }

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
        _frames.Stop();
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

/// <summary>
/// A phone of a <see cref="SwarmAppFixture"/>: its own data, Link identity, screen and window. Its app preview is the
/// app's own Preview screen, which a person's taps reach as real input on the phone's window.
/// </summary>
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
        Host = new SwarmDeviceHost(ui, new NoWebViewFactory(), new TopLevelStoragePicker(), profile);
        Member = new SwarmMemberService(Paths, Link, ui, Host);
        Page = new SwarmDevicePageViewModel(Member);
        Window = new Window { Width = 412, Height = 915, Content = new SwarmDevicePageView { DataContext = Page } };
        Window.Show();
    }

    public string Name { get; }

    public AppPaths Paths { get; }

    public LinkService Link { get; }

    public SwarmDeviceHost Host { get; }

    /// <summary>The device's app preview as a screen, which the tablet drives.</summary>
    public PreviewAppScreen Preview => Host.PreviewScreen;

    public SwarmMemberService Member { get; }

    public SwarmDevicePageViewModel Page { get; }

    public Window Window { get; }

    /// <summary>The texts the device's app shows.</summary>
    public IReadOnlyList<string> Texts() =>
        Host.Preview.View is { } view
            ? [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<TextBlock>().Select(t => t.Text ?? string.Empty)]
            : [];

    /// <summary>A person taps an element of the app on this phone: a click on the phone's window, in the element's middle.</summary>
    /// <param name="elementPath">The element, by its path or its name.</param>
    public void PersonTaps(string elementPath) => PersonClicks(elementPath, MouseButton.Left);

    /// <summary>A person long-presses an element, as a right click does with a mouse, as a recorder's check.</summary>
    /// <param name="elementPath">The element, by its path or its name.</param>
    public void PersonLongPresses(string elementPath) => PersonClicks(elementPath, MouseButton.Right);

    /// <summary>
    /// The phone reports an input on an element that only its own layout has, as a phone with another layout would,
    /// straight to the tablet.
    /// </summary>
    /// <param name="input">The input.</param>
    public void Reports(AppInput input) => Member.Member.ReportInput(SwarmScreens.ToSwarm(input));

    public async ValueTask DisposeAsync()
    {
        Window.Close();
        Page.Dispose();
        await Member.DisposeAsync();
        Host.Dispose();
        Link.Dispose();
    }

    private void PersonClicks(string elementPath, MouseButton button)
    {
        var view = Host.Preview.View ?? throw new InvalidOperationException($"{Name} shows no app.");
        var element = ElementPaths.Resolve(view, elementPath) ?? throw new InvalidOperationException($"{Name} shows no {elementPath}.");
        var middle = element.TranslatePoint(new Point(element.Bounds.Width / 2, element.Bounds.Height / 2), Window)
            ?? throw new InvalidOperationException($"{elementPath} is not in {Name}'s window.");

        // A finger lands on what the screen shows: the latest layout is drawn first.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Window.MouseMove(middle);
        Window.MouseDown(middle, button);
        Window.MouseUp(middle, button);
    }
}
