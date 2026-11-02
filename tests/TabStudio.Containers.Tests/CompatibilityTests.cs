using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Runtime;
using TabStudio.Testing;
using Xunit.Abstractions;

namespace TabStudio.Containers.Tests;

/// <summary>Runs only in containers.yml's compatibility job, which pulls two dozen real images.</summary>
public sealed class ContainerCompatibilityFactAttribute : FactAttribute
{
    public ContainerCompatibilityFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_COMPAT") != "1")
        {
            Skip = "The image compatibility sweep runs in containers.yml's compatibility job.";
        }
    }
}

/// <summary>
/// The images people put in their Compose files, each started under the same PRoot as the tablet: whether it answers
/// on its port (or runs its command), how long that took, and why not. The table goes to the job's log and to
/// <c>TABSTUDIO_CONTAINER_COMPAT_OUT</c>; the PR's compatibility table and docs/features/containers.md come from it.
/// </summary>
public sealed class CompatibilityTests(ITestOutputHelper output)
{
    /// <summary>One image and how to tell that it works.</summary>
    /// <param name="Area">Database, cache, queue, web server or runtime.</param>
    /// <param name="Image">The image, pinned to a tag.</param>
    /// <param name="Port">The TCP port that answers once it is ready, or 0 for a command.</param>
    /// <param name="Command">For a runtime, the command that must exit 0 and print <paramref name="Expect"/>.</param>
    /// <param name="Expect">Text the command prints.</param>
    /// <param name="Environment">What the image needs to start, as its documentation says.</param>
    /// <param name="Arguments">Its command, when the image has none that serves.</param>
    public sealed record Probe(string Area, string Image, int Port, string[]? Command = null, string? Expect = null, Dictionary<string, string>? Environment = null, string[]? Arguments = null);

    /// <summary>The images verified in RuntimeTests and on the emulator: these must keep working.</summary>
    private static readonly HashSet<string> Required = ["postgres:17-alpine", "redis:7-alpine", "nginx:alpine"];

    public static readonly Probe[] Probes =
    [
        new("Database", "postgres:17-alpine", 5432, Environment: new() { ["POSTGRES_PASSWORD"] = "tablet" }),
        new("Database", "postgres:16", 5432, Environment: new() { ["POSTGRES_PASSWORD"] = "tablet" }),
        new("Database", "mysql:8.4", 3306, Environment: new() { ["MYSQL_ROOT_PASSWORD"] = "tablet" }),
        new("Database", "mariadb:11", 3306, Environment: new() { ["MARIADB_ROOT_PASSWORD"] = "tablet" }),
        new("Database", "mongo:7", 27017),
        new("Cache", "redis:7-alpine", 6379),
        new("Cache", "redis:7", 6379),
        new("Cache", "valkey/valkey:8-alpine", 6379),
        new("Cache", "memcached:1.6-alpine", 11211),
        new("Queue", "rabbitmq:3-management-alpine", 5672),
        new("Queue", "nats:2-alpine", 4222),
        new("Queue", "apache/kafka:3.9.0", 9092),
        new("Web server", "nginx:alpine", 80),
        new("Web server", "nginx:1.27", 80),
        new("Web server", "httpd:2.4-alpine", 80),
        new("Web server", "caddy:2-alpine", 80),
        new("Storage", "minio/minio:latest", 9000, Arguments: ["server", "/data"]),
        new("Mail", "axllent/mailpit:latest", 8025),
        new("Tool", "adminer:latest", 8080),
        new("Web app", "wordpress:6.6-apache", 80),
        new("Git", "gitea/gitea:1.22", 3000),
        new("Monitoring", "grafana/grafana:11.2.0", 3000),
        new("Monitoring", "prom/prometheus:v2.54.1", 9090),
        new("Proxy", "traefik:v3.1", 8080, Arguments: ["--api.insecure=true"]),
        new("Runtime", "node:22-alpine", 0, ["node", "-e", "console.log('node-ok', process.version)"], "node-ok"),
        new("Runtime", "python:3.12-slim", 0, ["python3", "-c", "import sqlite3, ssl; print('python-ok')"], "python-ok"),
        new("Runtime", "golang:1.23-alpine", 0, ["go", "version"], "go version"),
        new("Runtime", "eclipse-temurin:21-jre-alpine", 0, ["java", "-version"], "openjdk"),
        new("Runtime", "php:8.3-cli-alpine", 0, ["php", "-r", "echo 'php-ok';"], "php-ok"),
        new("Runtime", "ruby:3.3-alpine", 0, ["ruby", "-e", "puts 'ruby-ok'"], "ruby-ok"),
        new("Runtime", "mcr.microsoft.com/dotnet/runtime:9.0-alpine", 0, ["dotnet", "--list-runtimes"], "Microsoft.NETCore.App"),
    ];

    [ContainerCompatibilityFact]
    public async Task Common_images_answer_or_say_why_not()
    {
        using var temp = new TempDirectory();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        await using var engine = new ContainerEngine(new()
        {
            StoreRoot = Path.Combine(temp.Path, "store"),
            ProotPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_PROOT"),
            LoaderPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_LOADER"),
        }, new RegistryClient(http, new SessionRegistryCredentialStore()));
        Assert.Null(engine.Options.Problem);

        var table = new StringBuilder("| Area | Image | Result | Ready in | Notes |\n| --- | --- | --- | --- | --- |\n");
        var failures = new List<string>();
        var file = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_COMPAT_OUT");
        if (file is { Length: > 0 })
        {
            await File.WriteAllTextAsync(file, $"Host: {engine.Options.Host}\n\n" + table);
        }

        // The job splits the images between shards, so each finishes well inside its time limit.
        var shard = int.TryParse(Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_COMPAT_SHARD"), CultureInfo.InvariantCulture, out var index) ? index : 0;
        var shards = int.TryParse(Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_COMPAT_SHARDS"), CultureInfo.InvariantCulture, out var count) && count > 0 ? count : 1;
        foreach (var probe in Probes.Where((_, i) => i % shards == shard))
        {
            // A step that ignores its cancellation would hold the whole shard: the probe gets one more minute, then
            // the row says which step never returned and the sweep moves on.
            var stage = new Stage();
            var attempt = Task.Run(() => TryAsync(engine, probe, stage));
            var (works, seconds, note) = await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromMinutes(5))) == attempt
                ? await attempt
                : (false, 0d, $"hung in {stage.Name} past its 4 min deadline");
            var row = $"| {probe.Area} | `{probe.Image}` | {(works ? "Works" : "Does not start")} | {(works ? seconds.ToString("0", CultureInfo.InvariantCulture) + " s" : "-")} | {note.Replace('|', '/')} |";
            table.AppendLine(row);
            output.WriteLine("COMPAT " + row);
            if (file is { Length: > 0 })
            {
                // Row by row, so a job that runs out of time still shows what it measured.
                await File.AppendAllTextAsync(file, row + "\n");
            }
            if (!works && Required.Contains(probe.Image))
            {
                failures.Add(probe.Image + ": " + note);
            }
        }

        output.WriteLine(table.ToString());

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>A public repository's own Compose file, pinned to a commit, and the service whose page must answer.</summary>
    public sealed record ComposeStack(string Name, string Url, string Web);

    /// <summary>docker/awesome-compose at 30f4b7f: stacks of images only, as people copy them.</summary>
    public static readonly ComposeStack[] Stacks =
    [
        new("awesome-compose/wordpress-mysql", "https://raw.githubusercontent.com/docker/awesome-compose/30f4b7f6a6c3b0c0ecf4d4efb0de203c48d11562/wordpress-mysql/compose.yaml", "wordpress"),
        new("awesome-compose/gitea-postgres", "https://raw.githubusercontent.com/docker/awesome-compose/30f4b7f6a6c3b0c0ecf4d4efb0de203c48d11562/gitea-postgres/compose.yaml", "gitea"),
    ];

    [ContainerCompatibilityFact]
    public async Task Public_compose_stacks_start_from_their_own_files_or_say_why_not()
    {
        // One shard runs them; the others keep their time for the images.
        if (Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_COMPAT_SHARD") is { Length: > 0 } shard && shard != "0") return;
        using var temp = new TempDirectory();
        using var registry = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        await using var engine = new ContainerEngine(new()
        {
            StoreRoot = Path.Combine(temp.Path, "store"),
            ProotPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_PROOT"),
            LoaderPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_LOADER"),
        }, new RegistryClient(registry, new SessionRegistryCredentialStore()));
        var file = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_COMPAT_OUT");
        foreach (var stack in Stacks)
        {
            var folder = Path.Combine(temp.Path, stack.Name.Replace('/', '-'));
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "compose.yaml"), await registry.GetStringAsync(stack.Url));
            var project = TabStudio.Services.Compose.ComposeReader.Discover(folder)!;
            var clock = Stopwatch.StartNew();
            string row;
            await using (var manager = new TabStudio.Services.ServiceManager(project, new TabStudio.Toolchains.Runtimes.HostRuntime(), new NoFallback(),
                new TabStudio.Services.Native.ServiceStorage(Path.Combine(temp.Path, "services"), folder), images: new ComposeImages(engine)))
            {
                try
                {
                    await manager.StartAllAsync().WaitAsync(TimeSpan.FromMinutes(12));
                    var port = manager.Endpoints[stack.Web][0].Published;
                    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                    int status = 0;
                    while (!deadline.IsCancellationRequested)
                    {
                        try
                        {
                            using var response = await http.GetAsync($"http://127.0.0.1:{port}/", deadline.Token);
                            status = (int)response.StatusCode;
                            if (status < 500) break;
                        }
                        catch (HttpRequestException) { }
                        await Task.Delay(1000, CancellationToken.None);
                    }

                    row = status is > 0 and < 500
                        ? $"| Stack | `{stack.Name}` | Works | {clock.Elapsed.TotalSeconds:0} s | {string.Join(", ", project.Services.Keys)}; {stack.Web} answers HTTP {status} |"
                        : $"| Stack | `{stack.Name}` | Does not start | - | {stack.Web} did not answer HTTP |";
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    row = $"| Stack | `{stack.Name}` | Does not start | - | {FirstLine(ex.Message).Replace('|', '/')} |";
                }

                foreach (var state in manager.Statuses) output.WriteLine($"{stack.Name} {state.Name}: {state.State} {state.Detail}");
                await manager.StopAsync();
            }

            output.WriteLine("COMPAT " + row);
            if (file is { Length: > 0 }) await File.AppendAllTextAsync(file, row + "\n");
            foreach (var container in engine.Containers.ToList()) await engine.RemoveAsync(container.Id, force: true);
            await engine.PruneAsync(allImages: true);
        }
    }

    private sealed class NoFallback : TabStudio.Services.IServicePackages
    {
        public Task<TabStudio.Services.Native.MappingResult> EnsureAsync(TabStudio.Services.Compose.ComposeService service, Action<string> log, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(service.Name + " needed a native fallback: its image did not run.");
    }

    /// <summary>The step a probe is in, for the row of one that never returns.</summary>
    private sealed class Stage
    {
        public volatile string Name = "pull";
    }

    private async Task<(bool Works, double Seconds, string Note)> TryAsync(ContainerEngine engine, Probe probe, Stage stage)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var clock = Stopwatch.StartNew();
        ContainerInfo? container = null;
        try
        {
            await engine.PullAsync(probe.Image, cancellationToken: deadline.Token);
            var pulled = clock.Elapsed;
            stage.Name = "create";
            container = await engine.CreateAsync(new ContainerSpec
            {
                Image = probe.Image,
                Name = "compat-" + Guid.NewGuid().ToString("N")[..8],
                Environment = probe.Environment ?? new Dictionary<string, string>(),
                // A runtime image's own command exits at once; it sleeps while the probe runs in it.
                Entrypoint = probe.Command is null ? null : new[] { "sleep" },
                Command = probe.Command is null ? probe.Arguments : new[] { "600" },
                Ports = probe.Port > 0 ? [new PortBinding(probe.Port)] : [],
            }, cancellationToken: deadline.Token);
            stage.Name = "start";
            await engine.StartAsync(container.Id, deadline.Token);
            stage.Name = probe.Command is null ? "waiting for the port" : "exec";
            if (probe.Command is { } command)
            {
                var (code, text) = await engine.ExecCaptureAsync(container.Id, command, TimeSpan.FromMinutes(2), deadline.Token);
                var ok = code == 0 && text.Contains(probe.Expect!, StringComparison.OrdinalIgnoreCase);
                return (ok, (clock.Elapsed - pulled).TotalSeconds, ok ? FirstLine(text) : $"exit {code}: {Tail(text)}");
            }

            while (!deadline.IsCancellationRequested)
            {
                var info = engine.Find(container.Id)!;
                if (!info.IsRunning)
                {
                    return (false, 0, $"exited {info.ExitCode}{(info.Error is { } error ? ", " + error : string.Empty)}: {Tail(engine.Log(container.Id))}");
                }

                if (info.Ports.FirstOrDefault(p => p.ContainerPort == probe.Port) is { } port && await AnswersAsync(port.HostPort))
                {
                    var ready = clock.Elapsed - pulled;
                    await Task.Delay(TimeSpan.FromSeconds(3), deadline.Token);
                    return engine.Find(container.Id)!.IsRunning
                        ? (true, ready.TotalSeconds, (info.Emulation is { } emulation ? emulation + "; " : string.Empty) + "answers on " + probe.Port)
                        : (false, 0, "answered, then exited: " + Tail(engine.Log(container.Id)));
                }

                await Task.Delay(500, deadline.Token);
            }

            return (false, 0, "no answer in 4 min: " + Tail(engine.Log(container.Id)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (false, 0, $"{ex.GetType().Name}: {FirstLine(ex.Message)}" + (container is null ? string.Empty : "; " + Tail(engine.Log(container.Id))));
        }
        finally
        {
            stage.Name = "remove";
            if (container is not null)
            {
                try { await engine.RemoveAsync(container.Id, force: true); } catch (Exception ex) when (ex is not OutOfMemoryException) { output.WriteLine("remove: " + ex.Message); }
            }

            stage.Name = "prune";
            // Two dozen images do not fit on a runner's disk together with their writable copies.
            try { await engine.PruneAsync(allImages: true); } catch (Exception ex) when (ex is not OutOfMemoryException) { output.WriteLine("prune: " + ex.Message); }
        }
    }

    private static async Task<bool> AnswersAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync("127.0.0.1", port, timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    private static string Tail(ContainerLog log) => Tail(string.Join("\n", log.Recent(4).Select(e => e.Text)));

    private static string Tail(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(2);
        var joined = string.Join(" / ", lines);
        return joined.Length > 220 ? joined[..220] + "…" : joined;
    }

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
}
