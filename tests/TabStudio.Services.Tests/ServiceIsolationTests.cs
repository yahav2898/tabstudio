using System.Text;
using TabStudio.Services.Compose;
using TabStudio.Services.Native;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Services.Tests;

/// <summary>
/// Missing bind sources are created as Docker creates them, and one service's error stays on that service: it
/// never marks the Compose file unreadable or stops another service.
/// </summary>
public sealed class ServiceIsolationTests : IDisposable
{
    private readonly TempDirectory _workspace = new();
    private readonly TempDirectory _storage = new();

    [Fact]
    public void A_missing_short_syntax_bind_source_is_created_as_an_empty_folder()
    {
        var project = Read("services:\n  mysql:\n    image: mysql:9.7\n    volumes:\n      - ./conf.d:/etc/mysql/conf.d:ro\n");
        var storage = new ServiceStorage(_storage.Path, _workspace.Path);

        var mounts = storage.Prepare(project.Services["mysql"]);

        Assert.True(Directory.Exists(_workspace.Resolve("conf.d")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_workspace.Resolve("conf.d")));
        Assert.Contains(mounts, m => m.Target == "/etc/mysql/conf.d");
    }

    [Fact]
    public void A_long_syntax_bind_with_create_host_path_false_fails_that_service_with_its_name()
    {
        var project = Read("services:\n  web:\n    image: nginx\n    volumes:\n      - type: bind\n        source: ./site\n        target: /usr/share/nginx/html\n        bind:\n          create_host_path: false\n");
        var storage = new ServiceStorage(_storage.Path, _workspace.Path);

        var error = Assert.Throws<ComposeException>(() => storage.Prepare(project.Services["web"]));

        Assert.StartsWith("web: the bind source site does not exist", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_workspace.Resolve("site")));
    }

    [Fact]
    public void A_missing_bind_source_outside_the_workspace_is_never_created()
    {
        var outside = Path.Combine(_storage.Path, "elsewhere", "data");
        var project = Read($"services:\n  db:\n    image: postgres:18\n    volumes:\n      - {outside}:/seed:ro\n");
        var storage = new ServiceStorage(_storage.Path, _workspace.Path);

        var error = Assert.Throws<ComposeException>(() => storage.Prepare(project.Services["db"]));

        Assert.Contains("only inside the workspace", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outside));
    }

    [Fact]
    public async Task A_service_that_fails_to_start_leaves_the_others_running()
    {
        var runtime = new FailingRuntime("mysql");
        var project = Read("services:\n  mysql:\n    image: mysql:9.7\n    environment: {MYSQL_ROOT_PASSWORD: '', MYSQL_ALLOW_EMPTY_PASSWORD: 'true'}\n    volumes: ['./conf.d:/etc/mysql/conf.d:ro']\n  postgres:\n    image: postgres:18\n    environment: {POSTGRES_PASSWORD: test}\n  cache:\n    image: redis:7\n    depends_on: [postgres]\n");
        await using var manager = new ServiceManager(project, runtime, new Packages(), new ServiceStorage(_storage.Path, _workspace.Path), new Network());

        var error = await Assert.ThrowsAsync<ComposeException>(() => manager.StartAllAsync());

        Assert.StartsWith("mysql:", error.Message, StringComparison.Ordinal);
        Assert.Equal(ServiceState.Failed, State(manager, "mysql"));
        Assert.Contains("exited during startup", Detail(manager, "mysql"), StringComparison.Ordinal);
        Assert.Equal(ServiceState.Healthy, State(manager, "postgres"));
        Assert.Equal(ServiceState.Healthy, State(manager, "cache"));
        Assert.True(Directory.Exists(_workspace.Resolve("conf.d")));
    }

    [Fact]
    public async Task A_dependency_that_fails_fails_its_dependents_and_names_it()
    {
        var runtime = new FailingRuntime("postgres");
        var project = Read("services:\n  postgres:\n    image: postgres:18\n    environment: {POSTGRES_PASSWORD: test}\n  api:\n    image: redis:7\n    depends_on: [postgres]\n  other:\n    image: redis:7\n");
        await using var manager = new ServiceManager(project, runtime, new Packages(), new ServiceStorage(_storage.Path, _workspace.Path), new Network());

        var error = await Assert.ThrowsAsync<ComposeException>(() => manager.StartAsync(["api", "other"]));

        Assert.Contains("postgres:", error.Message, StringComparison.Ordinal);
        Assert.Equal(ServiceState.Failed, State(manager, "postgres"));
        Assert.Equal(ServiceState.Failed, State(manager, "api"));
        Assert.Equal("Needs postgres, which did not start.", Detail(manager, "api"));
        Assert.Equal(ServiceState.Healthy, State(manager, "other"));
    }

    [Fact]
    public async Task F5_starts_nothing_when_the_run_points_at_no_service_and_only_what_it_points_at_otherwise()
    {
        var runtime = new FailingRuntime(null);
        var project = Read("services:\n  mysql:\n    image: mysql:9.7\n    ports: ['3306:3306']\n  postgres:\n    image: postgres:18\n    environment: {POSTGRES_PASSWORD: test}\n    ports: ['5432:5432']\n  cache:\n    image: redis:7\n    profiles: [cache]\n");
        await using var manager = new ServiceManager(project, runtime, new Packages(), new ServiceStorage(_storage.Path, _workspace.Path), new Network());

        Assert.Empty(manager.NeededBy(_workspace.Path));
        await manager.StartForProjectAsync(_workspace.Path);
        Assert.Empty(runtime.Started);
        Assert.All(manager.Statuses, s => Assert.Equal(ServiceState.Stopped, s.State));

        var environment = new Dictionary<string, string?> { ["DATABASE_URL"] = "postgresql://app@localhost:5432/app" };
        Assert.Equal(["postgres"], manager.NeededBy(_workspace.Path, environment));
        await manager.StartForProjectAsync(_workspace.Path, environment: environment);
        Assert.Equal(["postgres"], runtime.Started);
        Assert.Equal(ServiceState.Stopped, State(manager, "mysql"));
        Assert.Equal(ServiceState.Stopped, State(manager, "cache"));
    }

    [Fact]
    public async Task The_dependencies_of_the_service_built_from_the_project_still_start_for_it()
    {
        _workspace.Write("app/main.py", "print('hi')\n");
        var project = Read("services:\n  app:\n    build: ./app\n    depends_on: [cache]\n  cache:\n    image: redis:7\n  db:\n    image: postgres:18\n");
        var runtime = new FailingRuntime(null);
        await using var manager = new ServiceManager(project, runtime, new Packages(), new ServiceStorage(_storage.Path, _workspace.Path), new Network());

        Assert.Equal(["cache"], manager.NeededBy(_workspace.Resolve("app")));
    }

    [Fact]
    public async Task An_app_service_without_depends_on_starts_what_its_environment_points_at()
    {
        // An empty depends_on list ended the search, so this app debugged with no database.
        _workspace.Write("app/main.py", "print('hi')\n");
        var project = Read("services:\n  app:\n    build: ./app\n    environment:\n      DATABASE_URL: postgres://user:pass@db:5432/app\n  db:\n    image: postgres:18\n  cache:\n    image: redis:7\n");
        var runtime = new FailingRuntime(null);
        await using var manager = new ServiceManager(project, runtime, new Packages(), new ServiceStorage(_storage.Path, _workspace.Path), new Network());

        Assert.Equal(["db"], manager.NeededBy(_workspace.Resolve("app")));
    }

    public void Dispose()
    {
        _workspace.Dispose();
        _storage.Dispose();
    }

    private static ServiceState State(ServiceManager manager, string name) => manager.Statuses.Single(s => s.Name == name).State;

    private static string? Detail(ServiceManager manager, string name) => manager.Statuses.Single(s => s.Name == name).Detail;

    private ComposeProject Read(string yaml)
    {
        File.WriteAllText(Path.Combine(_workspace.Path, "compose.yaml"), yaml);
        return ComposeReader.Discover(_workspace.Path)!;
    }

    private sealed class Packages : IServicePackages
    {
        public Task<MappingResult> EnsureAsync(ComposeService service, Action<string> log, CancellationToken cancellationToken) => Task.FromResult(ServiceCatalog.Map(service));
    }

    private sealed class Network : IServiceNetwork
    {
        private int _next = 30000;
        public bool IsFree(int port) => true;
        public int FreePort() => ++_next;
        public Task<bool> IsListeningAsync(int port, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    /// <summary>Starts each service's process, except one, whose process exits at once as a daemon that cannot start.</summary>
    private sealed class FailingRuntime(string? failing) : IToolchainRuntime
    {
        private readonly Dictionary<string, Handle> _servers = new(StringComparer.Ordinal);
        public List<string> Started { get; } = [];
        public string DisplayName => "Fake Alpine";
        public RuntimeKind Kind => RuntimeKind.Linux;

        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            var name = Path.GetFileName(spec.WorkingDirectory)!;
            if (spec.Arguments.Count > 0 && spec.Arguments[0] == NativeServicePlan.GuestDirectory + "/run.sh")
            {
                if (name == failing)
                {
                    return new ToolchainProcess(new Handle(exitCode: 1, $"{name}: cannot start"), spec);
                }

                Started.Add(name);
                var server = new Handle(exitCode: null, name + " started");
                _servers[name] = server;
                return new ToolchainProcess(server, spec);
            }

            // A stop command ends the service's server, as its daemon's shutdown does.
            if (spec.Arguments.Count > 1 && spec.Arguments[0] == "-c" && spec.Arguments[1] != "exec \"$@\"" && _servers.Remove(name, out var stopped))
            {
                stopped.KillTree();
            }

            return new ToolchainProcess(new Handle(exitCode: 0, "ok"), spec);
        }

        public bool HasCommand(string command) => true;
        public string? Resolve(string command) => command;
        public void ShareFolder(string folder) { }
        public string? ToDevicePath(string programPath, string? workingDirectory) => programPath;
        public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => new() { Program = "/bin/sh", WorkingDirectory = workingDirectory };
    }

    private sealed class Handle : IProcessHandle
    {
        private readonly TaskCompletionSource<(int, int?)> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Handle(int? exitCode, string output)
        {
            Output = new MemoryStream(Encoding.UTF8.GetBytes(output + "\n"));
            if (exitCode is { } code)
            {
                _completion.TrySetResult((code, null));
            }
        }

        public int Id => 42;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; }
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _completion.Task;
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() => _completion.TrySetResult((0, null));
        public void Dispose() { Input.Dispose(); Output.Dispose(); }
    }
}
