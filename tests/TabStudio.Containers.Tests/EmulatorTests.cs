using TabStudio.Containers.Images;
using TabStudio.Containers.Runtime;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Containers.Tests;

public sealed class EmulatorTests
{
    [Fact]
    public void Missing_Box64_and_musl_images_get_specific_reasons_and_native_mounts_preserve_argv()
    {
        using var temp = new TempDirectory();
        var native = Path.Combine(temp.Path, "native");
        var rootfs = Path.Combine(temp.Path, "image"); Directory.CreateDirectory(rootfs);
        var binary = Path.Combine(temp.Path, "libbox64.so");
        var emulator = new Box64ContainerEmulator(() => native, binary);
        Assert.Contains("Toolchain Store", emulator.Problem(OciPlatform.Linux("amd64"), rootfs), StringComparison.Ordinal);
        File.WriteAllText(binary, "fixture");
        Directory.CreateDirectory(Path.Combine(native, "usr/lib/aarch64-linux-gnu"));
        File.WriteAllText(Path.Combine(native, "usr/lib/aarch64-linux-gnu/ld-linux-aarch64.so.1"), "fixture");
        Assert.Null(emulator.Problem(OciPlatform.Linux("amd64"), rootfs));
        var wrapped = emulator.Wrap(new() { Program = "/usr/bin/test", Arguments = ["literal; $(argument)"] });
        Assert.Equal(["/usr/bin/test", "literal; $(argument)"], wrapped.Arguments);
        Assert.Contains(wrapped.Mounts, m => m.Target == "/lib/ld-linux-aarch64.so.1");
        Directory.CreateDirectory(Path.Combine(rootfs, "lib"));
        File.WriteAllText(Path.Combine(rootfs, "lib/ld-musl-x86_64.so.1"), "fixture");
        Assert.Contains("musl", emulator.Problem(OciPlatform.Linux("amd64"), rootfs), StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_automatic_ports_work_and_unsupported_network_semantics_fail()
    {
        var port = Assert.Single(PortBinding.Parse("0:5432"));
        Assert.Equal(0, port.HostPort);
        Assert.Throws<FormatException>(() => PortBinding.Parse("8080:80-81"));
        using var temp = new TempDirectory();
        var network = new ContainerNetwork(temp.Path, _ => true, () => 30000);
        Assert.Throws<ContainerException>(() => network.Allocate("a", null, [], [new(53, "udp")]));
        Assert.Throws<ContainerException>(() => network.Allocate("a", null, [], [new(80, HostIp: "192.0.2.1")]));
    }
}
