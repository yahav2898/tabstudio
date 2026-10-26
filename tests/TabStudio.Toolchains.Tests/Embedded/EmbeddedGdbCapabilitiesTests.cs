using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedGdbCapabilitiesTests
{
    [Theory]
    [InlineData(40, "arm")]
    [InlineData(243, "riscv:rv32")]
    [InlineData(83, "avr")]
    [InlineData(8, "mips:isa32")]
    public void Accepts_only_architectures_the_actual_debugger_reports(int machine, string name)
    {
        var output = "GDB startup output\n" + EmbeddedGdbCapabilities.Marker + "{\"dap\":true,\"architectures\":[\"i386\",\"" + name + "\"]}\n";
        EmbeddedGdbCapabilities.RequireSupport(output, (ushort)machine);
    }

    [Fact]
    public void Rejects_avr_missing_from_a_multiarch_distribution_and_missing_DAP()
    {
        var output = EmbeddedGdbCapabilities.Marker + "{\"dap\":true,\"architectures\":[\"arm\",\"riscv:rv32\"]}";
        Assert.Throws<NotSupportedException>(() => EmbeddedGdbCapabilities.RequireSupport(output, 83));
        Assert.Throws<NotSupportedException>(() => EmbeddedGdbCapabilities.RequireSupport(output.Replace("true", "false", StringComparison.Ordinal), 40));
        Assert.Throws<NotSupportedException>(() => EmbeddedGdbCapabilities.RequireSupport("No Python installed", 40));
        Assert.Throws<NotSupportedException>(() => EmbeddedGdbCapabilities.RequireSupport(output, 999));
    }

    [Fact]
    public void Does_not_treat_arm64_or_similarly_named_vendor_architectures_as_ARM32()
    {
        var output = EmbeddedGdbCapabilities.Marker + "{\"dap\":true,\"architectures\":[\"aarch64\",\"arm-impostor\"]}";
        Assert.Throws<NotSupportedException>(() => EmbeddedGdbCapabilities.RequireSupport(output, 40));
    }
}
