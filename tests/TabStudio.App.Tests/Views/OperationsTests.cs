using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.LongOperations;
using TabStudio.App.Views;
using TabStudio.Core.Progress;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Long operations in the status bar and its panel: the newest with its stage, numbers and time left, all of them in
/// the panel with Cancel where it is safe and their raw logs, a notification when one runs 30 seconds in the
/// background, and an Error List that says "Analyzing…" while what it lists is about to change.
/// </summary>
public sealed class OperationsTests
{
    [Fact]
    public void The_status_bar_shows_the_newest_operation_and_the_panel_all_of_them()
    {
        using var operations = new OperationsViewModel(new Immediate());

        var sdk = operations.Begin("toolchain:pico-sdk", "Installing Pico SDK",
            new OperationProgress("Downloading Pico SDK", 120.0 * 1024 * 1024, 340.0 * 1024 * 1024, ProgressUnit.Bytes), () => { });
        Assert.Equal("Downloading Pico SDK 120/340 MB", operations.StatusText);
        Assert.True(operations.HasOperations);

        var build = operations.Begin("build", "Building blink", new OperationProgress("Building blink"));
        build.Log("[37/120] Building C object blink.c.obj", parse: true);
        Assert.Equal("Building blink 37/120 (+1 more)", operations.StatusText);
        Assert.Equal(2, operations.Items.Count);
        Assert.Same(build, operations.Current!.Operation);
        Assert.InRange(operations.Current.Percent, 30.8, 30.9);

        var item = operations.Items[0];
        Assert.True(item.CanCancel);
        Assert.False(operations.Current.CanCancel);
        Assert.Equal("Show details", operations.Current.DetailsLabel);
        operations.Current.ToggleDetailsCommand.Execute(null);
        Assert.Contains("[37/120]", operations.Current.Details, StringComparison.Ordinal);

        operations.TogglePanelCommand.Execute(null);
        Assert.True(operations.IsPanelOpen);
        build.Complete();
        sdk.Complete();
        Assert.False(operations.HasOperations);
        Assert.False(operations.IsPanelOpen);
        Assert.Null(operations.StatusText);
    }

    [Fact]
    public void A_long_operation_in_the_background_shows_its_progress_in_a_notification_and_then_how_it_ended()
    {
        var clock = new ManualClock();
        var notifications = new FakeNotifications();
        var activity = new FakeAppActivity();
        using var operations = new OperationsViewModel(new Immediate(), notifications, activity, clock);
        var sdk = operations.Begin("toolchain:pico-sdk", "Installing Pico SDK", new OperationProgress("Downloading Pico SDK", 30, 100, ProgressUnit.Percent));

        activity.Set(inBackground: true);
        Assert.Empty(notifications.Shown);

        clock.Advance(TimeSpan.FromSeconds(31));
        sdk.Report(new OperationProgress("Downloading Pico SDK", 40, 100, ProgressUnit.Percent));
        var progress = Assert.Single(notifications.Shown);
        Assert.True(progress.IsProgress);
        Assert.Equal(40, progress.ProgressPercent);
        Assert.Equal("Installing Pico SDK", progress.Title);
        Assert.Equal("operation:toolchain:pico-sdk", progress.Tag);

        sdk.Complete();
        var ended = notifications.Shown[^1];
        Assert.Equal("Installing Pico SDK finished", ended.Title);
        Assert.False(ended.IsProgress);
        Assert.Equal(progress.Tag, ended.Tag);
    }

    [Fact]
    public void A_toolchain_install_s_download_and_package_counts_become_the_operation_s_numbers()
    {
        var toolchain = ToolchainCatalog.Default.Find("pico-sdk")!;
        ToolchainStatus Status(InstallProgress progress) => new() { Toolchain = toolchain, State = ToolchainState.Installing, Progress = progress };

        Assert.Equal($"Downloading {toolchain.Name} 120/340 MB", WorkspaceViewModel.ProgressOf(Status(new InstallProgress(InstallStage.Downloading, "Downloading pico-sdk", 0.3, 120L * 1024 * 1024, 340L * 1024 * 1024))).Text);
        Assert.Equal("Installing packages 12/40", WorkspaceViewModel.ProgressOf(Status(new InstallProgress(InstallStage.Installing, "Installing cmake", 0.8, StepsDone: 12, StepsTotal: 40))).Text);
    }

    [AvaloniaFact]
    public async Task The_status_bar_item_opens_the_panel_and_the_Error_List_says_Analyzing_while_the_solution_is_analyzed()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => !workspace.Operations.HasOperations, TimeSpan.FromMinutes(2), "the workspace's own operations to end");
            var download = workspace.Operations.Begin("toolchain:pico-sdk", "Installing Pico SDK",
                new OperationProgress("Downloading Pico SDK", 120.0 * 1024 * 1024, 340.0 * 1024 * 1024, ProgressUnit.Bytes), () => { });
            var analysis = workspace.Operations.Begin("analysis", "Analyzing", new OperationProgress("Analyzing", 12, 40, ProgressUnit.Projects));
            download.Log("Downloading https://github.com/raspberrypi/pico-sdk/archive/079c6f3.tar.gz");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("Analyzing… 12/40 projects", workspace.ErrorList.PendingText);
            var text = window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "OperationsText");
            Assert.Equal("Analyzing 12/40 projects (+1 more)", text.Text);
            Assert.True(window.GetLogicalDescendants().OfType<Control>().Single(c => c.Name == "ProblemsPending").IsVisible);

            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "OperationsButton").Command!.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(workspace.Operations.IsPanelOpen);
            Assert.True(window.GetLogicalDescendants().OfType<Control>().Single(c => c.Name == "OperationsOverlay").IsVisible);
            workspace.Operations.Items[0].ToggleDetailsCommand.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            UiTestHelpers.SaveScreenshot(window, "70-operations-panel");

            analysis.Complete();
            Assert.Null(workspace.ErrorList.PendingText);
            workspace.Operations.Items[0].CancelCommand.Execute(null);
            download.Complete();
            Assert.False(workspace.Operations.HasOperations);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_refresh_runs_only_while_an_operation_does()
    {
        // An idle IDE woke the UI thread every two seconds to refresh an empty list.
        var clock = new TimerClock();
        using var operations = new OperationsViewModel(new Immediate(), time: clock);
        var timer = Assert.Single(clock.Timers);
        Assert.Equal(Timeout.InfiniteTimeSpan, timer.Due);

        var restore = operations.Begin("restore", "Restoring packages");
        Assert.Equal(TimeSpan.FromSeconds(2), timer.Due);

        restore.Complete();
        Assert.Equal(Timeout.InfiniteTimeSpan, timer.Due);
    }

    private sealed class TimerClock : TimeProvider
    {
        public List<RecordedTimer> Timers { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new RecordedTimer { Due = dueTime };
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class RecordedTimer : ITimer
    {
        public TimeSpan Due { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime;
            return true;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan time) => _ticks += time.Ticks;
    }
}
