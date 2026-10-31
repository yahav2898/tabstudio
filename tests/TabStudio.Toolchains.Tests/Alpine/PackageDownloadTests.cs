using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.Toolchains.Tests.Alpine;

public sealed class PackageDownloadTests
{
    [Fact]
    public async Task Downloads_overlap_with_bounded_connections_and_every_package_still_verifies()
    {
        using var device = new FakeDevice();
        var names = Enumerable.Range(0, 9).Select(i => "parallel-" + i).ToArray();
        foreach (var name in names)
        {
            device.Mirror.Add("main", name, "1.0-r0");
        }

        await device.Environment.SetUpAsync(null, CancellationToken.None);
        var plan = await device.Packages.PlanInstallAsync(names, CancellationToken.None);
        device.Mirror.Server.Latency = TimeSpan.FromMilliseconds(40);
        var progress = new List<long>();

        await device.Packages.DownloadAsync(plan, (bytes, _) => progress.Add(bytes), CancellationToken.None);

        Assert.InRange(device.Mirror.Server.PeakConcurrentRequests, 2, 4);
        Assert.All(progress, bytes => Assert.InRange(bytes, 0, plan.DownloadSize));
        Assert.Equal(plan.DownloadSize, progress[^1]);
        foreach (var package in plan.Packages)
        {
            var file = device.Rootfs($"{AlpinePackageManager.LocalRepository}/{package.Repository}/{FakeAlpineRepository.Architecture}/{package.FileName}");
            Assert.Null(ApkSignature.VerifyPackage(file, package));
        }
    }

    [Fact]
    public async Task A_sign_in_page_from_the_network_keeps_the_last_good_index()
    {
        // A captive portal answers every request with 200 and its HTML: written over the index before its check, it
        // was then deleted, and the toolchains could no longer be installed offline either.
        using var device = new FakeDevice();
        device.Mirror.Add("main", "kept", "1.0-r0");
        await device.Environment.SetUpAsync(null, CancellationToken.None);
        await device.Packages.LoadIndexAsync(refresh: true, CancellationToken.None);
        foreach (var repository in device.Environment.Distribution.Repositories)
        {
            device.Mirror.Server.Add(device.Mirror.IndexAddress(repository), System.Text.Encoding.UTF8.GetBytes("<html><body>Sign in to Cafe Wi-Fi</body></html>"));
        }

        var index = await device.Packages.LoadIndexAsync(refresh: true, CancellationToken.None);

        Assert.NotEmpty(index.Candidates(ApkDependency.Parse("kept")));
    }
}
