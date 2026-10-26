using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class UsbDescriptorInheritanceTests
{
    [Fact]
    public async Task Explicit_device_descriptor_reaches_the_child_without_leaking_to_other_processes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var path = directory.Write("device.txt", "permissioned USB descriptor");
        using var device = File.OpenRead(path);
        var descriptor = device.SafeFileHandle.DangerousGetHandle().ToInt32();
        var runtime = new HostRuntime();
        var result = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
        {
            Program = "/bin/sh", Arguments = ["-c", "cat /proc/self/fd/3"],
            InheritedDescriptors = new Dictionary<int, int> { [descriptor] = 3 },
        }, null, CancellationToken.None);
        Assert.True(result.Succeeded, result.Output);
        Assert.Contains("permissioned USB descriptor", result.Output, StringComparison.Ordinal);

        var other = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
        {
            Program = "/bin/sh", Arguments = ["-c", "test ! -e /proc/self/fd/" + descriptor],
        }, null, CancellationToken.None);
        Assert.True(other.Succeeded, other.Output);
    }

    [Fact]
    public void Device_descriptor_cannot_replace_standard_input_output_or_error()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Throws<ArgumentException>(() => new HostRuntime().Start(new ProcessStartSpec
        {
            Program = "/bin/sh", InheritedDescriptors = new Dictionary<int, int> { [99] = 1 },
        }));
    }

    [Fact]
    public async Task Same_number_mapping_still_clears_close_on_exec_only_in_the_child()
    {
        if (OperatingSystem.IsWindows()) return;
        using var folder = new TempDirectory();
        using var file = File.OpenRead(folder.Write("device", "same descriptor"));
        var fd = file.SafeFileHandle.DangerousGetHandle().ToInt32();
        var runtime = new HostRuntime();
        var child = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
        {
            Program = "/bin/sh", Arguments = ["-c", "cat /proc/self/fd/" + fd],
            InheritedDescriptors = new Dictionary<int, int> { [fd] = fd },
        }, null, CancellationToken.None);
        Assert.True(child.Succeeded, child.Output);
        Assert.Contains("same descriptor", child.Output, StringComparison.Ordinal);
        var other = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
        {
            Program = "/bin/sh", Arguments = ["-c", "test ! -e /proc/self/fd/" + fd],
        }, null, CancellationToken.None);
        Assert.True(other.Succeeded, other.Output);
    }
}
