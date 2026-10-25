using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm;
using TabStudio.Swarm.Device;

namespace TabStudio.App.Swarm;

/// <summary>
/// This device's side of a swarm in the app: the <see cref="SwarmMember"/>, with the same identity as TabStudio Link,
/// the swarms it paired with, its cache of builds, and the <see cref="SwarmDeviceHost"/> that shows what the tablet
/// sends.
/// </summary>
/// <remarks>
/// The swarms are kept under <c>State/Link/swarms.json</c>, the builds under <c>Cache/Swarm</c>. <see cref="Changed"/>
/// is raised on the UI thread. Every member is thread-safe.
/// </remarks>
public sealed class SwarmMemberService : IAsyncDisposable, IDisposable
{
    private readonly IUiDispatcher _ui;
    private int _disposed;

    /// <summary>Creates the device's side of a swarm. Nothing connects until it joins one.</summary>
    /// <param name="paths">Where the swarms and the builds are kept.</param>
    /// <param name="link">TabStudio Link, whose identity the swarm shares.</param>
    /// <param name="ui">Gets events back onto the UI thread.</param>
    /// <param name="host">The device's screen.</param>
    public SwarmMemberService(AppPaths paths, LinkService link, IUiDispatcher ui, SwarmDeviceHost host)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(host);
        _ui = ui;
        Host = host;
        Member = new SwarmMember(
            new SwarmMemberOptions
            {
                Identity = link.Identity,
                Coordinators = new LinkTrustStore(Path.Combine(paths.State, "Link", "swarms.json")),
                Name = link.DeviceName,
                Kind = link.DeviceKind,
                CacheDirectory = Path.Combine(paths.Cache, "Swarm"),
            },
            host);
        Member.Changed += (_, _) => _ui.Post(OnMemberChanged);
        Member.ConsoleOutput += (_, e) => _ui.Post(() => Host.Console.Write(e.Text, e.IsError ? OutputKind.Error : OutputKind.Program));
        Member.Coordinators.Changed += (_, _) => _ui.Post(() => Changed?.Invoke(this, EventArgs.Empty));
        host.InputCaptured += (_, input) => Member.ReportInput(SwarmScreens.ToSwarm(input));
        host.ScreenChanged += (_, _) => Member.ReportScreen();
    }

    /// <summary>Raised on the UI thread when anything about the membership or the app changed.</summary>
    public event EventHandler? Changed;

    /// <summary>This device's membership.</summary>
    public SwarmMember Member { get; }

    /// <summary>The device's screen.</summary>
    public SwarmDeviceHost Host { get; }

    /// <summary>Where the device stands with a swarm.</summary>
    public SwarmMembership Membership => Member.Membership;

    /// <summary>Whether the device is in a swarm, or rejoining one: then it shows the swarm's app full screen.</summary>
    public bool IsInSwarm => Membership is SwarmMembership.Joined or SwarmMembership.Reconnecting;

    /// <summary>The swarms this device paired with.</summary>
    public LinkTrustStore Coordinators => Member.Coordinators;

    /// <summary>Joins a swarm by its scanned QR code, its typed address and code, or as a device that paired before.</summary>
    /// <param name="target">The tablet, and the pairing code when pairing.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the device joined; <see cref="SwarmMember.Error"/> says why not.</returns>
    public Task<bool> JoinAsync(LinkTarget target, CancellationToken cancellationToken = default) => Member.JoinAsync(target, cancellationToken);

    /// <summary>Leaves the swarm; the device is the IDE again.</summary>
    public Task LeaveAsync() => Member.LeaveAsync();

    /// <summary>Leaves the swarm and stops its app.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await Member.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Leaves the swarm, waiting until done.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private void OnMemberChanged()
    {
        // A console app's output is the device's screen while it runs.
        if (Member.AppKind == SwarmAppKind.Console && Member.AppState is SwarmDeviceState.Starting or SwarmDeviceState.Running && Member.AppName is { } name)
        {
            Host.ShowConsole(name);
        }
        else if (Member.AppState is SwarmDeviceState.Ready && Host.Mode == SwarmDeviceMode.Console)
        {
            Host.ShowWaiting();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
