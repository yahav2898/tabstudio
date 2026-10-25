using System.Text;
using TabStudio.Link.Qr;
using TabStudio.Link.Security;
using ZXing;
using ZXing.Common;
using ZXing.Multi.QrCode;
using ZXing.QrCode;

namespace TabStudio.Link.Tests;

/// <summary>TabStudio's QR encoder, checked by decoding its codes with ZXing, the decoder the Android camera uses.</summary>
public sealed class QrCodeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(300)]
    [InlineData(660)]
    public void Codes_decode_to_their_text(int length)
    {
        var text = Text(length);

        var code = QrCode.Encode(text);

        Assert.Equal(text, Decode(code));
    }

    [Fact]
    public void The_smallest_version_that_fits_is_used()
    {
        Assert.Equal(1, QrCode.Encode(Text(14)).Version);
        Assert.Equal(2, QrCode.Encode(Text(15)).Version);
        Assert.Equal(21, QrCode.Encode(Text(14)).Size);
    }

    [Fact]
    public void An_invitation_fits_a_code_a_tablet_camera_reads_easily()
    {
        // A fixed key and pairing code, so every run checks the same code: the encoder picks its mask by the data.
        // Invitations_of_any_data_decode checks many others.
        Assert.True(LinkPairingCode.TryParse("7K3M-Q9TX", out var pairingCode));
        var invitation = new LinkInvitation("Yahav's Pixel 9 Pro", ["192.168.1.23", "10.0.0.5"], 47811, LinkFingerprint.FromPublicKey("TabStudio Link test host key"u8), pairingCode);

        var code = QrCode.Encode(invitation.ToUri());

        Assert.True(code.Version <= 8, $"Version {code.Version} is denser than needed.");
        Assert.True(LinkInvitation.TryParse(Decode(code), out var scanned));
        Assert.Equal(invitation.Fingerprint, scanned.Fingerprint);
        Assert.Equal(invitation.Code, scanned.Code);
        Assert.Equal(invitation.DeviceName, scanned.DeviceName);
        Assert.Equal(invitation.Addresses, scanned.Addresses);
    }

    [Fact]
    public void Invitations_of_any_data_decode()
    {
        // Random keys and pairing codes, from a fixed seed so that a failure names the same invitation every run.
        var random = new Random(18004);
        for (var i = 0; i < 1000; i++)
        {
            var invitation = RandomInvitation(random).ToUri();

            Assert.Equal(invitation, Decode(QrCode.Encode(invitation)));
        }
    }

    [Fact]
    public void A_code_with_a_finder_like_run_in_its_data_decodes()
    {
        // ZXing's QR reader alone reads nothing from this invitation's code: its search stops at a finder-like run in
        // the data before it reaches the real bottom-left finder. The camera decoder then looks at every candidate.
        Assert.True(LinkPairingCode.TryParse("8B9X636V", out var pairingCode));
        var invitation = new LinkInvitation(
            "Yahav's Pixel 9 Pro", ["192.168.1.23", "10.0.0.5"], 47811, LinkFingerprint.Parse("_RSz6IyzsqImujU0x5ta8tO8LgN63sRt9HqNyi0e-98"), pairingCode).ToUri();

        Assert.Equal(invitation, Decode(QrCode.Encode(invitation)));
    }

    [Fact]
    public void Every_mask_can_be_chosen_and_decodes()
    {
        var masks = new HashSet<int>();
        for (var i = 0; i < 400 && masks.Count < 8; i++)
        {
            var text = $"tabstudio-link://pair?n={i}&c={Text(20 + (i % 30))}";
            var code = QrCode.Encode(text);
            if (masks.Add(code.Mask))
            {
                Assert.Equal(text, Decode(code));
            }
        }

        Assert.True(masks.Count >= 4, $"Only masks {string.Join(", ", masks)} were chosen.");
    }

    [Fact]
    public void Text_that_does_not_fit_is_refused()
    {
        Assert.Throws<ArgumentException>(() => QrCode.Encode(Text(700)));
    }

    /// <summary>Text of a given length that exercises every byte value class of a URI.</summary>
    private static string Text(int length)
    {
        const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~:/?#[]@!$&'()*+,;=%";
        var text = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            text.Append(characters[(i * 7 + (i / 3)) % characters.Length]);
        }

        return text.ToString();
    }

    /// <summary>An invitation with a random key and pairing code.</summary>
    private static LinkInvitation RandomInvitation(Random random)
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var key = new byte[91];
        random.NextBytes(key);
        var text = string.Concat(Enumerable.Range(0, LinkPairingCode.Length).Select(_ => alphabet[random.Next(alphabet.Length)]));
        Assert.True(LinkPairingCode.TryParse(text, out var pairingCode));
        return new LinkInvitation("Yahav's Pixel 9 Pro", ["192.168.1.23", "10.0.0.5"], 47811, LinkFingerprint.FromPublicKey(key), pairingCode);
    }

    /// <summary>
    /// Draws a code with a quiet zone, four pixels a module, and reads it back as the Android camera decoder
    /// (TabStudio.App's QrFrameDecoder) does: with ZXing's QR reader, then its multiple-code reader.
    /// </summary>
    private static string Decode(QrCode code)
    {
        const int scale = 4;
        const int quiet = 4;
        var size = (code.Size + (2 * quiet)) * scale;
        var pixels = new byte[size * size];
        Array.Fill(pixels, (byte)255);
        for (var y = 0; y < code.Size; y++)
        {
            for (var x = 0; x < code.Size; x++)
            {
                if (!code[x, y])
                {
                    continue;
                }

                for (var dy = 0; dy < scale; dy++)
                {
                    for (var dx = 0; dx < scale; dx++)
                    {
                        pixels[(((y + quiet) * scale) + dy) * size + ((x + quiet) * scale) + dx] = 0;
                    }
                }
            }
        }

        var source = new RGBLuminanceSource(pixels, size, size, RGBLuminanceSource.BitmapFormat.Gray8);
        var hints = new Dictionary<DecodeHintType, object> { [DecodeHintType.CHARACTER_SET] = "UTF-8" };
        var bitmap = new BinaryBitmap(new HybridBinarizer(source));
        var result = new QRCodeReader().decode(bitmap, hints) ?? new QRCodeMultiReader().decodeMultiple(bitmap, hints)?.FirstOrDefault();
        Assert.NotNull(result);
        return result.Text;
    }
}
