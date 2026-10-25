using TabStudio.LanguageServers.Hosting;

namespace TabStudio.LanguageServers.Loopback;

/// <summary>
/// A launcher that starts TabStudio's built-in loopback language server and debug adapter in-process, by their
/// reserved commands, and every other program with the launcher it wraps. With <see cref="Settings"/> in a workspace's
/// <c>.tabstudio/settings.json</c>, <c>.loopback</c> files get IntelliSense and a debugger without any program
/// installed, which is how the self test proves both on a device.
/// </summary>
/// <param name="inner">Starts every other program.</param>
public sealed class LoopbackLauncher(IServerProcessLauncher inner) : IServerProcessLauncher
{
    /// <summary>The command that starts the loopback language server.</summary>
    public const string LanguageServerCommand = "tabstudio-loopback-language-server";

    /// <summary>The command that starts the loopback debug adapter.</summary>
    public const string DebugAdapterCommand = "tabstudio-loopback-debug-adapter";

    /// <summary>The launch configuration type of the loopback debug adapter.</summary>
    public const string DebugAdapterType = "loopback";

    /// <summary>The <c>.tabstudio/settings.json</c> that sets up the loopback language, server and adapter.</summary>
    public const string Settings = """
        {
          "languages": { "loopback": { "name": "Loopback", "extensions": [ ".loopback" ] } },
          "languageServers": {
            "loopback": { "name": "Loopback Server", "languages": [ "loopback" ], "command": "tabstudio-loopback-language-server" }
          },
          "debugAdapters": {
            "loopback": { "name": "Loopback", "languages": [ "loopback" ], "command": "tabstudio-loopback-debug-adapter" }
          }
        }
        """;

    private readonly IServerProcessLauncher _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public string? Resolve(string command) =>
        command is LanguageServerCommand or DebugAdapterCommand ? command : _inner.Resolve(command);

    /// <inheritdoc />
    public IServerProcess Start(ServerStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        switch (startInfo.FileName)
        {
            case LanguageServerCommand:
                var server = new LoopbackLanguageServer();
                server.Start();
                return new LoopbackProcess(server.StandardInput, server.StandardOutput, server.Exited, server.Stop, server);
            case DebugAdapterCommand:
                var adapter = new LoopbackDebugAdapter();
                adapter.Start();
                return new LoopbackProcess(adapter.StandardInput, adapter.StandardOutput, adapter.Exited, adapter.Stop, adapter);
            default:
                return _inner.Start(startInfo);
        }
    }

    /// <summary>A loopback server or adapter as a process: its streams, and its end.</summary>
    private sealed class LoopbackProcess(Stream input, Stream output, Task<int> exited, Action<int> stop, IAsyncDisposable owner) : IServerProcess
    {
        public Stream StandardInput => input;

        public Stream StandardOutput => output;

        public Stream StandardError { get; } = new MemoryStream();

        public Task<int> Exited => exited;

        public void Kill() => stop(-1);

        public void Dispose()
        {
            StandardError.Dispose();
            _ = owner.DisposeAsync().AsTask();
        }
    }
}
