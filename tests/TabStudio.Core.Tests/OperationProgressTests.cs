using TabStudio.Core.Progress;

namespace TabStudio.Core.Tests;

/// <summary>
/// What the status bar says about long operations: the stage and its numbers ("Downloading Pico SDK 120/340 MB",
/// "Installing packages 12/40", "Building 37/120"), the time left once it can be estimated, and a notification when
/// one runs for 30 seconds while TabStudio is in the background.
/// </summary>
public sealed class OperationProgressTests
{
    [Fact]
    public void The_text_names_the_stage_with_its_numbers_in_their_unit()
    {
        Assert.Equal("Downloading Pico SDK 120/340 MB", new OperationProgress("Downloading Pico SDK", 120.0 * 1024 * 1024, 340.0 * 1024 * 1024, ProgressUnit.Bytes).Text);
        Assert.Equal("Downloading ninja 1.5/3.2 MB", new OperationProgress("Downloading ninja", 1.5 * 1024 * 1024, 3.2 * 1024 * 1024, ProgressUnit.Bytes).Text);
        Assert.Equal("Downloading tzdata 12/600 KB", new OperationProgress("Downloading tzdata", 12 * 1024, 600 * 1024, ProgressUnit.Bytes).Text);
        Assert.Equal("Installing packages 12/40", new OperationProgress("Installing packages", 12, 40).Text);
        Assert.Equal("Analyzing 12/40 projects", new OperationProgress("Analyzing", 12, 40, ProgressUnit.Projects).Text);
        Assert.Equal("Building 37%", new OperationProgress("Building", 37, 100, ProgressUnit.Percent).Text);
        Assert.Equal("Configuring CMake", new OperationProgress("Configuring CMake").Text);
        Assert.Equal(0.3, new OperationProgress("Installing packages", 12, 40).Fraction);
        Assert.Null(new OperationProgress("Restoring", 3, 0).Fraction);
    }

    [Theory]
    [InlineData("[37/120] Building C object blink/CMakeFiles/blink.dir/blink.c.obj", "Building blink", "Building blink 37/120")]
    [InlineData("[ 45%] Building C object pico-sdk/src/rp2_common/CMakeFiles/foo.dir/bar.c.obj", "Building pico_examples", "Building pico_examples 45%")]
    [InlineData("(12/40) Installing gcc-arm-none-eabi (14.2.0-r0)", "Installing Arm GCC", "Installing packages 12/40")]
    [InlineData("-- Configuring done (3.2s)", "Configuring CMake", "Configuring CMake")]
    [InlineData("-- Generating done (0.4s)", "Configuring CMake", "Generating build files")]
    [InlineData("[3/9] Linking CXX executable app", "Running ninja", "Building 3/9")]
    public void Build_and_install_tools_numbers_are_read_from_their_output(string line, string stage, string text)
    {
        Assert.Equal(text, OutputProgressParser.Parse(line, stage)!.Text);
    }

    [Theory]
    [InlineData("-- The C compiler identification is GNU 14.2.0")]
    [InlineData("warning: [-Wunused] 3/4 of the time")]
    [InlineData("added 312 packages in 9s")]
    public void Lines_without_progress_say_nothing(string line) => Assert.Null(OutputProgressParser.Parse(line, "Building"));

    [Fact]
    public void A_command_s_stage_comes_from_its_program()
    {
        Assert.Equal("Configuring CMake", OutputProgressParser.StageOf("cmake", ["-S", ".", "-B", "build", "-G", "Ninja"], "blink"));
        Assert.Equal("Building blink", OutputProgressParser.StageOf("/usr/bin/cmake", ["--build", "build", "--target", "blink"], "blink"));
        Assert.Equal("Installing packages", OutputProgressParser.StageOf("npm", ["install"], "app"));
        Assert.Equal("Building firmware", OutputProgressParser.StageOf("idf.py", ["build"], "firmware"));
    }

    [Fact]
    public void The_time_left_is_estimated_only_once_there_is_enough_to_go_on()
    {
        var clock = new ManualClock();
        var tracker = new OperationTracker(clock);
        const double MB = 1024 * 1024;
        var download = tracker.Begin("toolchain:pico-sdk", "Installing Pico SDK", new OperationProgress("Downloading Pico SDK", 0, 340 * MB, ProgressUnit.Bytes));
        Assert.Null(download.RemainingText);

        clock.Advance(TimeSpan.FromSeconds(1));
        download.Report(new OperationProgress("Downloading Pico SDK", 10 * MB, 340 * MB, ProgressUnit.Bytes));
        Assert.Null(download.RemainingText);

        // 10 MB a second: 220 MB left after 12 seconds is about 22 seconds.
        clock.Advance(TimeSpan.FromSeconds(11));
        download.Report(new OperationProgress("Downloading Pico SDK", 120 * MB, 340 * MB, ProgressUnit.Bytes));
        Assert.InRange(download.Remaining!.Value.TotalSeconds, 21, 23);
        Assert.Equal("Downloading Pico SDK 120/340 MB · about 20 s left", download.Text);

        // A new stage starts the estimate over.
        download.Report(new OperationProgress("Installing packages", 1, 40));
        Assert.Null(download.RemainingText);
    }

    [Theory]
    [InlineData(3, "less than 5 s left")]
    [InlineData(44, "about 40 s left")]
    [InlineData(58, "about 1 min left")]
    [InlineData(150, "about 3 min left")]
    [InlineData(4300, "about 1 h 12 min left")]
    public void The_time_left_reads_as_a_person_would_say_it(int seconds, string text) =>
        Assert.Equal(text, ProgressEstimator.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void The_newest_operation_is_current_and_a_running_key_is_reused()
    {
        var tracker = new OperationTracker(new ManualClock());
        var changes = 0;
        tracker.Changed += (_, _) => changes++;

        var restore = tracker.Begin("restore", "Restoring NuGet packages");
        var analysis = tracker.Begin("analysis", "Analyzing", new OperationProgress("Analyzing", 0, 40, ProgressUnit.Projects));
        Assert.Same(analysis, tracker.Current);
        Assert.Same(restore, tracker.Begin("restore", "Restoring NuGet packages", new OperationProgress("Restoring Conduit", 2, 5, ProgressUnit.Projects)));
        Assert.Equal("Restoring Conduit 2/5 projects", restore.Progress.Text);
        Assert.Equal(2, tracker.Active.Count);

        analysis.Complete();
        Assert.Same(restore, tracker.Current);
        Assert.Equal(OperationOutcome.Succeeded, analysis.Outcome);
        restore.Complete(OperationOutcome.Failed);
        Assert.Null(tracker.Current);
        Assert.True(changes >= 5);
    }

    [Fact]
    public void Cancel_is_offered_only_where_it_is_safe_and_reports_the_outcome()
    {
        var tracker = new OperationTracker(new ManualClock());
        var cancelled = 0;
        var setup = tracker.Begin("setup", "Setting up projects", cancel: () => cancelled++);
        var analysis = tracker.Begin("analysis", "Analyzing");

        Assert.True(setup.CanCancel);
        Assert.False(analysis.CanCancel);
        analysis.Cancel();
        setup.Cancel();
        setup.Cancel();
        Assert.Equal(1, cancelled);
        Assert.Equal("Cancelling", setup.Progress.Stage);

        setup.Complete(OperationOutcome.Failed);
        Assert.Equal(OperationOutcome.Cancelled, setup.Outcome);
        Assert.False(setup.CanCancel);
    }

    [Fact]
    public void Show_details_keeps_the_raw_log_and_build_lines_move_the_numbers()
    {
        var tracker = new OperationTracker(new ManualClock());
        var build = tracker.Begin("build", "Building blink", new OperationProgress("Building blink"));

        build.Log("-- Build files have been written to: /work/build", parse: true);
        build.Log("[37/120] Building C object blink.c.obj", parse: true);
        build.Log("[38/120] Building C object foo.c.obj");

        Assert.Equal("Building blink 37/120", build.Progress.Text);
        Assert.Equal(3, build.Details.Count);
        for (var i = 0; i < TrackedOperation.MaxDetailLines + 10; i++)
        {
            build.Log("line " + i);
        }

        Assert.Equal(TrackedOperation.MaxDetailLines, build.Details.Count);
        Assert.Equal("line " + (TrackedOperation.MaxDetailLines + 9), build.Details[^1]);
    }

    [Fact]
    public void An_operation_that_runs_30_seconds_in_the_background_gets_a_notification_which_says_how_it_ended()
    {
        var clock = new ManualClock();
        var tracker = new OperationTracker(clock);
        var due = new List<string>();
        var ended = new List<OperationOutcome?>();
        tracker.NotificationDue += (_, o) => due.Add(o.Text);
        tracker.NotificationEnded += (_, o) => ended.Add(o.Outcome);
        var quick = tracker.Begin("restore", "Restoring NuGet packages");
        var sdk = tracker.Begin("toolchain:pico-sdk", "Installing Pico SDK", new OperationProgress("Downloading Pico SDK"));

        // In the foreground nothing is notified, however long it runs.
        clock.Advance(TimeSpan.FromSeconds(40));
        tracker.Poll();
        Assert.Empty(due);

        quick.Complete();
        tracker.IsInBackground = true;
        Assert.Equal(["Downloading Pico SDK"], due);

        // Updates come at most every two seconds, and only when the text changes.
        sdk.Report(new OperationProgress("Installing packages", 1, 40));
        clock.Advance(TimeSpan.FromSeconds(3));
        tracker.Poll();
        tracker.Poll();
        Assert.Equal(["Downloading Pico SDK", "Installing packages 1/40"], due);

        sdk.Complete();
        Assert.Equal([OperationOutcome.Succeeded], ended);
    }

    [Fact]
    public void A_short_operation_in_the_background_is_not_notified()
    {
        var clock = new ManualClock();
        var tracker = new OperationTracker(clock) { IsInBackground = true };
        var notified = 0;
        tracker.NotificationDue += (_, _) => notified++;
        var restore = tracker.Begin("restore", "Restoring NuGet packages");

        clock.Advance(TimeSpan.FromSeconds(29));
        restore.Report(new OperationProgress("Restoring NuGet packages", 3, 4));
        restore.Complete();

        Assert.Equal(0, notified);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan time) => _ticks += time.Ticks;
    }
}
