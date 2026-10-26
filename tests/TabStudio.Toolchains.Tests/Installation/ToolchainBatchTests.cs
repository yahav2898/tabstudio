using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Runtimes;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.Toolchains.Tests.Installation;

public sealed class ToolchainBatchTests
{
    [Fact]
    public async Task Installs_every_distinct_toolchain_and_measures_the_shared_tree_once()
    {
        using var device = new FakeDevice();
        var installer = new MeasuredInstaller(device.Installer);
        using var manager = new ToolchainManager(device.Catalog, installer);

        await manager.InstallManyAsync(["shared", "hello", "shared"]);

        Assert.True(manager.IsReady("shared"));
        Assert.True(manager.IsReady("hello"));
        Assert.Equal(2, device.Apk.Calls.Count);
        Assert.Equal(1, installer.Measurements);
        Assert.True(manager.DiskUsage.Toolchains > 0);
        Assert.True(File.Exists(device.Rootfs("/usr/bin/hello-lint")));
    }

    [Fact]
    public async Task A_failed_later_install_keeps_completed_toolchains_and_their_disk_usage()
    {
        using var device = new FakeDevice();
        var bad = device.Mirror.Package("hello-tools");
        var tampered = bad.Bytes.ToArray();
        tampered[^12] ^= 0x5a;
        device.Mirror.Server.Add(new Uri(bad.Address), tampered);
        var installer = new MeasuredInstaller(device.Installer);
        using var manager = new ToolchainManager(device.Catalog, installer);

        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => manager.InstallManyAsync(["shared", "hello"]));

        Assert.Equal(InstallFailure.IntegrityFailed, error.Failure);
        Assert.True(manager.IsReady("shared"));
        Assert.False(manager.IsReady("hello"));
        Assert.Single(device.Apk.Calls);
        Assert.Equal(1, installer.Measurements);
        Assert.True(manager.DiskUsage.Toolchains > 0);
    }

    [Fact]
    public async Task Cancellation_stops_the_next_install_and_keeps_the_completed_one()
    {
        using var device = new FakeDevice();
        var installer = new MeasuredInstaller(device.Installer);
        using var manager = new ToolchainManager(device.Catalog, installer);
        using var stop = new CancellationTokenSource();
        manager.StatusChanged += (_, status) =>
        {
            if (status.Toolchain.Id == "hello" && status.State == ToolchainState.Installing)
            {
                stop.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.InstallManyAsync(["shared", "hello"], stop.Token));

        Assert.True(manager.IsReady("shared"));
        Assert.False(manager.IsReady("hello"));
        Assert.Single(device.Apk.Calls);
        Assert.Equal(1, installer.Measurements);
        Assert.True(manager.DiskUsage.Toolchains > 0);
    }

    [Fact]
    public async Task An_unknown_id_is_rejected_before_any_install()
    {
        using var device = new FakeDevice();
        var installer = new MeasuredInstaller(device.Installer);
        using var manager = new ToolchainManager(device.Catalog, installer);

        await Assert.ThrowsAsync<ArgumentException>(() => manager.InstallManyAsync(["hello", "missing"]));

        Assert.Empty(device.Apk.Calls);
        Assert.Equal(0, installer.Measurements);
    }

    private sealed class MeasuredInstaller(IToolchainInstaller inner) : IToolchainInstaller
    {
        public int Measurements { get; private set; }
        public IToolchainRuntime Runtime => inner.Runtime;
        public bool CanInstall => inner.CanInstall;
        public string? Problem => inner.Problem;
        public bool IsRuntimeReady => inner.IsRuntimeReady;
        public Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => inner.PrepareRuntimeAsync(progress, cancellationToken);
        public Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains, bool checkForUpdates, CancellationToken cancellationToken) => inner.ReadStatusAsync(toolchains, checkForUpdates, cancellationToken);
        public Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => inner.InstallAsync(toolchain, progress, cancellationToken);
        public Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => inner.UpdateAsync(toolchain, progress, cancellationToken);
        public Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => inner.RemoveAsync(toolchain, installed, progress, cancellationToken);
        public DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains)
        {
            Measurements++;
            return inner.MeasureDiskUsage(toolchains);
        }
    }
}
