using System.Text;
using TabStudio.Link.Qr;
using TabStudio.Link.Security;
using ZXing;
using ZXing.Common;
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
        using var identity = LinkIdentity.Create();
        var invitation = new LinkInvitation("Yahav's Pixel 9 Pro", ["192.168.1.23", "10.0.0.5"], 47811, identity.Fingerprint, LinkPairingCode.Create());

        var code = QrCode.Encode(invitation.ToUri());

        Assert.True(code.Version <= 8, $"Version {code.Version} is denser than needed.");
        Assert.True(LinkInvitation.TryParse(Decode(code), out var scanned));
        Assert.Equal(invitation.Fingerprint, scanned.Fingerprint);
        Assert.Equal(invitation.Code, scanned.Code);
        Assert.Equal(invitation.DeviceName, scanned.DeviceName);
        Assert.Equal(invitation.Addresses, scanned.Addresses);
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

    /// <summary>Draws a code with a quiet zone, four pixels a module, and reads it back.</summary>
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
        var result = new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(source)), hints);
        Assert.NotNull(result);
        return result.Text;
    }
}
