using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class ProbeRsSafeLaunchTests
{
    [Fact]
    public void Typed_probe_operations_clear_environment_permissions_and_select_the_managed_preset_boundary()
    {
        var environment = ProbeTargetInfoReader.SafeAttachEnvironment();
        Assert.Equal("1", environment[ProbeRsSafeLaunch.Marker]);
        foreach (var name in new[] { "PROBE_RS_ALLOW_ERASE_ALL", "PROBE_RS_CYCLE_POWER", "PROBE_RS_CONNECT_UNDER_RESET", "PROBE_RS_CONFIG_PRESET", "PROBE_RS_REMOTE_HOST", "PROBE_RS_REMOTE_TOKEN" })
        {
            Assert.Null(environment[name]);
        }
        var info = ProbeTargetInfoReader.Command(new UsbDeviceInfo("/dev/bus/usb/001/002", 0x0483, 0x3748, "Synthetic probe", []), "/project");
        Assert.Equal("1", info.Environment[ProbeRsSafeLaunch.Marker]);
        Assert.DoesNotContain(ProbeRsSafeLaunch.Marker, ProbeRsDebugPlan.Server("/project", 12345).Environment.Keys);
    }

    [Fact]
    public void Installer_wrapper_preserves_the_private_original_executable_and_rejects_arbitrary_shell_text()
    {
        var launcher = ProbeRsSafeLaunch.Launcher("/opt/tabstudio/probe-rs/probe-rs");
        Assert.Contains("python3 " + ProbeRsSafeLaunch.ScriptPath, launcher, StringComparison.Ordinal);
        Assert.Contains("/opt/tabstudio/probe-rs/probe-rs \"$@\"", launcher, StringComparison.Ordinal);
        Assert.Contains(ProbeRsSafeLaunch.Marker, ProbeRsSafeLaunch.Source, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => ProbeRsSafeLaunch.Launcher("/workspace/probe-rs"));
        Assert.Throws<ArgumentException>(() => ProbeRsSafeLaunch.Launcher("/opt/tabstudio/probe-rs/$(erase)"));
        Assert.Throws<ArgumentException>(() => ProbeRsSafeLaunch.Launcher("/opt/tabstudio/probe-rs/probe-rs;erase"));
    }
}
