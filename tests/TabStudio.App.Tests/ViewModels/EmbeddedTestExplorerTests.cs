using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;
using TabStudio.Toolchains.Embedded.Testing;

namespace TabStudio.App.Tests.ViewModels;

public sealed class EmbeddedTestExplorerTests
{
    [Fact]
    public void Board_results_use_the_existing_Test_Explorer_counts_traits_and_clickable_locations()
    {
        var explorer = new TestExplorerViewModel(new NoRuns(), new Immediate());
        var project = new ProjectInfo { Name = "sensor", FilePath = "/work/sensor/platformio.ini", Kind = ProjectKind.EmbeddedFirmware };
        explorer.ReportEmbeddedResults(project,
        [
            new("board-A", EmbeddedTestFormat.Unity, "reads_sensor", EmbeddedTestOutcome.Passed, File: "test/sensor.c", Line: 12),
            new("board-B", EmbeddedTestFormat.Unity, "reads_sensor", EmbeddedTestOutcome.Failed, "expected ready", "test/sensor.c", 12),
            new("board-A", EmbeddedTestFormat.Unity, "optional", EmbeddedTestOutcome.Skipped, "no fixture"),
        ]);
        Assert.Equal((3, 1, 1, 1), (explorer.TotalCount, explorer.PassedCount, explorer.FailedCount, explorer.SkippedCount));
        Assert.Equal(3, explorer.Entries.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(explorer.Entries, e => e.DisplayName == "reads_sensor [board-B]" && e.State == TestState.Failed);
        var failed = explorer.Entries.Single(e => e.State == TestState.Failed);
        Assert.Equal("/work/sensor/test/sensor.c", failed.Result!.FailureLocation!.FilePath);
        Assert.Contains(failed.Result.Test.Traits, t => t.Name == "Board" && t.Value == "board-B");
        Assert.Equal(TestFramework.External, failed.Result.Test.Framework);
    }

    [Fact]
    public void A_retransmitted_board_result_updates_one_entry()
    {
        var explorer = new TestExplorerViewModel(new NoRuns(), new Immediate());
        var project = new ProjectInfo { Name = "sensor", FilePath = "/work/sensor/platformio.ini" };
        var result = new EmbeddedTestResult("board-A", EmbeddedTestFormat.Tap, "one", EmbeddedTestOutcome.Passed);
        explorer.ReportEmbeddedResults(project, [result, result with { Outcome = EmbeddedTestOutcome.Failed, Message = "latest result" }]);
        Assert.Equal(TestState.Failed, Assert.Single(explorer.Entries).State);
        Assert.Equal(1, explorer.FailedCount);
    }

    [Fact]
    public void Transport_failures_are_visible_in_the_same_test_tree()
    {
        var explorer = new TestExplorerViewModel(new NoRuns(), new Immediate());
        var project = new ProjectInfo { Name = "sensor", FilePath = "/work/sensor/platformio.ini" };
        var parser = new EmbeddedTestOutputParser("board-A", EmbeddedTestFormat.DefmtTest);
        parser.Feed("(1/1) running `sensor`...");
        explorer.ReportEmbeddedResults(project, parser.Complete(false));
        Assert.True(explorer.HasFailures);
        Assert.DoesNotContain(explorer.Entries, e => e.State == TestState.Passed);
    }

    [Fact]
    public void Importing_a_second_board_keeps_the_first_boards_results()
    {
        var explorer = new TestExplorerViewModel(new NoRuns(), new Immediate());
        var project = new ProjectInfo { Name = "sensor", FilePath = "/work/sensor/platformio.ini" };
        var result = new EmbeddedTestResult("board-A", EmbeddedTestFormat.Unity, "one", EmbeddedTestOutcome.Passed);
        explorer.ReportEmbeddedResults(project, [result]);
        explorer.ReportEmbeddedResults(project, [result with { BoardId = "board-B", Outcome = EmbeddedTestOutcome.Failed }]);
        Assert.Equal((2, 1, 1), (explorer.TotalCount, explorer.PassedCount, explorer.FailedCount));
    }

    private sealed class NoRuns : ITestRunHost
    {
        public bool CanStartTestRun => false;
        public Task RunTestsAsync(TestRunRequest request) => Task.CompletedTask;
        public void CancelTestRun() { }
        public Task NavigateToAsync(string path, int line, int column) => Task.CompletedTask;
    }

    private sealed class Immediate : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
    }
}
