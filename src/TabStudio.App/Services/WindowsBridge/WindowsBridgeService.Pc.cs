using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.WindowsBridge;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.Services.WindowsBridge;

/// <summary>
/// The PC's role, on TabStudio's desktop build on Windows: being the Windows Bridge for a tablet, with its pairing
/// code, the tools it found, the tablet it serves and the programs it runs for it.
/// </summary>
public sealed partial class WindowsBridgeService
{
    private readonly string _settingsPath;
    private BridgeServer? _server;
    private IBridgeDesktop? _desktop;
    private IReadOnlyList<string> _bridgeAddresses = [];
    private string? _bridgeError;
    private string? _debuggerError;
    private bool _installingDebugger;
    private BridgeInputPolicy _inputPolicy;

    /// <summary>Whether this device can be a Windows Bridge: TabStudio's desktop build on Windows.</summary>
    public bool CanBeBridge => _desktops is not null;

    /// <summary>The tablets paired with this PC's bridge.</summary>
    public LinkTrustStore BridgeTablets { get; }

    /// <summary>Whether this PC is a Windows Bridge now, listening for its tablet.</summary>
    public bool IsBridging => Server() is { IsRunning: true };

    /// <summary>The bridge, while this PC is one.</summary>
    public BridgeServer? BridgeServer => Server();

    /// <summary>The addresses tablets reach the bridge at, the most likely first.</summary>
    public IReadOnlyList<string> BridgeAddresses
    {
        get
        {
            lock (_gate)
            {
                return _bridgeAddresses;
            }
        }
    }

    /// <summary>What the bridge's QR code shows, while it runs on a network.</summary>
    public string? BridgeInvitationUri =>
        Server() is { } server && BridgeAddresses.Count > 0 && server.CreateInvitation(BridgeAddresses) is { } invitation
            ? BridgeInvitation.ToUri(invitation)
            : null;

    /// <summary>Why the bridge could not start, if it could not.</summary>
    public string? BridgeError
    {
        get
        {
            lock (_gate)
            {
                return _bridgeError;
            }
        }
    }

    /// <summary>Whether netcoredbg is being installed.</summary>
    public bool IsInstallingDebugger
    {
        get
        {
            lock (_gate)
            {
                return _installingDebugger;
            }
        }
    }

    /// <summary>Why installing netcoredbg failed, if it did.</summary>
    public string? DebuggerError
    {
        get
        {
            lock (_gate)
            {
                return _debuggerError;
            }
        }
    }

    /// <summary>How the tablet's input is played into programs on this PC; kept between sessions.</summary>
    public BridgeInputPolicy InputPolicy
    {
        get
        {
            lock (_gate)
            {
                return _inputPolicy;
            }
        }

        set
        {
            lock (_gate)
            {
                _inputPolicy = value;
                if (_desktop is not null)
                {
                    _desktop.InputPolicy = value;
                }
            }

            SaveSettings();
            RaiseChanged();
        }
    }

    /// <summary>Starts being a Windows Bridge: looks for this PC's tools, then listens for a tablet.</summary>
    /// <param name="port">The port to listen on when it is free.</param>
    /// <param name="loopbackOnly">Listens on this device only, for tests.</param>
    /// <returns>Whether the bridge started; <see cref="BridgeError"/> says why not.</returns>
    public async Task<bool> StartBridgeAsync(int port = BridgeProtocol.DefaultPort, bool loopbackOnly = false)
    {
        if (_desktops is null)
        {
            lock (_gate)
            {
                _bridgeError = "Only TabStudio for Windows can be a Windows Bridge.";
            }

            RaiseChanged();
            return false;
        }

        if (IsBridging)
        {
            return true;
        }

        var desktop = _desktops.Create();
        desktop.InputPolicy = InputPolicy;
        var server = new BridgeServer(
            new BridgeServerOptions
            {
                Identity = _link.Identity,
                Tablets = BridgeTablets,
                PcName = _link.DeviceName,
                DataFolder = Path.Combine(_paths.Cache, "Bridge"),
                Port = port,
                LoopbackOnly = loopbackOnly,
            },
            desktop);
        server.Changed += (_, _) => RaiseChanged();
        try
        {
            await server.StartAsync().ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            await server.DisposeAsync().ConfigureAwait(false);
            desktop.Dispose();
            lock (_gate)
            {
                _bridgeError = $"The Windows Bridge could not open a network port: {ex.Message}";
            }

            RaiseChanged();
            return false;
        }

        lock (_gate)
        {
            _server = server;
            _desktop = desktop;
            _bridgeError = null;
            _bridgeAddresses = loopbackOnly ? ["127.0.0.1"] : LinkNetwork.GetLocalAddresses();
        }

        RaiseChanged();
        return true;
    }

    /// <summary>Stops being a bridge: the tablet is disconnected and the programs it ran stop.</summary>
    public async Task StopBridgeAsync()
    {
        BridgeServer? server;
        IBridgeDesktop? desktop;
        lock (_gate)
        {
            server = _server;
            desktop = _desktop;
            _server = null;
            _desktop = null;
        }

        if (server is not null)
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }

        desktop?.Dispose();
        RaiseChanged();
    }

    /// <summary>Reads the network addresses again, for the QR code, after the PC changed networks.</summary>
    public void RefreshBridgeAddresses()
    {
        lock (_gate)
        {
            if (_server is null || _server.Options.LoopbackOnly)
            {
                return;
            }

            _bridgeAddresses = LinkNetwork.GetLocalAddresses();
        }

        RaiseChanged();
    }

    /// <summary>Installs netcoredbg on this PC, so tablets can debug .NET apps here.</summary>
    public async Task InstallDebuggerAsync()
    {
        if (Server() is not { } server)
        {
            return;
        }

        lock (_gate)
        {
            _installingDebugger = true;
            _debuggerError = null;
        }

        RaiseChanged();
        try
        {
            await server.InstallDebuggerAsync(_http).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or TaskCanceledException or PlatformNotSupportedException)
        {
            lock (_gate)
            {
                _debuggerError = $"netcoredbg could not be installed: {ex.Message}";
            }
        }
        finally
        {
            lock (_gate)
            {
                _installingDebugger = false;
            }

            RaiseChanged();
        }
    }

    private BridgeServer? Server()
    {
        lock (_gate)
        {
            return _server;
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath) && JsonSerializer.Deserialize(File.ReadAllText(_settingsPath), BridgeSettingsJsonContext.Default.BridgeSettings) is { } settings)
            {
                _inputPolicy = settings.InputPolicy;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Damaged settings: the defaults.
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new BridgeSettings(InputPolicy), BridgeSettingsJsonContext.Default.BridgeSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The setting holds for this session.
        }
    }
}

/// <summary>The Windows Bridge's settings on a PC.</summary>
/// <param name="InputPolicy">How the tablet's input is played into programs.</param>
internal sealed record BridgeSettings(BridgeInputPolicy InputPolicy);

/// <summary>Source-generated JSON for the bridge's settings.</summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BridgeSettings))]
internal sealed partial class BridgeSettingsJsonContext : JsonSerializerContext
{
}
