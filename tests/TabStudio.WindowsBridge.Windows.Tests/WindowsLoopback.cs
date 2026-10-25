using System.Collections.Concurrent;
using System.Runtime.Versioning;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Windows.Tests;

/// <summary>
/// The Windows Bridge on this PC, with its real desktop, and a tablet paired with it in the same process over the
/// loopback address: the tablet's workspace is a folder of the test's, the bridge's mirror another.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsLoopback : IAsyncDisposable
{
    private readonly LinkIdentity _bridgeIdentity = LinkIdentity.Create();
    private readonly LinkIdentity _tabletIdentity = LinkIdentity.Create();
    private LinkClientSession? _session;

    private WindowsLoopback(TempDirectory folder)
    {
        Folder = folder;
        Desktop = new WindowsBridgeDesktop();
        Server = new BridgeServer(
            new BridgeServerOptions
            {
                Identity = _bridgeIdentity,
                Tablets = new LinkTrustStore(null),
                PcName = Environment.MachineName,
                DataFolder = Path.Combine(folder.Path, "Bridge"),
                Port = 0,
                LoopbackOnly = true,
            },
            Desktop);
    }

    public TempDirectory Folder { get; }

    public WindowsBridgeDesktop Desktop { get; }

    public BridgeServer Server { get; }

    public BridgeClient Client { get; } = new();

    /// <summary>Starts the bridge, looking for this PC's tools, and pairs the tablet with it.</summary>
    public static async Task<WindowsLoopback> StartAsync(TempDirectory folder)
    {
        var loopback = new WindowsLoopback(folder);
        await loopback.Server.StartAsync();
        var invitation = loopback.Server.CreateInvitation(["127.0.0.1"])!;
        loopback._session = await LinkClient.ConnectAsync(
            new LinkClientOptions { Identity = loopback._tabletIdentity, DeviceName = "Test tablet" },
            LinkTarget.FromInvitation(invitation),
            loopback.Client);
        loopback.Client.Attach(loopback._session.Connection);
        return loopback;
    }

    /// <summary>Sends a project's folder to the bridge as the tablet's workspace, and builds the project there.</summary>
    public async Task<BuildOutcome> SyncAndBuildAsync(string projectPath, ConcurrentQueue<string>? output = null)
    {
        var workspace = WorkspaceLoader.Load(Path.GetDirectoryName(projectPath)!);
        await Client.SyncWorkspaceAsync(workspace, FileSystemTextProvider.Instance);
        return await Client.BuildAsync(projectPath, "Debug", false, line => output?.Enqueue(line));
    }

    /// <summary>Runs a built program and waits for its first window.</summary>
    public async Task<(AppStarted App, BridgeWindowInfo Window)> RunAsync(string projectPath, BuiltProgram program, string title)
    {
        var windows = new TaskCompletionSource<BridgeWindowInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        Client.AppWindowsChanged += (_, e) =>
        {
            if (e.Windows.FirstOrDefault(w => w.Title.Contains(title, StringComparison.Ordinal)) is { } window)
            {
                windows.TrySetResult(window);
            }
        };
        var app = await Client.RunAsync(projectPath, program);
        return (app, await windows.Task.WaitAsync(TimeSpan.FromSeconds(90)));
    }

    /// <summary>Streams a window and waits for a picture that passes a check.</summary>
    public static async Task<FrameBitmap> WaitForFrameAsync(BridgeStreamReceiver stream, Func<FrameBitmap, bool> check, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var picture = stream.Read(frame => check(frame) ? frame.Clone() : null);
            if (picture is not null)
            {
                return picture;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No frame showed {what} within a minute (frames so far: {stream.FrameCount}, capture: {stream.CaptureMethod}).");
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
        }

        await Server.DisposeAsync();
        Desktop.Dispose();
        _bridgeIdentity.Dispose();
        _tabletIdentity.Dispose();
    }
}
