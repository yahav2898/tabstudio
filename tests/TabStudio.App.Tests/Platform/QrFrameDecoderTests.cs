using TabStudio.App.Platform;
using TabStudio.Link.Qr;
using TabStudio.Link.Security;

namespace TabStudio.App.Tests.Platform;

/// <summary>
/// The decoder of the Android camera scanner, on frames drawn as a camera sees a host's screen: off center, turned,
/// unevenly lit, noisy, with padded rows as camera buffers have.
/// </summary>
public sealed class QrFrameDecoderTests
{
    private const int Width = 640;
    private const int Height = 480;
    private const int RowStride = 704;

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Reads_a_hosts_code_from_a_camera_frame(int degrees)
    {
        // A fixed key and pairing code, so every run draws the same code: the encoder picks its mask by the data.
        Assert.True(LinkPairingCode.TryParse("7K3M-Q9TX", out var code));
        var fingerprint = LinkFingerprint.FromPublicKey("TabStudio Link test host key"u8);
        var invitation = new LinkInvitation("Pixel 9", ["192.168.1.23", "10.0.0.7"], 50517, fingerprint, code).ToUri();
        var frame = Frame(QrCode.Encode(invitation), degrees, seed: degrees);

        var text = QrFrameDecoder.Decode(frame, RowStride, Width, Height);

        Assert.Equal(invitation, text);
    }

    [Fact]
    public void Reads_the_codes_of_invitations_of_any_data()
    {
        // Random keys and pairing codes, from a fixed seed so that a failure names the same invitation every run.
        var random = new Random(18004);
        int[] rotations = [0, 90, 180, 270];
        for (var i = 0; i < 100; i++)
        {
            var invitation = RandomInvitation(random).ToUri();
            var code = QrCode.Encode(invitation);
            foreach (var degrees in rotations)
            {
                Assert.Equal(invitation, QrFrameDecoder.Decode(Frame(code, degrees, seed: i + degrees), RowStride, Width, Height));
            }
        }
    }

    [Fact]
    public void Reads_a_code_with_a_finder_like_run_in_its_data()
    {
        // ZXing's QR reader alone reads nothing from this frame: its search stops at a finder-like run in the data
        // before it reaches the real bottom-left finder.
        Assert.True(LinkPairingCode.TryParse("NRC2Y4QM", out var code));
        var fingerprint = LinkFingerprint.Parse("X820S2Bb31ilLRlK-Tip3rX_7wgxm47LIeiY99M765s");
        var invitation = new LinkInvitation("Pixel 9", ["192.168.1.23", "10.0.0.7"], 50517, fingerprint, code).ToUri();
        var frame = Frame(QrCode.Encode(invitation), degrees: 0, seed: 0);

        Assert.Equal(invitation, QrFrameDecoder.Decode(frame, RowStride, Width, Height));
    }

    [Fact]
    public void A_frame_without_a_code_reads_as_nothing()
    {
        var frame = Frame(code: null, degrees: 0, seed: 1);

        Assert.Null(QrFrameDecoder.Decode(frame, RowStride, Width, Height));
    }

    /// <summary>An invitation with a random key and pairing code.</summary>
    private static LinkInvitation RandomInvitation(Random random)
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var key = new byte[91];
        random.NextBytes(key);
        var text = string.Concat(Enumerable.Range(0, LinkPairingCode.Length).Select(_ => alphabet[random.Next(alphabet.Length)]));
        Assert.True(LinkPairingCode.TryParse(text, out var code));
        return new LinkInvitation("Pixel 9", ["192.168.1.23", "10.0.0.7"], 50517, LinkFingerprint.FromPublicKey(key), code);
    }

    /// <summary>A Y plane: light falling off from left to right, noise, and the code turned and placed off center.</summary>
    private static byte[] Frame(QrCode? code, int degrees, int seed)
    {
        var random = new Random(seed);
        var frame = new byte[RowStride * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < RowStride; x++)
            {
                frame[(y * RowStride) + x] = (byte)Math.Clamp(120 - (x / 12) + random.Next(-12, 13), 0, 255);
            }
        }

        if (code is null)
        {
            return frame;
        }

        const int scale = 5;
        const int quiet = 3;
        var modules = code.Size + (2 * quiet);
        var left = 230;
        var top = (Height - (modules * scale)) / 2;
        for (var my = 0; my < modules; my++)
        {
            for (var mx = 0; mx < modules; mx++)
            {
                var (x, y) = (mx - quiet, my - quiet);
                var dark = x >= 0 && y >= 0 && x < code.Size && y < code.Size && Turned(code, x, y, degrees);
                for (var dy = 0; dy < scale; dy++)
                {
                    for (var dx = 0; dx < scale; dx++)
                    {
                        var px = left + (mx * scale) + dx;
                        var py = top + (my * scale) + dy;
                        var light = 215 - (px / 10);
                        frame[(py * RowStride) + px] = (byte)Math.Clamp((dark ? 35 : light) + random.Next(-15, 16), 0, 255);
                    }
                }
            }
        }

        return frame;
    }

    /// <summary>Whether the module at (x, y) of the code turned by the given degrees is dark.</summary>
    private static bool Turned(QrCode code, int x, int y, int degrees)
    {
        var last = code.Size - 1;
        return degrees switch
        {
            90 => code[y, last - x],
            180 => code[last - x, last - y],
            270 => code[last - y, x],
            _ => code[x, y],
        };
    }
}
