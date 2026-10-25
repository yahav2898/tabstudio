using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.ViewModels;
using TabStudio.Link.Networking;
using TabStudio.LiveTesting.Results;
using TabStudio.Testing;
using static TabStudio.App.Tests.ViewModels.LiveTestingTests;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Live Unit Testing with a TabStudio Link host building: a host in the same process, on the loopback address,
/// compiles and instruments the pricing solution, and the tablet runs its xUnit tests and shows the markers. When the
/// host leaves, the tablet builds on its own.
/// </summary>
public sealed class LinkLiveTestingTests : IClassFixture<SharedToolchain>, IDisposable
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    private readonly SharedToolchain _toolchain;
    private readonly TempDirectory _temp = new();
    private readonly List<IDisposable> _disposables = [];

    public LinkLiveTestingTests(SharedToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    [AvaloniaFact]
    public async Task Markers_come_from_the_linked_hosts_builds_and_from_this_device_after_it_left()
    {
        var (workspace, link) = await OpenAsync();
        await using var host = link.CreateLoopbackHost("Test host");
        Assert.True(await link.PairAsync(host.Invitation), link.ClientError);
        var language = Assert.IsType<LinkedLanguageService>(workspace.LanguageService);
        await UiTestHelpers.WaitUntilAsync(() => language.IsLinked && workspace.IsLinkedToHost, RunTimeout, "the workspace to move to the host");
        var pricing = (await workspace.OpenDocumentAsync(_temp.Resolve(PricingSolution.LibraryFile)))!;
        var requests = host.RequestCount;

        workspace.LiveTesting.StartCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.LiveTesting.State == LiveTestingState.UpToDate
                && workspace.LiveTesting.PassedCount + workspace.LiveTesting.FailedCount == 4
                && pricing.LiveTestMarkers is { Count: 8 }
                && Markers(pricing).All(m => !m.Marker.IsStale),
            RunTimeout,
            "the first live run");

        // The same markers as without a host, from builds the host made: the link never fell back to this device.
        Assert.Equal(
            new[]
            {
                (PricingSolution.LineOf("if (quantity < 0)"), LineCoverageState.Covered),
                (PricingSolution.LineOf("throw new"), LineCoverageState.NotCovered),
                (PricingSolution.LineOf("return price * quantity;"), LineCoverageState.Covered),
                (PricingSolution.LineOf("if (total >= 100)"), LineCoverageState.Covered),
                (PricingSolution.LineOf("return total * 9 / 10;"), LineCoverageState.Covered),
                (PricingSolution.LineOf("return total;"), LineCoverageState.NotCovered),
                (PricingSolution.LineOf("var rate = 20;"), LineCoverageState.Failing),
                (PricingSolution.LineOf("return total * rate / 100;"), LineCoverageState.Failing),
            },
            Markers(pricing).Select(m => (m.Line, m.Marker.State)));
        Assert.Equal((3, 1), (workspace.LiveTesting.PassedCount, workspace.LiveTesting.FailedCount));
        Assert.True(language.IsLinked);
        Assert.True(host.RequestCount > requests, "The host built the tests.");

        // An edit runs the test that covered the line again, built on the host.
        var rateLine = PricingSolution.LineOf("var rate = 20;");
        var beforeEdit = host.RequestCount;
        pricing.Document.Replace(pricing.Document.Text.IndexOf("20;", StringComparison.Ordinal), 2, "17");
        await UiTestHelpers.WaitUntilAsync(
            () => MarkerOn(pricing, rateLine) is { State: LineCoverageState.Covered, IsStale: false } && workspace.LiveTesting.State == LiveTestingState.UpToDate,
            RunTimeout,
            "the tax test to run again and pass");
        Assert.Equal(TestState.Passed, Entry(workspace, "Tax_is_seventeen_percent").State);
        Assert.True(host.RequestCount > beforeEdit, "The host built the edit.");

        // The host leaves: the next edit is built on this device.
        await host.StopAsync();
        await UiTestHelpers.WaitUntilAsync(() => !language.IsLinked, RunTimeout, "the fallback to this device");
        pricing.Document.Replace(pricing.Document.Text.IndexOf("17;", StringComparison.Ordinal), 2, "20");
        await UiTestHelpers.WaitUntilAsync(
            () => MarkerOn(pricing, rateLine) is { State: LineCoverageState.Failing, IsStale: false } && workspace.LiveTesting.State == LiveTestingState.UpToDate,
            RunTimeout,
            "the tax test to run again on this device and fail");
        Assert.Equal(TestState.Failed, Entry(workspace, "Tax_is_seventeen_percent").State);
        Assert.NotNull(language.Local);
    }

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }

        _temp.Dispose();
    }

    /// <summary>The pricing solution in a workspace that follows TabStudio Link, as the app opens it.</summary>
    private async Task<(WorkspaceViewModel Workspace, LinkService Link)> OpenAsync()
    {
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var profile = DeviceProfile.For(device.Device);
        var link = Track(new LinkService(_toolchain.Paths, _toolchain.Compiler, device, profile, new TestDeviceIdentity("Test tablet", LinkDeviceKinds.Tablet), new AvaloniaUiDispatcher()));
        var dialog = Track(new LinkViewModel(link, new NoQrScanner()));
        var workspace = Track(new WorkspaceViewModel(
            _toolchain.Compiler, device, profile, new AvaloniaUiDispatcher(), new SessionStore(_toolchain.Paths), _toolchain.Paths, link: link, linkDialog: dialog));

        await workspace.LoadAsync(PricingSolution.Write(_temp));
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.TotalCount == 4, because: "the tests found in source");
        return (workspace, link);
    }

    private T Track<T>(T disposable)
        where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }
}
