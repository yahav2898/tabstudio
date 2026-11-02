using TabStudio.Containers.Registry;
using TabStudio.Containers.Runtime;
using TabStudio.Containers.Store;
using TabStudio.Services;
using TabStudio.Services.Compose;
using TabStudio.Testing;
using Xunit.Abstractions;

namespace TabStudio.Containers.Tests;

/// <summary>Restart policies, low memory, the system's kills and Compose data, with real images under PRoot.</summary>
public sealed class RuntimeHardeningTests(ITestOutputHelper output)
{
    private static ContainerEngine Engine(TempDirectory temp, HttpClient http) => new(new()
    {
        StoreRoot = Path.Combine(temp.Path, "store"),
        ProotPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_PROOT"),
        LoaderPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_LOADER"),
    }, new RegistryClient(http, new SessionRegistryCredentialStore()));

    private static async Task Until(Func<bool> condition, TimeSpan timeout, string what)
    {
        using var deadline = new CancellationTokenSource(timeout);
        while (!condition())
        {
            if (deadline.IsCancellationRequested) throw new TimeoutException(what);
            await Task.Delay(100, CancellationToken.None);
        }
    }

    [ContainerRuntimeFact]
    public async Task On_failure_restarts_as_often_as_asked_then_stays_exited()
    {
        // Every restart policy was ignored: the monitor checked a token it had itself just cancelled.
        using var temp = new TempDirectory();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        await using var engine = Engine(temp, http);
        var container = await engine.CreateAsync(new ContainerSpec
        {
            Image = "redis:7-alpine", Name = "fails", Entrypoint = ["sh", "-c", "echo attempt; exit 3"], Restart = RestartPolicy.Parse("on-failure:2"),
        });
        await engine.StartAsync(container.Id);
        await Until(() => engine.Find(container.Id) is { RestartCount: 2, Status: ContainerStatus.Exited }, TimeSpan.FromSeconds(60), "two restarts");
        await Task.Delay(1500);
        var info = engine.Find(container.Id)!;
        Assert.Equal(2, info.RestartCount);
        Assert.Equal(3, info.ExitCode);
        Assert.Equal(ContainerStatus.Exited, info.Status);
        Assert.Equal(3, engine.Log(container.Id).Recent(20).Count(e => e.Text.Contains("attempt", StringComparison.Ordinal)));
    }

    [ContainerRuntimeFact]
    public async Task Low_memory_stops_the_largest_container_and_a_system_kill_is_named()
    {
        using var temp = new TempDirectory();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        await using var engine = Engine(temp, http);
        var small = await engine.CreateAsync(new ContainerSpec { Image = "redis:7-alpine", Name = "small", Restart = RestartPolicy.Parse("always") });
        // A container that holds 96 MB, so it is the one to stop.
        var large = await engine.CreateAsync(new ContainerSpec
        {
            Image = "redis:7-alpine", Name = "large",
            Entrypoint = ["sh", "-c", "redis-server --port 6380 --save '' --enable-debug-command yes & until redis-cli -p 6380 ping; do sleep 0.2; done; redis-cli -p 6380 debug populate 300000 key 300; wait"],
        });
        await engine.StartAsync(small.Id);
        await engine.StartAsync(large.Id);
        await Until(() => engine.Stats(large.Id).MemoryBytes > 64L * 1024 * 1024, TimeSpan.FromSeconds(90), "the large container to fill its memory");

        // Its processes are the kernel's first choice when memory runs out, before the IDE's.
        await Task.Delay(1500);
        var pid = engine.Find(small.Id)!.Pid;
        Assert.Equal("1000", (await File.ReadAllTextAsync($"/proc/{pid}/oom_score_adj")).Trim());

        // Moderate pressure leaves containers that hold less than a quarter of a gigabyte together.
        Assert.Null(await engine.RelieveMemoryAsync(critical: false));
        var stopped = await engine.RelieveMemoryAsync(critical: true);
        Assert.Equal("large", stopped?.Name);
        var info = engine.Find(large.Id)!;
        Assert.False(info.IsRunning);
        Assert.True(info.StoppedByUser);
        Assert.Contains("free memory", info.Error, StringComparison.Ordinal);
        Assert.True(engine.Find(small.Id)!.IsRunning);

        // Android's low-memory killer: SIGKILL to the whole tree, sent by nobody in TabStudio. A restart policy then
        // starts it again, and the reason stays readable.
        var tree = new[] { pid }.Concat(ProcessTreeOf(pid)).ToList();
        foreach (var process in tree) UnixFiles.TrySignal(process, Signals.Kill);
        await Until(() => engine.Find(small.Id)!.RestartCount == 1 && engine.Find(small.Id)!.IsRunning, TimeSpan.FromSeconds(30), "the killed container to restart");
        Assert.Equal(ContainerEngine.KilledBySystem, engine.Find(small.Id)!.Error);
        output.WriteLine($"stopped {stopped!.Name}: {info.Error}");
    }

    [ContainerRuntimeFact]
    public async Task A_compose_service_keeps_its_image_volume_data_when_its_container_is_created_again()
    {
        using var temp = new TempDirectory();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        await using var engine = Engine(temp, http);
        var file = temp.Write("compose.yaml", "services:\n  cache:\n    image: redis:7-alpine\n    container_name: shop-cache\n    environment: {SETTING: one}\n");
        var project = ComposeReader.Read([file]);
        var images = new ComposeImages(engine);
        var service = project.Services["cache"];

        var first = await images.StartAsync(project, service, [], output.WriteLine, (_, _) => { }, CancellationToken.None);
        var before = engine.Find("shop-cache")!;
        // redis declares VOLUME /data and nothing in the Compose file mounts it.
        var write = await engine.ExecCaptureAsync(before.Id, ["sh", "-c", "echo kept > /data/proof"], TimeSpan.FromSeconds(10));
        Assert.Equal(0, write.ExitCode);
        await first.StopAsync(CancellationToken.None);

        // A changed setting creates the container again, as Compose does; the data must come along.
        var changed = service with { Environment = new Dictionary<string, string?> { ["SETTING"] = "two" } };
        var second = await images.StartAsync(project, changed, [], output.WriteLine, (_, _) => { }, CancellationToken.None);
        var after = engine.Find("shop-cache")!;
        Assert.NotEqual(before.Id, after.Id);
        var read = await engine.ExecCaptureAsync(after.Id, ["cat", "/data/proof"], TimeSpan.FromSeconds(10));
        Assert.Equal(0, read.ExitCode);
        Assert.Contains("kept", read.Output, StringComparison.Ordinal);

        // Another container (the alias test) reaches it by its container_name.
        var peer = await engine.CreateAsync(new ContainerSpec { Image = "redis:7-alpine", Name = "peer" });
        await engine.StartAsync(peer.Id);
        var ping = await engine.ExecCaptureAsync(peer.Id, ["redis-cli", "-h", "shop-cache", "ping"], TimeSpan.FromSeconds(20));
        Assert.Contains("PONG", ping.Output, StringComparison.Ordinal);
        await second.StopAsync(CancellationToken.None);
    }

    private static List<int> ProcessTreeOf(int root)
    {
        var result = new List<int>();
        foreach (var folder in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(folder), out var candidate)) continue;
            for (var at = candidate; at > 1;)
            {
                string stat;
                try { stat = File.ReadAllText($"/proc/{at}/stat"); }
                catch (IOException) { break; }
                var parent = int.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
                if (parent == root) { result.Add(candidate); break; }
                at = parent;
            }
        }

        return result;
    }
}
