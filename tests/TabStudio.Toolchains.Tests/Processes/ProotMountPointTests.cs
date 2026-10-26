using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>
/// PRoot removes the placeholder directories it creates for a binding when that PRoot exits. Programs share one
/// root file system, so a folder's path must exist there before any PRoot binds it, or one program's exit removes
/// the working directory of another that is still running.
/// </summary>
public sealed class ProotMountPointTests
{
    [Fact]
    public void Shared_folders_and_the_working_directory_exist_in_the_root_file_system_before_PRoot_binds_them()
    {
        using var folder = new TempDirectory();
        var rootfs = Directory.CreateDirectory(folder.Resolve("rootfs")).FullName;
        var projects = Directory.CreateDirectory(folder.Resolve("Projects")).FullName;
        var cache = Directory.CreateDirectory(folder.Resolve("cache/ts-dap")).FullName;
        var crate = Directory.CreateDirectory(Path.Combine(projects, "SelfTestRust")).FullName;
        var runtime = new ProotRuntime(new ProotRuntimeOptions
        {
            ProotPath = folder.Resolve("native/libproot.so"), LoaderPath = folder.Resolve("native/libproot-loader.so"),
            Rootfs = rootfs, TemporaryDirectory = folder.Resolve("tmp"), SharedFolders = [projects],
        });
        runtime.ShareFolder(cache);

        var arguments = runtime.BuildArguments(new ProcessStartSpec { Program = "cargo", Arguments = ["build"], WorkingDirectory = crate });

        foreach (var shared in new[] { projects, cache })
        {
            Assert.Equal("-b", arguments[arguments.IndexOf(shared) - 1]);
            Assert.True(Directory.Exists(Path.Combine(rootfs, shared.TrimStart('/'))), $"{shared} has no mount point in the root file system.");
        }

        Assert.Equal(crate, arguments[arguments.IndexOf("-w") + 1]);

        // A session's folder that is gone is not bound, and its empty mount point does not pile up.
        Directory.Delete(cache);
        Assert.DoesNotContain(cache, runtime.BuildArguments(new ProcessStartSpec { Program = "cargo", WorkingDirectory = crate }));
        Assert.False(Directory.Exists(Path.Combine(rootfs, cache.TrimStart('/'))));
        Assert.True(Directory.Exists(Path.Combine(rootfs, projects.TrimStart('/'))));
    }

    [Fact]
    public void A_runtime_whose_root_file_system_is_not_set_up_creates_nothing()
    {
        using var folder = new TempDirectory();
        var projects = Directory.CreateDirectory(folder.Resolve("Projects")).FullName;
        var runtime = new ProotRuntime(new ProotRuntimeOptions
        {
            ProotPath = folder.Resolve("native/libproot.so"), LoaderPath = folder.Resolve("native/libproot-loader.so"),
            Rootfs = folder.Resolve("rootfs"), TemporaryDirectory = folder.Resolve("tmp"), SharedFolders = [projects],
        });

        Assert.Contains(projects, runtime.BuildArguments(new ProcessStartSpec { Program = "sh", WorkingDirectory = projects }));
        Assert.False(Directory.Exists(folder.Resolve("rootfs")));
    }
}
