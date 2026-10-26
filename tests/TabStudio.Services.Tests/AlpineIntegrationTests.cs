using TabStudio.Services.Compose;
using TabStudio.Services.Native;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Services.Tests;

public sealed class AlpineFactAttribute : FactAttribute
{
    public AlpineFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TABSTUDIO_SERVICE_ROOTFS"))) Skip = "Set up Alpine with build/ci/services-alpine.sh to run native service integration tests.";
    }
}

/// <summary>Uses the same PRoot runtime and native service plans as Android, on x64 and ARM64 CI.</summary>
public sealed class AlpineServiceFixture : IDisposable
{
    private readonly TempDirectory _temp = new();
    public string Directory => _temp.Path;
    public ProotRuntime Runtime { get; }
    public AlpineServiceFixture()
    {
        Runtime = new ProotRuntime(new()
        {
            ProotPath = Environment.GetEnvironmentVariable("TABSTUDIO_SERVICE_PROOT")!,
            LoaderPath = Environment.GetEnvironmentVariable("TABSTUDIO_SERVICE_LOADER")!,
            Rootfs = Environment.GetEnvironmentVariable("TABSTUDIO_SERVICE_ROOTFS")!,
            TemporaryDirectory = Path.Combine(_temp.Path, "proot-tmp"), SharedFolders = [_temp.Path],
        });
    }
    public ServiceManager Create(ComposeProject project) => new(project, Runtime, new InstalledPackages(), new ServiceStorage(Directory, project.Directory));
    public Task<CommandResult> Command(string command, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken token = default) => CommandRunner.RunAsync(Runtime, new()
    {
        Program = "/bin/sh", Arguments = ["-c", command], WorkingDirectory = Directory,
        Environment = environment ?? new Dictionary<string, string?>(),
    }, null, token);
    public void Dispose() => _temp.Dispose();
    private sealed class InstalledPackages : IServicePackages
    {
        public Task<MappingResult> EnsureAsync(ComposeService service, Action<string> log, CancellationToken cancellationToken) => Task.FromResult(ServiceCatalog.Map(service));
    }
}

public sealed class AlpineIntegrationTests
{
    [AlpineFact]
    public async Task Postgres_initializes_credentials_database_and_scripts_once_and_persists_on_restart()
    {
        using var fixture = new AlpineServiceFixture();
        var init = Path.Combine(fixture.Directory, "init"); System.IO.Directory.CreateDirectory(init);
        File.WriteAllText(Path.Combine(init, "01.sql"), "CREATE TABLE proof(value text); INSERT INTO proof VALUES ('alpine');");
        File.WriteAllText(Path.Combine(init, "02.sh"), "psql -X -w -v ON_ERROR_STOP=1 -c \"INSERT INTO proof VALUES ('shell')\"\n");
        File.WriteAllText(Path.Combine(fixture.Directory, "compose.yaml"), """
            services:
              db:
                image: postgres:18
                environment: {POSTGRES_USER: developer, POSTGRES_PASSWORD: "pass'word@x", POSTGRES_DB: sample}
                ports: ['0:5432']
                volumes: [pgdata:/var/lib/postgresql/data, ./init:/docker-entrypoint-initdb.d:ro]
            """);
        await using var manager = fixture.Create(ComposeReader.Discover(fixture.Directory)!);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await manager.StartAllAsync(timeout.Token);
            var port = manager.Endpoints["db"][0].Published;
            var env = new Dictionary<string, string?> { ["PGPASSWORD"] = "pass'word@x" };
            var query = await fixture.Command($"psql -h 127.0.0.1 -p {port} -U developer -d sample -Atqc 'SELECT string_agg(value, chr(44) ORDER BY value) FROM proof'", env, timeout.Token);
            Assert.True(query.Succeeded, query.Output);
            Assert.Contains("alpine,shell", query.Output, StringComparison.Ordinal);
            var denied = await fixture.Command($"psql -w -h 127.0.0.1 -p {port} -U developer -d sample -Atqc 'SELECT 1'", new Dictionary<string, string?> { ["PGPASSWORD"] = "wrong" }, timeout.Token);
            Assert.False(denied.Succeeded);
            await manager.RestartAsync("db", timeout.Token);
            var again = await fixture.Command($"psql -h 127.0.0.1 -p {manager.Endpoints["db"][0].Published} -U developer -d sample -Atqc 'SELECT count(*) FROM proof'", env, timeout.Token);
            Assert.True(again.Succeeded, again.Output);
            Assert.Contains("2", again.Output, StringComparison.Ordinal);
            await manager.StopAsync();
            Assert.False(await new LocalServiceNetwork().IsListeningAsync(port, timeout.Token));
        }
        catch (Exception ex) { throw new InvalidOperationException(ex.Message + "\n" + string.Join('\n', manager.Logs("db").Select(l => l.Text)), ex); }
    }

    [AlpineFact]
    public async Task Redis_runs_natively_in_Alpine_and_saves_data_across_restart()
    {
        using var fixture = new AlpineServiceFixture();
        var valkey = !fixture.Runtime.HasCommand("redis-server");
        var definition = new ComposeService("cache", valkey ? "valkey/valkey" : "redis")
        {
            Ports = [new(6379, 0)], Command = [valkey ? "valkey-server" : "redis-server", "--appendonly", "yes"],
            Environment = new Dictionary<string, string?> { ["REDIS_PASSWORD"] = "service-secret" },
        };
        await using var manager = fixture.Create(new(fixture.Directory, [], new Dictionary<string, ComposeService> { ["cache"] = definition }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var cli = valkey ? "valkey-cli" : "redis-cli";
        var env = new Dictionary<string, string?> { ["REDISCLI_AUTH"] = "service-secret" };
        try
        {
            await manager.StartAllAsync(timeout.Token);
            var set = await fixture.Command($"{cli} -h 127.0.0.1 -p {manager.Endpoints["cache"][0].Published} set proof persisted", env, timeout.Token);
            Assert.True(set.Succeeded, set.Output);
            await manager.RestartAsync("cache", timeout.Token);
            var get = await fixture.Command($"{cli} -h 127.0.0.1 -p {manager.Endpoints["cache"][0].Published} get proof", env, timeout.Token);
            Assert.Contains("persisted", get.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(manager.Logs("cache"), l => l.Text.Contains("service-secret", StringComparison.Ordinal));
        }
        catch (Exception ex) { throw new InvalidOperationException(ex.Message + "\n" + string.Join('\n', manager.Logs("cache").Select(l => l.Text)), ex); }
    }
}
