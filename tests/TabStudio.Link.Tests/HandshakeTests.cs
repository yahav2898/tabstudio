using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.Link.Tests;

/// <summary>TLS, pinning and pairing between a real host and a real tablet over loopback.</summary>
public sealed class HandshakeTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly LinkIdentity _hostIdentity = LinkIdentity.Create();
    private readonly LinkIdentity _tabletIdentity = LinkIdentity.Create();
    private readonly LinkTrustStore _pairedTablets = new(null);
    private readonly List<LinkHost> _hosts = [];

    [Fact]
    public async Task Pairing_with_the_typed_code_authenticates_both_devices()
    {
        var host = StartHost();

        await using var session = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler());

        Assert.True(session.IsNewPairing);
        Assert.Equal("Pixel 9", session.Host.Name);
        Assert.Equal(_hostIdentity.Fingerprint, session.Host.Fingerprint);
        Assert.StartsWith("TLS 1.", session.Host.Security, StringComparison.Ordinal);
        Assert.Equal("Yahav's tablet", _pairedTablets.Find(_tabletIdentity.Fingerprint)?.Name);
        await UntilAsync(() => host.Client is not null);
        Assert.Equal(_tabletIdentity.Fingerprint, host.Client!.Fingerprint);
        var answer = await session.Connection.RequestAsync("echo", TestHandler.Text("over TLS"));
        Assert.Equal("over TLS", answer.Read(TestJsonContext.Default.String));
    }

    [Fact]
    public async Task Scanning_the_QR_code_pins_the_host_and_pairs()
    {
        var host = StartHost();
        var invitation = host.CreateInvitation(["127.0.0.1"]);
        Assert.True(LinkInvitation.TryParse(invitation.ToUri(), out var scanned));

        await using var session = await LinkClient.ConnectAsync(Tablet(), LinkTarget.FromInvitation(scanned), new TestHandler());

        Assert.True(session.IsNewPairing);
        Assert.Equal(_hostIdentity.Fingerprint, session.Host.Fingerprint);
    }

    [Fact]
    public async Task A_remembered_pairing_connects_without_a_code()
    {
        var host = StartHost();
        await using (var first = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler()))
        {
        }

        await using var again = await LinkClient.ConnectAsync(Tablet(), Paired(host), new TestHandler());

        Assert.False(again.IsNewPairing);
        Assert.Equal(_hostIdentity.Fingerprint, again.Host.Fingerprint);
    }

    [Fact]
    public async Task A_pairing_code_works_only_once()
    {
        var host = StartHost();
        var code = host.PairingCode;
        await using (var first = await LinkClient.ConnectAsync(Tablet(), ByCode(host, code), new TestHandler()))
        {
            Assert.NotEqual(code, host.PairingCode);
        }

        using var otherTablet = LinkIdentity.Create();
        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() =>
            LinkClient.ConnectAsync(Tablet(otherTablet), ByCode(host, code), new TestHandler()));

        Assert.Equal(LinkRefusal.WrongCode, error.Refusal);
    }

    [Fact]
    public async Task A_wrong_code_is_refused_and_the_host_draws_a_new_one()
    {
        var host = StartHost();
        var shown = host.PairingCode;
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.PairingCodeChanged += (_, _) => changed.TrySetResult();
        Assert.True(LinkPairingCode.TryParse(shown.Value == "00000000" ? "11111111" : "00000000", out var wrong));

        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() => LinkClient.ConnectAsync(Tablet(), ByCode(host, wrong), new TestHandler()));

        Assert.Equal(LinkRefusal.WrongCode, error.Refusal);
        await changed.Task.WaitAsync(Patience);
        Assert.NotEqual(shown, host.PairingCode);
        Assert.Null(_pairedTablets.Find(_tabletIdentity.Fingerprint));
    }

    [Fact]
    public async Task A_device_that_never_paired_is_refused()
    {
        var host = StartHost();

        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() => LinkClient.ConnectAsync(Tablet(), Paired(host), new TestHandler()));

        Assert.Equal(LinkRefusal.NotPaired, error.Refusal);
        Assert.Contains("Pixel 9", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_host_with_another_certificate_is_refused_before_anything_is_sent()
    {
        var host = StartHost();
        using var impostor = LinkIdentity.Create();
        var target = new LinkTarget("Pixel 9", ["127.0.0.1"], host.Port, impostor.Fingerprint, null);

        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() => LinkClient.ConnectAsync(Tablet(), target, new TestHandler()));

        Assert.Equal(LinkRefusal.WrongHost, error.Refusal);
        Assert.Null(host.Client);
    }

    [Fact]
    public async Task A_forgotten_tablet_has_to_pair_again()
    {
        var host = StartHost();
        await using (var first = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler()))
        {
        }

        Assert.True(_pairedTablets.Forget(_tabletIdentity.Fingerprint));
        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() => LinkClient.ConnectAsync(Tablet(), Paired(host), new TestHandler()));

        Assert.Equal(LinkRefusal.NotPaired, error.Refusal);
    }

    [Fact]
    public async Task Different_versions_of_TabStudio_are_refused()
    {
        var host = StartHost();
        var older = Tablet() with { AppVersion = "0.1.0" };

        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() => LinkClient.ConnectAsync(older, ByCode(host, host.PairingCode), new TestHandler()));

        Assert.Equal(LinkRefusal.VersionMismatch, error.Refusal);
        Assert.Contains("0.1.0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_tablet_is_refused_while_one_is_linked()
    {
        var host = StartHost();
        await using var first = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler());
        using var secondIdentity = LinkIdentity.Create();

        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() =>
            LinkClient.ConnectAsync(Tablet(secondIdentity, "Second tablet"), ByCode(host, host.PairingCode), new TestHandler()));

        Assert.Equal(LinkRefusal.Busy, error.Refusal);
        Assert.Contains("Yahav's tablet", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_tablet_connecting_again_replaces_its_old_connection()
    {
        var host = StartHost();
        var first = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler());

        await using var second = await LinkClient.ConnectAsync(Tablet(), Paired(host), new TestHandler());

        await first.Connection.Completion.WaitAsync(Patience);
        await first.DisposeAsync();
        Assert.True(second.Connection.IsOpen);
        await UntilAsync(() => host.ClientConnection == second.Connection || host.Client is not null);
    }

    [Fact]
    public async Task A_tablet_reconnects_after_the_host_restarts()
    {
        var host = StartHost();
        var port = host.Port;
        await using (var first = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler()))
        {
        }

        await host.DisposeAsync();
        var restarted = StartHost(port);

        await using var again = await LinkClient.ConnectAsync(Tablet(), Paired(restarted), new TestHandler());

        Assert.Equal(port, restarted.Port);
        Assert.Equal(_hostIdentity.Fingerprint, again.Host.Fingerprint);
    }

    [Fact]
    public async Task The_host_notices_when_the_tablet_leaves()
    {
        var host = StartHost();
        var session = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler());
        await UntilAsync(() => host.Client is not null);

        await session.DisposeAsync();

        await UntilAsync(() => host.Client is null);
    }

    [Fact]
    public async Task A_host_that_stops_answering_is_noticed_by_the_tablet()
    {
        var host = StartHost();
        await using var session = await LinkClient.ConnectAsync(Tablet(), ByCode(host, host.PairingCode), new TestHandler());

        await host.DisposeAsync();

        await session.Connection.Completion.WaitAsync(Patience);
        Assert.False(session.Connection.IsOpen);
    }

    [Fact]
    public async Task Nobody_listening_is_reported_as_unreachable()
    {
        var host = StartHost();
        var port = host.Port;
        await host.DisposeAsync();
        Assert.True(LinkPairingCode.TryParse("ABCD-EFGH", out var code));

        var error = await Assert.ThrowsAsync<LinkUnreachableException>(() =>
            LinkClient.ConnectAsync(Tablet(), LinkTarget.FromCode(new LinkEndpoint("127.0.0.1", port), code), new TestHandler()));

        Assert.Contains("127.0.0.1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_device_cannot_link_to_itself()
    {
        var host = StartHost();

        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() =>
            LinkClient.ConnectAsync(Tablet(_hostIdentity), ByCode(host, host.PairingCode), new TestHandler()));

        Assert.Equal(LinkRefusal.Self, error.Refusal);
    }

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        _hostIdentity.Dispose();
        _tabletIdentity.Dispose();
    }

    private LinkHost StartHost(int port = 0)
    {
        var host = new LinkHost(
            new LinkHostOptions { Identity = _hostIdentity, Clients = _pairedTablets, DeviceName = "Pixel 9", Port = port, LoopbackOnly = true },
            (_, _) => new TestHandler());
        host.Start();
        _hosts.Add(host);
        return host;
    }

    private LinkClientOptions Tablet(LinkIdentity? identity = null, string name = "Yahav's tablet") =>
        new() { Identity = identity ?? _tabletIdentity, DeviceName = name };

    private static LinkTarget ByCode(LinkHost host, LinkPairingCode code) => LinkTarget.FromCode(new LinkEndpoint("127.0.0.1", host.Port), code);

    private LinkTarget Paired(LinkHost host) =>
        new("Pixel 9", ["127.0.0.1"], host.Port, _hostIdentity.Fingerprint, null);

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out.");
            await Task.Delay(20);
        }
    }
}
