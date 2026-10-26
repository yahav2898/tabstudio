using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class PicMemoryUsageTests
{
    [Fact]
    public void Reads_the_real_xc8_spike_report_and_keeps_bytes_separate_from_configuration_words()
    {
        const string output = """
            18F45K22 Memory Summary:
                Program space        used    3Ch (    60) of  8000h bytes   (  0.2%)
                Data space           used     2h (     2) of   600h bytes   (  0.1%)
                Configuration bits   used     4h (     4) of     7h words   ( 57.1%)
                EEPROM space         used     0h (     0) of   100h bytes   (  0.0%)
            """;

        var usage = PicMemoryUsage.Parse(output);

        Assert.Equal(new PicMemoryRegion(60, 32768), usage.Program);
        Assert.Equal(new PicMemoryRegion(2, 1536), usage.Data);
    }

    [Fact]
    public void A_failed_link_does_not_claim_zero_memory_usage()
    {
        var usage = PicMemoryUsage.Parse("main.c:10: error: undefined reference to led");

        Assert.Null(usage.Program);
        Assert.Null(usage.Data);
    }

    [Fact]
    public void Takes_the_last_complete_link_summary_in_a_multi_build_log()
    {
        var usage = PicMemoryUsage.Parse("""
            Program space used 10h (16) of 8000h bytes
            Program space used 20h (32) of 8000h bytes
            Data space used 0h (0) of 600h bytes
            """);

        Assert.Equal(new PicMemoryRegion(32, 32768), usage.Program);
        Assert.Equal(new PicMemoryRegion(0, 1536), usage.Data);
    }

    [Fact]
    public void Xc32_totals_without_capacities_leave_capacity_unknown()
    {
        var usage = PicMemoryUsage.Parse("""
            Total program memory used (bytes) : 0x108 (264) 0.2%
            Total data memory used (bytes) : 0x10 (16) 0.1%
            """);

        Assert.Equal(new PicMemoryRegion(264, null), usage.Program);
        Assert.Equal(new PicMemoryRegion(16, null), usage.Data);
    }

    [Fact]
    public void Reads_xc32_6_columns_from_the_linked_pic32mx_spike_including_zero_data()
    {
        var usage = PicMemoryUsage.Parse("""
                  Total kseg0_program_mem used  :       0x3a0         928  0.7% of 0x1f000
                     Total Program Memory used  :       0x66c        1644  1.2% of 0x20490
                Total kseg0/kseg1 data_mem used  :           0           0  <1% of 0x8000
                        Total Data Memory used  :           0           0  <1% of 0x8000
                stack                   0xa0000020         0x7fd8       32728  Reserved for stack
                """);

        Assert.Equal(new PicMemoryRegion(1644, 0x20490), usage.Program);
        Assert.Equal(new PicMemoryRegion(0, 0x8000), usage.Data);
    }
}
