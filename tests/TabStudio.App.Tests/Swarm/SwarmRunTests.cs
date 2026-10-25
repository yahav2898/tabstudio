using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Swarm;
using TabStudio.Core.Diagnostics;
using TabStudio.Swarm;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// The swarm in the IDE, against the real nuget.org: two phones join the tablet's swarm over TLS on the loopback
/// address; F5 on the swarm runs the Avalonia template on both, full screen, and saving its XAML updates both; mirrored
/// input taps on both; three taps recorded on one phone become a UI test in a new test project, which compiles and
/// passes on every screen, with a picture of the screen when it fails.
/// </summary>
[Collection(SwarmAppFixture.Collection)]
public sealed class SwarmRunTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);

    [AvaloniaFact]
    public async Task F5_runs_on_both_devices_a_save_updates_both_and_three_recorded_taps_become_a_passing_test()
    {
        await using var fixture = new SwarmAppFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.AvaloniaId, "HelloAvalonia");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = fixture.ShowIde();

        // Two phones scan the code of the tablet's swarm.
        await fixture.StartAsync();
        var pixel = await fixture.JoinAsync("Pixel 7");
        var galaxy = await fixture.JoinAsync("Galaxy S24");
        SwarmAppFixture.Progress("run: joined");
        Assert.True(pixel.Member.IsInSwarm);
        Assert.True(pixel.Page.IsVisible);
        Assert.Equal("Yahav's tablet", pixel.Page.CoordinatorName);
        await UiTestHelpers.WaitUntilAsync(() => fixture.SwarmWindow.Devices.Count == 2, because: "both phones in the Swarm window");
        Assert.Equal("Swarm: 2 devices", fixture.SwarmWindow.StatusText);
        Assert.Equal(2, fixture.Registry.Screens.Count(s => s.Kind == AppScreenKind.Device));

        // "Swarm" in the Run button's menu, then F5.
        Assert.Contains(workspace.RunTargets, t => t.Name == "Swarm");
        workspace.RunTargets.First(t => t.Name == "Swarm").Select.Execute(null);
        Assert.True(workspace.IsRunOnSwarm);
        Assert.Equal("Swarm · 2 devices", workspace.RunHostText);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        var run = workspace.SwarmRun;
        SwarmAppFixture.Progress("run: F5 started");
        await UiTestHelpers.WaitUntilAsync(
            () => (pixel.Texts().Contains("Hello from Avalonia!") && galaxy.Texts().Contains("Hello from Avalonia!")) || run.IsCompleted,
            StartTimeout,
            "the app on both phones");
        Assert.True(galaxy.Texts().Contains("Hello from Avalonia!"), workspace.Output.Text + workspace.SwarmOutput.Text);
        Assert.True(pixel.Page.ShowsApp);
        Assert.Equal("Debugging HelloAvalonia", pixel.Page.StateText);
        Assert.True(workspace.IsSwarmSelected);
        Assert.Contains("[Pixel 7] received", workspace.SwarmOutput.Text, StringComparison.Ordinal);
        Assert.Contains("[Galaxy S24] received", workspace.SwarmOutput.Text, StringComparison.Ordinal);
        await UiTestHelpers.WaitUntilAsync(() => fixture.SwarmWindow.Devices.All(d => d.IsRunning), because: "both rows to say Running");
        UiTestHelpers.SaveScreenshot(window, "swarm-01-window-with-devices");
        SwarmAppFixture.Progress("run: running on both");
        UiTestHelpers.SaveScreenshot(pixel.Window, "swarm-02-device-app-full-screen");
        pixel.Page.ToggleExpandedCommand.Execute(null);
        UiTestHelpers.SaveScreenshot(pixel.Window, "swarm-03-device-overlay");
        pixel.Page.ToggleExpandedCommand.Execute(null);

        // Saving the view's XAML builds once and updates both phones; the second time, they had most files already.
        var xamlPath = Path.Combine(Path.GetDirectoryName(solution)!, "HelloAvalonia", "Views", "MainView.axaml");
        var document = await workspace.OpenDocumentAsync(xamlPath);
        var hint = document!.Document.Text.IndexOf("Edit Views/MainView.axaml", StringComparison.Ordinal);
        document.Document.Replace(hint, "Edit Views/MainView.axaml".Length, "Changed on a tablet");
        await workspace.SaveCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(60));
        await UiTestHelpers.WaitUntilAsync(
            () => (Shows(pixel, "Changed on a tablet") && Shows(galaxy, "Changed on a tablet")) || run.IsCompleted,
            StartTimeout,
            "both phones to show the change");
        Assert.True(Shows(galaxy, "Changed on a tablet"), workspace.SwarmOutput.Text);
        Assert.Contains("Updating every device with the saved changes to HelloAvalonia.", workspace.SwarmOutput.Text, StringComparison.Ordinal);
        SwarmAppFixture.Progress("run: updated by a save");

        // Mirrored input: a tap on one phone happens on the other.
        fixture.SwarmWindow.MirrorInput = true;
        pixel.Preview.PersonTaps("CountButton");
        await UiTestHelpers.WaitUntilAsync(() => galaxy.Texts().Contains("Clicked 1 times"), because: "the tap mirrored to the Galaxy");
        Assert.Contains("Clicked 1 times", pixel.Texts());

        // A tap that the other screen cannot take says so there, and in the Swarm window.
        pixel.Preview.PersonTaps("MainView/StackPanel[0]/Button#OnlyOnThePixel");
        await UiTestHelpers.WaitUntilAsync(() => galaxy.Host.Notice is not null && fixture.SwarmWindow.Notice is not null, because: "the failed mirror's notices");
        Assert.Contains("from Pixel 7 did not arrive here", galaxy.Host.Notice, StringComparison.Ordinal);
        Assert.Contains("could not be delivered to Galaxy S24", fixture.SwarmWindow.Notice, StringComparison.Ordinal);
        fixture.SwarmWindow.MirrorInput = false;
        SwarmAppFixture.Progress("run: mirrored");

        // Record three taps on the Pixel, and a long press that checks what the count shows.
        var recorder = fixture.SwarmWindow.Recorder;
        recorder.Open(start: false);
        recorder.SelectedScreen = recorder.Screens.First(s => s.Screen.Profile.Name == "Pixel 7");
        recorder.TestName = "Counting three taps";
        await recorder.StartCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(60));
        Assert.True(recorder.IsRecording, recorder.Error);
        SwarmAppFixture.Progress("run: recording");
        await UiTestHelpers.WaitUntilAsync(() => pixel.Texts().Contains("Clicked 0 times"), because: "the app to start again for the recording");
        for (var i = 0; i < 3; i++)
        {
            pixel.Preview.PersonTaps("MainView/StackPanel[0]/Button#CountButton");
        }

        await UiTestHelpers.WaitUntilAsync(() => pixel.Texts().Contains("Clicked 3 times"), because: "three taps");
        pixel.Preview.PersonLongPresses("MainView/StackPanel[0]/TextBlock[2]");
        await WaitQuietlyAsync(() => recorder.Steps.Count >= 4 || recorder.Error is not null);
        Assert.True(recorder.Error is null, recorder.Error);
        Assert.Equal(["Tap CountButton", "Tap CountButton", "Tap CountButton", "Check that MainView/StackPanel[0]/TextBlock[2] shows \"Clicked 3 times\""], recorder.Steps.Select(s => s.Text));
        UiTestHelpers.SaveScreenshot(window, "swarm-04-recorder");
        SwarmAppFixture.Progress("run: recorded");

        // Saving writes the test into a new test project, added to the solution.
        await recorder.SaveCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(2));
        Assert.Null(recorder.Error);
        var testsFolder = Path.Combine(Path.GetDirectoryName(solution)!, "HelloAvalonia.UITests");
        var testFile = Path.Combine(testsFolder, "Recorded", "CountingThreeTapsTest.cs");
        Assert.True(File.Exists(testFile));
        Assert.True(File.Exists(Path.Combine(testsFolder, "RecordedApp.cs")));
        Assert.True(File.Exists(Path.Combine(testsFolder, "TestAppBuilder.cs")));
        Assert.Contains("HelloAvalonia.UITests", File.ReadAllText(solution), StringComparison.Ordinal);
        var test = File.ReadAllText(testFile);
        Assert.Contains("public void Counting_three_taps()", test, StringComparison.Ordinal);
        Assert.Contains("using var app = RecordedApp.Start(width: 412, height: 915);", test, StringComparison.Ordinal);
        Assert.Equal(3, test.Split("app.Tap(\"CountButton\");").Length - 1);
        Assert.Contains("app.ShouldShow(\"MainView/StackPanel[0]/TextBlock[2]\", \"Clicked 3 times\");", test, StringComparison.Ordinal);
        Assert.Contains(workspace.Workspace!.Projects, p => p.Name == "HelloAvalonia.UITests" && p.IsTestProject);
        RecordedTestCompilation.AssertCompiles(testsFolder, fixture.Swarm.Coordinator!.CurrentRun!.Build);
        SwarmAppFixture.Progress("run: saved and compiled");

        // Its packages restore in the background, and then the editor finds nothing wrong with it.
        await WaitQuietlyAsync(() => workspace.PackagesText is null && workspace.ErrorList.ErrorCount == 0, TimeSpan.FromMinutes(3));
        await WaitQuietlyAsync(() => workspace.ErrorList.ErrorCount > 0, TimeSpan.FromSeconds(3)); // analysis still running may yet report
        Assert.True(workspace.ErrorList.ErrorCount == 0, Errors(workspace));
        SwarmAppFixture.Progress("run: restored");

        // In the Test Explorer, the test is a row per phone, and passes on both.
        await UiTestHelpers.WaitUntilAsync(() => ScreenRows(workspace).Count == 2, because: "a row of the test for each phone");
        await workspace.Tests.RunAsync(new TestRunRequest("recorded UI tests", workspace.Tests.ScreenTestSelections())).WaitAsync(TimeSpan.FromMinutes(3));
        Assert.All(ScreenRows(workspace), row => Assert.Equal(TestState.Passed, row.State));
        Assert.Contains(ScreenRows(workspace), row => row.DisplayName.StartsWith("on Galaxy S24", StringComparison.Ordinal));
        SwarmAppFixture.Progress("run: replayed");

        // The passing test also runs with dotnet test, in CI.
        CopyForDotnetTest(Path.GetDirectoryName(solution)!);

        // A check that does not hold fails with a picture of the screen it failed on.
        document = await workspace.OpenDocumentAsync(testFile);
        var expected = document!.Document.Text.IndexOf("\"Clicked 3 times\"", StringComparison.Ordinal);
        document.Document.Replace(expected, "\"Clicked 3 times\"".Length, "\"Clicked 4 times\"");
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.Tests.ScreenTestOf(ScreenRows(workspace)[0])?.Test.Steps[^1].Text == "Clicked 4 times", because: "the changed test to be read again");
        await workspace.Tests.RunAsync(new TestRunRequest("recorded UI tests", workspace.Tests.ScreenTestSelections())).WaitAsync(TimeSpan.FromMinutes(3));
        var failed = ScreenRows(workspace).First(r => r.DisplayName.StartsWith("on Pixel 7", StringComparison.Ordinal));
        Assert.Equal(TestState.Failed, failed.State);
        Assert.Contains("shows \"Clicked 3 times\" on Pixel 7, not \"Clicked 4 times\"", failed.Result!.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(failed.ScreenshotPath));
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.TestExplorer);
        workspace.Tests.SelectedNode = workspace.Tests.Rows.SelectMany(Flatten).First(n => n.Entry?.Id == failed.Id);
        Assert.True(workspace.Tests.Details?.HasScreenshot);
        UiTestHelpers.SaveScreenshot(window, "swarm-05-recorded-test-failed-on-a-screen");
        SwarmAppFixture.Progress("run: failure shown");

        workspace.StopDebuggingCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(60));
        await UiTestHelpers.WaitUntilAsync(() => pixel.Page.ShowsWaiting && galaxy.Page.ShowsWaiting, because: "both phones to wait for the next app");
        Assert.Contains("HelloAvalonia was stopped on every device.", workspace.SwarmOutput.Text, StringComparison.Ordinal);
        window.Close();
    }

    /// <summary>Waits for a condition without failing, so the assertion after it shows what happened instead.</summary>
    private static async Task WaitQuietlyAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }

    /// <summary>The first errors in the Error List, and the end of the Output, for a failed assertion.</summary>
    private static string Errors(WorkspaceViewModel workspace) =>
        string.Join(
            Environment.NewLine,
            workspace.ErrorList.AllDiagnostics
                .Where(d => d.Severity == DiagnosticSeverityLevel.Error)
                .Take(8)
                .Select(d => $"{Path.GetFileName(d.Location?.FilePath)}: {d.Id} {d.Message}")
                .Append(workspace.Output.Text[Math.Max(0, workspace.Output.Text.Length - 1500)..]));

    private static bool Shows(SwarmAppDevice device, string text) =>
        device.Texts().Any(t => t.StartsWith(text, StringComparison.Ordinal));

    private static List<TestEntry> ScreenRows(WorkspaceViewModel workspace) =>
        [.. workspace.Tests.Entries.Where(e => e.DataRow?.StartsWith("(on ", StringComparison.Ordinal) == true)];

    private static IEnumerable<TestNodeViewModel> Flatten(TestNodeViewModel node) => [node, .. node.Children.SelectMany(Flatten)];

    /// <summary>Copies the solution, without build output, where CI runs dotnet test on its recorded UI tests.</summary>
    private static void CopyForDotnetTest(string solutionFolder)
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_RECORDED_UI_TESTS") is not { Length: > 0 } target)
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(solutionFolder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(solutionFolder, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".tabstudio"))
            {
                continue;
            }

            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
