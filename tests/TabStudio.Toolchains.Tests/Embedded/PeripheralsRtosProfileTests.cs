using System.Text.Json;
using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class PeripheralsRtosProfileTests
{
    [Fact]
    public void Profile_preserves_actual_build_facts_and_task_thresholds_in_bytes()
    {
        var profile = EmbeddedRtosProfile.Parse("""
            {"kind":"Zephyr", "stackFillByte":170, "stackGrowsDown":true,
             "maximumThreads":32, "maximumStackScanBytes":1024, "maximumTotalStackScanBytes":8192,
             "timeoutSeconds":10, "stackMemory":[{"start":4096,"length":4096}],
             "taskLimits":[{"name":"Sensor","minimumFreeBytes":128,"expectedStackBytes":1024,"taskAddress":256}]}
            """);
        var options = profile.ToReadOptions();
        Assert.Equal((byte)170, options.StackFillByte);
        Assert.True(options.StackGrowsDown);
        Assert.Equal(32, options.MaximumThreads);
        Assert.Equal(8192, options.MaximumTotalStackScanBytes);
        var finding = Assert.Single(profile.CheckTaskLimits([new(256, "Sensor", "Flags 0x4", -1, 1024, 64)]));
        Assert.True(finding.Verified);
        Assert.True(finding.BelowMinimum);
        Assert.Contains("64 B", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"kind\":\"FreeRtos\",\"stackFillByte\":165}")]
    [InlineData("{\"kind\":\"Zephyr\",\"stackGrowsDown\":false}")]
    [InlineData("{\"kind\":\"FreeRtos\",\"maximumThreads\":0}")]
    [InlineData("{\"kind\":\"FreeRtos\",\"maximumStackScanBytes\":1048577}")]
    [InlineData("{\"kind\":\"FreeRtos\",\"timeoutSeconds\":121}")]
    [InlineData("{\"kind\":\"FreeRtos\",\"stackMemory\":[{\"start\":18446744073709551615,\"length\":2}]}")]
    [InlineData("{\"kind\":\"FreeRtos\",\"Kind\":\"Zephyr\"}")]
    public void Invalid_or_ambiguous_profiles_fail_before_a_target_is_read(string json) => Assert.Throws<InvalidDataException>(() => EmbeddedRtosProfile.Parse(json));

    [Fact]
    public void Unknown_fields_and_invented_ABI_offsets_are_rejected()
    {
        Assert.Throws<JsonException>(() => EmbeddedRtosProfile.Parse("{\"kind\":\"FreeRtos\",\"tcbOffset\":24}"));
        Assert.Throws<JsonException>(() => EmbeddedRtosProfile.Parse("{\"kind\":\"Zephyr\",\"stackFillByte\":256,\"stackGrowsDown\":true}"));
        Assert.Throws<InvalidDataException>(() => EmbeddedRtosProfile.Parse(new string(' ', 32769)));
    }

    [Fact]
    public void Legacy_verified_fill_configuration_is_validated_and_preserved()
    {
        var profile = EmbeddedRtosProfile.ParseLegacy("{\"verifiedStackFillByte\":165,\"stackGrowsDown\":false}", EmbeddedRtosKind.FreeRtos);
        Assert.Equal((byte)165, profile.StackFillByte);
        Assert.False(profile.StackGrowsDown);
        Assert.Equal(1024, profile.MaximumThreads);
        Assert.Throws<InvalidDataException>(() => EmbeddedRtosProfile.ParseLegacy("{\"verifiedStackFillByte\":165}", EmbeddedRtosKind.FreeRtos));
    }

    [Fact]
    public void Missing_fill_ambiguous_names_and_changed_allocation_do_not_pass_task_thresholds()
    {
        var profile = new EmbeddedRtosProfile
        {
            Kind = EmbeddedRtosKind.FreeRtos,
            TaskLimits = [new("Sensor", 16, 128)],
        };
        Assert.False(Assert.Single(profile.CheckTaskLimits([new(1, "Sensor", "Ready", 1, 128, null)])).Verified);
        Assert.False(Assert.Single(profile.CheckTaskLimits([new(1, "Sensor", "Ready", 1, 64, 32)])).Verified);
        Assert.False(Assert.Single(profile.CheckTaskLimits([new(1, "Sensor", "Ready", 1, 128, 64), new(2, "Sensor", "Ready", 1, 128, 64)])).Verified);
        var exact = profile with { TaskLimits = [new("Sensor", 16, 128, 2)] };
        Assert.True(Assert.Single(exact.CheckTaskLimits([new(1, "Sensor", "Ready", 1, 128, 4), new(2, "Sensor", "Ready", 1, 128, 64)])).Verified);
    }

    [Theory]
    [InlineData("(unsigned long long) 0x1000 <symbol>", 4096UL)]
    [InlineData("165", 165UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void Debugger_scalar_parser_accepts_actual_unsigned_values(string text, ulong expected) => Assert.Equal(expected, RtosDebugValue.ParseUnsigned(text));

    [Theory]
    [InlineData("-1")]
    [InlineData("\"string containing 0x1000\"")]
    [InlineData("0x10garbage")]
    public void Debugger_scalar_parser_refuses_strings_or_partial_numbers(string text) => Assert.Throws<InvalidDataException>(() => RtosDebugValue.ParseUnsigned(text));
}
