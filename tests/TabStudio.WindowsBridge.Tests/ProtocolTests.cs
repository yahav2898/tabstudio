using System.Collections.Concurrent;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>
/// A tablet and a bridge on the loopback address, over TabStudio Link: the workspace is mirrored, builds run with
/// the PC's own dotnet, programs run with their console on the tablet, windows stream, and input reaches them.
/// </summary>
public sealed class ProtocolTests : IDisposable
{
    private const string Waiting = """
        Console.WriteLine("Ready on the PC");
        Console.ReadLine();
        """;

    private readonly TempDirectory _folder = new();

    [Fact]
    public async Task The_tablet_pairs_like_a_Link_host_and_learns_what_the_PC_is()
    {
        await using var bridge = await BridgeLoopback.StartAsync(_folder);

        var info = await bridge.Client.GetInfoAsync();

        Assert.Equal("Test PC", info.PcName);
        Assert.Equal("Windows 11 (10.0.26100)", info.OperatingSystem);
        Assert.Equal(BridgePcState.Awake, info.State);
        Assert.Equal("Test tablet", bridge.Server.ConnectedTablet?.Name);
        Assert.True(((FakeDesktop)bridge.Desktop).IsKeptAwake, "The PC stays awake while a tablet uses it.");
    }

    [Fact]
    public async Task Only_new_and_changed_files_are_sent_to_the_PC()
    {
        await using var bridge = await BridgeLoopback.StartAsync(_folder);
        var (workspace, _) = bridge.CreateConsoleProject("Hello", "Console.WriteLine(1);");

        var first = await bridge.SyncAsync(workspace);
        var unchanged = await bridge.SyncAsync(workspace);
        _folder.Write("Tablet/Hello/Program.cs", "Console.WriteLine(2);");
        var changed = await bridge.SyncAsync(workspace);

        Assert.Equal(2, first.SentFiles.Count);
        Assert.Empty(unchanged.SentFiles);
        Assert.Equal([_folder.Resolve("Tablet/Hello/Program.cs")], changed.SentFiles);
    }

    [Fact]
    public async Task A_build_on_the_PC_streams_its_output_and_its_errors_land_on_the_tablets_files()
    {
        await using var bridge = await BridgeLoopback.StartAsync(_folder);
        var (workspace, project) = bridge.CreateConsoleProject("Broken", "Console.WriteLine(\"no semicolon\")\n");
        await bridge.SyncAsync(workspace);
        var output = new ConcurrentQueue<string>();

        var broken = await bridge.Client.BuildAsync(project, "Debug", skipReferences: false, output.Enqueue);
        _folder.Write("Tablet/Broken/Program.cs", "Console.WriteLine(\"fixed\");\n");
        await bridge.SyncAsync(workspace);
        var fixedBuild = await bridge.Client.BuildAsync(project, "Debug", skipReferences: true, _ => { });

        var error = Assert.Single(broken.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.False(broken.Success);
        Assert.Equal("CS1002", error.Id);
        Assert.Equal(_folder.Resolve("Tablet/Broken/Program.cs"), error.Location!.FilePath);
        Assert.Equal(1, error.Location.Line);
        Assert.Equal("dotnet build", broken.Tool);
        Assert.Contains(output, line => line.Contains("CS1002", StringComparison.Ordinal) && line.Contains(_folder.Resolve("Tablet/Broken/Program.cs"), StringComparison.Ordinal));
        Assert.DoesNotContain(output, line => line.Contains(bridge.Server.Options.DataFolder, StringComparison.Ordinal));
        Assert.True(fixedBuild.Success, string.Join('\n', fixedBuild.Diagnostics));
        Assert.Equal(_folder.Resolve("Tablet/Broken/bin/Debug/net10.0/Broken.dll"), fixedBuild.Program!.Path);
        Assert.True(fixedBuild.Program.IsDotNet);
    }

    [Fact]
    public async Task A_program_runs_on_the_PC_with_its_console_on_the_tablet()
    {
        await using var bridge = await BridgeLoopback.StartAsync(_folder);
        var (workspace, project) = bridge.CreateConsoleProject("Hello", "Console.WriteLine(\"Hello from the PC\");\nreturn 3;\n");
        await bridge.SyncAsync(workspace);
        var build = await bridge.Client.BuildAsync(project, "Debug", false, _ => { });
        var output = new ConcurrentQueue<string>();
        bridge.Client.AppOutput += (_, e) => output.Enqueue(e.Text);
        var exited = Waits.ForAsync<AppExited>(h => bridge.Client.AppExited += h, h => bridge.Client.AppExited -= h, _ => true, "the program's exit");

        var started = await bridge.Client.RunAsync(project, build.Program!);
        var exit = await exited;

        Assert.Equal("Hello", started.Name);
        Assert.Equal(started.AppId, exit.AppId);
        Assert.Equal(3, exit.ExitCode);
        Assert.Contains("Hello from the PC", output);
    }

    [Fact]
    public async Task A_window_streams_and_a_tap_on_its_button_shows_in_the_next_frames()
    {
        var desktop = new FakeDesktop();
        await using var bridge = await BridgeLoopback.StartAsync(_folder, desktop);
        var (app, window) = await StartWindowedAsync(bridge, desktop);
        using var stream = await OpenStreamAsync(bridge, app.AppId, window.Id);

        string State() => $"Tablet: {stream}. Window: {window.Clicks} clicks, {desktop.Input.Count} inputs, {desktop.Captures}. Bridge: {bridge.Server.DescribeStreams()}.";
        await Waits.UntilAsync(() => stream.FrameCount > 0 && Color(stream, window.Button) == FakeWindow.Gray, "the window's first frame", State);
        var map = FrameMapping.Fit(640, 480, stream.WindowWidth, stream.WindowHeight);
        var touch = new TouchGestureRecognizer();
        var (x, y) = (map.Left + (map.DrawnWidth / 2), map.Top + (map.DrawnHeight / 2));
        touch.Down(1, x, y, TimeSpan.Zero, map);
        bridge.Client.SendInput(app.AppId, window.Id, touch.Up(1, x, y, TimeSpan.FromMilliseconds(90), map));

        await Waits.UntilAsync(() => Color(stream, window.Button) == FakeWindow.Green, "the clicked button in a frame", State);
        Assert.Equal(1, window.Clicks);
        Assert.Equal(
            [BridgeInputKind.PointerMove, BridgeInputKind.PointerDown, BridgeInputKind.PointerUp],
            desktop.Input.Select(i => i.Event.Kind));
        Assert.Equal("Fake capture", stream.CaptureMethod);
    }

    [Fact]
    public async Task The_window_follows_the_pane_and_the_clipboard_goes_both_ways()
    {
        var desktop = new FakeDesktop();
        await using var bridge = await BridgeLoopback.StartAsync(_folder, desktop);
        var (app, window) = await StartWindowedAsync(bridge, desktop);
        using var stream = await OpenStreamAsync(bridge, app.AppId, window.Id);
        string State() => $"Tablet: {stream}. Window: {desktop.Captures}. Bridge: {bridge.Server.DescribeStreams()}.";
        await Waits.UntilAsync(() => stream.FrameCount > 0, "the window's first frame", State);

        await bridge.Client.ResizeAsync(app.AppId, window.Id, 500, 300);
        await Waits.UntilAsync(() => stream.WindowWidth == 500 && stream.Read(f => f.Height) == 300, "a frame of the resized window", State);
        desktop.Clipboard = "from the PC";
        var fromPc = await bridge.Client.GetClipboardAsync();
        await bridge.Client.SetClipboardAsync("from the tablet");

        Assert.Equal("from the PC", fromPc);
        Assert.Equal("from the tablet", desktop.Clipboard);
    }

    [Fact]
    public async Task Dialogs_are_listed_after_their_window_and_windows_that_close_leave_the_list()
    {
        var desktop = new FakeDesktop();
        await using var bridge = await BridgeLoopback.StartAsync(_folder, desktop);
        var (app, window) = await StartWindowedAsync(bridge, desktop);
        var withDialog = Waits.ForAsync<AppWindows>(h => bridge.Client.AppWindowsChanged += h, h => bridge.Client.AppWindowsChanged -= h, w => w.Windows.Count == 2, "the dialog");

        var dialog = desktop.Show(app.ProcessId, "Save changes?", 300, 120, isDialog: true);
        var both = await withDialog;
        var closed = Waits.ForAsync<AppWindows>(h => bridge.Client.AppWindowsChanged += h, h => bridge.Client.AppWindowsChanged -= h, w => w.Windows.Count == 1, "the dialog to close");
        dialog.Close();
        var one = await closed;

        Assert.Equal(["Main window", "Save changes?"], both.Windows.Select(w => w.Title));
        Assert.True(both.Windows[1].IsDialog);
        Assert.Equal(window.Id, Assert.Single(one.Windows).Id);
    }

    [Fact]
    public async Task The_PC_going_to_sleep_is_told_before_the_connection_drops()
    {
        var desktop = new FakeDesktop();
        await using var bridge = await BridgeLoopback.StartAsync(_folder, desktop);
        var sleeping = Waits.ForAsync<BridgeStatus>(h => bridge.Client.StatusChanged += h, h => bridge.Client.StatusChanged -= h, s => s.State == BridgePcState.Sleeping, "the sleep notice");

        desktop.SetState(BridgePcState.Sleeping);

        Assert.Equal(BridgePcState.Sleeping, (await sleeping).State);
    }

    [Fact]
    public async Task Programs_keep_running_while_the_tablet_is_away_and_are_there_when_it_is_back()
    {
        var desktop = new FakeDesktop();
        await using var bridge = await BridgeLoopback.StartAsync(_folder, desktop);
        var (app, window) = await StartWindowedAsync(bridge, desktop);

        await bridge.DisconnectAsync();
        await Waits.UntilAsync(() => bridge.Server.ConnectedTablet is null, "the bridge to see the tablet leave");
        var client = new BridgeClient();
        var again = Waits.ForAsync<AppStarted>(h => client.AppStarted += h, h => client.AppStarted -= h, a => a.AppId == app.AppId, "the running program");
        var windows = Waits.ForAsync<AppWindows>(h => client.AppWindowsChanged += h, h => client.AppWindowsChanged -= h, w => w.AppId == app.AppId && w.Windows.Count == 1, "its window");
        await bridge.ReconnectAsync(client);

        Assert.Equal(app.ProcessId, (await again).ProcessId);
        Assert.Equal(window.Id, (await windows).Windows[0].Id);
        Assert.Contains(bridge.Server.Apps, a => a.App.AppId == app.AppId);
    }

    [Fact]
    public async Task Requests_about_a_workspace_the_bridge_does_not_have_say_so()
    {
        await using var bridge = await BridgeLoopback.StartAsync(_folder);

        var error = await Assert.ThrowsAsync<TabStudio.Link.LinkRemoteException>(() => bridge.Client.BuildAsync("/storage/x/App.csproj", "Debug", false, _ => { }));

        Assert.Equal(TabStudio.Link.LinkErrorCodes.NoWorkspace, error.Code);
    }

    public void Dispose() => _folder.Dispose();

    private static uint? Color(BridgeStreamReceiver stream, PixelRect button) =>
        stream.Read(frame => frame.Width > button.X + (button.Width / 2) ? frame.GetPixel(button.X + (button.Width / 2), button.Y + (button.Height / 2)) : 0u);

    private static async Task<BridgeStreamReceiver> OpenStreamAsync(BridgeLoopback bridge, int appId, long windowId)
    {
        var receiver = await BridgeStreamReceiver.StartAsync(bridge.Client, appId, windowId, 1920, 1200);
        receiver.FrameReady += (_, _) => receiver.Shown();
        receiver.Shown();
        return receiver;
    }

    /// <summary>Builds and runs a program that waits, and opens a window for it on the fake desktop.</summary>
    private static async Task<(AppStarted App, FakeWindow Window)> StartWindowedAsync(BridgeLoopback bridge, FakeDesktop desktop)
    {
        var (workspace, project) = bridge.CreateConsoleProject("Windowed", Waiting);
        await bridge.SyncAsync(workspace);
        var build = await bridge.Client.BuildAsync(project, "Debug", false, _ => { });
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var listed = Waits.ForAsync<AppWindows>(h => bridge.Client.AppWindowsChanged += h, h => bridge.Client.AppWindowsChanged -= h, w => w.Windows.Count > 0, "the program's window");
        var app = await bridge.Client.RunAsync(project, build.Program!);
        var window = desktop.Show(app.ProcessId, "Main window", 320, 240);
        var windows = await listed;
        Assert.Equal(window.Id, windows.Windows[0].Id);
        return (app, window);
    }
}
