using TabStudio.LanguageServers.Hosting;

namespace TabStudio.LanguageServers.Tests.Fakes;

/// <summary>
/// Starts <see cref="FakeLanguageServer"/>s instead of programs. Every command counts as installed under
/// <c>/fake/bin</c>, except those the test removes.
/// </summary>
internal sealed class FakeLauncher(Action<FakeLanguageServer>? configure = null) : IServerProcessLauncher, IAsyncDisposable
{
    private readonly List<FakeLanguageServer> _started = [];
    private readonly List<ServerStartInfo> _startInfos = [];

    /// <summary>Commands that are not installed.</summary>
    public HashSet<string> Missing { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, starting a program fails with this message.</summary>
    public string? StartFailure { get; set; }

    /// <summary>The servers started, in order.</summary>
    public IReadOnlyList<FakeLanguageServer> Started
    {
        get
        {
            lock (_started)
            {
                return [.. _started];
            }
        }
    }

    /// <summary>What was started, in order.</summary>
    public IReadOnlyList<ServerStartInfo> StartInfos
    {
        get
        {
            lock (_started)
            {
                return [.. _startInfos];
            }
        }
    }

    public string? Resolve(string command) => Missing.Contains(command) ? null : "/fake/bin/" + command;

    public IServerProcess Start(ServerStartInfo startInfo)
    {
        if (StartFailure is { } failure)
        {
            throw new ServerStartException(failure);
        }

        var server = new FakeLanguageServer();
        configure?.Invoke(server);
        server.Start();
        lock (_started)
        {
            _started.Add(server);
            _startInfos.Add(startInfo);
        }

        return new FakeProcess(server);
    }

    /// <summary>Waits until a number of servers were started.</summary>
    public async Task<FakeLanguageServer> WaitForServerAsync(int count = 1)
    {
        var deadline = DateTime.UtcNow + TestTimeouts.Default;
        while (Started.Count < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Server {count} was not started in time.");
            }

            await Task.Delay(20);
        }

        return Started[count - 1];
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var server in Started)
        {
            await server.DisposeAsync();
        }
    }

    private sealed class FakeProcess(FakeLanguageServer server) : IServerProcess
    {
        public Stream StandardInput => server.ClientOutput;

        public Stream StandardOutput => server.ClientInput;

        public Stream StandardError { get; } = new MemoryStream();

        public Task<int> Exited => server.Exited;

        public void Kill() => server.Crash(-1);

        public void Dispose() => StandardError.Dispose();
    }
}
