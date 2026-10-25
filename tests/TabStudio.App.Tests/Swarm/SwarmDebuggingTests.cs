using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Debugging;
using TabStudio.Swarm;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// Debugging on the swarm with the tablet's debugger, against the real nuget.org: a breakpoint hit on the second phone
/// stops the tablet's debugger there, with the phone's call stack, locals, Watch and DataTips; Pause all on a
/// breakpoint pauses the first phone too, which joins the Call Stack's box; Continue runs both on.
/// </summary>
[Collection(SwarmAppFixture.Collection)]
public sealed class SwarmDebuggingTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);

    /// <summary>
    /// Waits by yielding to the dispatcher, never by running its jobs inside the test: a tap that stops at a breakpoint
    /// runs a nested frame until the app continues, and inside the test's own call it would wait for the test itself.
    /// </summary>
    private static async Task UntilAsync(Func<bool> condition, TimeSpan? timeout = null, string? because = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(60));
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting{(because is null ? string.Empty : " for " + because)}.");
            await Task.Delay(25);
        }
    }

    [AvaloniaFact]
    public async Task A_breakpoint_on_the_second_device_stops_the_tablet_debugger_with_its_locals_and_pauses_all()
    {
        await using var fixture = new SwarmAppFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.AvaloniaId, "HelloAvalonia");
        var viewModel = Path.Combine(Path.GetDirectoryName(solution)!, "HelloAvalonia", "ViewModels", "MainViewModel.cs");
        File.WriteAllText(viewModel, """
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;

            namespace HelloAvalonia.ViewModels;

            public partial class MainViewModel : ObservableObject
            {
                public string Greeting => "Hello from Avalonia!";

                [ObservableProperty]
                private int _clicks;

                [RelayCommand]
                private void Count()
                {
                    var next = Clicks + 1;
                    Clicks = next;
                }
            }

            """);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        SwarmAppFixture.Progress("debugging: loaded");
        var window = fixture.ShowIde();
        await fixture.StartAsync();
        var pixel = await fixture.JoinAsync("Pixel 7");
        var galaxy = await fixture.JoinAsync("Galaxy S24");
        SwarmAppFixture.Progress("debugging: joined");
        workspace.SelectSwarmTarget();
        Assert.True(fixture.SwarmWindow.PauseAllOnBreakpoint);

        var breakpointLine = Array.FindIndex(File.ReadAllLines(viewModel), l => l.Contains("Clicks = next;", StringComparison.Ordinal)) + 1;
        workspace.Breakpoints.Add(viewModel, breakpointLine);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        var run = workspace.SwarmRun;
        SwarmAppFixture.Progress("debugging: F5 started");
        await UntilAsync(
            () => (pixel.Texts().Contains("Hello from Avalonia!") && galaxy.Texts().Contains("Hello from Avalonia!")) || run.IsCompleted,
            StartTimeout,
            "the app on both phones, under the debugger");
        Assert.True(workspace.IsDebugging, workspace.Output.Text + workspace.SwarmOutput.Text);
        Assert.Equal("Debugging HelloAvalonia", galaxy.Page.StateText);
        Assert.True(workspace.Breakpoints.All.Single().Binding is { IsBound: true }, "The tablet binds the breakpoint to a statement of the build.");
        SwarmAppFixture.Progress("debugging: running on both");

        // A tap on the second phone runs into the breakpoint there.
        Dispatcher.UIThread.Post(() => galaxy.Preview.PersonTaps("CountButton"));
        SwarmAppFixture.Progress("debugging: galaxy tapped");
        await UntilAsync(() => workspace.IsPaused && workspace.ShownSwarmPause?.Device.Name == "Galaxy S24", because: "the Galaxy to stop at the breakpoint");
        await workspace.PauseShown.WaitAsync(TimeSpan.FromSeconds(60));
        SwarmAppFixture.Progress("debugging: pause shown");
        Assert.Equal($"Paused at a breakpoint on Galaxy S24 · MainViewModel.cs, line {breakpointLine}", workspace.StatusText);
        Assert.StartsWith("Galaxy S24!", workspace.CallStack.Frames[0].MethodName, StringComparison.Ordinal);
        Assert.Contains("Count", workspace.CallStack.Frames[0].MethodName, StringComparison.Ordinal);
        Assert.Equal(breakpointLine, workspace.CallStack.Frames[0].Location?.Line);
        Assert.Equal(viewModel, workspace.ActiveDocument?.FilePath);
        await UntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "next"), because: "the Galaxy's locals");
        Assert.Equal("1", workspace.Locals.Tree.Rows.Single(r => r.Name == "next").Value);
        Assert.True(galaxy.Page.IsPaused);
        Assert.Equal("Paused in the tablet's debugger", galaxy.Page.StateText);
        SwarmAppFixture.Progress("debugging: locals checked");

        // Watch and DataTips read the Galaxy's values.
        await workspace.Watch.AddAsync("Clicks").WaitAsync(TimeSpan.FromSeconds(60));
        await UntilAsync(() => workspace.Watch.Tree.Rows.Any(r => r.Name == "Clicks" && r.Value == "0"), because: "the Watch to read Clicks on the Galaxy");
        var tip = await ((IEditorDebugger)workspace).EvaluateDataTipAsync("next").WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal("1", tip?.Rows.Single().Value);
        SwarmAppFixture.Progress("debugging: watch and datatip checked");

        // Pause all on a breakpoint: the Pixel pauses at its next statement, which a tap runs.
        var pixelOnTablet = fixture.Swarm.Devices.Single(d => d.Name == "Pixel 7");
        await UntilAsync(() => pixelOnTablet.State == SwarmDeviceState.Pausing, because: "the Pixel to pause with the Galaxy");
        Dispatcher.UIThread.Post(() => pixel.Preview.PersonTaps("CountButton"));
        SwarmAppFixture.Progress("debugging: pixel tapped");
        await UntilAsync(() => pixelOnTablet.State == SwarmDeviceState.Paused && workspace.CallStack.Threads.Count == 2, because: "the Pixel in the Call Stack's box");
        Assert.Equal(PauseReason.Pause, pixelOnTablet.Pause?.Reason);
        Assert.Equal("Galaxy S24", workspace.ShownSwarmPause?.Device.Name);
        Assert.StartsWith("Galaxy S24", workspace.CallStack.SelectedThread?.Name, StringComparison.Ordinal);
        await UntilAsync(() => fixture.SwarmWindow.Devices.All(d => d.IsPaused), because: "both rows to say Paused");
        workspace.SelectPanelCommand.Execute(BottomPanel.Locals);
        UiTestHelpers.SaveScreenshot(window, "swarm-06-debugger-paused-on-a-device");
        UiTestHelpers.SaveScreenshot(galaxy.Window, "swarm-07-device-paused");
        SwarmAppFixture.Progress("debugging: screenshots taken");

        // Choosing the Pixel in the box shows where it is.
        workspace.CallStack.SelectedThread = workspace.CallStack.Threads.Single(t => t.Name.StartsWith("Pixel 7", StringComparison.Ordinal));
        await UntilAsync(() => workspace.ShownSwarmPause?.Device.Name == "Pixel 7", because: "the Pixel's pause");
        Assert.StartsWith("Pixel 7!", workspace.CallStack.Frames[0].MethodName, StringComparison.Ordinal);
        SwarmAppFixture.Progress("debugging: pixel shown");

        // Continue: both phones run on, together.
        workspace.DeleteAllBreakpointsCommand.Execute(null);
        await UntilAsync(() => fixture.Swarm.Coordinator!.Breakpoints.Count == 0, because: "the breakpoints to leave the phones");
        SwarmAppFixture.Progress("debugging: breakpoints cleared");
        Assert.True(workspace.StartDebuggingCommand.CanExecute(null), "F5 continues while the app is paused on the swarm.");
        Assert.Equal("Continue", workspace.StartDebuggingLabel);
        await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(60));
        await UntilAsync(
            () => galaxy.Texts().Contains("Clicked 1 times") && pixel.Texts().Contains("Clicked 1 times") && workspace.State == IdeState.Debugging,
            because: "both phones to finish counting");
        Assert.False(galaxy.Page.IsPaused);
        Assert.Empty(workspace.CallStack.Frames);
        SwarmAppFixture.Progress("debugging: continued");

        workspace.StopDebuggingCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(60));
        SwarmAppFixture.Progress("debugging: stopped");
        Assert.False(workspace.IsDebugging);
        window.Close();
    }
}
