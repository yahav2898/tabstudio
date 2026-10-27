using System.Buffers.Binary;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Flashing;

namespace TabStudio.Toolchains.Tests.Embedded;

/// <summary>Actual addressed bytes control in-flash protection checks before upload planning.</summary>
public sealed class ConfigurationGuardTests
{
    [Theory]
    [InlineData(0x12345678u)]
    [InlineData(0x87654321u)]
    [InlineData(0x43218765u)]
    [InlineData(0x4E697370u)]
    [InlineData(0xABCDEF01u)]
    public void Lpc_crp_payload_is_refused_even_inside_allowed_application_flash(uint value)
    {
        using var folder = new TempDirectory();
        var bytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        var path = folder.Resolve("app.bin"); File.WriteAllBytes(path, bytes);
        var request = Request(path, "LPC1768", 0x2FC);

        var error = Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(request));

        Assert.Contains("CRP at 0x2FC", error.Message, StringComparison.Ordinal);
        Assert.Contains(value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.Contains("before erase", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void Explicit_unprotected_lpc_defaults_remain_uploadable(uint value)
    {
        using var folder = new TempDirectory();
        var bytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        var path = folder.Resolve("app.bin"); File.WriteAllBytes(path, bytes);
        Assert.Equal("openocd", Assert.Single(EmbeddedFlashPlanner.Plan(Request(path, "lpc1768", 0x2FC)).Steps).Program);
    }

    [Fact]
    public void Partial_crp_and_generic_part_with_sensitive_openocd_script_are_refused()
    {
        using var folder = new TempDirectory();
        var path = folder.Resolve("app.bin"); File.WriteAllBytes(path, [0xFF, 0xFF]);
        Assert.Contains("partial CRP", Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(Request(path, "LPC1768", 0x2FC))).Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => EmbeddedFlashPlanner.Plan(Request(path, "lpc17xx", 0x2FC)));
        Assert.Throws<NotSupportedException>(() => EmbeddedFlashPlanner.Plan(Request(path, "LPC1751", 0x2FC)));
        Assert.False(EmbeddedConfigurationGuard.HasPolicy("LPC1751"));
        Assert.False(EmbeddedConfigurationGuard.HasPolicy("MK64FUTURE"));
    }

    [Fact]
    public void K64_default_is_allowed_but_erased_security_and_each_changed_field_are_refused()
    {
        using var folder = new TempDirectory();
        var path = folder.Resolve("app.bin");
        var safe = Enumerable.Repeat((byte)0xFF, 16).ToArray(); safe[12] = 0xFE;
        File.WriteAllBytes(path, safe);
        var request = Request(path, "MK64FN1M0VLL12", 0x400) with { TargetConfig = "target/kx.cfg" };
        Assert.Single(EmbeddedFlashPlanner.Plan(request).Steps);
        for (var i = 0; i < safe.Length; i++)
        {
            var unsafeBytes = safe.ToArray(); unsafeBytes[i] ^= 1;
            File.WriteAllBytes(path, unsafeBytes);
            var error = Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(request));
            Assert.Contains($"FCF byte 0x{0x400 + i:X}", error.Message, StringComparison.Ordinal);
            Assert.Contains("before erase", error.Message, StringComparison.Ordinal);
        }
        File.WriteAllBytes(path, Enumerable.Repeat((byte)0xFF, 16).ToArray());
        Assert.Contains("FSEC=FF is secured", Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(request)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void K64_sparse_configuration_is_unknown_instead_of_guessed_erased_or_existing_values()
    {
        using var folder = new TempDirectory();
        var path = folder.Resolve("partial.bin"); File.WriteAllBytes(path, [0xFE]);
        var request = Request(path, "MK64FN1M0VLL12", 0x40C) with { TargetConfig = "target/kx.cfg" };
        Assert.Contains("complete FCF", Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(request)).Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => EmbeddedFlashPlanner.Plan(request with { Target = "kinetis" }));
        Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(request with { BinaryAddress = 0x10000 }));
    }

    [Fact]
    public void Hex_records_split_across_crp_combine_exact_little_endian_bytes()
    {
        using var folder = new TempDirectory();
        var path = folder.Write("app.hex", Record(0x2FC, 0, [0x78, 0x56]) + Record(0x2FE, 0, [0x34, 0x12]) + Record(0, 1, []));
        Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(Request(path, "LPC1768", null)));
        Assert.Equal(new byte?[] { null, 0x78, 0x56, 0x34, 0x12, null }, FirmwareImageContent.ReadRange(File.ReadAllBytes(path), ".hex", 0x2FB, 6));
    }

    [Fact]
    public void Hex_extended_modes_are_applied_and_unrelated_malformed_records_still_fail()
    {
        var text = Record(0, 2, [0x12, 0x34]) + Record(0x10, 0, [1, 2]) + Record(0, 4, [0x08, 0x00]) + Record(0x400, 0, [3]) + Record(0, 1, []);
        Assert.Equal(new byte?[] { null, 1, 2, null }, FirmwareImageContent.ReadRange(Encoding.ASCII.GetBytes(text), ".hex", 0x1234F, 4));
        Assert.Equal(new byte?[] { 3 }, FirmwareImageContent.ReadRange(Encoding.ASCII.GetBytes(text), ".hex", 0x08000400, 1));
        Assert.Throws<InvalidDataException>(() => FirmwareImageContent.ReadRange(Encoding.ASCII.GetBytes(text + ":00000001FE\n"), ".hex", 0x12350, 2));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void Elf_width_and_byte_order_do_not_rebase_physical_configuration_or_bss(bool wide, bool little)
    {
        using var folder = new TempDirectory();
        var path = folder.Resolve("app.elf"); File.WriteAllBytes(path, Elf(wide, little, 0x2FC, [0x78, 0x56, 0x34, 0x12]));
        var request = Request(path, "LPC1768", null) with { Protocol = EmbeddedFlashProtocol.ProbeRs, ProbeSelector = "1234:5678:serial" };
        Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(request));
        Assert.Equal(new byte?[] { 0x78, 0x56, 0x34, 0x12, null }, FirmwareImageContent.ReadRange(File.ReadAllBytes(path), ".elf", 0x2FC, 5));
        Assert.All(FirmwareImageContent.ReadRange(File.ReadAllBytes(path), ".elf", 0x20000000, 4), value => Assert.Null(value));
    }

    private static EmbeddedFlashRequest Request(string path, string target, ulong? address) => new()
    {
        Protocol = EmbeddedFlashProtocol.OpenOcd, FirmwarePath = path, WorkingDirectory = Path.GetDirectoryName(path)!,
        Target = target, BinaryAddress = address, ApplicationFlash = [new(0, 0x100000)],
        InterfaceConfig = "interface/cmsis-dap.cfg", TargetConfig = "target/lpc17xx.cfg",
    };

    private static string Record(ushort address, byte type, byte[] data)
    {
        byte[] header = [(byte)data.Length, (byte)(address >> 8), (byte)address, type];
        var bytes = header.Concat(data).ToArray();
        return ":" + Convert.ToHexString(bytes) + ((byte)(-bytes.Sum(value => value) & 255)).ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + "\n";
    }

    private static byte[] Elf(bool wide, bool little, uint physicalAddress, byte[] payload)
    {
        var bytes = new byte[256 + payload.Length];
        bytes[0] = 0x7F; bytes[1] = (byte)'E'; bytes[2] = (byte)'L'; bytes[3] = (byte)'F'; bytes[4] = (byte)(wide ? 2 : 1); bytes[5] = (byte)(little ? 1 : 2); bytes[6] = 1;
        void W16(int at, ushort value) { if (little) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), value); else BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(at), value); }
        void W32(int at, uint value) { if (little) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value); else BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at), value); }
        void W64(int at, ulong value) { if (little) BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at), value); else BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(at), value); }
        var header = wide ? 64 : 52;
        if (wide) W64(32, (ulong)header); else W32(28, (uint)header);
        W16(wide ? 54 : 42, (ushort)(wide ? 56 : 32)); W16(wide ? 56 : 44, 1); W32(header, 1);
        if (wide) { W64(header + 8, 256); W64(header + 16, 0x20000000); W64(header + 24, physicalAddress); W64(header + 32, (ulong)payload.Length); W64(header + 40, 64); }
        else { W32(header + 4, 256); W32(header + 8, 0x20000000); W32(header + 12, physicalAddress); W32(header + 16, (uint)payload.Length); W32(header + 20, 64); }
        payload.CopyTo(bytes, 256);
        return bytes;
    }
}
