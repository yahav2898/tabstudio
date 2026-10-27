using System.Text;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedRttTests
{
    private static readonly FirmwareMemoryRegion[] Ram = [new("SRAM", 0x20000000, 0x10000, true)];
    private static EmbeddedDebugProfile Profile => new()
    {
        Firmware = "app.elf", InterfaceConfig = "interface/stlink.cfg", TargetConfig = "target/stm32f4x.cfg",
        RttAddress = 0x20000000, RttSearchSize = 4096, RttChannel = 0,
    };

    [Fact]
    public void Generates_only_numeric_RTT_commands_in_explicit_RAM_on_separate_loopback_port()
    {
        var server = Profile.CreateServer("/project", "/scripts", 43123, 43124, Ram);
        var command = Assert.Single(server.Arguments, argument => argument.Contains("rtt setup", StringComparison.Ordinal));
        Assert.Contains("rtt setup 0x20000000 4096 \"SEGGER RTT\"; rtt start; rtt server start 43124 0", command, StringComparison.Ordinal);
        Assert.Contains("TABSTUDIO_RTT_ERROR:", command, StringComparison.Ordinal);
        Assert.Contains("bindto 127.0.0.1", server.Arguments);
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile.CreateServer("/project", "/scripts", 43123, 43123, Ram));
    }

    [Theory]
    [InlineData(0x20000001UL, 4096U, 0)]
    [InlineData(0x20000000UL, 0U, 0)]
    [InlineData(0x20000000UL, 65537U, 0)]
    [InlineData(0x20000000UL, 4096U, 16)]
    [InlineData(0xfffffffffffffffcUL, 4096U, 0)]
    public void Rejects_unbounded_unaligned_or_overflowing_searches(ulong address, uint size, int channel)
    {
        Assert.Throws<InvalidDataException>(() => (Profile with { RttAddress = address, RttSearchSize = size, RttChannel = channel }).Validate());
    }

    [Fact]
    public void Rejects_inferred_RAM_flash_ranges_and_searches_crossing_the_chip_boundary()
    {
        Assert.Throws<InvalidDataException>(() => Profile.ValidateRttMemory(null));
        Assert.Throws<InvalidDataException>(() => Profile.ValidateRttMemory([new("FLASH", 0x20000000, 0x10000, false)]));
        Assert.Throws<InvalidDataException>(() => (Profile with { RttAddress = 0x2000f800 }).ValidateRttMemory(Ram));
        Assert.Throws<InvalidDataException>(() => (Profile with { RttSearchSize = null }).Validate());
    }

    [Fact]
    public void Hexadecimal_RAM_address_is_readable_without_becoming_a_Tcl_expression()
    {
        var profile = EmbeddedDebugProfile.Parse("""
            {"firmware":"app.elf","interfaceConfig":"interface/stlink.cfg","targetConfig":"target/stm32f4x.cfg","rttAddress":"0x20000000","rttSearchSize":4096,"rttChannel":0}
            """);
        Assert.Equal(0x20000000UL, profile.RttAddress);
        profile.ValidateRttMemory(Ram);
    }

    [Fact]
    public async Task Text_log_preserves_UTF8_and_applies_bounded_awaited_backpressure()
    {
        var expected = string.Concat(Enumerable.Repeat("שלום sensor ✓\n", 1000));
        using var channel = new MemoryStream(Encoding.UTF8.GetBytes(expected));
        var actual = new StringBuilder();
        var largest = 0;
        await EmbeddedRttTextReader.CopyAsync(channel, async chunk =>
        {
            largest = Math.Max(largest, chunk.Length);
            await Task.Yield();
            actual.Append(chunk);
        }, CancellationToken.None);
        Assert.Equal(expected, actual.ToString());
        Assert.InRange(largest, 1, 2048);
        Assert.True(channel.CanRead);
    }

    [Fact]
    public async Task Binary_frames_are_not_presented_as_decoded_defmt_text()
    {
        using var channel = new MemoryStream([0xc3, 0x28]);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => EmbeddedRttTextReader.CopyAsync(channel, _ => Task.CompletedTask, CancellationToken.None));
    }
}
