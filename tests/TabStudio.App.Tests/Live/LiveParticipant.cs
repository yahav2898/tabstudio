using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.Services.Live;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Live;
using TabStudio.Link.Networking;
using TabStudio.Live.Sessions;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Live;

/// <summary>
/// One device of a TabStudio Live session in the test's process: its own folder, identity and name, its own Link and
/// Live services and its own IDE, as the app builds them, joined to the others over the real encrypted transport on
/// the loopback address. Every participant shares the test's compiler, as they would share nothing but the network.
/// </summary>
internal sealed class LiveParticipant : IAsyncDisposable
{
    private readonly TempDirectory _temp = new();

    private LiveParticipant(string name, CompilerServices? compiler)
    {
        Name = name;
        Paths = new AppPaths(_temp.Path);
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var profile = DeviceProfile.For(device.Device);
        Compiler = compiler ?? new CompilerServices(Paths);
        var ui = new AvaloniaUiDispatcher();
        Link = new LinkService(Paths, Compiler, device, profile, new TestDeviceIdentity(name, LinkDeviceKinds.Tablet), ui);
        Live = new LiveService(Paths, Link, ui) { LoopbackOnly = true };
        Join = new LiveJoinViewModel(Live, new NoQrScanner());
        Workspace = new WorkspaceViewModel(Compiler, device, profile, ui, new SessionStore(Paths), Paths, link: Link, live: Live, liveJoin: Join);
    }

    /// <summary>The device's name, as the others see it.</summary>
    public string Name { get; }

    public AppPaths Paths { get; }

    public CompilerServices Compiler { get; }

    public LinkService Link { get; }

    public LiveService Live { get; }

    public LiveJoinViewModel Join { get; }

    public WorkspaceViewModel Workspace { get; }

    /// <summary>The session this device is in.</summary>
    public LiveSession Session => Live.Session ?? throw new InvalidOperationException($"{Name} is in no session.");

    /// <summary>The session this device hosts.</summary>
    public LiveHost Host => Live.Host ?? throw new InvalidOperationException($"{Name} hosts no session.");

    /// <summary>The session this device joined.</summary>
    public LiveGuest Guest => Live.Guest ?? throw new InvalidOperationException($"{Name} joined no session.");

    /// <summary>A device that opens the sample solution and shares it.</summary>
    /// <param name="name">Its name.</param>
    public static async Task<LiveParticipant> HostAsync(string name = "Dana's tablet")
    {
        var host = new LiveParticipant(name, compiler: null);
        var solution = await new SampleLibrary(host.Paths).EnsureExtractedAsync();
        await host.Workspace.LoadAsync(solution);
        await host.Workspace.ShareLiveSessionAsync();
        Assert.True(host.Workspace.IsLiveHost, host.Workspace.LivePanel?.Error);
        return host;
    }

    /// <summary>A device that joins the host's session by its QR code and opens the shared workspace, as the app does.</summary>
    /// <param name="host">The host.</param>
    /// <param name="name">Its name.</param>
    public static async Task<LiveParticipant> JoinAsync(LiveParticipant host, string name)
    {
        var guest = new LiveParticipant(name, host.Compiler);
        var invitation = host.Host.Invitation ?? throw new InvalidOperationException("The host has no invitation.");
        Assert.True(await guest.Join.JoinInvitationAsync(invitation), guest.Live.JoinError);
        var session = guest.Guest;
        var entry = session.EntryPath is { } relative && session.FullPathOf(relative) is { } full && File.Exists(full) ? full : session.WorkspaceRoot!;
        await guest.Workspace.LoadAsync(entry);
        await UiTestHelpers.WaitUntilAsync(() => guest.Workspace.IsLiveGuest, because: $"{name} to show the session");
        return guest;
    }

    /// <summary>This device's path of a file of the host's.</summary>
    /// <param name="host">The host.</param>
    /// <param name="hostPath">The file on the host.</param>
    public string PathOf(LiveParticipant host, string hostPath)
    {
        var relative = host.Host.RelativePathOf(hostPath) ?? throw new InvalidOperationException($"{hostPath} is not shared.");
        return Session.FullPathOf(relative) ?? throw new InvalidOperationException($"{relative} has no copy here.");
    }

    /// <summary>Opens a file and waits until it is shared in the session.</summary>
    /// <param name="path">The file, on this device.</param>
    public async Task<DocumentViewModel> OpenSharedAsync(string path)
    {
        var document = await Workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException($"{path} did not open.");
        await UiTestHelpers.WaitUntilAsync(() => document.Live is not null, because: $"{Path.GetFileName(path)} to be shared on {Name}");
        return document;
    }

    public async ValueTask DisposeAsync()
    {
        Workspace.Dispose();
        await Live.DisposeAsync();
        Join.Dispose();
        await Link.DisposeAsync();
        _temp.Dispose();
    }
}
