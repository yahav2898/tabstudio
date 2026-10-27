using System.Text;
using TabStudio.Services.Compose;
using TabStudio.Services.Native;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Services.Tests;

public sealed class ManagerTests
{
    [Fact]
    public async Task Dependencies_start_first_stop_last_and_logs_are_separate_and_redacted()
    {
        using var temp = new TempDirectory();
        var runtime = new FakeRuntime();
        var secret = "super-private";
        var db = new ComposeService("db", "postgres:18") { Environment = new Dictionary<string, string?> { ["POSTGRES_PASSWORD"] = secret } };
        var cache = new ComposeService("cache", "redis:7") { Dependencies = [new("db", "service_healthy")] };
        await using var manager = Make(temp, runtime, [cache, db]);
        await manager.StartAllAsync();
        Assert.Equal(["db", "cache"], runtime.Started);
        Assert.All(manager.Statuses, s => Assert.Equal(ServiceState.Healthy, s.State));
        Assert.Contains(manager.Logs("db"), line => line.Text.Contains("***", StringComparison.Ordinal));
        Assert.DoesNotContain(manager.Logs("db"), line => line.Text.Contains(secret, StringComparison.Ordinal));
        Assert.DoesNotContain(manager.Logs("cache"), line => line.Text.Contains("db started", StringComparison.Ordinal));
        await manager.StopAsync();
        Assert.Equal(["cache", "db"], runtime.Stopped);
        Assert.All(manager.Statuses, s => Assert.Equal(ServiceState.Stopped, s.State));
    }

    [Fact]
    public async Task Cancellation_rolls_back_only_new_services()
    {
        using var temp = new TempDirectory();
        var runtime = new FakeRuntime();
        var packages = new Packages();
        var network = new Network();
        var services = new[] { new ComposeService("one", "redis"), new ComposeService("two", "redis") };
        await using var manager = Make(temp, runtime, services, packages, network);
        await manager.StartAsync(["one"]);
        using var stop = new CancellationTokenSource();
        packages.DelayNext = true;
        var run = manager.StartAsync(["two"], stop.Token);
        await packages.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(ServiceState.Healthy, manager.Statuses.Single(s => s.Name == "one").State);
        Assert.Equal(ServiceState.Stopped, manager.Statuses.Single(s => s.Name == "two").State);
        Assert.Equal(["one"], runtime.Started);
    }

    [Fact]
    public async Task Cycles_and_unknown_required_dependencies_fail_before_starting_any_process()
    {
        using var temp = new TempDirectory();
        var runtime = new FakeRuntime();
        var services = new[] { new ComposeService("one", "redis") { Dependencies = [new("two")] }, new ComposeService("two", "redis") { Dependencies = [new("one")] } };
        await using var manager = Make(temp, runtime, services);
        Assert.Throws<ComposeException>(() => manager.DependencyOrder(["one"]));
        await Assert.ThrowsAsync<ComposeException>(() => manager.StartAllAsync());
        Assert.Empty(runtime.Started);
    }

    [Fact]
    public async Task Unsupported_required_dependency_does_not_start_its_dependents()
    {
        using var temp = new TempDirectory(); var runtime = new FakeRuntime();
        await using var manager = Make(temp, runtime, [new("unknown", "example/custom"), new ComposeService("cache", "redis") { Dependencies = [new("unknown")] }]);
        await Assert.ThrowsAsync<ComposeException>(() => manager.StartAsync(["cache"]));
        Assert.Empty(runtime.Started);
        Assert.Contains("remote host", manager.Statuses.Single(s => s.Name == "unknown").Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stopping_a_dependency_stops_its_dependents_and_leaves_unrelated_services()
    {
        using var temp = new TempDirectory(); var runtime = new FakeRuntime();
        await using var manager = Make(temp, runtime, [new("one", "redis"), new ComposeService("two", "redis") { Dependencies = [new("one")] }, new("other", "redis")]);
        await manager.StartAllAsync();
        await manager.StopAsync("one");
        Assert.Equal(["two", "one"], runtime.Stopped);
        Assert.Equal(ServiceState.Healthy, manager.Statuses.Single(s => s.Name == "other").State);
    }

    [Fact]
    public async Task A_conflicting_explicit_port_fails_its_service_before_installing_and_the_other_starts()
    {
        using var temp = new TempDirectory(); var runtime = new FakeRuntime();
        await using var manager = Make(temp, runtime, [new ComposeService("one", "redis") { Ports = [new(6379, 16379)] }, new ComposeService("two", "redis") { Ports = [new(6379, 16379)] }]);
        var error = await Assert.ThrowsAsync<ComposeException>(() => manager.StartAllAsync());
        Assert.StartsWith("two:", error.Message, StringComparison.Ordinal);
        Assert.Equal(["one"], runtime.Started);
        Assert.Equal(ServiceState.Healthy, manager.Statuses.Single(s => s.Name == "one").State);
        var two = manager.Statuses.Single(s => s.Name == "two");
        Assert.Equal(ServiceState.Failed, two.State);
        Assert.Contains("16379", two.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_existing_unhealthy_dependency_blocks_new_dependents()
    {
        using var temp = new TempDirectory(); var runtime = new FakeRuntime(); var network = new Network();
        var dependency = new ComposeService("one", "redis") { Healthcheck = new(["CMD", "redis-cli", "ping"]) { Interval = TimeSpan.FromMilliseconds(10), Retries = 1 } };
        await using var manager = Make(temp, runtime, [dependency, new ComposeService("two", "redis") { Dependencies = [new("one", "service_healthy")] }], network: network);
        await manager.StartAsync(["one"]);
        network.Ready = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (manager.Statuses.Single(s => s.Name == "one").State != ServiceState.Unhealthy) await Task.Delay(10, timeout.Token);
        await Assert.ThrowsAsync<ComposeException>(() => manager.StartAsync(["two"], timeout.Token));
        Assert.Equal(["one"], runtime.Started);
    }

    private static ServiceManager Make(TempDirectory temp, FakeRuntime runtime, IEnumerable<ComposeService> services, Packages? packages = null, Network? network = null) =>
        new(new(temp.Path, [], services.ToDictionary(x => x.Name)), runtime, packages ?? new(), new ServiceStorage(temp.Path, temp.Path), network ?? new());

    private sealed class Network : IServiceNetwork
    {
        private int _next = 20000;
        public bool Ready { get; set; } = true;
        public bool IsFree(int port) => true;
        public int FreePort() => ++_next;
        public Task<bool> IsListeningAsync(int port, CancellationToken cancellationToken) => Task.FromResult(Ready);
    }
    private sealed class Packages : IServicePackages
    {
        public bool DelayNext { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<MappingResult> EnsureAsync(ComposeService service, Action<string> log, CancellationToken cancellationToken)
        {
            if (DelayNext) { Entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            return ServiceCatalog.Map(service);
        }
    }
    private sealed class FakeRuntime : IToolchainRuntime
    {
        private readonly Dictionary<string, Handle> _servers = new(StringComparer.Ordinal);
        public List<string> Started { get; } = [];
        public List<string> Stopped { get; } = [];
        public string DisplayName => "Fake Alpine";
        public RuntimeKind Kind => RuntimeKind.Linux;
        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            var name = Path.GetFileName(spec.WorkingDirectory)!;
            if (spec.Arguments.Count > 0 && spec.Arguments[0] == NativeServicePlan.GuestDirectory + "/run.sh")
            {
                Started.Add(name);
                var handle = new Handle(false, name + " started " + spec.Environment.GetValueOrDefault("POSTGRES_PASSWORD"));
                _servers[name] = handle;
                return new ToolchainProcess(handle, spec);
            }
            if (spec.Arguments.Count > 1 && spec.Arguments[0] == "-c" && spec.Arguments[1] != "exec \"$@\"")
            {
                Stopped.Add(name); _servers[name].End();
            }
            return new ToolchainProcess(new Handle(true, "ok"), spec);
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
        public Handle(bool completed, string output) { Output = new MemoryStream(Encoding.UTF8.GetBytes(output + "\n")); if (completed) End(); }
        public int Id => 42;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; }
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _completion.Task;
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() => End();
        public void End() => _completion.TrySetResult((0, null));
        public void Dispose() { Input.Dispose(); Output.Dispose(); }
    }
}
