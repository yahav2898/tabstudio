using System.Threading.Channels;
using TabStudio.Compilation.Running;
using TabStudio.Debugging;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.Swarm.Tests;

/// <summary>
/// Debugging the app on a device from the tablet: breakpoints, pausing, the call stack, locals, members and Watch
/// expressions, stepping, Break All, and pausing every device when one hits a breakpoint.
/// </summary>
public sealed class RemoteDebuggingTests
{
    private const string Adder = """
        var total = 0;
        for (var i = 0; i < 3; i++)
        {
            total = Add(total, i);
        }

        Console.WriteLine($"Total: {total}");

        static int Add(int a, int b)
        {
            var sum = a + b;
            return sum;
        }
        """;

    [Fact]
    public async Task A_breakpoint_on_a_device_pauses_it_with_its_call_stack_and_locals_and_steps_from_there()
    {
        await using var swarm = new SwarmLoopback();
        await swarm.JoinAsync("Pixel 9");
        var pauses = Pauses(swarm.Coordinator);
        var exited = Exits(swarm.Coordinator);
        var build = TestPrograms.Debug(Adder);
        await swarm.Coordinator.SetBreakpointsAsync([new SwarmBreakpoint(TestPrograms.FilePath, 11)]);

        await swarm.Coordinator.RunAsync(DebugBuild(build, SwarmAppKind.Console), CancellationToken.None);

        var first = await NextAsync(pauses);
        Assert.Equal(PauseReason.Breakpoint, first.Reason);
        Assert.Equal("Pixel 9", first.Device.Name);
        Assert.Equal(11, first.Location!.Line);
        Assert.Equal(TestPrograms.FilePath, first.Location.FilePath);
        Assert.Contains("Add", first.Frames[0].MethodName, StringComparison.Ordinal);
        Assert.True(first.Frames.Count >= 2, "The caller is on the call stack.");
        Assert.Equal(4, first.Frames[1].Location!.Line);
        Assert.Equal((TestPrograms.FilePath, 11), (first.BreakpointFile, first.BreakpointLine));
        Assert.Equal(SwarmDeviceState.Paused, first.Device.State);
        Assert.Same(first, first.Device.Pause);

        var locals = await first.GetLocalsAsync(CancellationToken.None);
        Assert.Equal("0", Assert.Single(locals, v => v.Name == "a").Value);
        Assert.Equal("int", Assert.Single(locals, v => v.Name == "b").Type);
        Assert.Equal("0", (await first.EvaluateAsync("b", CancellationToken.None)).Value!.Value);
        Assert.Contains("not a variable", (await first.EvaluateAsync("nothing", CancellationToken.None)).Error, StringComparison.Ordinal);

        Assert.True(await first.ResumeAsync(SwarmResumeMode.StepOver, CancellationToken.None));
        var stepped = await NextAsync(pauses);
        Assert.Equal(PauseReason.Step, stepped.Reason);
        Assert.Equal(12, stepped.Location!.Line);
        Assert.False(first.IsCurrent);
        Assert.Equal("0", Assert.Single(await stepped.GetLocalsAsync(CancellationToken.None), v => v.Name == "sum").Value);
        await Assert.ThrowsAsync<TabStudio.Link.LinkRemoteException>(() => first.GetLocalsAsync(CancellationToken.None));

        Assert.True(await stepped.ResumeAsync(SwarmResumeMode.Continue, CancellationToken.None));
        var second = await NextAsync(pauses);
        Assert.Equal("1", Assert.Single(await second.GetLocalsAsync(CancellationToken.None), v => v.Name == "b").Value);

        await swarm.Coordinator.SetBreakpointsAsync([]);
        await swarm.Coordinator.ContinueAllAsync();
        var exit = await exited.Task.WaitAsync(SwarmLoopback.Patience);
        Assert.Equal(RunOutcome.Completed, exit.Outcome);
    }

    [Fact]
    public async Task The_members_and_items_of_a_paused_value_are_read_from_the_device()
    {
        await using var swarm = new SwarmLoopback();
        await swarm.JoinAsync("Pixel 9");
        var pauses = Pauses(swarm.Coordinator);
        var build = TestPrograms.Debug("""
            var order = new Order("Tea", [3, 4]);
            Console.WriteLine(order);

            record Order(string Name, int[] Counts);
            """);
        await swarm.Coordinator.SetBreakpointsAsync([new SwarmBreakpoint(TestPrograms.FilePath, 2)]);

        await swarm.Coordinator.RunAsync(DebugBuild(build, SwarmAppKind.Console), CancellationToken.None);

        var pause = await NextAsync(pauses);
        var order = Assert.Single(await pause.GetLocalsAsync(CancellationToken.None), v => v.Name == "order");
        Assert.True(order.HasChildren);
        var members = await pause.GetChildrenAsync(order, CancellationToken.None);
        Assert.Equal("\"Tea\"", Assert.Single(members, m => m.Name == "Name").Value);
        var counts = Assert.Single(members, m => m.Name == "Counts");
        Assert.Equal("order.Counts", counts.Path);
        var items = await pause.GetChildrenAsync(counts, CancellationToken.None);
        Assert.Equal(["3", "4"], items.Select(i => i.Value));
        Assert.Equal("order.Counts[1]", items[1].Path);
        Assert.Equal("4", (await pause.EvaluateAsync("order.Counts[1]", CancellationToken.None)).Value!.Value);
        await swarm.Coordinator.StopRunAsync();
    }

    [Fact]
    public async Task Break_All_pauses_a_running_device_and_continuing_lets_it_finish()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var pauses = Pauses(swarm.Coordinator);
        var exited = Exits(swarm.Coordinator);
        var looping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        swarm.Coordinator.OutputReceived += (_, e) => looping.TrySetResult();
        var build = TestPrograms.Debug("""
            Console.WriteLine("Looping");
            for (var i = 0; i < 400; i++)
            {
                Thread.Sleep(5);
            }
            """);
        await swarm.Coordinator.RunAsync(DebugBuild(build, SwarmAppKind.Console), CancellationToken.None);
        var device = phone.OnCoordinator(swarm.Coordinator)!;
        await looping.Task.WaitAsync(SwarmLoopback.Patience);

        await swarm.Coordinator.PauseAllAsync();

        var pause = await NextAsync(pauses);
        Assert.Equal(PauseReason.Pause, pause.Reason);
        Assert.Contains(pause.Location!.Line, new[] { 2, 4 });
        Assert.Equal(SwarmDeviceState.Paused, device.State);
        await swarm.Coordinator.ContinueAllAsync();
        await SwarmLoopback.UntilAsync(() => device.Pause is null, "the device to continue");
        Assert.Equal(RunOutcome.Completed, (await exited.Task.WaitAsync(SwarmLoopback.Patience)).Outcome);
    }

    [Fact]
    public async Task With_pause_all_a_breakpoint_on_one_device_pauses_every_device_until_they_continue_together()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9", host => host.RunsEntryPoint = true);
        var tablet = await swarm.JoinAsync("Galaxy Tab S9", host => host.RunsEntryPoint = true);
        swarm.Coordinator.PauseAllOnBreakpoint = true;
        var pauses = Pauses(swarm.Coordinator);
        var build = TestPrograms.Debug("""
            var device = args[0];
            var work = 0;
            for (var i = 0; i < 400; i++)
            {
                Thread.Sleep(5);
                if (device == "Pixel 9" && i == 40)
                {
                    work += 1000;
                }
            }
            """);
        await swarm.Coordinator.SetBreakpointsAsync([new SwarmBreakpoint(TestPrograms.FilePath, 8)]);

        await swarm.Coordinator.RunAsync(DebugBuild(build, SwarmAppKind.Avalonia), CancellationToken.None);

        var hit = await NextAsync(pauses);
        Assert.Equal("Pixel 9", hit.Device.Name);
        Assert.Equal(PauseReason.Breakpoint, hit.Reason);
        var other = await NextAsync(pauses);
        Assert.Equal("Galaxy Tab S9", other.Device.Name);
        Assert.Equal(PauseReason.Pause, other.Reason);
        Assert.All(swarm.Coordinator.Devices, d => Assert.Equal(SwarmDeviceState.Paused, d.State));

        await swarm.Coordinator.ContinueAllAsync();

        await SwarmLoopback.UntilAsync(() => swarm.Coordinator.Devices.All(d => d.State == SwarmDeviceState.Exited), "both devices to finish");
        Assert.All(swarm.Coordinator.Devices, d => Assert.Null(d.Pause));
        Assert.Equal(2, phone.Host.Shown.Count + tablet.Host.Shown.Count);
    }

    private static SwarmBuild DebugBuild(TabStudio.Debugging.Building.DebugBuild build, SwarmAppKind kind) => new()
    {
        AppName = "Hello",
        Kind = kind,
        Entry = build.Result.EntryAssembly!,
        Dependencies = build.Result.Dependencies,
        Debug = new SwarmDebugOptions(build.DebugInfo),
    };

    private static Channel<SwarmPause> Pauses(SwarmCoordinator coordinator)
    {
        var pauses = Channel.CreateUnbounded<SwarmPause>();
        coordinator.Paused += (_, pause) => pauses.Writer.TryWrite(pause);
        return pauses;
    }

    private static TaskCompletionSource<SwarmExitEventArgs> Exits(SwarmCoordinator coordinator)
    {
        var exited = new TaskCompletionSource<SwarmExitEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.AppExited += (_, e) => exited.TrySetResult(e);
        return exited;
    }

    private static async Task<SwarmPause> NextAsync(Channel<SwarmPause> pauses) =>
        await pauses.Reader.ReadAsync().AsTask().WaitAsync(SwarmLoopback.Patience);
}
