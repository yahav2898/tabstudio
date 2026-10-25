using System.Text;
using TabStudio.Extensions.Security;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

public sealed class PackageVerifierTests
{
    // RFC 8032, section 7.1: tests 1 to 3.
    [Theory]
    [InlineData(
        "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60",
        "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a",
        "",
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b")]
    [InlineData(
        "4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb",
        "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c",
        "72",
        "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00")]
    [InlineData(
        "c5aa8df43f9f837bedb7442f31dcb7b166d38535076f094b85ce3a2e0b4458f7",
        "fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025",
        "af82",
        "6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a")]
    public void The_rfc_8032_test_vectors_verify(string secretKey, string publicKey, string message, string signature)
    {
        var key = Convert.FromHexString(publicKey);
        var data = Convert.FromHexString(message);
        var sig = Convert.FromHexString(signature);

        Assert.True(Ed25519.Verify(key, data, sig));
        Assert.Equal(sig, new Ed25519TestSigner(Convert.FromHexString(secretKey)).Sign(data));
        Assert.Equal(key, new Ed25519TestSigner(Convert.FromHexString(secretKey)).PublicKey);

        var tampered = (byte[])sig.Clone();
        tampered[10] ^= 0x01;
        Assert.False(Ed25519.Verify(key, data, tampered));
        Assert.False(Ed25519.Verify(key, [.. data, 0x00], sig));
    }

    [Fact]
    public void Malformed_keys_and_signatures_do_not_verify()
    {
        var signer = Ed25519TestSigner.FromSeed("malformed");
        var message = Encoding.UTF8.GetBytes("message");
        var signature = signer.Sign(message);

        Assert.False(Ed25519.Verify(signer.PublicKey[..31], message, signature));
        Assert.False(Ed25519.Verify(signer.PublicKey, message, signature[..63]));

        // A signature whose S is not reduced below the group order is rejected.
        var unreduced = (byte[])signature.Clone();
        unreduced[63] |= 0xF0;
        Assert.False(Ed25519.Verify(signer.PublicKey, message, unreduced));
    }

    [Fact]
    public void A_package_is_verified_from_its_signature_archive_and_the_registry_key()
    {
        var signer = Ed25519TestSigner.FromSeed("registry");
        var package = LumenExtension.Build();
        using var archive = new MemoryStream(signer.SignatureArchive(package));

        var signature = PackageVerifier.ReadSignature(archive);
        var key = PackageVerifier.ReadPublicKey(signer.PublicKeyPem);

        using (var stream = new MemoryStream(package))
        {
            Assert.True(PackageVerifier.VerifySignature(stream, signature, key));
        }

        var changed = LumenExtension.Build("9.9.9");
        using var other = new MemoryStream(changed);
        Assert.False(PackageVerifier.VerifySignature(other, signature, key));
    }

    [Fact]
    public void Keys_that_are_not_ed25519_are_refused()
    {
        Assert.Throws<FormatException>(() => PackageVerifier.ReadPublicKey("not a key"));
        Assert.Throws<FormatException>(() => PackageVerifier.ReadPublicKey("-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----"));
        Assert.Throws<FormatException>(() => PackageVerifier.ReadSignature(new MemoryStream(Encoding.UTF8.GetBytes("not a zip"))));
    }

    [Theory]
    [InlineData("ABCDEF0123", "abcdef0123", true)]
    [InlineData("abcdef0123  vscode.perl-1.95.3.vsix\n", "abcdef0123", true)]
    [InlineData("abcdef0124", "abcdef0123", false)]
    [InlineData("", "abcdef0123", false)]
    public void Published_hashes_are_compared_without_case_and_file_names(string published, string computed, bool matches)
    {
        Assert.Equal(matches, PackageVerifier.MatchesPublishedSha256(computed, published));
    }

    [Fact]
    public void The_hash_of_a_package_is_lowercase_hexadecimal()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", PackageVerifier.ComputeSha256(stream));
    }
}
