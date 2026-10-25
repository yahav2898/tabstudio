using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.WindowsBridge.Client;

/// <summary>
/// Starts the debug adapter on the bridge's PC instead of on the tablet: the tablet's debugger gives it to
/// <see cref="TabStudio.LanguageServers.DebugAdapters.DebugAdapterSession.StartAsync"/> as it would any launcher, and
/// drives netcoredbg there through the tunnel.
/// </summary>
public sealed class BridgeDebugLauncher : IServerProcessLauncher
{
    /// <summary>The command the bridge's adapter is started by.</summary>
    public const string Command = "netcoredbg";

    private readonly BridgeClient _client;
    private readonly string _projectPath;

    /// <summary>Creates a launcher for one debug session.</summary>
    /// <param name="client">The connected bridge.</param>
    /// <param name="projectPath">The project whose program is debugged, on the tablet's paths.</param>
    public BridgeDebugLauncher(BridgeClient client, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        _client = client;
        _projectPath = projectPath;
    }

    /// <summary>The debug adapter as the tablet's debugger knows it: netcoredbg on a PC, for C#.</summary>
    /// <param name="pcName">The PC's name, for the debugger's messages.</param>
    public static DebugAdapterDefinition Adapter(string pcName) => new()
    {
        Id = "coreclr",
        Name = $"netcoredbg on {pcName}",
        Languages = ["csharp"],
        Command = Command,
        Arguments = ["--interpreter=vscode"],
        Transport = DebugAdapterTransport.Stdio,
    };

    /// <summary>The adapter of the last session started, for its tunnel's state.</summary>
    public BridgeDebugProcess? Process { get; private set; }

    /// <inheritdoc />
    public string? Resolve(string command) => string.Equals(command, Command, StringComparison.Ordinal) ? Command : null;

    /// <inheritdoc />
    public IServerProcess Start(ServerStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (!string.Equals(startInfo.FileName, Command, StringComparison.Ordinal))
        {
            throw new ServerStartException($"The Windows Bridge starts only netcoredbg, not {startInfo.FileName}.");
        }

        try
        {
            Process = _client.OpenDebugger(startInfo.WorkingDirectory, _projectPath);
        }
        catch (TabStudio.Link.LinkConnectionLostException ex)
        {
            throw new ServerStartException(ex.Message, ex);
        }

        return Process;
    }
}
