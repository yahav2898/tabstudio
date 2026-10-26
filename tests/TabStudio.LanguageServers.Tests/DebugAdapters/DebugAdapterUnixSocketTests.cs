using System.Net.Sockets;
using System.Text;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.DebugAdapters;

/// <summary>Private local DAP sockets keep protocol messages apart from a debuggee's stdin.</summary>
public sealed class DebugAdapterUnixSocketTests
{
    [Fact]
    public async Task A_Unix_socket_adapter_initializes_privately_preserves_input_and_removes_its_socket()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await using var launcher = new UnixLauncher();
        await using var session = await DebugAdapterSession.StartAsync(Adapter(), "/bin/fake", "/work", launcher);
        var directory = Path.GetDirectoryName(launcher.SocketPath)!;
        Assert.True(session.Capabilities.ConfigurationDone);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
        Assert.True(session.AcceptsProgramInput);
        Assert.Equal(0, launcher.Input.Length);
        await session.WriteProgramInputAsync("Ada");
        Assert.Equal("Ada\n", Encoding.UTF8.GetString(launcher.Input.ToArray()));

        await session.DisposeAsync();

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task A_failed_adapter_start_removes_its_private_socket_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await using var launcher = new UnixLauncher { FailStart = true };
        await Assert.ThrowsAsync<ServerStartException>(() => DebugAdapterSession.StartAsync(Adapter(), "/bin/fake", "/work", launcher));

        Assert.False(Directory.Exists(Path.GetDirectoryName(launcher.SocketPath)));
    }

    private static DebugAdapterDefinition Adapter() => new()
    {
        Id = "private", Name = "Private", Command = "fake", Arguments = ["--socket", "${socket}"],
        Transport = DebugAdapterTransport.UnixSocket, UsesProcessInput = true,
    };

    private sealed class UnixLauncher : IServerProcessLauncher, IServerProcess, IAsyncDisposable
    {
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Socket? _listener;
        private Socket? _client;
        private FakeDebugAdapter? _adapter;
        private Task? _worker;

        public bool FailStart { get; init; }
        public string SocketPath { get; private set; } = string.Empty;
        public MemoryStream Input { get; } = new();
        public Stream StandardInput => Input;
        public Stream StandardOutput => Stream.Null;
        public Stream StandardError => Stream.Null;
        public Task<int> Exited => _exited.Task;
        public string? Resolve(string command) => command;

        public IServerProcess Start(ServerStartInfo startInfo)
        {
            SocketPath = startInfo.Arguments[1];
            if (FailStart)
            {
                throw new ServerStartException("missing adapter");
            }

            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            _listener.Listen(1);
            _worker = Task.Run(async () =>
            {
                _client = await _listener.AcceptAsync();
                var stream = new NetworkStream(_client, ownsSocket: false);
                _adapter = new FakeDebugAdapter(stream, stream);
                _adapter.Start();
                _exited.TrySetResult(await _adapter.Exited);
            });
            return this;
        }

        public void Kill()
        {
            _adapter?.Crash();
            _client?.Dispose();
            _listener?.Dispose();
            _exited.TrySetResult(-1);
        }

        public void Dispose() => Kill();

        public async ValueTask DisposeAsync()
        {
            Kill();
            if (_adapter is not null)
            {
                await _adapter.DisposeAsync();
            }

            if (_worker is not null)
            {
                try
                {
                    await _worker;
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
                {
                    // The test ended before the fake adapter accepted a client.
                }
            }

            Input.Dispose();
        }
    }
}
