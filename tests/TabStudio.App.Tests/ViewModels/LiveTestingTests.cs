using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.LiveTesting.Results;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Live Unit Testing in the IDE against a real xUnit project, restored from nuget.org: markers on the right lines,
/// stale markers while typing, the impacted tests run again, the marker popup, pausing and excluding.
/// </summary>
public sealed class LiveTestingTests : IClassFixture<SharedToolchain>, IDisposable
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    private readonly SharedToolchain _toolchain;
    private readonly TempDirectory _temp = new();
    private readonly List<WorkspaceViewModel> _workspaces = [];

    public LiveTestingTests(SharedToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    [AvaloniaFact]
    public async Task Starting_shows_green_red_and_blue_markers_on_the_right_lines()
    {
        var (workspace, pricing) = await StartAsync();

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
        Assert.All(Markers(pricing), m => Assert.False(m.Marker.IsStale));

        var live = workspace.LiveTesting;
        Assert.Equal((3, 1), (live.PassedCount, live.FailedCount));
        Assert.Equal("Live Unit Testing: 3 passed, 1 failed", live.StatusText);
        Assert.Equal(TestState.Failed, workspace.Tests.Entries.Single(e => e.MethodName == "Tax_is_seventeen_percent").State);
        Assert.Equal(3, workspace.Tests.PassedCount);
        Assert.Equal(IdeState.Ready, workspace.State);
    }

    [AvaloniaFact]
    public async Task Editing_a_covered_line_marks_it_stale_then_runs_only_its_tests_again()
    {
        var (workspace, pricing) = await StartAsync();
        var totalResult = Entry(workspace, "Total_multiplies_price_and_quantity").Result;
        var rateLine = PricingSolution.LineOf("var rate = 20;");

        var text = pricing.Document.Text;
        pricing.Document.Replace(text.IndexOf("20;", StringComparison.Ordinal), 2, "17");

        var edited = MarkerOn(pricing, rateLine);
        Assert.True(edited.IsStale);
        Assert.Equal(LineCoverageState.Failing, edited.State);

        // The test that ran the line runs again, so the other line it ran is out of date too; other tests' lines are not.
        Assert.True(MarkerOn(pricing, rateLine + 1).IsStale);
        Assert.False(MarkerOn(pricing, PricingSolution.LineOf("return price * quantity;")).IsStale);

        await UiTestHelpers.WaitUntilAsync(
            () => MarkerOn(pricing, rateLine) is { State: LineCoverageState.Covered, IsStale: false } && workspace.LiveTesting.State == LiveTestingState.UpToDate,
            RunTimeout,
            "the tax test to run again and pass");

        Assert.Equal(LineCoverageState.Covered, MarkerOn(pricing, rateLine + 1).State);
        Assert.Equal(TestState.Passed, Entry(workspace, "Tax_is_seventeen_percent").State);
        Assert.Same(totalResult, Entry(workspace, "Total_multiplies_price_and_quantity").Result);
        Assert.Equal((4, 0), (workspace.LiveTesting.PassedCount, workspace.LiveTesting.FailedCount));
    }

    [AvaloniaFact]
    public async Task Markers_follow_their_lines_while_typing()
    {
        var (_, pricing) = await StartAsync();
        var before = Markers(pricing).Select(m => (m.Line + 2, m.Marker.State)).ToList();

        pricing.Document.Insert(pricing.Document.Text.IndexOf("namespace Shop;", StringComparison.Ordinal), "// Prices.\n\n");

        Assert.Equal(before, Markers(pricing).Select(m => (m.Line, m.Marker.State)));
        Assert.All(Markers(pricing), m => Assert.False(m.Marker.IsStale));
    }

    [AvaloniaFact]
    public async Task The_marker_popup_lists_the_covering_tests_and_opens_or_runs_them()
    {
        var (workspace, pricing) = await StartAsync();
        var line = PricingSolution.LineOf("return total * 9 / 10;");

        var popup = new LiveTestPopupViewModel(line, MarkerOn(pricing, line), workspace.LiveTesting);

        Assert.Equal("Covered by 2 tests", popup.Title);
        Assert.All(popup.Tests, t => Assert.Equal("Discount_takes_ten_percent_off_large_orders", t.Name));
        Assert.Equal(
            new[] { "PricingTests · total: 100, expected: 90", "PricingTests · total: 200, expected: 180" },
            popup.Tests.Select(t => t.Detail));
        Assert.All(popup.Tests, t => Assert.Equal(TestState.Passed, t.State));
        Assert.True(popup.CanRunAll);

        await ((IAsyncRelayCommand)popup.Tests[0].GoToCommand).ExecuteAsync(null);
        Assert.Equal(_temp.Resolve(PricingSolution.TestFile), workspace.ActiveDocument?.FilePath);
        Assert.Equal(popup.Tests[0].Test.Key.TestId, workspace.Tests.SelectedNode?.Entry?.Id);

        var before = Entry(workspace, "Total_multiplies_price_and_quantity").Result;
        var runFirst = popup.Tests[0].Test.Key;
        var firstBefore = workspace.Tests.Entries.Single(e => e.Id == runFirst.TestId).Result;
        await ((IAsyncRelayCommand)popup.Tests[0].RunCommand).ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(
            () => !ReferenceEquals(workspace.Tests.Entries.Single(e => e.Id == runFirst.TestId).Result, firstBefore) && workspace.LiveTesting.State == LiveTestingState.UpToDate,
            RunTimeout,
            "the test to run again");
        Assert.Same(before, Entry(workspace, "Total_multiplies_price_and_quantity").Result);

        var uncovered = new LiveTestPopupViewModel(PricingSolution.LineOf("return total;"), MarkerOn(pricing, PricingSolution.LineOf("return total;")), workspace.LiveTesting);
        Assert.Equal("Not covered by any test", uncovered.Title);
        Assert.False(uncovered.HasTests);
    }

    [AvaloniaFact]
    public async Task An_excluded_test_never_runs_live_and_the_choice_is_remembered()
    {
        var (workspace, pricing) = await StartAsync();
        var taxNode = workspace.Tests.Rows.Single(r => r.Name == "Tax_is_seventeen_percent");
        var taxResult = Entry(workspace, "Tax_is_seventeen_percent").Result;
        var rateLine = PricingSolution.LineOf("var rate = 20;");

        taxNode.ExcludeFromLiveTestingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => MarkerOn(pricing, rateLine).State == LineCoverageState.NotCovered, RunTimeout, "the tax lines to lose their only test");
        await UiTestHelpers.WaitUntilAsync(() => Row(workspace, "Tax_is_seventeen_percent").IsExcludedFromLiveTesting, because: "the row to show it is excluded");
        Assert.False(Row(workspace, "Total_multiplies_price_and_quantity").IsExcludedFromLiveTesting);
        Assert.False(Row(workspace, "PricingTests").IsExcludedFromLiveTesting);

        pricing.Document.Replace(pricing.Document.Text.IndexOf("20;", StringComparison.Ordinal), 2, "17");
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.LiveTesting.State == LiveTestingState.UpToDate && !MarkerOn(pricing, rateLine).IsStale,
            RunTimeout,
            "the live run after the edit");

        Assert.Same(taxResult, Entry(workspace, "Tax_is_seventeen_percent").Result);
        Assert.Equal(LineCoverageState.NotCovered, MarkerOn(pricing, rateLine).State);
        var saved = new LiveTestingSettingsStore(_toolchain.Paths).Load(workspace.Workspace!.Solution!.FilePath);
        var rule = Assert.Single(saved!.Rules);
        Assert.Equal(("Shop.Tests.PricingTests.Tax_is_seventeen_percent", false), (rule.Name, rule.Include));

        // Reopening the solution brings the exclusion back.
        var reopened = await OpenAsync();
        Assert.False(reopened.LiveTesting.IsIncluded(rule.ProjectFilePath, "Shop.Tests.PricingTests.Tax_is_seventeen_percent"));
        Assert.True(reopened.LiveTesting.IsIncluded(rule.ProjectFilePath, "Shop.Tests.PricingTests.Total_multiplies_price_and_quantity"));
    }

    [AvaloniaFact]
    public async Task Excluding_the_code_under_test_removes_its_markers()
    {
        var (workspace, pricing) = await StartAsync();
        var library = workspace.Explorer.Rows.Single(r => r.IsProject && r.Name == "Shop");

        Assert.True(library.CanSetLiveTesting);
        library.ExcludeFromLiveTestingCommand.Execute(null);

        await UiTestHelpers.WaitUntilAsync(() => pricing.LiveTestMarkers is null && workspace.LiveTesting.State == LiveTestingState.UpToDate, RunTimeout, "the markers to go");
        library.IncludeInLiveTestingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => pricing.LiveTestMarkers is { Count: 8 } && workspace.LiveTesting.State == LiveTestingState.UpToDate, RunTimeout, "the markers to come back");
    }

    [AvaloniaFact]
    public async Task Pausing_keeps_the_markers_and_stopping_removes_them()
    {
        var (workspace, pricing) = await StartAsync();
        var live = workspace.LiveTesting;
        var taxResult = Entry(workspace, "Tax_is_seventeen_percent").Result;

        live.PauseCommand.Execute(null);
        pricing.Document.Replace(pricing.Document.Text.IndexOf("20;", StringComparison.Ordinal), 2, "17");
        await Task.Delay(TimeSpan.FromSeconds(2));
        await UiTestHelpers.WaitUntilAsync(() => true);

        Assert.Equal(LiveTestingState.Paused, live.State);
        Assert.Equal("Live Unit Testing paused", live.StatusText);
        Assert.Same(taxResult, Entry(workspace, "Tax_is_seventeen_percent").Result);
        Assert.NotNull(pricing.LiveTestMarkers);

        live.ContinueCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => Entry(workspace, "Tax_is_seventeen_percent").State == TestState.Passed && live.State == LiveTestingState.UpToDate, RunTimeout, "the run after Continue");

        live.StopCommand.Execute(null);
        Assert.Equal(LiveTestingState.Off, live.State);
        Assert.Null(pricing.LiveTestMarkers);
        Assert.True(live.StartCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Live_runs_give_way_to_the_Test_Explorer()
    {
        var (workspace, _) = await StartAsync();

        await workspace.Tests.RunAllCommand.ExecuteAsync(null);

        Assert.Equal((3, 1), (workspace.Tests.PassedCount, workspace.Tests.FailedCount));
        await UiTestHelpers.WaitUntilAsync(() => workspace.LiveTesting.State == LiveTestingState.UpToDate, RunTimeout, "Live Unit Testing to settle");
        Assert.True(workspace.LiveTesting.IsActive);
    }

    [AvaloniaFact]
    public async Task A_hot_device_holds_runs_back_until_it_cools_down()
    {
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var (workspace, pricing) = await StartAsync(device);
        var live = workspace.LiveTesting;

        device.SetThermal(ThermalState.Hot);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsThrottled, because: "the thermal state");
        pricing.Document.Replace(pricing.Document.Text.IndexOf("20;", StringComparison.Ordinal), 2, "17");
        await UiTestHelpers.WaitUntilAsync(() => live.State == LiveTestingState.WaitingForDevice, RunTimeout, "the run to wait");

        Assert.Equal(TestState.Failed, Entry(workspace, "Tax_is_seventeen_percent").State);
        Assert.Equal("Live Unit Testing waits for the device to cool down", live.StatusText);

        device.SetThermal(ThermalState.Normal);
        await UiTestHelpers.WaitUntilAsync(
            () => Entry(workspace, "Tax_is_seventeen_percent").State == TestState.Passed && live.State == LiveTestingState.UpToDate,
            RunTimeout,
            "the run once the device cooled down");
    }

    [AvaloniaFact]
    public async Task On_a_compact_device_tests_run_when_a_file_is_saved()
    {
        var device = new DefaultDeviceStatus(new DeviceInfo(2L * 1024 * 1024 * 1024, 4, IsLowRamDevice: true));
        var (workspace, pricing) = await StartAsync(device);
        var live = workspace.LiveTesting;
        Assert.True(live.RunsOnSave);

        pricing.Document.Replace(pricing.Document.Text.IndexOf("20;", StringComparison.Ordinal), 2, "17");
        await Task.Delay(TimeSpan.FromSeconds(3));
        await UiTestHelpers.WaitUntilAsync(() => true);

        Assert.Equal(LiveTestingState.Pending, live.State);
        Assert.Equal("Live Unit Testing: save to run tests", live.StatusText);
        Assert.Equal(TestState.Failed, Entry(workspace, "Tax_is_seventeen_percent").State);

        workspace.ActiveDocument = pricing;
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(
            () => Entry(workspace, "Tax_is_seventeen_percent").State == TestState.Passed && live.State == LiveTestingState.UpToDate,
            RunTimeout,
            "the run after saving");
    }

    public void Dispose()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }

        _temp.Dispose();
    }

    internal static TestEntry Entry(WorkspaceViewModel workspace, string methodName) =>
        workspace.Tests.Entries.Single(e => e.MethodName == methodName);

    private static TestNodeViewModel Row(WorkspaceViewModel workspace, string name) =>
        workspace.Tests.Rows.Single(r => r.Name == name);

    internal static IReadOnlyList<(int Line, LiveTestMarker Marker)> Markers(DocumentViewModel document) =>
        document.LiveTestMarkers?.Lines(document.Document) ?? [];

    internal static LiveTestMarker MarkerOn(DocumentViewModel document, int line) =>
        Assert.Single(Markers(document), m => m.Line == line).Marker;

    private async Task<(WorkspaceViewModel Workspace, DocumentViewModel Pricing)> StartAsync(DefaultDeviceStatus? device = null)
    {
        var workspace = await OpenAsync(device);
        var pricing = (await workspace.OpenDocumentAsync(_temp.Resolve(PricingSolution.LibraryFile)))!;
        workspace.LiveTesting.StartCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.LiveTesting.State == LiveTestingState.UpToDate
                && workspace.LiveTesting.PassedCount + workspace.LiveTesting.FailedCount == 4
                && pricing.LiveTestMarkers is { Count: 8 }
                && Markers(pricing).All(m => !m.Marker.IsStale),
            RunTimeout,
            "the first live run");
        return (workspace, pricing);
    }

    private async Task<WorkspaceViewModel> OpenAsync(DefaultDeviceStatus? device = null)
    {
        device ??= new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var workspace = new WorkspaceViewModel(
            _toolchain.Compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(_toolchain.Paths), _toolchain.Paths);
        _workspaces.Add(workspace);

        var solution = _temp.Resolve(PricingSolution.Folder + "/Shop.slnx");
        await workspace.LoadAsync(File.Exists(solution) ? solution : PricingSolution.Write(_temp));
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.TotalCount == 4, because: "the tests found in source");
        return workspace;
    }
}
