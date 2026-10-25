using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Loopback;

namespace TabStudio.App.Services.LanguageServers;

/// <summary>
/// What every workspace's language servers and debug adapters share: the launcher that starts their programs, and how
/// TabStudio introduces itself to them. The desktop starts programs with <see cref="ProcessServerLauncher"/>; a
/// platform registers its own <see cref="IServerProcessLauncher"/>, such as the Toolchain Store's on Android.
/// </summary>
/// <param name="launcher">Starts the servers' and adapters' programs and finds where they are installed.</param>
public sealed class LanguageServerServices(IServerProcessLauncher launcher)
{
    /// <summary>
    /// Starts the servers' and adapters' programs and finds where they are installed; the built-in loopback server
    /// and adapter, which the self test uses, start in-process.
    /// </summary>
    public IServerProcessLauncher Launcher { get; } = new LoopbackLauncher(launcher ?? throw new ArgumentNullException(nameof(launcher)));

    /// <summary>TabStudio's version, as servers are told in <c>initialize</c>.</summary>
    public string ClientVersion { get; } = typeof(LanguageServerServices).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>How long a server may take to answer <c>initialize</c>; slow devices start servers slowly.</summary>
    public TimeSpan InitializeTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>Whether every message to and from servers and adapters is written to their log.</summary>
    public bool TraceMessages { get; init; }

    /// <summary>The options for a workspace's host.</summary>
    /// <param name="readText">Reads a file that is not open, for results in other files.</param>
    public LanguageServerHostOptions CreateHostOptions(Func<string, string?> readText) => new()
    {
        ClientName = "TabStudio",
        ClientVersion = ClientVersion,
        ReadText = readText,
        InitializeTimeout = InitializeTimeout,
        TraceMessages = TraceMessages,
    };
}
