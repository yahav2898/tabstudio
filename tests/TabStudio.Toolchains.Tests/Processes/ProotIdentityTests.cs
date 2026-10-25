using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>Delve authenticates sockets with the real device identity while package installation uses simulated root.</summary>
public sealed class ProotIdentityTests
{
    [Fact]
    public void Preserving_the_host_identity_omits_only_the_simulated_root_option()
    {
        using var folder = new TempDirectory();
        var runtime = new ProotRuntime(new ProotRuntimeOptions
        {
            ProotPath = "/native/libproot.so", LoaderPath = "/native/libproot-loader.so",
            Rootfs = folder.Path, TemporaryDirectory = folder.Resolve("tmp"),
        });
        var spec = new ProcessStartSpec { Program = "dlv", Arguments = ["dap"] };
        var root = runtime.BuildArguments(spec);
        var real = runtime.BuildArguments(spec with { UseHostIdentity = true });

        Assert.Contains("-0", root);
        Assert.DoesNotContain("-0", real);
        Assert.Equal(root.Where(a => a != "-0"), real);
        Assert.Equal(["dlv", "dap"], real.TakeLast(2));
    }
}
