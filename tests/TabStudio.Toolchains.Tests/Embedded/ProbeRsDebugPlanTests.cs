using System.Text.Json;
using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class ProbeRsDebugPlanTests
{
    private static EmbeddedDebugProfile Profile => new()
    {
        Backend = EmbeddedDebugBackend.ProbeRs, Firmware = "firmware.elf", Chip = "STM32F401RETx", CoreIndex = 0,
    };

    [Fact]
    public void Uses_upstream_attach_schema_and_explicitly_disables_flashing_recovery_and_reset()
    {
        var firmware = Path.Combine(Path.GetTempPath(), "firmware.elf");
        var properties = ProbeRsDebugPlan.Attach(Profile, Path.GetTempPath(), firmware, 43123, "0483:3748");
        Assert.Equal("attach", properties["request"]!.GetValue<string>());
        Assert.Equal("STM32F401RETx", properties["chip"]!.GetValue<string>());
        Assert.Equal("0483:3748", properties["probe"]!.GetValue<string>());
        Assert.Equal(43123, properties["debugServer"]!.GetValue<int>());
        Assert.False(properties["allowEraseAll"]!.GetValue<bool>());
        Assert.False(properties["connectUnderReset"]!.GetValue<bool>());
        var flashing = properties["flashingConfig"]!.AsObject();
        Assert.All(flashing, item => Assert.False(item.Value!.GetValue<bool>()));
        var core = Assert.Single(properties["coreConfigs"]!.AsArray())!.AsObject();
        Assert.Equal(firmware, core["programBinary"]!.GetValue<string>());
        Assert.Equal(0, core["coreIndex"]!.GetValue<int>());
        Assert.False(core["rttEnabled"]!.GetValue<bool>());
        Assert.Null(properties["chipDescriptionPath"]);
    }

    [Fact]
    public void Server_is_loopback_only_and_scrubs_inherited_dangerous_probe_options()
    {
        var server = ProbeRsDebugPlan.Server("/project", 43123);
        Assert.Equal(new[] { "dap-server", "--ip", "127.0.0.1", "--port", "43123" }, server.Arguments);
        Assert.All(server.Environment, variable => Assert.Null(variable.Value));
        Assert.True(server.Environment.ContainsKey("PROBE_RS_ALLOW_ERASE_ALL"));
        Assert.True(server.Environment.ContainsKey("PROBE_RS_CONNECT_UNDER_RESET"));
        Assert.True(server.Environment.ContainsKey("PROBE_RS_CYCLE_POWER"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProbeRsDebugPlan.Server("/project", 0));
    }

    [Fact]
    public void Probe_profile_needs_no_OpenOCD_files_but_requires_an_exact_chip_and_valid_selector()
    {
        var profile = EmbeddedDebugProfile.Parse("""
            {"backend":"ProbeRs","firmware":"app.elf","chip":"ESP32-C3","coreIndex":0,"probeSelector":"303a:1001:DC:DA:0C:D3:FE:D8"}
            """);
        Assert.Equal(EmbeddedDebugBackend.ProbeRs, profile.Backend);
        Assert.Equal("ESP32-C3", profile.Chip);
        Assert.Throws<InvalidDataException>(() => (Profile with { Chip = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (Profile with { CoreIndex = 16 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Profile with { Chip = "target; erase" }).Validate());
        Assert.Throws<InvalidDataException>(() => (Profile with { ProbeSelector = "just-serial" }).Validate());
        Assert.Throws<InvalidDataException>(() => (Profile with { ProbeSelector = "0483:3748-256" }).Validate());
        Assert.Throws<InvalidDataException>(() => (Profile with { RttAddress = 0x20000000, RttSearchSize = 4096 }).Validate());
        Assert.Throws<InvalidOperationException>(() => Profile.CreateServer("/project", "/scripts", 43123));
    }

    [Fact]
    public void User_profiles_cannot_override_destructive_attach_permissions()
    {
        Assert.Throws<JsonException>(() => EmbeddedDebugProfile.Parse("""
            {"backend":"ProbeRs","firmware":"app.elf","chip":"RP2040","allowEraseAll":true}
            """));
        Assert.Throws<JsonException>(() => EmbeddedDebugProfile.Parse("""
            {"backend":"ProbeRs","firmware":"app.elf","chip":"RP2040","flashingConfig":{"flashingEnabled":true}}
            """));
    }
}
