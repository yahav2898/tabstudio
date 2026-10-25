using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.Link.Tests;

/// <summary>What people type and cameras scan: pairing codes, host addresses, QR invitations and fingerprints.</summary>
public sealed class PairingFormatsTests
{
    [Theory]
    [InlineData("7K3Q-M9XA", "7K3QM9XA")]
    [InlineData("7k3q m9xa", "7K3QM9XA")]
    [InlineData(" 7K3QM9XA ", "7K3QM9XA")]
    [InlineData("OIL0-1234", "0110-1234")]
    public void Codes_are_read_the_way_people_type_them(string typed, string expected)
    {
        Assert.True(LinkPairingCode.TryParse(typed, out var code));
        Assert.Equal(expected.Replace("-", string.Empty, StringComparison.Ordinal), code.Value);
        Assert.Equal($"{code.Value[..4]}-{code.Value[4..]}", code.DisplayText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("7K3Q-M9X")]
    [InlineData("7K3Q-M9XAB")]
    [InlineData("7K3Q-M9XU")]
    [InlineData("hello!!!")]
    public void Anything_else_is_not_a_code(string typed)
    {
        Assert.False(LinkPairingCode.TryParse(typed, out _));
    }

    [Fact]
    public void New_codes_are_random_and_valid()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => LinkPairingCode.Create()).ToList();

        Assert.All(codes, code => Assert.True(LinkPairingCode.TryParse(code.DisplayText, out var parsed) && parsed == code));
        Assert.Equal(50, codes.Select(c => c.Value).Distinct().Count());
    }

    [Theory]
    [InlineData("192.168.1.23", "192.168.1.23", LinkProtocol.DefaultPort)]
    [InlineData("192.168.1.23:52817", "192.168.1.23", 52817)]
    [InlineData("pixel-9.local", "pixel-9.local", LinkProtocol.DefaultPort)]
    [InlineData("[fe80::1]:47900", "fe80::1", 47900)]
    [InlineData("fe80::1", "fe80::1", LinkProtocol.DefaultPort)]
    public void Addresses_are_read_with_or_without_a_port(string typed, string host, int port)
    {
        Assert.True(LinkEndpoint.TryParse(typed, out var endpoint));
        Assert.Equal(host, endpoint.Host);
        Assert.Equal(port, endpoint.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.23:")]
    [InlineData("192.168.1.23:99999")]
    [InlineData("192.168 1.23")]
    [InlineData("[fe80::1")]
    public void Anything_else_is_not_an_address(string typed)
    {
        Assert.False(LinkEndpoint.TryParse(typed, out _));
    }

    [Fact]
    public void An_address_reads_back_the_way_it_is_shown()
    {
        Assert.Equal("192.168.1.23", new LinkEndpoint("192.168.1.23", LinkProtocol.DefaultPort).ToString());
        Assert.Equal("192.168.1.23:52817", new LinkEndpoint("192.168.1.23", 52817).ToString());
        Assert.Equal("[fe80::1]:52817", new LinkEndpoint("fe80::1", 52817).ToString());
    }

    [Fact]
    public void An_invitation_survives_its_URI()
    {
        using var identity = LinkIdentity.Create();
        var invitation = new LinkInvitation("Yahav's phone & co", ["192.168.1.23", "10.0.0.7"], 52817, identity.Fingerprint, LinkPairingCode.Create());

        Assert.True(LinkInvitation.TryParse(invitation.ToUri(), out var parsed));

        Assert.StartsWith("tabstudio-link://pair?", invitation.ToUri(), StringComparison.Ordinal);
        Assert.Equal(invitation.DeviceName, parsed.DeviceName);
        Assert.Equal(invitation.Addresses, parsed.Addresses);
        Assert.Equal(52817, parsed.Port);
        Assert.Equal(identity.Fingerprint, parsed.Fingerprint);
        Assert.Equal(invitation.Code, parsed.Code);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("tabstudio-link://pair?v=1&n=x&a=1.2.3.4&p=47811")]
    [InlineData("tabstudio-link://pair?v=1&n=x&a=1.2.3.4&p=0&f=AAAA&c=7K3QM9XA")]
    [InlineData("tabstudio-link://pair?v=1&n=x&a=&p=47811&f=AAAA&c=7K3QM9XA")]
    public void Other_QR_codes_are_not_invitations(string text)
    {
        Assert.False(LinkInvitation.TryParse(text, out _));
    }

    [Fact]
    public void Fingerprints_identify_a_key_and_read_back()
    {
        using var first = LinkIdentity.Create();
        using var second = LinkIdentity.Create();

        Assert.True(LinkFingerprint.TryParse(first.Fingerprint.Value, out var parsed));
        Assert.Equal(first.Fingerprint, parsed);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
        Assert.Equal(43, first.Fingerprint.Value.Length);
        Assert.Matches("^[0-9A-F]{4} [0-9A-F]{4} [0-9A-F]{4} [0-9A-F]{4}$", first.Fingerprint.ShortText);
        Assert.False(LinkFingerprint.TryParse("not a fingerprint", out _));
    }

    [Fact]
    public void An_identity_is_kept_across_restarts()
    {
        using var temp = new TabStudio.Testing.TempDirectory();
        var path = Path.Combine(temp.Path, "identity.p12");

        using var created = LinkIdentity.LoadOrCreate(path);
        using var loaded = LinkIdentity.LoadOrCreate(path);

        Assert.Equal(created.Fingerprint, loaded.Fingerprint);
        Assert.True(loaded.Certificate.HasPrivateKey);
    }

    [Fact]
    public void Paired_devices_are_remembered_in_their_file()
    {
        using var temp = new TabStudio.Testing.TempDirectory();
        var path = Path.Combine(temp.Path, "hosts.json");
        using var identity = LinkIdentity.Create();
        var now = DateTimeOffset.UtcNow;

        new LinkTrustStore(path).Remember(new LinkPairedDevice("Pixel 9", LinkDeviceKinds.Phone, identity.Fingerprint, now, now, ["192.168.1.23"], 47811));
        var reloaded = new LinkTrustStore(path);

        var device = Assert.Single(reloaded.Devices);
        Assert.Equal("Pixel 9", device.Name);
        Assert.Equal(identity.Fingerprint, device.Fingerprint);
        Assert.Equal(["192.168.1.23"], device.Addresses);
        Assert.True(reloaded.Forget(identity.Fingerprint));
        Assert.Empty(new LinkTrustStore(path).Devices);
    }
}
