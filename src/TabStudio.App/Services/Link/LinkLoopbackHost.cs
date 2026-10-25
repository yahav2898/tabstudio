using TabStudio.Link.Language;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.App.Services.Link;

/// <summary>
/// A TabStudio Link host inside this app, on the loopback address, with an identity of its own: what the self test and
/// the tests link to, so a device can check that the whole link works without a second device.
/// </summary>
internal sealed class LinkLoopbackHost : IAsyncDisposable
{
    private readonly LinkIdentity _identity = LinkIdentity.Create();
    private readonly LinkTrustStore _tablets = new(null);
    private readonly CompilerServices _compiler;
    private readonly string _mirrors;
    private readonly string _name;
    private readonly string _kind;
    private readonly Lock _gate = new();
    private readonly List<LinkLanguageServer> _servers = [];
    private LinkHost? _host;
    private int _port;
    private long _finishedRequests;
    private int _disposed;

    /// <summary>Starts the host.</summary>
    /// <param name="compiler">The compiler services the host compiles with.</param>
    /// <param name="mirrors">Where the host keeps the tablet's workspace.</param>
    /// <param name="name">The host's name.</param>
    /// <param name="kind">What the host is, one of <see cref="LinkDeviceKinds"/>.</param>
    public LinkLoopbackHost(CompilerServices compiler, string mirrors, string name, string kind)
    {
        _compiler = compiler;
        _mirrors = mirrors;
        _name = name;
        _kind = kind;
        Restart();
    }

    /// <summary>The host's fingerprint, as the tablet remembers it.</summary>
    public LinkFingerprint Fingerprint => _identity.Fingerprint;

    /// <summary>What scanning the host's QR code gives.</summary>
    /// <exception cref="InvalidOperationException">The host is stopped.</exception>
    public LinkTarget Invitation => LinkTarget.FromInvitation(
        (Current() ?? throw new InvalidOperationException("The host is stopped.")).CreateInvitation(["127.0.0.1"]));

    /// <summary>Requests answered for the tablet so far.</summary>
    public long RequestCount
    {
        get
        {
            lock (_gate)
            {
                return _finishedRequests + _servers.Sum(s => s.RequestCount);
            }
        }
    }

    /// <summary>Stops listening and drops the tablet, as when the device leaves the Wi-Fi. <see cref="Restart"/> brings it back.</summary>
    public async Task StopAsync()
    {
        LinkHost? host;
        List<LinkLanguageServer> servers;
        lock (_gate)
        {
            host = _host;
            _host = null;
            servers = [.. _servers];
            _servers.Clear();
            _finishedRequests += servers.Sum(s => s.RequestCount);
        }

        if (host is not null)
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var server in servers)
        {
            server.Dispose();
        }
    }

    /// <summary>Listens again, on the same port and with the same identity, so the tablet reconnects by itself.</summary>
    public void Restart()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        var host = new LinkHost(
            new LinkHostOptions
            {
                Identity = _identity,
                Clients = _tablets,
                DeviceName = _name,
                DeviceKind = _kind,
                Port = _port,
                LoopbackOnly = true,
            },
            CreateServer);
        host.Start();
        lock (_gate)
        {
            _host = host;
            _port = host.Port;
        }
    }

    /// <summary>Stops the host for good.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _identity.Dispose();
    }

    private LinkHost? Current()
    {
        lock (_gate)
        {
            return _host;
        }
    }

    private LinkLanguageServer CreateServer(LinkPeer tablet, LinkConnection connection)
    {
        var server = new LinkLanguageServer(
            Path.Combine(_mirrors, Convert.ToHexString(tablet.Fingerprint.ToBytes())[..16]),
            async _ => new LocalLinkBackend(new LocalLanguageService(await _compiler.CreateHostAsync().ConfigureAwait(false), _compiler.Packages)));
        lock (_gate)
        {
            _servers.Add(server);
        }

        return server;
    }
}
