using System.Net;
using System.Net.Sockets;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.Link.Tests;

/// <summary>Finding hosts on the local network with a UDP broadcast, here answered over the loopback address.</summary>
public sealed class DiscoveryTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task A_running_host_answers_with_its_name_fingerprint_and_port()
    {
        using var identity = LinkIdentity.Create();
        var port = FreeUdpPort();
        using var responder = new LinkDiscoveryResponder(identity.Fingerprint, () => 50517, () => "Pixel 9 · Yahav's");
        Assert.True(responder.Start(port));

        var found = await LinkDiscovery.FindAsync(null, Wait, CancellationToken.None, port);

        var host = Assert.Single(found, h => h.Fingerprint == identity.Fingerprint);
        Assert.Equal("Pixel 9 · Yahav's", host.Name);
        Assert.Equal(50517, host.Endpoint.Port);
    }

    [Fact]
    public async Task Looking_for_one_host_ignores_the_others()
    {
        using var wanted = LinkIdentity.Create();
        using var other = LinkIdentity.Create();
        var port = FreeUdpPort();
        using var responder = new LinkDiscoveryResponder(other.Fingerprint, () => 50517, () => "Someone else's PC");
        Assert.True(responder.Start(port));

        var found = await LinkDiscovery.FindAsync(wanted.Fingerprint, Wait, CancellationToken.None, port);

        Assert.Empty(found);
    }

    [Fact]
    public async Task A_stopped_host_does_not_answer()
    {
        using var identity = LinkIdentity.Create();
        var port = FreeUdpPort();
        var responder = new LinkDiscoveryResponder(identity.Fingerprint, () => 50517, () => "Pixel 9");
        Assert.True(responder.Start(port));
        responder.Dispose();

        var found = await LinkDiscovery.FindAsync(identity.Fingerprint, Wait, CancellationToken.None, port);

        Assert.Empty(found);
    }

    [Theory]
    [InlineData("TABSTUDIO-LINK/1 HOST")]
    [InlineData("TABSTUDIO-LINK/1 HOST nope 50517 Pixel")]
    [InlineData("TABSTUDIO-LINK/1 HOST {0} 0 Pixel")]
    [InlineData("TABSTUDIO-LINK/1 HOST {0} 70000 Pixel")]
    [InlineData("TABSTUDIO-LINK/2 HOST {0} 50517 Pixel")]
    [InlineData("HTTP/1.1 200 OK")]
    public void Answers_that_are_not_from_a_host_are_ignored(string answer)
    {
        using var identity = LinkIdentity.Create();

        var host = LinkDiscovery.ParseAnswer(answer.Replace("{0}", identity.Fingerprint.Value, StringComparison.Ordinal), IPAddress.Loopback);

        Assert.Null(host);
    }

    [Theory]
    [InlineData("TABSTUDIO-LINK/1 DISCOVER *", true)]
    [InlineData("TABSTUDIO-LINK/1 DISCOVER {0}", true)]
    [InlineData("TABSTUDIO-LINK/1 DISCOVER someone-else", false)]
    [InlineData("TABSTUDIO-LINK/1 HELLO *", false)]
    [InlineData("M-SEARCH * HTTP/1.1", false)]
    public void A_host_answers_requests_for_any_host_or_for_itself(string request, bool answers)
    {
        using var identity = LinkIdentity.Create();

        var answer = LinkDiscovery.Answer(request.Replace("{0}", identity.Fingerprint.Value, StringComparison.Ordinal), identity.Fingerprint, 50517, "Pixel 9");

        Assert.Equal(answers, answer is not null);
        if (answer is not null)
        {
            var host = LinkDiscovery.ParseAnswer(answer, IPAddress.Parse("::ffff:192.168.1.23"));
            Assert.NotNull(host);
            Assert.Equal("Pixel 9", host.Name);
            Assert.Equal("192.168.1.23", host.Endpoint.Host);
            Assert.Equal(identity.Fingerprint, host.Fingerprint);
        }
    }

    private static int FreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
