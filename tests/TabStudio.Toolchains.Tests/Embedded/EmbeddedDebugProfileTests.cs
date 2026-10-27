using System.Text.Json;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedDebugProfileTests
{
    [Fact]
    public void Parses_typed_profile_and_starts_only_a_private_nonprogramming_server()
    {
        var profile = EmbeddedDebugProfile.Parse("""
            { "firmware":"build/app.elf", "interfaceConfig":"interface/stlink.cfg", "targetConfig":"target/stm32f4x.cfg", "probeSelector":"1234-abcd:01" }
            """);
        var server = profile.CreateServer("/project", "/usr/share/openocd/scripts", 43123);
        Assert.Equal("openocd", server.Program);
        Assert.Contains("/usr/share/openocd/scripts/interface/stlink.cfg", server.Arguments);
        Assert.Contains("/usr/share/openocd/scripts/target/stm32f4x.cfg", server.Arguments);
        Assert.Contains("bindto 127.0.0.1", server.Arguments);
        Assert.Contains("gdb_port 43123", server.Arguments);
        Assert.Contains("tcl_port disabled", server.Arguments);
        Assert.Contains("telnet_port disabled", server.Arguments);
        Assert.Contains("adapter serial 1234-abcd:01", server.Arguments);
        Assert.DoesNotContain(server.Arguments, argument => argument.StartsWith("program ", StringComparison.Ordinal) || argument.Contains("flash write", StringComparison.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.CreateServer("/project", "/scripts", 0));
    }

    [Theory]
    [InlineData("interface/stlink.cfg; shutdown", "target/stm32f4x.cfg", null)]
    [InlineData("interface/../target/evil.cfg", "target/stm32f4x.cfg", null)]
    [InlineData("/tmp/probe.cfg", "target/stm32f4x.cfg", null)]
    [InlineData("interface/stlink.cfg", "target/[exec evil].cfg", null)]
    [InlineData("interface/stlink.cfg", "target/stm32f4x.cfg", "one; shutdown")]
    [InlineData("interface/stlink.cfg", "target/stm32f4x.cfg", "$env(SERIAL)")]
    [InlineData("interface/stlink.cfg", "target/stm32f4x.cfg", "[exec evil]")]
    [InlineData("interface/stlink.cfg", "target/stm32f4x.cfg", "two words")]
    public void Rejects_profile_Tcl_injection_and_untrusted_script_paths(string probe, string target, string? serial)
    {
        var profile = new EmbeddedDebugProfile { Firmware = "app.elf", InterfaceConfig = probe, TargetConfig = target, ProbeSelector = serial };
        Assert.Throws<InvalidDataException>(() => profile.CreateServer("/project", "/scripts", 43123));
    }

    [Fact]
    public void Rejects_unknown_keys_missing_required_values_and_oversized_input()
    {
        Assert.Throws<JsonException>(() => EmbeddedDebugProfile.Parse("""
            { "firmware":"app.elf", "interfaceConfig":"interface/stlink.cfg", "targetConfig":"target/stm32f4x.cfg", "commands":["mass_erase"] }
            """));
        Assert.Throws<JsonException>(() => EmbeddedDebugProfile.Parse("{}"));
        Assert.Throws<InvalidDataException>(() => EmbeddedDebugProfile.Parse(new string(' ', 65537)));
    }

    [Fact]
    public void Requires_executable_ELF_content_instead_of_trusting_a_file_extension()
    {
        using var folder = new TempDirectory();
        var path = folder.Write("firmware.elf", "not firmware");
        var profile = new EmbeddedDebugProfile { Firmware = "firmware.elf", InterfaceConfig = "interface/stlink.cfg", TargetConfig = "target/stm32f4x.cfg" };
        Assert.Throws<InvalidDataException>(() => profile.ResolveFirmware(folder.Path));
        byte[] elf = new byte[52];
        new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F', 1, 1, 1 }.CopyTo(elf, 0);
        elf[16] = 2;
        elf[18] = 40;
        File.WriteAllBytes(path, elf);
        Assert.Equal(path, profile.ResolveFirmware(folder.Path));
        Assert.Equal((ushort)40, profile.ElfMachine(folder.Path));
        elf[5] = 2;
        elf[16] = 0;
        elf[17] = 2;
        elf[18] = 0;
        elf[19] = 243;
        File.WriteAllBytes(path, elf);
        Assert.Equal((ushort)243, profile.ElfMachine(folder.Path));
        elf[5] = 1;
        elf[16] = 1;
        File.WriteAllBytes(path, elf);
        Assert.Throws<InvalidDataException>(() => profile.ResolveFirmware(folder.Path));
        File.WriteAllBytes(path, elf[..20]);
        Assert.Throws<InvalidDataException>(() => profile.ResolveFirmware(folder.Path));
    }
}
