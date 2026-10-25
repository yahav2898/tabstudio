using System.Security.Cryptography;
using TabStudio.Testing;
using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.Toolchains.Tests.Alpine;

/// <summary>Verifying Alpine's signed indexes and the packages they list.</summary>
public sealed class ApkSignatureTests : IDisposable
{
    private readonly FakeAlpineRepository _mirror = new();
    private readonly TempDirectory _temp = new();
    private readonly Dictionary<string, RSA> _keys;

    public ApkSignatureTests()
    {
        _temp.Write($"keys/{FakeAlpineRepository.KeyName}", _mirror.PublicKeyPem);
        _temp.Write("keys/broken.rsa.pub", "not a key");
        _keys = ApkSignature.LoadKeys(_temp.Resolve("keys"));
    }

    [Fact]
    public void Loads_the_trusted_keys_and_skips_broken_ones()
    {
        Assert.Equal([FakeAlpineRepository.KeyName], _keys.Keys);
    }

    [Fact]
    public void Reads_a_signed_index()
    {
        _mirror.Add("main", "hello", "1.0-r0", depends: ["so:libc.musl-x86_64.so.1"]);
        _mirror.Add("main", "world", "2.1-r3");

        var packages = ApkSignature.ReadIndex(IndexBytes("main"), _keys, "main");

        Assert.Equal(["hello", "world"], packages.Select(p => p.Name));
        Assert.All(packages, p => Assert.Equal("main", p.Repository));
        Assert.Equal("so:libc.musl-x86_64.so.1", Assert.Single(packages[0].Depends).Name);
    }

    [Fact]
    public void Refuses_an_index_whose_contents_changed()
    {
        _mirror.Add("main", "hello", "1.0-r0");
        var index = IndexBytes("main");
        index[^20] ^= 0xff;

        Assert.Throws<InvalidDataException>(() => ApkSignature.ReadIndex(index, _keys));
    }

    [Fact]
    public void Refuses_an_index_signed_with_an_unknown_key()
    {
        _mirror.Add("main", "hello", "1.0-r0");

        var error = Assert.Throws<InvalidDataException>(() => ApkSignature.ReadIndex(IndexBytes("main"), new Dictionary<string, RSA>()));

        Assert.Contains(FakeAlpineRepository.KeyName, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_file_that_is_not_an_index()
    {
        Assert.Throws<InvalidDataException>(() => ApkSignature.ReadIndex("<html>not found</html>"u8.ToArray(), _keys));
    }

    [Fact]
    public void Verifies_a_package_against_its_index_entry()
    {
        var package = _mirror.Add("main", "hello", "1.0-r0", new Dictionary<string, string> { ["usr/bin/hello"] = "#!/bin/sh\necho hello\n" });
        var entry = ApkSignature.ReadIndex(IndexBytes("main"), _keys).Single();
        var file = _temp.Write("hello-1.0-r0.apk");
        File.WriteAllBytes(file, package.Bytes);

        Assert.Null(ApkSignature.VerifyPackage(file, entry));
    }

    [Fact]
    public void Finds_a_package_whose_files_were_changed()
    {
        var package = _mirror.Add("main", "hello", "1.0-r0");
        var entry = ApkSignature.ReadIndex(IndexBytes("main"), _keys).Single();
        var file = _temp.Write("hello.apk");
        var bytes = package.Bytes.ToArray();
        bytes[^10] ^= 0x55;
        File.WriteAllBytes(file, bytes);

        Assert.Contains("do not match", ApkSignature.VerifyPackage(file, entry), StringComparison.Ordinal);
    }

    [Fact]
    public void Finds_a_package_that_is_not_the_one_the_index_lists()
    {
        _mirror.Add("main", "hello", "1.0-r0");
        var other = _mirror.Add("community", "other", "1.0-r0");
        var entry = ApkSignature.ReadIndex(IndexBytes("main"), _keys).Single();
        var file = _temp.Write("hello.apk");
        File.WriteAllBytes(file, other.Bytes);

        Assert.Contains("signed package index", ApkSignature.VerifyPackage(file, entry), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        foreach (var key in _keys.Values)
        {
            key.Dispose();
        }

        _mirror.Dispose();
        _temp.Dispose();
    }

    private byte[] IndexBytes(string repository)
    {
        using var client = _mirror.Server.CreateClient();
        return client.GetByteArrayAsync(_mirror.IndexAddress(repository)).GetAwaiter().GetResult();
    }
}
