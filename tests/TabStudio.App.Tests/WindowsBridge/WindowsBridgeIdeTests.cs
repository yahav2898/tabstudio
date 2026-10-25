using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Controls;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.Tests.WindowsBridge;

/// <summary>
/// The Windows Bridge in the IDE, with a PC in the test whose desktop the test paints: the Doctor and the Run button
/// with a PC paired, a program that runs on the PC and streams into the Bridge pane and takes clicks, a build error
/// on the PC in the Error List, and the PC going away while the tablet keeps working.
/// </summary>
public sealed class WindowsBridgeIdeTests
{
    private const string Via = "Windows Bridge: Test PC";

    [AvaloniaFact]
    public async Task A_paired_PC_makes_WPF_ready_on_it_and_joins_the_Run_menu()
    {
        await using var devices = BridgeDevices.Start(new FakeBridgeDesktop());
        devices.WriteConsoleProject("Hello");
        devices.WriteWpfProject("HelloWpf");
        var workspace = await devices.OpenAsync(devices.SolutionFolder);
        Assert.False(Row(workspace, "HelloWpf").IsReady);
        Assert.DoesNotContain(workspace.RunTargets, t => t.Detail == Via);

        await devices.PairAsync();
        await UiTestHelpers.WaitUntilAsync(() => Row(workspace, "HelloWpf").IsReady, because: "the Doctor's verdict with the PC");
        workspace.SelectPanelCommand.Execute(BottomPanel.Compatibility);
        workspace.SetStartupProject(workspace.Workspace!.FindProject("HelloWpf"));
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-01-doctor");

        Assert.Equal(Via, Row(workspace, "HelloWpf").RunsVia);
        Assert.Equal("Ready", Row(workspace, "HelloWpf").Status);
        Assert.Equal(Via, workspace.RunHostText);
        Assert.Contains(workspace.RunTargets, t => t.Name == "HelloWpf" && t.Detail == Via);
        Assert.Contains(workspace.RunTargets, t => t.Name == "Hello" && t.Detail == Via);
        Assert.Contains(workspace.RunTargets, t => t.Name == "Hello" && t.Detail == "C# project");
        Assert.StartsWith("Test PC", workspace.WindowsBridgeStatusText, StringComparison.Ordinal);

        await devices.Tablet.ForgetPcAsync(devices.Tablet.PairedPcs.Devices[0]);
        await UiTestHelpers.WaitUntilAsync(() => !Row(workspace, "HelloWpf").IsReady, because: "the Doctor's own verdict again");
        Assert.DoesNotContain(workspace.RunTargets, t => t.Detail == Via);
        Assert.NotEqual(Via, workspace.RunHostText);
    }

    [AvaloniaFact]
    public async Task A_program_runs_on_the_PC_and_its_window_streams_into_the_Bridge_pane_and_takes_clicks()
    {
        var desktop = new FakeBridgeDesktop();
        await using var devices = BridgeDevices.Start(desktop);
        devices.WriteConsoleProject("Hello");
        await devices.PairAsync();
        var workspace = await devices.OpenAsync(devices.SolutionFolder);
        workspace.RunTargets.Single(t => t.Name == "Hello" && t.Detail == Via).Select.Execute(null);
        Assert.Equal(Via, workspace.RunHostText);
        var picture = BridgePictures.AppWindow("Hello", "Hello from the PC", clicked: false);
        var clicked = BridgePictures.AppWindow("Hello", "Hello from the PC", clicked: true);

        await workspace.RunCommand.ExecuteAsync(null);
        var pane = workspace.WindowsBridgePane!;
        await UiTestHelpers.WaitUntilAsync(() => pane.SelectedApp is not null, TimeSpan.FromMinutes(3), "the program on the PC");
        var window = desktop.Show(pane.SelectedApp!.ProcessId, "Hello", picture, clicked);
        await UiTestHelpers.WaitUntilAsync(() => pane.Stream is { FrameCount: > 0 } && FrameView(devices.Window).Mapping is { IsEmpty: false }, because: "the window's first frame");
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-02-pane");

        Assert.True(workspace.IsWindowsBridgeSelected);
        Assert.Equal("Hello", pane.Title);
        Assert.Contains("Build started on Test PC", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("Hello started on Test PC", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("Hello from the PC", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal("Hello runs on Test PC", workspace.StatusText);

        // A click in the middle of the picture is a click in the middle of the window, which then shows its change.
        var view = FrameView(devices.Window);
        var map = view.Mapping;
        var middle = view.TranslatePoint(new Point(map.Left + (map.DrawnWidth / 2), map.Top + (map.DrawnHeight / 2)), devices.Window)!.Value;
        devices.Window.MouseDown(middle, MouseButton.Left);
        devices.Window.MouseUp(middle, MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => window.Clicks == 1 && pane.Stream!.Read(f => f.GetPixel(20, 200)) == BridgePictures.Green, because: "the clicked window in the pane");
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-03-pane-clicked");

        var down = desktop.Input.Select(i => i.Event).First(e => e.Kind == BridgeInputKind.PointerDown);
        Assert.Equal(BridgePointerButton.Left, down.Button);
        Assert.InRange(down.X, 255, 265);
        Assert.InRange(down.Y, 175, 185);

        workspace.StopDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => pane.SelectedApp is null, because: "the program to stop");
        Assert.Contains("The program 'Hello' has", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_build_error_on_the_PC_lands_in_the_Error_List_on_the_tablets_file()
    {
        await using var devices = BridgeDevices.Start(new FakeBridgeDesktop());
        var project = devices.WriteConsoleProject("Broken", "Console.WriteLine(greeting);\n");
        await devices.PairAsync();
        var workspace = await devices.OpenAsync(devices.SolutionFolder);
        workspace.RunTargets.Single(t => t.Name == "Broken" && t.Detail == Via).Select.Execute(null);

        await workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ErrorList.Items.Any(i => i.IsFromBridge), TimeSpan.FromMinutes(3), "the PC's build error");
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-04-error-list");

        var error = workspace.ErrorList.Items.First(i => i.IsFromBridge);
        Assert.Equal("CS0103", error.Code);
        Assert.Equal("Test PC", error.BridgePc);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(project)!, "Program.cs"), error.Diagnostic.Location!.FilePath);
        Assert.Equal("1", error.Line);
        Assert.Equal(BottomPanel.ErrorList, workspace.SelectedPanel);
        Assert.Contains("0 succeeded, 1 failed, on Test PC", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Null(workspace.WindowsBridgePane!.SelectedApp);
    }

    [AvaloniaFact]
    public async Task The_PC_going_to_sleep_is_said_plainly_and_the_tablet_keeps_working()
    {
        var desktop = new FakeBridgeDesktop();
        await using var devices = BridgeDevices.Start(desktop);
        devices.WriteConsoleProject("Hello");
        await devices.PairAsync();
        var workspace = await devices.OpenAsync(devices.SolutionFolder);
        workspace.RunTargets.Single(t => t.Name == "Hello" && t.Detail == Via).Select.Execute(null);
        await workspace.RunCommand.ExecuteAsync(null);
        var pane = workspace.WindowsBridgePane!;
        await UiTestHelpers.WaitUntilAsync(() => pane.SelectedApp is not null, TimeSpan.FromMinutes(3), "the program on the PC");
        desktop.Show(pane.SelectedApp!.ProcessId, "Hello", BridgePictures.AppWindow("Hello", "Hello from the PC", clicked: false));
        await UiTestHelpers.WaitUntilAsync(() => pane.Stream is { FrameCount: > 0 }, because: "the window's first frame");

        desktop.SetState(BridgePcState.Sleeping);
        await UiTestHelpers.WaitUntilAsync(() => devices.Tablet.Status?.State == BridgePcState.Sleeping, because: "the sleep notice");
        await devices.Pc.StopBridgeAsync();
        await UiTestHelpers.WaitUntilAsync(() => pane.IsPcAway && workspace.IsWindowsBridgeAway, because: "the PC to be away");
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-05-pc-away");

        Assert.Equal("Test PC is asleep or away", pane.MessageTitle);
        Assert.Equal("Test PC is away", workspace.WindowsBridgeStatusText);
        Assert.NotEqual(IdeState.Busy, workspace.State);
        Assert.True(workspace.RunCommand.CanExecute(null), "The tablet keeps working: it builds and runs its own projects.");
    }

    [AvaloniaFact]
    public async Task The_bridge_window_on_the_PC_shows_the_tablet_and_what_runs_for_it()
    {
        var desktop = new FakeBridgeDesktop();
        await using var devices = BridgeDevices.Start(desktop);
        var project = devices.WriteConsoleProject("Hello");
        await devices.PairAsync();
        var workspace = await devices.OpenAsync(devices.SolutionFolder);
        workspace.RunTargets.Single(t => t.Name == "Hello" && t.Detail == Via).Select.Execute(null);
        await workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WindowsBridgePane!.SelectedApp is not null, TimeSpan.FromMinutes(3), "the program on the PC");

        var pc = devices.PcServices.GetRequiredService<MainViewModel>();
        var pcWindow = new MainWindow { DataContext = pc, Width = 1280, Height = 860 };
        pcWindow.Show();
        pc.Link.Open(LinkTab.Bridge);
        var bridge = pc.Link.Bridge!;
        await UiTestHelpers.WaitUntilAsync(() => bridge.IsServing && bridge.RunningApps.Count == 1, because: "the tablet and its program on the PC");
        UiTestHelpers.SaveScreenshot(pcWindow, "windows-bridge-06-pc-window");

        Assert.True(bridge.IsPc);
        Assert.Equal("Be a Windows Bridge", bridge.TabTitle);
        Assert.Equal("Serving Galaxy Tab S9", bridge.BridgeTitle);
        Assert.Equal("Hello", bridge.RunningApps[0].Name);
        Assert.Equal(project, bridge.RunningApps[0].App.ProjectPath);
        Assert.Equal("Galaxy Tab S9", Assert.Single(bridge.BridgeTablets).Name);
        pcWindow.Close();
    }

    private static CompatibilityRowViewModel Row(WorkspaceViewModel workspace, string project) =>
        workspace.Compatibility.Single(r => r.Project == project);

    private static BridgeFrameView FrameView(Window window) =>
        window.GetVisualDescendants().OfType<BridgeFrameView>().Single(v => v.IsEffectivelyVisible);
}
