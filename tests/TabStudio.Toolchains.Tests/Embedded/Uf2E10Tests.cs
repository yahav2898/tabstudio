using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Toolchains.Embedded.Flashing;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class Uf2E10Tests
{
    [Fact]
    public void Actual_pinned_picotool_Rp2350_output_is_refused_with_actionable_revision_dependent_flash_reason()
    {
        var image = Image();
        Assert.Equal(1024, image.Length);
        Assert.Equal("e1cd7edfc28e0d166a721ae1743e8f8d4b2173882323cb792b2169a0d894b76f", Convert.ToHexStringLower(SHA256.HashData(image)));
        Assert.Equal(0xA000u, U32(image, 8));
        Assert.Equal(0x10FFFF00u, U32(image, 12));
        Assert.Equal(0x9957E304u, U32(image, 288));
        // These are independent logical files: the workaround advertises two
        // blocks but has one; the following application has its own 0/1 count.
        Assert.Equal(2u, U32(image, 24));
        Assert.Equal(0u, U32(image, 512 + 20));
        Assert.Equal(1u, U32(image, 512 + 24));
        var error = Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.Uf2(image));
        Assert.Contains("RP2350-E10", error.Message, StringComparison.Ordinal);
        Assert.Contains("erase the last flash block on A2", error.Message, StringComparison.Ordinal);
        Assert.Contains("ELF with picotool", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_workaround_without_ignore_tag_is_also_refused_and_application_alone_remains_parseable()
    {
        var image = Image();
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(8), 0x2000);
        image.AsSpan(288, 4).Clear();
        var error = Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.Uf2(image));
        Assert.Contains("RP2350-E10", error.Message, StringComparison.Ordinal);
        Assert.Equal(new FlashAddressRange(0x10000000, 256), Assert.Single(FirmwareImageInspector.Uf2(image.AsSpan(512))));
    }

    internal static byte[] Image()
    {
        using var stream = typeof(Uf2E10Tests).Assembly.GetManifestResourceStream("Embedded.Picotool231Rp2350E10.json")!;
        using var fixture = JsonDocument.Parse(stream);
        return Convert.FromBase64String(fixture.RootElement.GetProperty("uf2Base64").GetString()!);
    }

    private static uint U32(byte[] image, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset));
}
