using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Hosting;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>
/// A bridge and a tablet in one process, on the loopback address, over real TLS: the bridge with a desktop the test
/// gives it and its own folders, the tablet paired with it by its QR code.
/// </summary>
internal sealed class BridgeLoopback : IAsyncDisposable
{
    private readonly LinkIdentity _bridgeIdentity = LinkIdentity.Create();
    private readonly LinkIdentity _tabletIdentity = LinkIdentity.Create();
    private readonly LinkTrustStore _tablets = new(null);

    private BridgeLoopback(TempDirectory folder, IBridgeDesktop desktop)
    {
        Folder = folder;
        Desktop = desktop;
        Server = new BridgeServer(
            new BridgeServerOptions
            {
                Identity = _bridgeIdentity,
                Tablets = _tablets,
                PcName = "Test PC",
                DataFolder = Path.Combine(folder.Path, "Bridge"),
                Port = 0,
                LoopbackOnly = true,
            },
            desktop);
    }

    /// <summary>The test's folder: the tablet's workspaces under <c>Tablet</c>, the bridge's files under <c>Bridge</c>.</summary>
    public TempDirectory Folder { get; }

    public IBridgeDesktop Desktop { get; }

    public BridgeServer Server { get; }

    public BridgeClient Client { get; private set; } = new();

    public LinkClientSession? Session { get; private set; }

    /// <summary>The bridge's tools folder, where netcoredbg is installed.</summary>
    public string ToolsFolder => Server.Options.ToolsFolder;

    /// <summary>Starts the bridge and pairs the tablet with it.</summary>
    public static async Task<BridgeLoopback> StartAsync(TempDirectory folder, IBridgeDesktop? desktop = null)
    {
        var loopback = new BridgeLoopback(folder, desktop ?? new FakeDesktop());
        await loopback.Server.StartAsync();
        var invitation = loopback.Server.CreateInvitation(["127.0.0.1"])!;
        await loopback.ConnectAsync(LinkTarget.FromInvitation(invitation));
        return loopback;
    }

    /// <summary>Drops the tablet's connection, as Wi-Fi dropping does.</summary>
    public async Task DisconnectAsync()
    {
        if (Session is { } session)
        {
            await session.DisposeAsync();
            Session = null;
        }
    }

    /// <summary>Connects again as the paired tablet, with a new client.</summary>
    /// <param name="client">The client, whose events the test listens to before it connects.</param>
    public Task ReconnectAsync(BridgeClient? client = null)
    {
        var paired = new LinkPairedDevice("Test PC", LinkDeviceKinds.Computer, _bridgeIdentity.Fingerprint, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ["127.0.0.1"], Server.Port);
        return ConnectAsync(LinkTarget.Paired(paired), client);
    }

    /// <summary>A console project in the tablet's workspace, loaded as the IDE loads it.</summary>
    /// <param name="name">The project's name.</param>
    /// <param name="program">Its Program.cs.</param>
    public (Workspace Workspace, string Project) CreateConsoleProject(string name, string program)
    {
        var project = Folder.Write($"Tablet/{name}/{name}.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        Folder.Write($"Tablet/{name}/Program.cs", program);
        return (WorkspaceLoader.Load(Folder.Resolve($"Tablet/{name}")), project);
    }

    /// <summary>Sends the workspace to the bridge.</summary>
    public Task<WorkspaceSyncResult> SyncAsync(Workspace workspace) => Client.SyncWorkspaceAsync(workspace, FileSystemTextProvider.Instance);

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        await Server.DisposeAsync();
        Desktop.Dispose();
        _bridgeIdentity.Dispose();
        _tabletIdentity.Dispose();
    }

    private async Task ConnectAsync(LinkTarget target, BridgeClient? client = null)
    {
        Client = client ?? new BridgeClient();
        Session = await LinkClient.ConnectAsync(new LinkClientOptions { Identity = _tabletIdentity, DeviceName = "Test tablet" }, target, Client);
        Client.Attach(Session.Connection);
    }
}
