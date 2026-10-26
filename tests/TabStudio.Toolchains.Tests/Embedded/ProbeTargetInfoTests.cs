using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class ProbeTargetInfoTests
{
    [Fact]
    public void Identify_command_has_no_erase_unlock_reset_or_user_supplied_target_overrides()
    {
        var command = ProbeTargetInfoReader.Command(new("usb-device", 0x0483, 0x3748, "Probe", []), "/work");
        Assert.Equal("probe-rs", command.Program);
        Assert.Equal(new[] { "info", "--verbose", "--non-interactive", "--probe", "0483:3748" }, command.Arguments);
        Assert.Null(command.Environment["PROBE_RS_ALLOW_ERASE_ALL"]);
        Assert.Null(command.Environment["PROBE_RS_CYCLE_POWER"]);
        Assert.Null(command.Environment["PROBE_RS_CONNECT_UNDER_RESET"]);
        Assert.Null(command.Environment["PROBE_RS_CHIP_DESCRIPTION_PATH"]);
        Assert.Equal("1", command.Environment[EmbeddedGlibcRuntime.RoutingMarker]);
        Assert.DoesNotContain(command.Arguments, argument => argument.Contains("erase", StringComparison.Ordinal) || argument.Contains("reset", StringComparison.Ordinal));
    }

    [Fact]
    public void Parser_retains_actual_IDCODE_protocol_and_core_evidence_without_mapping_to_a_guessed_chip()
    {
        // Source-derived formatter fixture from probe-rs0.32; not a hardware capture.
        const string output = """
            Probing target via JTAG
            -----------------------
            RISC-V Chip:
              IDCODE: 0012345679
                Version: 1
                Part: 9029
            Probing target via SWD
            Debug Port: DPv2, Designer: ARM Ltd, Part: 0x123, Revision: 0x1
            └── 0 MemoryAP
                └── CPUID
                    ├── IMPLEMENTER: ARM Ltd
                    ├── VARIANT: 0
                    ├── PARTNO: Cortex-M4
                    └── REVISION: 1
            """;
        var info = ProbeTargetInfoReader.Parse(output);
        Assert.True(info.HasFacts);
        var id = Assert.Single(info.Facts, fact => fact.Kind == ProbeTargetFactKind.Idcode);
        Assert.Equal("0x12345679", id.Value);
        Assert.Equal("JTAG", id.Protocol);
        Assert.Equal("RISC-V", id.Architecture);
        Assert.Equal(4, id.Line);
        Assert.DoesNotContain(info.Facts, fact => fact.Kind == ProbeTargetFactKind.DetectedChip);
        var part = Assert.Single(info.Facts, fact => fact.Kind == ProbeTargetFactKind.CorePart);
        Assert.Equal("Cortex-M4", part.Value);
        Assert.Equal("SWD", part.Protocol);
        Assert.Equal("ARM", part.Architecture);
        Assert.Equal(1, part.CoreOrdinal);
    }

    [Fact]
    public void Parser_distinguishes_explicit_model_and_vendor_id_from_transport_hints_and_failures()
    {
        var info = ProbeTargetInfoReader.Parse("\u001b[32mDetected chip: STM32F401RETx\u001b[0m\nAtmel device (DID = 0x10010005)\nNo IDCODE info for this RISC-V chip.");
        Assert.Equal("STM32F401RETx", info.Facts[0].Value);
        Assert.Equal(ProbeTargetFactKind.DetectedChip, info.Facts[0].Kind);
        Assert.Equal(ProbeTargetFactKind.VendorDeviceId, info.Facts[1].Kind);
        Assert.False(ProbeTargetInfoReader.Parse("ST-Link (0483:3748)\nCould not attach to target\nError showing ARM chip information").HasFacts);
        Assert.False(ProbeTargetInfoReader.Parse("IDCODE: ffffffffff").HasFacts);
        Assert.False(ProbeTargetInfoReader.Parse("PARTNO: Cortex-M4").HasFacts);
    }
}
