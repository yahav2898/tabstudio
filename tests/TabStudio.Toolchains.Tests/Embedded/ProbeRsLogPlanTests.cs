using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class ProbeRsLogPlanTests
{
    [Fact]
    public void Native_logging_uses_attach_with_matching_elf_and_no_flash_reset_or_erase_permissions()
    {
        var profile = new EmbeddedDebugProfile { Backend = EmbeddedDebugBackend.ProbeRs, Chip = "nRF52840_xxAA", Firmware = "firmware.elf" };
        var command = ProbeRsLogPlan.Command(profile, "/work", "/work/firmware.elf", 0x20000100, "1366:0105");
        Assert.Equal("probe-rs", command.Program);
        Assert.Equal("attach", command.Arguments[0]);
        Assert.Equal("/work/firmware.elf", command.Arguments[^1]);
        Assert.Contains("no-block-skip", command.Arguments);
        Assert.Contains("full", command.Arguments);
        Assert.Contains("0x20000100", command.Arguments);
        Assert.Null(command.Environment["PROBE_RS_ALLOW_ERASE_ALL"]);
        Assert.Null(command.Environment["PROBE_RS_CYCLE_POWER"]);
        Assert.Null(command.Environment["PROBE_RS_CONNECT_UNDER_RESET"]);
        Assert.DoesNotContain(command.Arguments, argument => argument is "run" or "download" or "erase" or "--reset" or "--allow-erase-all" or "--connect-under-reset");
    }

    [Fact]
    public void Logging_requires_explicit_backend_probe_and_one_actual_RAM_control_block_symbol()
    {
        var report = new FirmwareMemoryReport(0, 128,
            [new(".bss", 0x20000000, null, 128, 0, true, null)],
            [new("_SEGGER_RTT", 0x20000020, 64, ".bss", null)], true);
        Assert.Equal(0x20000020UL, ProbeRsLogPlan.ControlBlockAddress(report));
        Assert.Throws<InvalidDataException>(() => ProbeRsLogPlan.ControlBlockAddress(report with { Symbols = [] }));
        Assert.Throws<InvalidDataException>(() => ProbeRsLogPlan.ControlBlockAddress(report with { Symbols = [new("_SEGGER_RTT", 0x20000070, 64, ".bss", null)] }));
        Assert.Throws<InvalidDataException>(() => ProbeRsLogPlan.ControlBlockAddress(report with { Sections = [report.Sections[0] with { IsRam = false }] }));
        var profile = new EmbeddedDebugProfile { Backend = EmbeddedDebugBackend.ProbeRs, Chip = "nRF52840_xxAA", Firmware = "firmware.elf" };
        Assert.Throws<InvalidDataException>(() => ProbeRsLogPlan.Command(profile, "/work", "/work/firmware.elf", 0));
        Assert.Throws<InvalidOperationException>(() => ProbeRsLogPlan.Command(profile with { Backend = EmbeddedDebugBackend.OpenOcd, InterfaceConfig = "interface/stlink.cfg", TargetConfig = "target/stm32f4x.cfg" }, "/work", "/work/firmware.elf", 0, "0483:3748"));
    }
}
