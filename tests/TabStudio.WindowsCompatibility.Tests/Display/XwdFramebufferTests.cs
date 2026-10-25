using TabStudio.Testing;
using TabStudio.WindowsCompatibility.Display;
using TabStudio.WindowsCompatibility.Display.X11;
using TabStudio.WindowsCompatibility.Tests.Infrastructure;

namespace TabStudio.WindowsCompatibility.Tests.Display;

/// <summary>Reading Xvfb's framebuffer file, which it maps into its own memory.</summary>
public sealed class XwdFramebufferTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Reads_the_size_and_copies_a_region_into_a_buffer_of_the_screens_size()
    {
        using var synthetic = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 64, 48);
        synthetic.Fill(4, 6, 8, 2, blue: 200, green: 100, red: 50);
        using var framebuffer = XwdFramebuffer.Open(synthetic.Path);

        var pixels = new byte[64 * 48 * 4];
        framebuffer.Copy(new ScreenRect(0, 0, 64, 48), pixels, 64 * 4);

        Assert.Equal((64, 48, 256), (framebuffer.Width, framebuffer.Height, framebuffer.Stride));
        var inside = ((7 * 64) + 11) * 4;
        Assert.Equal(new byte[] { 200, 100, 50 }, pixels[inside..(inside + 3)]);
        var outside = ((8 * 64) + 11) * 4;
        Assert.Equal(new byte[] { 0, 0, 0 }, pixels[outside..(outside + 3)]);
    }

    [Fact]
    public void Sees_what_is_drawn_after_it_was_opened()
    {
        using var synthetic = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 32, 32);
        using var framebuffer = XwdFramebuffer.Open(synthetic.Path);
        var before = framebuffer.Checksum(new ScreenRect(0, 0, 32, 32));

        synthetic.Fill(0, 0, 4, 4, 9, 9, 9);

        Assert.NotEqual(before, framebuffer.Checksum(new ScreenRect(0, 0, 32, 32)));
    }

    [Fact]
    public void Copies_an_area_into_a_buffer_of_its_own_size_and_clips_to_the_screen()
    {
        using var synthetic = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 16, 16);
        synthetic.Fill(14, 14, 2, 2, 1, 2, 3);
        using var framebuffer = XwdFramebuffer.Open(synthetic.Path);

        var area = new byte[4 * 4 * 4];
        framebuffer.Copy(new ScreenRect(14, 14, 4, 4), area, 4 * 4, destinationIsArea: true);

        Assert.Equal(new byte[] { 1, 2, 3 }, area[0..3]);
        Assert.Equal(new byte[] { 0, 0, 0 }, area[(2 * 4)..((2 * 4) + 3)]);
    }

    [Fact]
    public void Refuses_a_framebuffer_that_is_not_32_bit_bgrx()
    {
        var header = new byte[100];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 7);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), 10);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20), 10);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(44), 16);

        Assert.Throws<InvalidDataException>(() => XwdFramebuffer.ReadHeader(header));
    }
}
