using System.Diagnostics;
using System.Globalization;
using Npgsql;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Runtime;
using TabStudio.Services;
using TabStudio.Services.Compose;
using TabStudio.Services.Native;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;
using Xunit.Abstractions;

namespace TabStudio.Containers.Tests;

public sealed class ContainerRuntimeFactAttribute : FactAttribute
{
    public ContainerRuntimeFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_PROOT"))
            && Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_REQUIRED") != "1")
        {
            Skip = "Real PRoot image runtime is configured by containers.yml.";
        }
    }
}

public sealed class RuntimeTests(ITestOutputHelper output)
{
    [ContainerRuntimeFact]
    public async Task Postgres_redis_and_nginx_run_from_real_images_with_compose_exec_ports_and_persistent_data()
    {
        using var temp = new TempDirectory();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        await using var engine = new ContainerEngine(new()
        {
            StoreRoot = Path.Combine(temp.Path, "store"),
            ProotPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_PROOT"),
            LoaderPath = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_LOADER"),
        }, new RegistryClient(http, new SessionRegistryCredentialStore()));
        Assert.Null(engine.Options.Problem);
        var init = Path.Combine(temp.Path, "init"); Directory.CreateDirectory(init);
        await File.WriteAllTextAsync(Path.Combine(init, "01-proof.sql"), "CREATE TABLE proof(value text); INSERT INTO proof VALUES ('oci-postgres');");
        var file = Path.Combine(temp.Path, "compose.yaml");
        await File.WriteAllTextAsync(file, """
            services:
              db:
                image: postgres:17-alpine
                environment: { POSTGRES_PASSWORD: development, POSTGRES_USER: tablet, POSTGRES_DB: tablet }
                ports: ['0:5432']
                volumes: [data:/var/lib/postgresql/data, './init:/docker-entrypoint-initdb.d:ro']
                healthcheck: { test: ['CMD', 'pg_isready', '-U', 'tablet'], interval: 1s, retries: 60 }
              cache:
                image: redis:7-alpine
                ports: ['0:6379']
                healthcheck: { test: ['CMD', 'redis-cli', 'ping'], interval: 1s, retries: 60 }
              web:
                image: nginx:alpine
                ports: ['0:80']
            """);
        var project = ComposeReader.Read([file]);
        await using var manager = new ServiceManager(project, new HostRuntime(), new NoNativePackages(), new ServiceStorage(temp.Path, temp.Path), images: new ComposeImages(engine));
        manager.Logged += (_, line) => output.WriteLine($"{line.Service}: {line.Text}");
        var clock = Stopwatch.StartNew();
        await manager.StartAllAsync().WaitAsync(TimeSpan.FromMinutes(6));
        output.WriteLine($"IMAGE_START_MS={clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)} ARCH={engine.Options.Host}");
        Assert.Equal(3, engine.Containers.Count(c => c.IsRunning));
        Assert.Equal(3, engine.Containers.Select(c => c.Address).Distinct().Count());
        Assert.Equal(3, manager.Endpoints.Values.SelectMany(p => p).Select(p => p.Published).Distinct().Count());
        var db = engine.Containers.Single(c => c.Label("com.docker.compose.service") == "db");
        await using (var connection = new NpgsqlConnection($"Host=127.0.0.1;Port={manager.Endpoints["db"][0].Published};Username=tablet;Password=development;Database=tablet;Timeout=20"))
        {
            await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT value FROM proof", connection);
            Assert.Equal("oci-postgres", await query.ExecuteScalarAsync());
        }
        var cache = engine.Containers.Single(c => c.Label("com.docker.compose.service") == "cache");
        var ping = await engine.ExecCaptureAsync(cache.Id, ["redis-cli", "ping"], TimeSpan.FromSeconds(10));
        Assert.Equal(0, ping.ExitCode); Assert.Contains("PONG", ping.Output, StringComparison.Ordinal);
        await using (var bridge = new DockerCommandBridge(new DockerCommand(engine, _ => manager)))
        {
            var rootfs = engine.RootfsOf(cache.Id);
            var shim = Path.Combine(rootfs, "usr/local/bin/docker");
            await File.WriteAllTextAsync(shim, bridge.ShellScript);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var terminalRuntime = new ProotRuntime(new()
            {
                ProotPath = engine.Options.ProotPath!, LoaderPath = engine.Options.LoaderPath!, Rootfs = rootfs,
                TemporaryDirectory = Path.Combine(temp.Path, "terminal-tmp"), ShareWorkingDirectory = false,
            });
            var cli = await CommandRunner.RunAsync(terminalRuntime, new()
            { Program = "/bin/sh", Arguments = ["-c", "docker ps && docker exec " + cache.Name + " redis-cli ping"], WorkingDirectory = "/" }, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(cli.Succeeded, cli.Output);
            Assert.Contains("PONG", cli.Output, StringComparison.Ordinal);
            Assert.Contains("nginx:alpine", cli.Output, StringComparison.Ordinal);
            var stdin = await CommandRunner.RunAsync(terminalRuntime, new()
            {
                Program = "/bin/sh", WorkingDirectory = "/",
                Arguments = ["-c", $$"""printf 'stdin-from-shim\n' | docker exec -i {{cache.Name}} sh -c 'cat; echo shell-error >&2; exit 7'; code=$?; [ "$code" -eq 7 ]"""],
            }, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(stdin.Succeeded, stdin.Output);
            Assert.Contains("stdin-from-shim", stdin.Output, StringComparison.Ordinal);
            Assert.Contains("shell-error", stdin.Output, StringComparison.Ordinal);
        }
        var second = await engine.CreateAsync(new ContainerSpec { Image = "redis:7-alpine", Name = "second-cache" });
        await engine.StartAsync(second.Id);
        var secondPing = await engine.ExecCaptureAsync(second.Id, ["sh", "-c", "until redis-cli ping >/dev/null 2>&1; do sleep 1; done; redis-cli -h cache ping"], TimeSpan.FromSeconds(20));
        Assert.Equal(0, secondPing.ExitCode); Assert.Contains("PONG", secondPing.Output, StringComparison.Ordinal);
        Assert.NotEqual(engine.Find(second.Id)!.Ports[0].HostPort, engine.Find(cache.Id)!.Ports[0].HostPort);
        await engine.RemoveAsync(second.Id, force: true);
        using var response = await http.GetAsync($"http://127.0.0.1:{manager.Endpoints["web"][0].Published}/");
        response.EnsureSuccessStatusCode();
        Assert.Contains("Welcome to nginx", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await engine.RestartAsync(db.Id);
        var check = await engine.ExecCaptureAsync(db.Id, ["sh", "-c", "until pg_isready -U tablet >/dev/null; do sleep 1; done; psql -U tablet -d tablet -Atqc 'SELECT value FROM proof'"], TimeSpan.FromSeconds(45));
        Assert.Equal(0, check.ExitCode); Assert.Contains("oci-postgres", check.Output, StringComparison.Ordinal);
        var marker = await engine.ExecCaptureAsync(db.Id, ["sh", "-c", "echo writable-root > /var/tmp/restart-proof"], TimeSpan.FromSeconds(10));
        Assert.Equal(0, marker.ExitCode);
        await manager.RestartAsync("db");
        Assert.NotNull(engine.Find(db.Id));
        var kept = await engine.ExecCaptureAsync(db.Id, ["cat", "/var/tmp/restart-proof"], TimeSpan.FromSeconds(10));
        Assert.Equal(0, kept.ExitCode); Assert.Contains("writable-root", kept.Output, StringComparison.Ordinal);
        foreach (var image in await engine.Store.ListAsync()) output.WriteLine($"VERIFIED_IMAGE={image.DisplayName}@{image.ManifestDigest} BYTES={image.Size.ToString(CultureInfo.InvariantCulture)}");
        await manager.StopAsync();
        Assert.DoesNotContain(engine.Containers, c => c.IsRunning);
    }

    private sealed class NoNativePackages : IServicePackages
    {
        public Task<MappingResult> EnsureAsync(ComposeService service, Action<string> log, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This test must run real images, never a native fallback: " + service.Name);
    }
}
