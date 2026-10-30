using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.Toolchains.Tests.Installation;

/// <summary>Installing, updating and removing toolchains on a fake tablet against a fake Alpine mirror.</summary>
public sealed class ToolchainManagerTests : IDisposable
{
    private readonly FakeDevice _device = new();

    private ToolchainManager Manager => _device.Manager;

    [Fact]
    public async Task Architecture_specific_setup_never_downloads_or_extracts_another_CPUs_tools()
    {
        var toolchain = _device.Hello with
        {
            Setup = [
                new TabStudio.Toolchains.Catalog.SetupStepDefinition
                {
                    Name = "This architecture",
                    Architectures = [FakeAlpineRepository.Architecture],
                    File = new TabStudio.Toolchains.Catalog.FileDefinition { Path = "/opt/chosen-architecture", Content = "chosen" },
                },
                new TabStudio.Toolchains.Catalog.SetupStepDefinition
                {
                    Name = "Wrong architecture",
                    Architectures = ["armv7"],
                    Archive = new TabStudio.Toolchains.Catalog.ArchiveDefinition
                    {
                        Url = new Uri("https://downloads.example.org/must-not-download.zip"),
                        Sha256 = new string('0', 64), Destination = "/opt/wrong-architecture",
                    },
                },
            ],
        };
        await _device.Installer.InstallAsync(toolchain, null, CancellationToken.None);
        Assert.Equal("chosen", File.ReadAllText(_device.Rootfs("/opt/chosen-architecture")));
        Assert.False(Directory.Exists(_device.Rootfs("/opt/wrong-architecture")));
    }

    [Fact]
    public async Task An_update_whose_archive_fails_to_unpack_keeps_the_installed_copy()
    {
        // The installed debugger was deleted before the new one was unpacked: a failed or cancelled update left the
        // toolchain without it while the Store still called it ready.
        await Manager.InstallAsync("hello");
        var adapter = _device.Rootfs("/opt/tabstudio/hello/hello-debugger/adapter.js");
        Assert.True(File.Exists(adapter));

        byte[] broken = [.. "not an archive at all"u8];
        var address = new Uri("https://downloads.example.org/hello-debugger-2.0.tar.gz");
        _device.Mirror.Server.Add(address, broken);
        var step = _device.Hello.Setup[0];
        var update = _device.Hello with
        {
            Setup = [step with { Archive = step.Archive! with { Url = address, Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(broken)), Size = broken.Length } }],
        };

        await Assert.ThrowsAsync<ToolchainInstallException>(() => _device.Installer.InstallAsync(update, null, CancellationToken.None));

        Assert.True(File.Exists(adapter));
        Assert.False(Directory.Exists(_device.Rootfs("/opt/tabstudio/hello.tabstudio-new")));
    }

    [Fact]
    public async Task Before_the_first_install_estimates_and_says_linux_will_be_set_up()
    {
        await Manager.RefreshAsync();

        var status = Manager.Status("hello");
        Assert.Equal(ToolchainState.NotInstalled, status.State);
        Assert.False(status.SizesAreExact);
        Assert.Equal(1000 + _device.Mirror.Rootfs.Length, status.DownloadSize);
        Assert.Equal("Sets up Alpine Linux 3.24 the first time.", status.Note);
    }

    [Fact]
    public async Task Installs_a_toolchain_with_its_dependencies_and_setup()
    {
        var reports = new List<InstallProgress>();
        Manager.StatusChanged += (_, status) =>
        {
            if (status.Toolchain.Id == "hello" && status.Progress is { } progress)
            {
                lock (reports)
                {
                    reports.Add(progress);
                }
            }
        };

        await Manager.InstallAsync("hello");

        var status = Manager.Status("hello");
        Assert.Equal(ToolchainState.Installed, status.State);
        Assert.Equal("2.0", status.Version);
        Assert.True(status.InstalledSize > 0);
        Assert.Null(status.Error);
        Assert.True(_device.Environment.IsSetUp);
        Assert.Equal("nameserver 192.0.2.53\n", File.ReadAllText(_device.Rootfs("/etc/resolv.conf")));
        Assert.Equal("https://mirror.example.org/alpine/v3.24/main\nhttps://mirror.example.org/alpine/v3.24/community\n", File.ReadAllText(_device.Rootfs("/etc/apk/repositories")));
        Assert.True(File.Exists(_device.Rootfs("/usr/bin/hello")));
        Assert.True(File.Exists(_device.Rootfs("/usr/lib/libhello.so.1")));
        Assert.True(File.Exists(_device.Rootfs("/usr/bin/hello-lint")));
        Assert.Equal("console.log('adapter');\n", File.ReadAllText(_device.Rootfs("/opt/tabstudio/hello/hello-debugger/adapter.js")));
        Assert.Equal(["add main,community hello hello-tools"], _device.Apk.Calls);

        // The downloads were installed and are gone.
        Assert.False(Directory.Exists(_device.Rootfs(AlpinePackageManager.LocalRepository)));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_device.Environment.Downloads, "archives")));

        InstallStage[] stages;
        lock (reports)
        {
            stages = [.. reports.Select(r => r.Stage).Distinct()];
        }

        Assert.Equal([InstallStage.Preparing, InstallStage.SettingUpLinux, InstallStage.Resolving, InstallStage.Downloading, InstallStage.Installing, InstallStage.Configuring, InstallStage.Completed], stages);
        Assert.All(reports.Where(r => r.Fraction is not null), r => Assert.InRange(r.Fraction!.Value, 0, 1));
    }

    [Fact]
    public async Task Exact_sizes_come_from_the_index_once_it_was_read()
    {
        await Manager.InstallAsync("shared");

        await Manager.RefreshAsync();

        var hello = Manager.Status("hello");
        Assert.Equal(ToolchainState.NotInstalled, hello.State);
        Assert.True(hello.SizesAreExact);
        Assert.Equal("2.0", hello.AvailableVersion);
        Assert.Equal(_device.Archive.Length + PackageSize("hello-tools"), hello.DownloadSize);
        Assert.Null(hello.Note);
    }

    [Fact]
    public async Task Continues_a_package_download_after_the_connection_breaks()
    {
        var package = await PrepareAsync();
        _device.Mirror.Server.BreakAfter(new Uri(package.Address), 100);

        await Manager.InstallAsync("hello");

        Assert.Equal(ToolchainState.Installed, Manager.Status("hello").State);
        Assert.Contains(_device.Mirror.Server.Requests, r => r.StartsWith($"GET {package.Address} (100-)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refuses_a_package_that_does_not_match_the_signed_index()
    {
        var package = await PrepareAsync();
        var tampered = package.Bytes.ToArray();
        tampered[^12] ^= 0x5a;
        _device.Mirror.Server.Add(new Uri(package.Address), tampered);

        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => Manager.InstallAsync("hello"));

        Assert.Equal(InstallFailure.IntegrityFailed, error.Failure);
        Assert.Contains("did not download correctly", error.Message, StringComparison.Ordinal);
        Assert.Empty(_device.Apk.Calls);
        var status = Manager.Status("hello");
        Assert.Equal(ToolchainState.NotInstalled, status.State);
        Assert.Equal(error.Message, status.Error);
    }

    [Fact]
    public async Task Says_when_the_tablet_is_offline()
    {
        _device.Mirror.Server.Offline = true;

        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => Manager.InstallAsync("hello"));

        Assert.Equal(InstallFailure.NoNetwork, error.Failure);
        Assert.Contains("offline", error.Message, StringComparison.Ordinal);
        Assert.False(_device.Environment.IsSetUp);
    }

    [Fact]
    public async Task Says_when_there_is_not_enough_space_before_downloading()
    {
        await PrepareAsync();
        _device.Mirror.Server.Requests.Clear();
        _device.FreeSpace = 10_000_000;

        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => Manager.InstallAsync("hello"));

        Assert.Equal(InstallFailure.NotEnoughSpace, error.Failure);
        Assert.Contains("10 MB free", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_device.Mirror.Server.Requests, r => r.Contains(".apk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Storage_filling_up_during_an_install_says_so_and_leaves_the_toolchain_ready_to_install_again()
    {
        // An IOException the steps did not describe left the toolchain "Installing" until a restart, and reached
        // the Store's button as an error that ended the app.
        await PrepareAsync();
        _device.Apk.ThrowNextAdd = new IOException("No space left on device", 28);

        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => Manager.InstallAsync("hello"));

        Assert.Equal(InstallFailure.NotEnoughSpace, error.Failure);
        Assert.Contains("storage filled up", error.Message, StringComparison.Ordinal);
        var status = Manager.Status("hello");
        Assert.False(status.IsBusy);
        Assert.Equal(ToolchainState.NotInstalled, status.State);
        Assert.Equal(error.Message, status.Error);

        await Manager.InstallAsync("hello");
        Assert.Equal(ToolchainState.Installed, Manager.Status("hello").State);
    }

    [Fact]
    public async Task Says_which_package_the_distribution_lacks()
    {
        await PrepareAsync();
        _device.Mirror.Add("main", "hello", "2.0-r1", depends: ["libgoodbye"]);

        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => Manager.InstallAsync("hello"));

        Assert.Equal(InstallFailure.PackageNotFound, error.Failure);
        Assert.Contains("libgoodbye", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_what_apk_said_when_it_fails()
    {
        await PrepareAsync();
        _device.Apk.FailNextAdd = "ERROR: hello-2.0-r1: trying to overwrite usr/bin/hello owned by busybox-1.37.0-r30.";

        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => Manager.InstallAsync("hello"));

        Assert.Equal(InstallFailure.CommandFailed, error.Failure);
        Assert.Contains("trying to overwrite usr/bin/hello", error.Message, StringComparison.Ordinal);
        Assert.Contains("ERROR:", error.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_leaves_the_toolchain_ready_to_install_again()
    {
        await PrepareAsync();
        _device.Mirror.Server.Latency = TimeSpan.FromSeconds(30);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Manager.InstallAsync("hello", cancellation.Token));

        Assert.Equal(ToolchainState.NotInstalled, Manager.Status("hello").State);
        Assert.Null(Manager.Status("hello").Error);
        _device.Mirror.Server.Latency = TimeSpan.Zero;
        await Manager.InstallAsync("hello");
        Assert.Equal(ToolchainState.Installed, Manager.Status("hello").State);
    }

    [Fact]
    public async Task Removes_a_toolchain_and_its_setup_but_keeps_what_another_toolchain_needs()
    {
        await Manager.InstallAsync("hello");
        await Manager.InstallAsync("shared");

        await Manager.RemoveAsync("hello");

        Assert.Equal(ToolchainState.NotInstalled, Manager.Status("hello").State);
        Assert.Equal(ToolchainState.Installed, Manager.Status("shared").State);
        Assert.Equal("del hello-tools", _device.Apk.Calls[^1]);
        Assert.False(File.Exists(_device.Rootfs("/usr/bin/hello-lint")));
        Assert.True(File.Exists(_device.Rootfs("/usr/bin/hello")));
        Assert.False(Directory.Exists(_device.Rootfs("/opt/tabstudio/hello")));
    }

    [Fact]
    public async Task Offers_and_installs_an_update_when_the_mirror_has_a_newer_version()
    {
        await Manager.InstallAsync("hello");
        _device.Mirror.Add("main", "hello", "2.1-r0", new Dictionary<string, string> { ["usr/bin/hello"] = "#!/bin/sh\necho hello 2.1\n" }, depends: ["libhello"]);

        await Manager.RefreshAsync(checkForUpdates: true);

        var available = Manager.Status("hello");
        Assert.Equal(ToolchainState.UpdateAvailable, available.State);
        Assert.Equal("2.0", available.Version);
        Assert.Equal("2.1", available.AvailableVersion);
        Assert.Equal(PackageSize("hello"), available.DownloadSize);

        await Manager.UpdateAsync("hello");

        var updated = Manager.Status("hello");
        Assert.Equal(ToolchainState.Installed, updated.State);
        Assert.Equal("2.1", updated.Version);
        Assert.Equal("add --upgrade main,community hello hello-tools", _device.Apk.Calls[^1]);
        Assert.Contains("hello 2.1", File.ReadAllText(_device.Rootfs("/usr/bin/hello")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Measures_the_space_toolchains_take()
    {
        await Manager.InstallAsync("hello");

        var usage = Manager.DiskUsage;

        var hello = Manager.Status("hello");
        Assert.True(usage.Toolchains > 0);
        Assert.Equal(hello.InstalledSize, usage.Toolchains);
        Assert.Equal(DiskSpace.Size(_device.Environment.Rootfs) + DiskSpace.Size(_device.Environment.Downloads), usage.Total);
        Assert.Equal(_device.FreeSpace, usage.Free);

        await Manager.RemoveAsync("hello");
        Assert.Equal(0, Manager.DiskUsage.Toolchains);
    }

    [Fact]
    public async Task Runs_one_operation_at_a_time()
    {
        _device.Mirror.Server.Latency = TimeSpan.FromMilliseconds(50);

        var first = Manager.InstallAsync("hello");
        var second = Manager.InstallAsync("shared");
        await Task.WhenAll(first, second);

        Assert.True(Manager.Status("hello").IsReady);
        Assert.True(Manager.Status("shared").IsReady);
    }

    public void Dispose() => _device.Dispose();

    /// <summary>Sets up the environment without installing a toolchain, as after a failed first attempt.</summary>
    private async Task<FakePackage> PrepareAsync()
    {
        await _device.Environment.SetUpAsync(null, CancellationToken.None);
        return _device.Mirror.Add("main", "hello", "2.0-r1", new Dictionary<string, string> { ["usr/bin/hello"] = "#!/bin/sh\necho hello\n" }, depends: ["libhello"]);
    }

    private long PackageSize(string name) => _device.Mirror.Package(name).Bytes.Length;
}
