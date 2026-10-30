using System.Security.Cryptography;
using TabStudio.Remote;
using TabStudio.Testing;

namespace TabStudio.Remote.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void Import_honors_aliases_quotes_first_values_wildcards_and_exclusions()
    {
        var hosts = SshConfig.Parse("""
            Host dev other
              HostName example.com
              User deploy
              IdentityFile "~/.ssh/device key"
            Host * !other
              Port=2200
              User ignored
            Host other
              User also-ignored
            """, "/home/tablet", "tablet");
        Assert.Equal(2, hosts.Count);
        Assert.Equal(new SshHost("dev", "example.com", "deploy", 2200, "/home/tablet/.ssh/device key"), hosts[0]);
        Assert.Equal(22, hosts[1].Port);
        Assert.Equal("deploy", hosts[1].User);
    }

    [Fact]
    public void Import_never_executes_proxy_commands_or_misrepresents_a_jump_host()
    {
        var hosts = SshConfig.Parse("Host internal\n ProxyJump jump\nHost proxy\n ProxyCommand touch /tmp/must-not-run\nHost direct\n HostName localhost\nMatch all\n User ignored", "/home/user", "user");
        Assert.Single(hosts);
        Assert.Equal("direct", hosts[0].Name);
        Assert.Equal("user", hosts[0].User);
    }

    [Fact]
    public void Host_key_pins_cannot_be_silently_replaced()
    {
        using var temp = new TempDirectory();
        var store = new SshHostStore(temp.Path);
        var host = new SshHost("test", "example.com", "user");
        store.Trust(host, "SHA256:first");
        Assert.Equal("SHA256:first", new SshHostStore(temp.Path).Fingerprint(host));
        Assert.Throws<IOException>(() => store.Trust(host, "SHA256:second"));
        Assert.Null(store.Fingerprint(host with { Port = 2200 }));

        // A reinstalled server: forgetting its key on purpose lets the user verify and trust the new one.
        store.Forget(host);
        Assert.Null(new SshHostStore(temp.Path).Fingerprint(host));
        store.Trust(host, "SHA256:second");
        Assert.Equal("SHA256:second", store.Fingerprint(host));
    }

    [Fact]
    public void Platform_signer_produces_RSA_SHA2_authentication_without_exporting_a_private_key()
    {
        using var rsa = RSA.Create(3072);
        var parameters = rsa.ExportParameters(false);
        var called = false;
        var source = new SigningKeySource(parameters.Exponent!, parameters.Modulus!, (data, hash) => { called = true; return rsa.SignData(data, hash, RSASignaturePadding.Pkcs1); });
        var algorithm = source.HostKeyAlgorithms.First();
        var data = "SSH authentication"u8.ToArray();
        var signature = algorithm.Sign(data);
        Assert.True(called);
        Assert.Equal("rsa-sha2-512", algorithm.Name);
        var nameLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(signature);
        var rawSignature = signature[(8 + nameLength)..];
        Assert.True(rsa.VerifyData(data, rawSignature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1));
        Assert.StartsWith("ssh-rsa ", source.PublicKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_keys_and_profiles_survive_reopening_and_no_credentials_enter_profiles()
    {
        using var temp = new TempDirectory();
        var store = new FileSshKeyStore(Path.Combine(temp.Path, "keys"));
        using var first = store.GetOrCreate("abc123");
        using var second = store.GetOrCreate("abc123");
        Assert.Equal(first.PublicKey, second.PublicKey);
        if (!OperatingSystem.IsWindows()) { Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(temp.Path, "keys", "abc123.pem"))); }
        var run = new RemoteRunProfile("npm run build", "npm start", 3000);
        run.Save(temp.Path);
        Assert.Equal(run, RemoteRunProfile.Load(temp.Path));
        var publish = new ServerPublishProfile("production", "/srv/my-app", "my-app.service", "linux-arm64");
        publish.Save(temp.Path);
        Assert.Equal(publish, ServerPublishProfile.Load(temp.Path));
        Assert.DoesNotContain("PRIVATE KEY", File.ReadAllText(Path.Combine(temp.Path, ".tabstudio", "server-publish.json")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/", "app.service")]
    [InlineData("relative", "app.service")]
    [InlineData("/srv/app", "app;touch /tmp/x")]
    [InlineData("/srv/app", "-malicious")]
    public void Publish_rejects_unsafe_destinations_or_unit_names(string directory, string service) => Assert.ThrowsAny<ArgumentException>(() => new ServerPublishProfile("host", directory, service).Validate());

    [Fact]
    public void Shell_arguments_are_quoted_as_data() => Assert.Equal("'a'\\'';$(echo bad)'", SshRuntime.Quote("a';$(echo bad)"));
}
