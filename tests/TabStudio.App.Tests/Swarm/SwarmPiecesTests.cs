using Avalonia;
using Avalonia.Headless.XUnit;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.App.Services;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Recording;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Swarm;
using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// The swarm's pieces in the IDE on their own: recorded tests as a row per screen in the Test Explorer and how a run
/// splits between the screens and the test runner; the recorder joining typing and scrolls; and the Swarm pane's lines.
/// </summary>
public sealed class SwarmPiecesTests
{
    private static readonly ProjectInfo Project = new() { Name = "Shop.UITests", FilePath = "/work/Shop.UITests/Shop.UITests.csproj" };

    [Fact]
    public void A_recorded_test_is_a_row_per_screen_and_a_run_replays_those_rows_and_leaves_the_rest_to_the_runner()
    {
        var explorer = new TestExplorerViewModel(new NoTests(), new Immediate());
        explorer.SetProjects([Project]);
        explorer.SetSourceTests(Project, [.. SourceTestDiscoverer.Discover(CSharpSyntaxTree.ParseText("""
            using Xunit;

            namespace Shop.UITests;

            public class PriceTests
            {
                [Fact]
                public void Adds_tax() { }
            }

            public class BuyingTest
            {
                [Fact]
                [Trait("Category", "Recorded UI test")]
                public void Buying() { }
            }
            """, path: "/work/Shop.UITests/Tests.cs"))]);
        var test = new UiTestDefinition("/work/Shop.UITests/Recorded/BuyingTest.cs", 9, "Shop.UITests.BuyingTest", "Buying", 412, 915, [new UiTestStep(UiTestStepKind.Tap, "Buy")], []);
        IAppScreen[] screens = [new Screen("swarm:1", "Pixel 7"), new Screen("swarm:2", "Galaxy Tab S9")];

        explorer.SetScreenTests(Project.FilePath, Project.Name, [test], screens);

        Assert.True(explorer.HasScreenTests);
        Assert.Equal(["Adds_tax", "on Pixel 7 (412 × 915)", "on Galaxy Tab S9 (412 × 915)"], explorer.Entries.Select(e => e.DisplayName));
        Assert.Equal("Shop.UITests.BuyingTest.Buying(on swarm:2)", explorer.Entries[2].Id);

        var (rows, rest) = explorer.SplitScreenTests(new TestRunRequest("all", [new TestProjectSelection(Project.FilePath, null)]));
        Assert.Equal(2, rows.Count);
        Assert.Equal(["Shop.UITests.PriceTests.Adds_tax"], Assert.Single(rest.Projects).Names);
        Assert.Same(screens[1], explorer.ScreenTestOf(rows[1])?.Screen);

        (rows, rest) = explorer.SplitScreenTests(new TestRunRequest("one", [new TestProjectSelection(Project.FilePath, ["Shop.UITests.BuyingTest.Buying(on swarm:1)"])]));
        Assert.Same(screens[0], explorer.ScreenTestOf(Assert.Single(rows))?.Screen);
        Assert.Empty(rest.Projects);

        (rows, rest) = explorer.SplitScreenTests(new TestRunRequest("tax", [new TestProjectSelection(Project.FilePath, ["Shop.UITests.PriceTests"])]));
        Assert.Empty(rows);
        Assert.Equal(["Shop.UITests.PriceTests"], Assert.Single(rest.Projects).Names);

        // Without a screen, the test is one row, which says how to run it.
        explorer.SetScreenTests(Project.FilePath, Project.Name, [test], []);
        Assert.Equal(["Adds_tax", "Buying"], explorer.Entries.Select(e => e.DisplayName));
        Assert.Null(explorer.ScreenTestOf(explorer.Entries[1])?.Screen);
    }

    [AvaloniaFact]
    public void The_recorder_joins_typing_into_one_step_and_scrolls_of_one_element_into_one()
    {
        var recorder = new UiTestRecorderViewModel(new AppScreenRegistry(), new AvaloniaUiDispatcher(), _ => Task.FromResult<string?>(null));
        recorder.Add(UiTestStep.From(new AppInput(AppInputKind.Tap, "MainView/TextBox#Name", new Point(0.5, 0.5))));
        recorder.Add(UiTestStep.From(new AppInput(AppInputKind.Text, string.Empty, Text: "Ad")));
        recorder.Add(UiTestStep.From(new AppInput(AppInputKind.Text, string.Empty, Text: "a")));
        recorder.Add(UiTestStep.From(new AppInput(AppInputKind.Scroll, "MainView/ScrollViewer[0]", ScrollDelta: new Vector(0, 120))));
        recorder.Add(UiTestStep.From(new AppInput(AppInputKind.Scroll, "MainView/ScrollViewer[0]", ScrollDelta: new Vector(0, 80))));
        recorder.Add(UiTestStep.From(new AppInput(AppInputKind.Key, string.Empty, Key: "Enter")));

        Assert.Equal(["Tap Name", "Type \"Ada\"", "Scroll MainView/ScrollViewer[0]", "Press Enter"], recorder.Steps.Select(s => s.Text));
        Assert.Equal(200, recorder.RecordedSteps[2].ScrollY);
        Assert.True(recorder.HasSteps);

        recorder.RemoveStepCommand.Execute(recorder.Steps[2]);
        Assert.Equal(["Tap Name", "Type \"Ada\"", "Press Enter"], recorder.Steps.Select(s => s.Text));
        Assert.Equal([1, 2, 3], recorder.Steps.Select(s => s.Number));
    }

    /// <summary>A screen that shows nothing, for its name and id.</summary>
    private sealed class Screen(string id, string name) : IAppScreen
    {
        public event EventHandler? Changed { add { } remove { } }

        public event EventHandler<AppInput>? InputCaptured { add { } remove { } }

        public string Id => id;

        public AppScreenKind Kind => AppScreenKind.Device;

        public ScreenProfile Profile { get; } = new(name, FormFactor.Phone, 412, 915, 2.6, true, TextDirection.LeftToRight, 1);

        public bool IsRunning => true;

        public Task<bool> SendInputAsync(AppInput input, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<byte[]?> CaptureAsync(CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

        public Task<IReadOnlyList<LayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LayoutIssue>>([]);
    }

    private sealed class NoTests : ITestRunHost
    {
        public bool CanStartTestRun => true;

        public Task RunTestsAsync(TestRunRequest request) => Task.CompletedTask;

        public void CancelTestRun()
        {
        }

        public Task NavigateToAsync(string path, int line, int column) => Task.CompletedTask;
    }

    private sealed class Immediate : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
