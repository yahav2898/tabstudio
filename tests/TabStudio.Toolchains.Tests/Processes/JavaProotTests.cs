using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>Only JVM launchers receive the Android mapping shim, through the same shared runtime paths.</summary>
public sealed class JavaProotTests
{
    [Theory]
    [InlineData("java", true)]
    [InlineData("/usr/bin/javac", true)]
    [InlineData("jdtls", true)]
    [InlineData("mvn", true)]
    [InlineData("gradle", true)]
    [InlineData("python3", false)]
    public void Java_launchers_share_the_packaged_mapping_shim_and_pass_it_to_their_children(string command, bool usesShim)
    {
        using var folder = new TempDirectory();
        var shim = folder.Write("native/libtabstudio-jvm.so", string.Empty);
        var runtime = new ProotRuntime(new ProotRuntimeOptions
        {
            ProotPath = folder.Resolve("native/libproot.so"), LoaderPath = folder.Resolve("native/libproot-loader.so"),
            Rootfs = folder.Resolve("rootfs"), TemporaryDirectory = folder.Resolve("tmp"),
        });
        var spec = new ProcessStartSpec { Program = command };
        var arguments = runtime.BuildArguments(spec);
        var environment = runtime.BuildEnvironment(spec);

        Assert.Equal(usesShim, environment.Contains("LD_PRELOAD=" + shim));
        Assert.Equal(usesShim, arguments.Contains(Path.GetDirectoryName(shim)!));
        Assert.DoesNotContain("LD_PRELOAD=" + shim, runtime.BuildEnvironment(spec with
        {
            Environment = new Dictionary<string, string?> { ["LD_PRELOAD"] = null },
        }));
    }
}
