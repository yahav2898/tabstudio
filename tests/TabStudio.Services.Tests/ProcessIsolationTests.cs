using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Services.Tests;

public sealed class ProcessIsolationTests
{
    [Fact]
    public void Per_service_mounts_and_user_do_not_leak_to_the_next_process()
    {
        using var temp = new TempDirectory();
        var runtime = new ProotRuntime(new() { ProotPath = "/proot", LoaderPath = "/loader", Rootfs = temp.Path, TemporaryDirectory = Path.Combine(temp.Path, "tmp") });
        var first = runtime.BuildArguments(new() { Program = "/bin/sh", UserId = 70, Mounts = [new("/storage/one", "/var/lib/postgresql/data")] });
        Assert.Contains("70:70", first);
        Assert.DoesNotContain("-0", first);
        Assert.Contains("/storage/one:/var/lib/postgresql/data", first);
        var next = runtime.BuildArguments(new() { Program = "/bin/sh" });
        Assert.Contains("-0", next);
        Assert.DoesNotContain("70:70", next);
        Assert.DoesNotContain("/storage/one:/var/lib/postgresql/data", next);
    }
    [Theory]
    [InlineData("relative", "/data")]
    [InlineData("/storage", "/../outside")]
    [InlineData("/storage:other", "/data")]
    public void Ambiguous_mount_paths_are_rejected(string source, string target)
    {
        using var temp = new TempDirectory();
        var runtime = new ProotRuntime(new() { ProotPath = "/proot", LoaderPath = "/loader", Rootfs = temp.Path, TemporaryDirectory = Path.Combine(temp.Path, "tmp") });
        Assert.Throws<ArgumentException>(() => runtime.BuildArguments(new() { Program = "/bin/sh", Mounts = [new(source, target)] }));
    }
}
