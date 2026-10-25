using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using TabStudio.App.Platform;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;
using TabStudio.Swarm.Device;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's Swarm part: a phone with an identity of its own joins this tablet's swarm over TLS on 127.0.0.1,
/// F5 on the swarm runs the Avalonia template on it under the debugger, and a tap there stops at a breakpoint, which
/// the tablet's swarm sees with the phone's locals before it continues the phone.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The Avalonia project the swarm runs.</summary>
    public const string SwarmAppName = "SelfTestSwarm";

    /// <summary>The name of the phone that joins the swarm.</summary>
    public const string SwarmDeviceName = "Self-test phone";

    /// <summary>How long the phone may take to show the app, its packages restored before by the Avalonia part.</summary>
    private static readonly TimeSpan SwarmStartTimeout = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Runs the Avalonia template on a phone of the swarm and stops it at a breakpoint there. The breakpoint is checked
    /// and continued from a pool thread: on Android the app's UI thread, which is TabStudio's own, waits at it.
    /// </summary>
    /// <returns>Whether the phone ran the app and paused at the breakpoint with the right locals, with a summary.</returns>
    internal static async Task<SelfTestResult> RunOnSwarmAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var solution = templates.Create(ProjectTemplates.AvaloniaId, SwarmAppName);
        var viewModel = Path.Combine(Path.GetDirectoryName(solution)!, SwarmAppName, "ViewModels", "MainViewModel.cs");
        await File.WriteAllTextAsync(viewModel, SwarmViewModelSource, cancellationToken);
        await main.OpenWorkspaceAsync(solution);
        if (main.Workspace is not { Swarm: { } swarmWindow } workspace)
        {
            return new SelfTestResult(false, "Swarm: the workspace has no swarm.", clock.Elapsed);
        }

        var swarm = swarmWindow.Service;
        var startedHere = !swarm.IsOn;
        if (startedHere && !await swarm.StartAsync(port: 0, loopbackOnly: true))
        {
            return new SelfTestResult(false, $"Swarm: the swarm did not start: {swarm.Error}", clock.Elapsed);
        }

        using var identity = LinkIdentity.Create();
        using var host = new SwarmDeviceHost(new AvaloniaUiDispatcher(), new NoWebViewFactory(), new TopLevelStoragePicker(), DeviceProfile.For(new DeviceInfo(4L * 1024 * 1024 * 1024, 4, IsLowRamDevice: false)));
        host.SetScreen(412, 915, 2.625, isDark: true);
        var phone = new SwarmMember(
            new SwarmMemberOptions
            {
                Identity = identity,
                Coordinators = new LinkTrustStore(null),
                Name = SwarmDeviceName,
                Kind = LinkDeviceKinds.Phone,
                CacheDirectory = Path.Combine(Path.GetTempPath(), "TabStudio-self-test-swarm"),
            },
            host);
        Task? run = null;
        try
        {
            if (swarm.Coordinator?.Invitation is not { } invitation || !await phone.JoinAsync(LinkTarget.FromInvitation(invitation), cancellationToken))
            {
                return new SelfTestResult(false, $"Swarm: the phone did not join: {phone.Error}", clock.Elapsed);
            }

            await WaitAsync(() => swarm.Devices.Any(d => d.Name == SwarmDeviceName && d.IsConnected), cancellationToken);
            var device = swarm.Devices.First(d => d.Name == SwarmDeviceName);
            var security = device.Security ?? "TLS";

            var line = Array.FindIndex(await File.ReadAllLinesAsync(viewModel, cancellationToken), l => l.Contains("Clicks = next;", StringComparison.Ordinal)) + 1;
            workspace.Breakpoints.Add(viewModel, line);
            workspace.SelectSwarmTarget();
            await workspace.StartDebuggingCommand.ExecuteAsync(null);
            run = workspace.SwarmRun;
            using (var starting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                starting.CancelAfter(SwarmStartTimeout);
                while (!(host.Preview.View is { } shown && SwarmTexts(shown).Contains("Hello from Avalonia!")) && !run.IsCompleted)
                {
                    await Task.Delay(PollInterval, starting.Token);
                }
            }

            if (host.Preview.View is not { } view || view.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "CountButton")?.Command is not ICommand count)
            {
                return new SelfTestResult(false, $"Swarm: the phone did not show the app: {device.Detail ?? LastLines(workspace)}", clock.Elapsed);
            }

            var shownAfter = clock.Elapsed;
            var check = Task.Run(() => CheckBreakpointAsync(swarm.Coordinator!, device, cancellationToken), cancellationToken);

            // The tap runs into the breakpoint on the phone, and returns once the check continued it.
            count.Execute(null);
            var (paused, detail) = await check;
            if (!paused)
            {
                return new SelfTestResult(false, $"Swarm: {detail}", clock.Elapsed);
            }

            await WaitAsync(() => SwarmTexts(view).Contains("Clicked 1 times"), cancellationToken);
            return new SelfTestResult(
                true,
                string.Create(CultureInfo.InvariantCulture, $"Swarm: {SwarmDeviceName} joined over {security}, showed {SwarmAppName} in {shownAfter.TotalSeconds:0.0} s, {detail}, and counted on"),
                clock.Elapsed);
        }
        finally
        {
            if (run is not null)
            {
                workspace.StopDebuggingCommand.Execute(null);
                await run.WaitAsync(cancellationToken);
            }

            workspace.Breakpoints.Clear();
            await phone.DisposeAsync();
            if (startedHere)
            {
                await swarm.StopAsync();
            }
        }
    }

    /// <summary>Waits off the UI thread for the phone to stop at the breakpoint, reads its locals, and continues it.</summary>
    private static async Task<(bool Paused, string Detail)> CheckBreakpointAsync(SwarmCoordinator coordinator, SwarmDevice device, CancellationToken cancellationToken)
    {
        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (device.Pause is not { IsCurrent: true } && DateTime.UtcNow < deadline)
            {
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }

            if (device.Pause is not { IsCurrent: true } pause)
            {
                return (false, "the phone did not stop at the breakpoint.");
            }

            var locals = await pause.GetLocalsAsync(cancellationToken).ConfigureAwait(false);
            var next = locals.FirstOrDefault(v => v.Name == "next")?.Value;
            var method = pause.Frames.Count > 0 ? pause.Frames[0].MethodName : "?";
            var where = pause.Location is { } location ? $"{Path.GetFileName(location.FilePath)}, line {location.Line.ToString(CultureInfo.InvariantCulture)}" : "external code";
            return next == "1" && method.Contains("Count", StringComparison.Ordinal)
                ? (true, $"paused at the breakpoint in {method} ({where}) with next = {next}")
                : (false, $"the phone paused in {method} ({where}) with next = {next ?? "nothing"}.");
        }
        finally
        {
            // Whatever the check found, the phone, and the UI thread it stops, run on.
            await coordinator.ContinueAllAsync().ConfigureAwait(false);
        }
    }

    private static List<string> SwarmTexts(Control view) =>
        [.. view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];

    /// <summary>The template's view model, with a block for the command, so the breakpoint has a local to show.</summary>
    private const string SwarmViewModelSource = $$"""
        using CommunityToolkit.Mvvm.ComponentModel;
        using CommunityToolkit.Mvvm.Input;

        namespace {{SwarmAppName}}.ViewModels;

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

        """;
}
