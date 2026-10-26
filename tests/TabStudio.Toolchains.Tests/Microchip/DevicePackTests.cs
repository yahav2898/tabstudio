using TabStudio.Testing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class DevicePackTests
{
    [Fact]
    public void Resolves_the_exact_requested_pack_instead_of_silently_upgrading_a_project()
    {
        var old = Pack("1.9.99", "PIC18F45K22");
        var newer = Pack("1.17.312", "PIC18F45K22");
        var request = new MplabPackReference("Microchip", "PIC18F-K_DFP", "1.9.99");

        Assert.Same(old, DevicePack.Resolve([old, newer], "PIC18F45K22", request));
        Assert.Same(newer, DevicePack.Resolve([old, newer], "pic18f45k22"));
        Assert.Null(DevicePack.Resolve([newer], "PIC18F45K22", request));
    }

    [Fact]
    public void Does_not_infer_device_support_from_a_similar_pack_name()
    {
        Assert.Null(DevicePack.Resolve([Pack("1.17.312", "PIC18F46K22")], "PIC18F45K22"));
    }

    [Fact]
    public void Reads_namespaced_pdsc_metadata_and_returns_the_real_compiler_directory()
    {
        using var folder = new TempDirectory();
        var descriptor = folder.Write("Microchip.PIC18F-K_DFP.pdsc", """
            <package xmlns="urn:cmsis-pack">
              <vendor>Microchip</vendor><name>PIC18F-K_DFP</name>
              <releases><release version="1.17.312" date="2026-09-02"/><release version="1.16.308"/></releases>
              <devices><family Dfamily="PIC18F-K"><device Dname="PIC18F45K22"/></family></devices>
            </package>
            """);
        folder.Write("xc8/pic/include/proc/pic18f45k22.h");

        var pack = DevicePack.Load(descriptor);

        Assert.Equal("1.17.312", pack.Version);
        Assert.Contains("PIC18F45K22", pack.Devices);
        Assert.Equal(folder.Resolve("xc8"), pack.CompilerDirectory("XC8"));
        Assert.Throws<InvalidDataException>(() => pack.CompilerDirectory("XC32"));
        Assert.Throws<ArgumentException>(() => pack.CompilerDirectory("../../outside"));
    }

    private static DevicePack Pack(string version, string device) => new("Microchip", "PIC18F-K_DFP", version,
        "/packs/" + version, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { device });

    [Fact]
    public void Xc32_six_uses_the_pack_root_while_legacy_compilers_use_the_xc32_subfolder()
    {
        using var folder = new TempDirectory();
        folder.Write("xc32/device_files/32MX250F128B.info");
        var pack = new DevicePack("Microchip", "PIC32MX_DFP", "1.7.380", folder.Path,
            new HashSet<string> { "PIC32MX250F128B" });

        Assert.Equal(folder.Path, pack.CompilerDirectory("XC32", "6.00"));
        Assert.Equal(folder.Resolve("xc32"), pack.CompilerDirectory("XC32", "4.60"));
    }
}
