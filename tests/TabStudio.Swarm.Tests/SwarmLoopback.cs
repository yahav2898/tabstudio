using System.Net;
using System.Net.Sockets;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm.Coordinator;
using TabStudio.Swarm.Device;
using TabStudio.Testing;

namespace TabStudio.Swarm.Tests;

/// <summary>
/// A swarm inside the test: a coordinator on the loopback address and devices with identities, caches and fake
/// screens of their own, connected to it over real TLS.
/// </summary>
internal sealed class SwarmLoopback : IAsyncDisposable
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly List<TestDevice> _devices = [];
    private readonly TempDirectory _temp = new();
    private readonly LinkIdentity _identity = LinkIdentity.Create();

    public SwarmLoopback(int port = 0)
    {
        Coordinator = new SwarmCoordinator(new SwarmCoordinatorOptions
        {
            Identity = _identity,
            Devices = DeviceStore,
            Name = "Yahav's tablet",
            Port = port,
            LoopbackOnly = true,
            PingInterval = TimeSpan.FromMilliseconds(300),
        });
        Coordinator.Start();
    }

    public SwarmCoordinator Coordinator { get; }

    /// <summary>The devices paired with the swarm.</summary>
    public LinkTrustStore DeviceStore { get; } = new(null);

    /// <summary>A free TCP port on the loopback address, for a coordinator that restarts on the same port.</summary>
    public static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>A device that joins by scanning the coordinator's QR code.</summary>
    public async Task<TestDevice> JoinAsync(string name, Action<FakeDeviceHost>? setUp = null)
    {
        var device = CreateDevice(name, setUp);
        var invitation = Coordinator.Invitation ?? throw new InvalidOperationException("The coordinator shows no QR code.");
        Assert.True(await device.Member.JoinAsync(LinkTarget.FromInvitation(invitation)), device.Member.Error);
        await UntilAsync(() => device.OnCoordinator(Coordinator) is { IsConnected: true }, $"{name} on the coordinator");
        return device;
    }

    /// <summary>A device that has not joined yet.</summary>
    public TestDevice CreateDevice(string name, Action<FakeDeviceHost>? setUp = null)
    {
        var host = new FakeDeviceHost { Name = name };
        setUp?.Invoke(host);
        var identity = LinkIdentity.Create();
        var member = new SwarmMember(
            new SwarmMemberOptions
            {
                Identity = identity,
                Coordinators = new LinkTrustStore(null),
                Name = name,
                CacheDirectory = Path.Combine(_temp.Path, name.Replace(' ', '-')),
                PingInterval = TimeSpan.FromMilliseconds(500),
                ReconnectDelays = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250)],
            },
            host);
        var device = new TestDevice(name, member, host, identity);
        _devices.Add(device);
        return device;
    }

    /// <summary>Waits until a condition holds, failing after <see cref="Patience"/>.</summary>
    public static async Task UntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {because}.");
            await Task.Delay(20);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var device in _devices)
        {
            await device.Member.DisposeAsync();
            device.Identity.Dispose();
        }

        await Coordinator.DisposeAsync();
        _identity.Dispose();
        _temp.Dispose();
    }
}

/// <summary>A device of a <see cref="SwarmLoopback"/>.</summary>
internal sealed record TestDevice(string Name, SwarmMember Member, FakeDeviceHost Host, LinkIdentity Identity)
{
    /// <summary>The device as the coordinator sees it.</summary>
    public SwarmDevice? OnCoordinator(SwarmCoordinator coordinator) => coordinator.Devices.FirstOrDefault(d => d.Fingerprint == Identity.Fingerprint);
}
