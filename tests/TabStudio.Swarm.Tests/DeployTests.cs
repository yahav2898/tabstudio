using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Swarm.Coordinator;
using TabStudio.Testing;

namespace TabStudio.Swarm.Tests;

/// <summary>Sending builds to every device through the cache of files by hash, updating them, stopping, and failures.</summary>
public sealed class DeployTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task A_build_runs_on_every_device_and_an_update_sends_only_what_changed()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var tablet = await swarm.JoinAsync("Galaxy Tab S9");
        var package = Package("Toolkit");

        var first = await swarm.Coordinator.RunAsync(Build("Clicked {0} times", package), CancellationToken.None);

        Assert.Equal(2, first.Deployments.Count);
        Assert.All(first.Deployments, d =>
        {
            Assert.True(d.Succeeded, d.Error);
            Assert.Equal(3, d.FileCount);
            Assert.Equal(3, d.FilesSent);
        });
        await SwarmLoopback.UntilAsync(() => phone.Host.Current is not null && tablet.Host.Current is not null, "both devices to show the app");
        var shown = phone.Host.Current!.App;
        Assert.Equal("HelloAvalonia", shown.Name);
        Assert.Equal(SwarmAppKind.Avalonia, shown.Kind);
        Assert.Equal(first.Build.Entry.Image, shown.Entry!.Image);
        Assert.Equal(first.Build.Entry.Symbols, shown.Entry.Symbols);
        Assert.Equal("Toolkit.dll", Path.GetFileName(Assert.Single(shown.PackageAssemblies)));
        Assert.Equal(await File.ReadAllBytesAsync(package), await File.ReadAllBytesAsync(shown.PackageAssemblies[0]));
        await SwarmLoopback.UntilAsync(() => swarm.Coordinator.Devices.All(d => d.State == SwarmDeviceState.Running), "both devices to run it");
        Assert.All(swarm.Coordinator.Devices, d => Assert.NotNull(d.LastUpdated));

        // A save: the app's own assembly changed, the package did not.
        var second = await swarm.Coordinator.RunAsync(Build("Tapped {0} times", package), CancellationToken.None);

        Assert.All(second.Deployments, d =>
        {
            Assert.True(d.Succeeded, d.Error);
            Assert.Equal(2, d.FilesSent);
            Assert.True(d.BytesSent < first.Deployments[0].BytesSent, "Only the changed files travel.");
        });
        await SwarmLoopback.UntilAsync(() => phone.Host.Shown.Count == 2 && tablet.Host.Shown.Count == 2, "both devices to restart the app");
        Assert.NotEqual(phone.Host.Shown.First().Entry!.Image, phone.Host.Current!.App.Entry!.Image);
        Assert.Equal(second.Build.Entry.Image, phone.Host.Current.App.Entry.Image);
        Assert.Equal(second.Id, swarm.Coordinator.Devices[0].RunId);
    }

    [Fact]
    public async Task A_device_that_joins_while_the_app_runs_gets_it()
    {
        await using var swarm = new SwarmLoopback();
        await swarm.JoinAsync("Pixel 9");
        var deployed = new TaskCompletionSource<SwarmDeployment>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = await swarm.Coordinator.RunAsync(Build("Clicked {0} times"), CancellationToken.None);
        swarm.Coordinator.Deployed += (_, deployment) =>
        {
            if (deployment.Device.Name == "Moto G")
            {
                deployed.TrySetResult(deployment);
            }
        };

        var late = await swarm.JoinAsync("Moto G");

        var deployment = await deployed.Task.WaitAsync(SwarmLoopback.Patience);
        Assert.True(deployment.Succeeded, deployment.Error);
        await SwarmLoopback.UntilAsync(() => late.Host.Current is not null, "the late device to show the app");
        Assert.Equal(run.Build.Entry.Image, late.Host.Current!.App.Entry!.Image);
        Assert.Equal(2, run.Deployments.Count);
    }

    [Fact]
    public async Task Restarting_on_one_device_starts_its_app_fresh_without_sending_files_or_touching_the_others()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var tablet = await swarm.JoinAsync("Galaxy Tab S9");
        await swarm.Coordinator.RunAsync(Build("Clicked {0} times"), CancellationToken.None);
        await SwarmLoopback.UntilAsync(() => swarm.Coordinator.Devices.All(d => d.State == SwarmDeviceState.Running), "both devices to run it");

        var device = phone.OnCoordinator(swarm.Coordinator)!;
        Assert.True(await swarm.Coordinator.RestartAsync(device, CancellationToken.None));

        await SwarmLoopback.UntilAsync(() => phone.Host.Shown.Count == 2 && device.State == SwarmDeviceState.Running, "the phone to start the app again");
        Assert.Single(tablet.Host.Shown);
        var restart = swarm.Coordinator.CurrentRun!.Deployments[^1];
        Assert.Same(device, restart.Device);
        Assert.Equal(0, restart.FilesSent);
    }

    [Fact]
    public async Task Stopping_stops_the_app_on_every_device()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var tablet = await swarm.JoinAsync("Galaxy Tab S9");
        await swarm.Coordinator.RunAsync(Build("Clicked {0} times"), CancellationToken.None);
        await SwarmLoopback.UntilAsync(() => phone.Host.Current is not null && tablet.Host.Current is not null, "both devices to show the app");

        await swarm.Coordinator.StopRunAsync();

        Assert.Null(swarm.Coordinator.CurrentRun);
        await SwarmLoopback.UntilAsync(() => phone.Host.Current!.IsDisposed && tablet.Host.Current!.IsDisposed, "both apps to stop");
        await SwarmLoopback.UntilAsync(() => swarm.Coordinator.Devices.All(d => d.State == SwarmDeviceState.Ready), "both devices to wait for the next build");
    }

    [Fact]
    public async Task A_device_that_fails_never_stops_the_others()
    {
        await using var swarm = new SwarmLoopback();
        var broken = await swarm.JoinAsync("Broken phone", host => host.FailToStart = true);
        var phone = await swarm.JoinAsync("Pixel 9");
        var leaving = await swarm.JoinAsync("Moto G");
        await leaving.Member.LeaveAsync();

        await swarm.Coordinator.RunAsync(Build("Clicked {0} times"), CancellationToken.None);

        await SwarmLoopback.UntilAsync(() => broken.OnCoordinator(swarm.Coordinator)!.State == SwarmDeviceState.Crashed, "the broken device to report");
        Assert.Contains("unknown element 'Buton'", broken.OnCoordinator(swarm.Coordinator)!.LastFault!.Message, StringComparison.Ordinal);
        await SwarmLoopback.UntilAsync(() => phone.OnCoordinator(swarm.Coordinator)!.State == SwarmDeviceState.Running, "the other device to run");
        Assert.NotNull(phone.Host.Current);
        Assert.Equal(2, swarm.Coordinator.Devices.Count);
    }

    [Fact]
    public async Task A_crash_on_a_device_reaches_the_tablet_with_its_stack_trace()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var exited = new TaskCompletionSource<SwarmExitEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        swarm.Coordinator.AppExited += (_, e) => exited.TrySetResult(e);
        await swarm.Coordinator.RunAsync(Build("Clicked {0} times"), CancellationToken.None);
        await SwarmLoopback.UntilAsync(() => phone.Host.Current is not null, "the app");

        phone.Host.Current!.Crash(Thrown());

        var exit = await exited.Task.WaitAsync(SwarmLoopback.Patience);
        Assert.Equal(RunOutcome.Crashed, exit.Outcome);
        Assert.Equal("System.InvalidOperationException", exit.Fault!.Type);
        Assert.Equal("The count overflowed.", exit.Fault.Message);
        Assert.Contains(nameof(Thrown), exit.Fault.StackTrace, StringComparison.Ordinal);
        await SwarmLoopback.UntilAsync(() => phone.OnCoordinator(swarm.Coordinator)!.State == SwarmDeviceState.Crashed, "the crashed state");
        Assert.Same(exit.Fault, exit.Device.LastFault);
    }

    [Fact]
    public async Task Screen_requests_reach_the_device_and_its_inputs_reach_the_tablet()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var device = phone.OnCoordinator(swarm.Coordinator)!;
        var captured = new TaskCompletionSource<SwarmInput>(TaskCreationOptions.RunContinuationsAsynchronously);
        swarm.Coordinator.InputCaptured += (_, e) => captured.TrySetResult(e.Input);
        await swarm.Coordinator.RunAsync(Build("Clicked {0} times"), CancellationToken.None);
        await SwarmLoopback.UntilAsync(() => phone.Host.Current is not null, "the app");
        phone.Host.Texts["MainView/TextBlock#Clicks"] = "Clicked 2 times";

        Assert.True(await device.SendInputAsync(new SwarmInput(SwarmInputKind.Tap, "MainView/Button#CountButton", 0.5, 0.5), CancellationToken.None));
        Assert.False(await device.SendInputAsync(new SwarmInput(SwarmInputKind.Tap, "MainView/Button#Missing"), CancellationToken.None));
        Assert.Equal(FakeDeviceHost.Picture, await device.CaptureAsync(CancellationToken.None));
        Assert.Equal("SmallTouchTarget", Assert.Single(await device.InspectLayoutAsync(CancellationToken.None)).Kind);
        Assert.Equal("Clicked 2 times", await device.ReadTextAsync("MainView/TextBlock#Clicks", CancellationToken.None));
        Assert.Null(await device.ReadTextAsync("MainView/TextBlock#Nothing", CancellationToken.None));
        await device.ShowNoticeAsync("Tap on Save could not be delivered here.");
        await SwarmLoopback.UntilAsync(() => !phone.Host.Notices.IsEmpty, "the notice");

        phone.Member.ReportInput(new SwarmInput(SwarmInputKind.Text, string.Empty, Text: "Ada"));
        Assert.Equal("Ada", (await captured.Task.WaitAsync(SwarmLoopback.Patience)).Text);

        phone.Host.Screen = new SwarmScreenInfo(915, 412, 2.6);
        phone.Member.ReportScreen();
        await SwarmLoopback.UntilAsync(() => device.Screen.Width == 915, "the turned screen");
    }

    public void Dispose() => _temp.Dispose();

    private static InvalidOperationException Thrown()
    {
        try
        {
            throw new InvalidOperationException("The count overflowed.");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    /// <summary>An "Avalonia app": its own assembly, with a PDB, and maybe a package.</summary>
    private static SwarmBuild Build(string format, params string[] packages) => new()
    {
        AppName = "HelloAvalonia",
        Kind = SwarmAppKind.Avalonia,
        Entry = TestPrograms.Library($$"""public static class Texts { public static string Clicks(int count) => string.Format("{{format}}", count); }""", "HelloAvalonia"),
        PackageAssemblies = packages,
    };

    /// <summary>A package assembly, as NuGet restored it on the tablet.</summary>
    private string Package(string name)
    {
        var library = TestPrograms.Library("public static class Toolkit { public static int Answer => 42; }", name);
        var path = Path.Combine(_temp.Path, "packages", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, library.Image);
        return path;
    }
}
