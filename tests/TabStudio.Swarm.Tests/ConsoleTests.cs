using System.Collections.Concurrent;
using TabStudio.Compilation.Running;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.Swarm.Tests;

/// <summary>Console apps on a device: their output streamed to the tablet, their input sent from it.</summary>
public sealed class ConsoleTests
{
    [Fact]
    public async Task Console_output_streams_back_and_input_comes_from_the_tablet()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        var output = new ConcurrentQueue<SwarmOutputEventArgs>();
        var exited = new TaskCompletionSource<SwarmExitEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        swarm.Coordinator.OutputReceived += (_, e) => output.Enqueue(e);
        swarm.Coordinator.AppExited += (_, e) => exited.TrySetResult(e);
        var shown = new ConcurrentQueue<string>();
        phone.Member.ConsoleOutput += (_, e) => shown.Enqueue(e.Text);

        var entry = TestPrograms.Console("""
            Console.WriteLine("What is your name?");
            var name = Console.ReadLine();
            Console.Error.WriteLine("Writing to standard error.");
            Console.WriteLine($"Hello, {name}!");
            return 7;
            """);
        await swarm.Coordinator.RunAsync(new SwarmBuild { AppName = "Hello", Kind = SwarmAppKind.Console, Entry = entry }, CancellationToken.None);

        await SwarmLoopback.UntilAsync(() => Text(output).Contains("What is your name?", StringComparison.Ordinal), "the question");
        swarm.Coordinator.SendConsoleInput("Ada");

        var exit = await exited.Task.WaitAsync(SwarmLoopback.Patience);
        Assert.Equal(RunOutcome.Completed, exit.Outcome);
        Assert.Equal(7, exit.ExitCode);
        Assert.Contains("Hello, Ada!", Text(output), StringComparison.Ordinal);
        Assert.Contains(output, e => e.IsError && e.Text.Contains("standard error", StringComparison.Ordinal));
        Assert.All(output, e => Assert.Equal("Pixel 9", e.Device.Name));
        Assert.Contains("Hello, Ada!", string.Concat(shown), StringComparison.Ordinal);
        await SwarmLoopback.UntilAsync(() => phone.OnCoordinator(swarm.Coordinator)!.State == SwarmDeviceState.Exited, "the exited state");
    }

    private static string Text(IEnumerable<SwarmOutputEventArgs> output) => string.Concat(output.Where(e => !e.IsError).Select(e => e.Text));
}
