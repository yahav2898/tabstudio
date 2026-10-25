using TabStudio.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm.Device;

namespace TabStudio.Swarm.Tests;

/// <summary>Joining a swarm over TabStudio Link's pairing, the coordinator's list of devices, leaving and coming back.</summary>
public sealed class JoiningTests
{
    [Fact]
    public async Task A_device_joins_by_scanning_the_QR_code_and_the_tablet_lists_it()
    {
        await using var swarm = new SwarmLoopback();
        var invitation = swarm.Coordinator.Invitation!;
        Assert.Equal(LinkRole.Swarm, invitation.Role);
        Assert.StartsWith("tabstudio-link://swarm?", invitation.ToUri(), StringComparison.Ordinal);
        Assert.True(LinkInvitation.TryParse(invitation.ToUri(), out var scanned));
        Assert.Equal(LinkRole.Swarm, scanned.Role);

        var phone = await swarm.JoinAsync("Pixel 9");

        var device = Assert.Single(swarm.Coordinator.Devices);
        Assert.Equal("Pixel 9", device.Name);
        Assert.Equal(LinkDeviceKinds.Phone, device.Kind);
        Assert.Equal(new SwarmScreenInfo(412, 915, 2.6), device.Screen);
        Assert.Equal(SwarmDeviceState.Ready, device.State);
        Assert.StartsWith("TLS 1.", device.Security, StringComparison.Ordinal);
        Assert.Equal(SwarmMembership.Joined, phone.Member.Membership);
        Assert.Equal("Yahav's tablet", phone.Member.CoordinatorName);
        Assert.NotNull(swarm.DeviceStore.Find(phone.Identity.Fingerprint));
        Assert.Equal("Yahav's tablet", Assert.Single(phone.Member.Coordinators.Devices).Name);
    }

    [Fact]
    public async Task Devices_join_one_after_another_and_each_code_works_once()
    {
        await using var swarm = new SwarmLoopback();
        var first = swarm.Coordinator.Invitation!;

        await swarm.JoinAsync("Pixel 9");
        Assert.NotEqual(first.Code, swarm.Coordinator.PairingCode);
        await swarm.JoinAsync("Galaxy Tab S9");

        var late = swarm.CreateDevice("Moto G");
        Assert.False(await late.Member.JoinAsync(LinkTarget.FromInvitation(first)));
        Assert.Contains("not the code", late.Member.Error, StringComparison.Ordinal);
        Assert.Equal(["Pixel 9", "Galaxy Tab S9"], swarm.Coordinator.Devices.Select(d => d.Name));
    }

    [Fact]
    public async Task A_device_joins_with_the_typed_address_and_code()
    {
        await using var swarm = new SwarmLoopback();
        var device = swarm.CreateDevice("Pixel 9");

        var target = LinkTarget.FromCode(new LinkEndpoint("127.0.0.1", swarm.Coordinator.Port), swarm.Coordinator.PairingCode!);
        Assert.True(await device.Member.JoinAsync(target), device.Member.Error);

        await SwarmLoopback.UntilAsync(() => swarm.Coordinator.Devices.Count == 1, "the device");
        Assert.Equal(SwarmMembership.Joined, device.Member.Membership);
    }

    [Fact]
    public async Task A_TabStudio_Link_host_is_not_a_swarm_and_a_swarm_is_not_a_Link_host()
    {
        await using var swarm = new SwarmLoopback();
        using var hostIdentity = LinkIdentity.Create();
        await using var linkHost = new LinkHost(
            new LinkHostOptions { Identity = hostIdentity, Clients = new LinkTrustStore(null), DeviceName = "Pixel 9", Port = 0, LoopbackOnly = true },
            (_, _) => throw new InvalidOperationException("Nothing may be served."));
        linkHost.Start();

        var device = swarm.CreateDevice("Moto G");
        Assert.False(await device.Member.JoinAsync(LinkTarget.FromInvitation(linkHost.CreateInvitation(["127.0.0.1"]))));
        Assert.Contains("is a TabStudio Link host, not a swarm", device.Member.Error, StringComparison.Ordinal);

        using var tabletIdentity = LinkIdentity.Create();
        var error = await Assert.ThrowsAsync<LinkAuthenticationException>(() => LinkClient.ConnectAsync(
            new LinkClientOptions { Identity = tabletIdentity, DeviceName = "Other tablet" },
            LinkTarget.FromInvitation(swarm.Coordinator.Invitation!),
            new NoMessages()));
        Assert.Equal(LinkRefusal.WrongRole, error.Refusal);
        Assert.Contains("coordinates a TabStudio Swarm", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_tablet_measures_the_latency_of_every_device()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var tablet = await swarm.JoinAsync("Galaxy Tab S9");

        await SwarmLoopback.UntilAsync(() => swarm.Coordinator.Devices.All(d => d.Latency is not null), "a latency for every device");
        await SwarmLoopback.UntilAsync(() => phone.Member.Latency is not null && tablet.Member.Latency is not null, "the devices' own pings");
    }

    [Fact]
    public async Task A_device_that_leaves_is_gone_and_the_others_stay()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var tablet = await swarm.JoinAsync("Galaxy Tab S9");

        await phone.Member.LeaveAsync();

        await SwarmLoopback.UntilAsync(() => swarm.Coordinator.Devices.Count == 1, "the device to leave");
        Assert.Equal("Galaxy Tab S9", Assert.Single(swarm.Coordinator.Devices).Name);
        Assert.True(tablet.OnCoordinator(swarm.Coordinator)!.IsConnected);
        Assert.Equal(SwarmMembership.Off, phone.Member.Membership);
        Assert.Equal(SwarmMembership.Joined, tablet.Member.Membership);
    }

    [Fact]
    public async Task A_removed_device_leaves_and_has_to_pair_again()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var paired = Assert.Single(phone.Member.Coordinators.Devices);

        await swarm.Coordinator.RemoveAsync(phone.OnCoordinator(swarm.Coordinator)!);

        await SwarmLoopback.UntilAsync(() => phone.Member.Membership == SwarmMembership.Off, "the device to be removed");
        Assert.Contains("removed this device", phone.Member.Error, StringComparison.Ordinal);
        Assert.Empty(swarm.Coordinator.Devices);
        Assert.Null(swarm.DeviceStore.Find(phone.Identity.Fingerprint));
        await SwarmLoopback.UntilAsync(() => phone.Member.Coordinators.Devices.Count == 0, "the device to forget the swarm");
        Assert.False(await phone.Member.RejoinAsync(paired));
        Assert.Contains("does not know", phone.Member.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lost_device_reconnects_by_itself_when_the_tablet_is_back()
    {
        var port = SwarmLoopback.FreePort();
        await using var swarm = new SwarmLoopback(port);
        var phone = await swarm.JoinAsync("Pixel 9");

        await swarm.Coordinator.StopAsync();
        await SwarmLoopback.UntilAsync(() => phone.Member.Membership == SwarmMembership.Reconnecting, "the device to notice");
        swarm.Coordinator.Start();

        await SwarmLoopback.UntilAsync(() => phone.Member.Membership == SwarmMembership.Joined, "the device to reconnect");
        await SwarmLoopback.UntilAsync(() => phone.OnCoordinator(swarm.Coordinator) is { IsConnected: true }, "the device on the coordinator again");
        Assert.Equal(port, swarm.Coordinator.Port);
    }

    /// <summary>Handles nothing: the connection is refused before anything arrives.</summary>
    private sealed class NoMessages : TabStudio.Link.Messaging.ILinkMessageHandler
    {
        public Task<TabStudio.Link.Messaging.LinkReply> HandleRequestAsync(string method, TabStudio.Link.Messaging.LinkPayload payload, CancellationToken cancellationToken) =>
            throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, method);

        public void HandleNotification(string method, TabStudio.Link.Messaging.LinkPayload payload)
        {
        }
    }
}
