using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Processes;

public sealed class ScopedPreloadProotTests
{
    [Theory]
    [InlineData("/opt/tabstudio/embedded/libusb-fd.so")]
    [InlineData("/opt/tabstudio/embedded/usbfs-fd.so:/workspace/user.so")]
    [InlineData("")]
    public void Guest_preloads_are_injected_after_PRoot_and_never_loaded_by_the_Android_parent(string preload)
    {
        using var folder = new TempDirectory();
        var runtime = CreateRuntime(folder);
        var spec = new ProcessStartSpec
        {
            Program = "probe-rs", Arguments = ["list"],
            Environment = new Dictionary<string, string?>
            {
                ["LD_PRELOAD"] = preload,
                ["TABSTUDIO_USB_FD"] = "3",
                ["TABSTUDIO_USB_DEVPATH"] = "/dev/bus/usb/001/002",
            }
        };

        Assert.Equal(new[] { "/usr/bin/env", "LD_PRELOAD=" + preload, "probe-rs", "list" }, runtime.BuildArguments(spec).TakeLast(4));
        var environment = runtime.BuildEnvironment(spec);
        Assert.DoesNotContain(environment, entry => entry.StartsWith("LD_PRELOAD=", StringComparison.Ordinal));
        Assert.Contains("TABSTUDIO_USB_FD=3", environment);
        Assert.Contains("TABSTUDIO_USB_DEVPATH=/dev/bus/usb/001/002", environment);
    }

    [Fact]
    public void Explicit_null_disables_the_automatic_JVM_preload_without_injecting_an_env_wrapper()
    {
        using var folder = new TempDirectory();
        folder.Write("native/libtabstudio-jvm.so", string.Empty);
        var runtime = CreateRuntime(folder);
        var spec = new ProcessStartSpec { Program = "java", Arguments = ["-version"], Environment = new Dictionary<string, string?> { ["LD_PRELOAD"] = null } };

        Assert.Equal(new[] { "java", "-version" }, runtime.BuildArguments(spec).TakeLast(2));
        Assert.DoesNotContain(runtime.BuildArguments(spec), argument => argument.StartsWith("LD_PRELOAD=", StringComparison.Ordinal));
        Assert.DoesNotContain(runtime.BuildEnvironment(spec), argument => argument.StartsWith("LD_PRELOAD=", StringComparison.Ordinal));
    }

    private static ProotRuntime CreateRuntime(TempDirectory folder) => new(new ProotRuntimeOptions
    {
        ProotPath = folder.Resolve("native/libproot.so"), LoaderPath = folder.Resolve("native/libproot-loader.so"),
        Rootfs = folder.Resolve("rootfs"), TemporaryDirectory = folder.Resolve("tmp"),
    });
}
