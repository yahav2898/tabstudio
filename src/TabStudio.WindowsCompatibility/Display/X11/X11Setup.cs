using System.Buffers.Binary;
using System.Text;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>One screen of an X server, from its connection setup.</summary>
/// <param name="Root">The root window.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Depth">The root window's depth, such as 24.</param>
public sealed record X11Screen(uint Root, int Width, int Height, int Depth);

/// <summary>What an X server says about itself when a client connects.</summary>
/// <param name="Vendor">The server's vendor, such as <c>The X.Org Foundation</c>.</param>
/// <param name="ReleaseNumber">The vendor's release number.</param>
/// <param name="ResourceIdBase">The first id this client may give its own resources.</param>
/// <param name="ResourceIdMask">The bits of a resource id the client chooses.</param>
/// <param name="MinKeycode">The lowest key code.</param>
/// <param name="MaxKeycode">The highest key code.</param>
/// <param name="Screens">The screens; Xvfb has one.</param>
public sealed record X11Setup(
    string Vendor,
    uint ReleaseNumber,
    uint ResourceIdBase,
    uint ResourceIdMask,
    byte MinKeycode,
    byte MaxKeycode,
    IReadOnlyList<X11Screen> Screens)
{
    /// <summary>The first screen.</summary>
    public X11Screen Screen => Screens[0];

    /// <summary>Reads the data that follows a successful setup reply's first 8 bytes.</summary>
    /// <param name="data">The additional data.</param>
    internal static X11Setup Parse(ReadOnlySpan<byte> data)
    {
        var release = BinaryPrimitives.ReadUInt32LittleEndian(data);
        var idBase = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        var idMask = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        var vendorLength = BinaryPrimitives.ReadUInt16LittleEndian(data[16..]);
        var screenCount = data[20];
        var formatCount = data[21];
        var minKeycode = data[26];
        var maxKeycode = data[27];
        var vendor = Encoding.ASCII.GetString(data.Slice(32, vendorLength));
        var offset = 32 + X11Wire.Pad(vendorLength) + (8 * formatCount);

        var screens = new List<X11Screen>(screenCount);
        for (var i = 0; i < screenCount; i++)
        {
            var screen = data[offset..];
            var root = BinaryPrimitives.ReadUInt32LittleEndian(screen);
            var width = BinaryPrimitives.ReadUInt16LittleEndian(screen[20..]);
            var height = BinaryPrimitives.ReadUInt16LittleEndian(screen[22..]);
            var depth = screen[38];
            var depthCount = screen[39];
            screens.Add(new X11Screen(root, width, height, depth));

            // Skip the screen's allowed depths and their visuals.
            offset += 40;
            for (var d = 0; d < depthCount; d++)
            {
                var visuals = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 2)..]);
                offset += 8 + (24 * visuals);
            }
        }

        return new X11Setup(vendor, release, idBase, idMask, minKeycode, maxKeycode, screens);
    }
}
