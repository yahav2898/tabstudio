using TabStudio.Link.Messaging;

namespace TabStudio.Link.Networking;

/// <summary>An authenticated connection from a tablet to its host.</summary>
public sealed class LinkClientSession : IAsyncDisposable, IDisposable
{
    internal LinkClientSession(LinkConnection connection, LinkPeer host, int port, bool isNewPairing)
    {
        Connection = connection;
        Host = host;
        Port = port;
        IsNewPairing = isNewPairing;
    }

    /// <summary>The connection, for requests to the host.</summary>
    public LinkConnection Connection { get; }

    /// <summary>The host, as the handshake verified it.</summary>
    public LinkPeer Host { get; }

    /// <summary>The port the host answered on.</summary>
    public int Port { get; }

    /// <summary>Whether this connection paired the devices, so the host should be remembered.</summary>
    public bool IsNewPairing { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    /// <inheritdoc />
    public void Dispose() => Connection.Dispose();
}
