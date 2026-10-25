using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using TabStudio.Core.Diagnostics;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;
using TabStudio.WindowsBridge.Windows.Desktop;

namespace TabStudio.WindowsBridge.Windows.Tests;

/// <summary>
/// The Windows Bridge on real Windows, through its service on the loopback address: template apps build with the
/// PC's own tools and run, their windows are captured and streamed, a tap on their button through the input path
/// shows in the next frames, MSBuild's errors land on the tablet's files, and the WPF app is debugged with netcoredbg.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsBridgeTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    [WindowsFact]
    public async Task The_bridge_finds_this_PCs_build_tools()
    {
        await using var bridge = await WindowsLoopback.StartAsync(_folder);

        var info = await bridge.Client.GetInfoAsync();

        Assert.StartsWith("Windows", info.OperatingSystem, StringComparison.Ordinal);
        Assert.Contains(info.Toolchains, t => t.Kind == BridgeToolchainKind.DotNetSdk);
        Assert.True(info.Capabilities.HasFlag(BridgeCapabilities.WindowsDesktop), info.Capabilities.ToString());
        Assert.True(info.Capabilities.HasFlag(BridgeCapabilities.MSBuild), string.Join('\n', info.Toolchains));
        Assert.True(info.Capabilities.HasFlag(BridgeCapabilities.Cpp), string.Join('\n', info.Toolchains));
    }

    [WindowsFact]
    public Task A_WPF_app_builds_runs_and_a_tap_on_its_button_shows_in_the_next_frames() =>
        TapTheButtonAsync(TemplateApps.CreateWpfAsync, "Hello WPF", BridgeInputPolicy.Automatic);

    [WindowsFact]
    public Task A_Windows_Forms_app_builds_runs_and_a_tap_on_its_button_shows_in_the_next_frames() =>
        TapTheButtonAsync(TemplateApps.CreateWinFormsAsync, "Hello Forms", BridgeInputPolicy.Automatic);

    [WindowsFact]
    public Task Input_reaches_a_Windows_Forms_app_without_taking_the_PCs_mouse() =>
        TapTheButtonAsync(TemplateApps.CreateWinFormsAsync, "Hello Forms", BridgeInputPolicy.Background);

    [WindowsFact]
    public async Task A_Visual_Cpp_project_builds_with_MSBuild_and_its_error_lands_on_the_tablets_file()
    {
        await using var bridge = await WindowsLoopback.StartAsync(_folder);
        var project = TemplateApps.CreateCpp(_folder, "return count;");
        var output = new ConcurrentQueue<string>();

        var broken = await bridge.SyncAndBuildAsync(project, output);
        TemplateApps.CreateCpp(_folder, "std::puts(\"Hello from C++\");\n    return 7;");
        var built = await bridge.SyncAndBuildAsync(project);
        var exited = new TaskCompletionSource<AppExited>(TaskCreationOptions.RunContinuationsAsynchronously);
        var console = new ConcurrentQueue<string>();
        bridge.Client.AppExited += (_, e) => exited.TrySetResult(e);
        bridge.Client.AppOutput += (_, e) => console.Enqueue(e.Text);
        await bridge.Client.RunAsync(project, built.Program!);
        var exit = await exited.Task.WaitAsync(TimeSpan.FromSeconds(60));

        var error = Assert.Single(broken.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("C2065", error.Id);
        Assert.Equal(_folder.Resolve("Tablet/HelloCpp/main.cpp"), error.Location!.FilePath);
        Assert.Equal(5, error.Location.Line);
        Assert.StartsWith("MSBuild", broken.Tool, StringComparison.Ordinal);
        Assert.Contains(output, line => line.Contains("C2065", StringComparison.Ordinal));
        Assert.True(built.Success, string.Join('\n', built.Diagnostics));
        Assert.False(built.Program!.IsDotNet);
        Assert.Equal(7, exit.ExitCode);
        Assert.Contains("Hello from C++", console);
    }

    [WindowsFact]
    public async Task A_NET_Framework_program_builds_with_MSBuild_and_runs()
    {
        await using var bridge = await WindowsLoopback.StartAsync(_folder);
        var project = TemplateApps.CreateNetFramework(_folder);

        var built = await bridge.SyncAndBuildAsync(project);
        var exited = new TaskCompletionSource<AppExited>(TaskCreationOptions.RunContinuationsAsynchronously);
        var console = new ConcurrentQueue<string>();
        bridge.Client.AppExited += (_, e) => exited.TrySetResult(e);
        bridge.Client.AppOutput += (_, e) => console.Enqueue(e.Text);
        await bridge.Client.RunAsync(project, built.Program!);
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(built.Success, string.Join('\n', built.Diagnostics));
        Assert.StartsWith("MSBuild", built.Tool, StringComparison.Ordinal);
        Assert.Contains(console, line => line.StartsWith("Hello from .NET Framework 4.", StringComparison.Ordinal));
    }

    [WindowsFact]
    public async Task The_WPF_app_is_debugged_to_a_breakpoint_with_netcoredbg_through_the_tunnel()
    {
        await using var bridge = await WindowsLoopback.StartAsync(_folder);
        using (var http = new HttpClient())
        {
            await bridge.Server.InstallDebuggerAsync(http);
        }

        var project = await TemplateApps.CreateWpfAsync(_folder);
        var build = await bridge.SyncAndBuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var file = _folder.Resolve("Tablet/HelloWpf/MainWindow.xaml.cs");
        var started = new TaskCompletionSource<AppStarted>(TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.Client.AppStarted += (_, e) =>
        {
            if (e.IsDebugging)
            {
                started.TrySetResult(e);
            }
        };

        await using var session = await DebugAdapterSession.StartAsync(
            BridgeDebugLauncher.Adapter(Environment.MachineName),
            BridgeDebugLauncher.Command,
            Path.GetDirectoryName(project)!,
            new BridgeDebugLauncher(bridge.Client, project));
        var stopped = new TaskCompletionSource<DapStoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Stopped += (_, e) => stopped.TrySetResult(e);
        var configuration = new LaunchConfiguration
        {
            Name = "HelloWpf",
            Type = "coreclr",
            Request = "launch",
            Properties = new JsonObject { ["program"] = build.Program!.Path, ["cwd"] = Path.GetDirectoryName(build.Program.Path) },
        };
        await session.LaunchAsync(
            configuration,
            new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> { [file] = [new DapSourceBreakpoint(TemplateApps.WpfConstructorLine)] },
            [],
            CancellationToken.None);

        var stop = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(120));
        var thread = stop.ThreadId ?? (await session.GetThreadsAsync(CancellationToken.None))[0].Id;
        var frames = await session.GetStackTraceAsync(thread, CancellationToken.None);
        var program = await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal("breakpoint", stop.Reason);
        Assert.Equal(file, frames[0].FilePath, ignoreCase: true);
        Assert.Equal(TemplateApps.WpfConstructorLine, frames[0].Line);
        Assert.Equal("HelloWpf", program.Name);
        await session.TerminateAsync(CancellationToken.None);
    }

    [WindowsFact]
    public async Task Clipboard_text_goes_both_ways()
    {
        await using var bridge = await WindowsLoopback.StartAsync(_folder);

        await bridge.Client.SetClipboardAsync("from the tablet");
        var back = await bridge.Client.GetClipboardAsync();

        Assert.Equal("from the tablet", back);
    }

    public void Dispose() => _folder.Dispose();

    /// <summary>Builds and runs an app, streams its window, taps its button and waits for the green window.</summary>
    private async Task TapTheButtonAsync(Func<TempDirectory, Task<string>> create, string title, BridgeInputPolicy policy)
    {
        await using var bridge = await WindowsLoopback.StartAsync(_folder);
        bridge.Desktop.InputPolicy = policy;
        var project = await create(_folder);
        var build = await bridge.SyncAndBuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.Equal("dotnet build", build.Tool);

        var (app, window) = await bridge.RunAsync(project, build.Program!, title);
        var started = await bridge.Client.StartStreamAsync(app.AppId, window.Id, 1920, 1200);
        using var stream = new BridgeStreamReceiver(bridge.Client, started.StreamId, started.CaptureMethod);
        stream.FrameReady += (_, _) => stream.Shown();

        // The window as it starts: the light background around its button.
        var before = await WindowsLoopback.WaitForFrameAsync(stream, frame => IsBackground(frame, 0xFFF3F3F3), "the window's light background");
        var map = FrameMapping.Fit(1280, 800, stream.WindowWidth, stream.WindowHeight, window.Scale);
        var touch = new TouchGestureRecognizer();
        var (x, y) = (map.Left + (map.DrawnWidth / 2), map.Top + (map.DrawnHeight / 2) + (map.DrawnHeight / 20));
        touch.Down(1, x, y, TimeSpan.Zero, map);
        bridge.Client.SendInput(app.AppId, window.Id, touch.Up(1, x, y, TimeSpan.FromMilliseconds(80), map));

        var after = await WindowsLoopback.WaitForFrameAsync(stream, frame => IsBackground(frame, TemplateApps.Green), "the green window after the tap");
        Assert.Equal(before.Width, after.Width);
        Assert.NotNull(started.CaptureMethod);
        if (policy == BridgeInputPolicy.Background)
        {
            Assert.Equal("window messages", bridge.Desktop.LastInputMode);
        }

        await bridge.Client.StopAsync(app.AppId);
    }

    /// <summary>Whether the window's client area left of its button has a color.</summary>
    private static bool IsBackground(FrameBitmap frame, uint argb)
    {
        var pixel = frame.GetPixel(Math.Min(16, frame.Width - 1), frame.Height * 3 / 4);
        return Close(pixel, argb);
    }

    private static bool Close(uint a, uint b)
    {
        for (var shift = 0; shift < 24; shift += 8)
        {
            if (Math.Abs((int)((a >> shift) & 0xFF) - (int)((b >> shift) & 0xFF)) > 6)
            {
                return false;
            }
        }

        return true;
    }
}
