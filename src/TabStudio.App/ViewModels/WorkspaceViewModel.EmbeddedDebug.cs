using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Debugging;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private ToolchainProcess? _embeddedDebugServer;
    private IUsbConnection? _embeddedDebugUsb;
    private CancellationTokenSource? _embeddedDebugCancellation;
    private Task _embeddedDebugCleanup = Task.CompletedTask;

    /// <summary>Builds matching symbols, opens the selected probe, and attaches the IDE's real GDB DAP debugger.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task DebugEmbeddedChipAsync()
    {
        try
        {
            if (_adapter is not null || IsRemoteWorkspace || await SelectEmbeddedProjectAsync() is not { } project) return;
            await _embeddedDebugCleanup;
            var profilePath = Path.Combine(project.Directory, EmbeddedDebugProfile.FileName);
            if (!File.Exists(profilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(new EmbeddedDebugProfile
                {
                    Firmware = "build/firmware.elf",
                    InterfaceConfig = "interface/SELECT_ACTUAL_PROBE.cfg",
                    TargetConfig = "target/SELECT_ACTUAL_CHIP.cfg",
                }, EmbeddedJson) + "\n");
                await OpenDocumentAsync(profilePath);
                Output.WriteLine("Set the actual probe/target configuration and firmware ELF. Upload that same firmware before attaching the debugger.", OutputKind.Info);
                SelectedPanel = BottomPanel.Output;
                IsBottomPanelVisible = true;
                return;
            }
            if (new FileInfo(profilePath).Length > 65536) throw new InvalidDataException("The embedded debug profile exceeds 64 KiB.");
            var profile = EmbeddedDebugProfile.Parse(await File.ReadAllTextAsync(profilePath));
            if (!await BuildEmbeddedCoreAsync(project)) return;
            var firmware = profile.ResolveFirmware(project.Directory);
            if (_toolchains.Catalog.Find("embedded-debug") is not { } toolchain
                || !await EnsureToolchainReadyAsync(toolchain, "On-chip debugging uses OpenOCD and multi-target GDB with its Python DAP adapter."))
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _embeddedDebugCancellation = cancellation;
            var token = cancellation.Token;
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "Preparing on-chip debugger";
            try
            {
                await CheckEmbeddedGdbAsync(profile.ElfMachine(project.Directory), token);
                var scripts = FindOpenOcdScripts(profile);
                var port = EmbeddedGdbPort();
                var spec = profile.CreateServer(project.Directory, scripts, port);
                if (_toolchains.Environment is not null)
                {
                    await SerialMonitor.ReleaseAsync();
                    await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), token);
                    var devices = _toolchains.Usb.Devices;
                    if (devices.Count == 0) throw new IOException("Connect the selected debug probe using USB-OTG.");
                    var index = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Debug probe", "Choose the probe connected to the target chip.", "Select", devices.Select(device => device.ToString()).ToArray());
                    if (index is null) return;
                    _embeddedDebugUsb = await _toolchains.Usb.OpenAsync(devices[index.Value], token);
                    spec = UsbBridgeProcess.CreateLibUsb(_embeddedDebugUsb, spec);
                }
                token.ThrowIfCancellationRequested();
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var tail = new StringBuilder();
                var gate = new Lock();
                void Line(string text)
                {
                    lock (gate)
                    {
                        tail.AppendLine(text);
                        if (tail.Length > 16384) tail.Remove(0, tail.Length - 8192);
                    }
                    if (text.Contains("Listening on port " + port.ToString(CultureInfo.InvariantCulture) + " for gdb connections", StringComparison.OrdinalIgnoreCase)) ready.TrySetResult();
                    _ui.Post(() => DebugAdapterOutput.WriteLine("[OpenOCD] " + text, OutputKind.Info));
                }
                var server = _toolchains.Runtime.Start(spec);
                _embeddedDebugServer = server;
                server.CloseInput();
                _ = ReadOpenOcdAsync(server.Output, Line);
                if (server.Error is { } error) _ = ReadOpenOcdAsync(error, Line);
                var startup = await Task.WhenAny(ready.Task, server.Completion).WaitAsync(TimeSpan.FromSeconds(25), token);
                if (startup != ready.Task || server.HasExited)
                {
                    string details;
                    lock (gate) details = tail.ToString();
                    throw new IOException("OpenOCD did not open its GDB server. Verify the exact probe, target configuration, power and wiring. " + details);
                }
                var configuration = new LaunchConfiguration
                {
                    Name = project.Name + " · on-chip", Type = "embedded-gdb", Request = "attach",
                    Properties = new JsonObject
                    {
                        ["name"] = project.Name + " · on-chip", ["type"] = "embedded-gdb", ["request"] = "attach",
                        ["program"] = firmware, ["target"] = "127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture),
                    },
                };
                await LaunchAdapterAsync(new LaunchTarget(configuration.Name, configuration, false), DebugStart.Run, noDebug: false);
                if (_adapter is null || _adapter.HasEnded) throw new IOException("GDB DAP did not attach. See Debug Adapters output; the selected GDB must support this chip architecture and DAP.");
                _ = ObserveEmbeddedServerAsync(server);
                Output.WriteLine("Attached to the chip through OpenOCD. Source locations use " + Path.GetFileName(firmware) + "; the programmed firmware must match.", OutputKind.Info);
            }
            finally
            {
                _run = null;
                if (_adapter?.Adapter.Id != "embedded-gdb") DisposeEmbeddedDebug();
                if (State == IdeState.Running) State = IdeState.Ready;
            }
        }
        catch (OperationCanceledException) { DisposeEmbeddedDebug(); State = IdeState.Ready; StatusText = "On-chip debug startup stopped"; }
        catch (Exception error) when (IsEmbeddedError(error) || error is TimeoutException or SocketException or DebugAdapterException)
        {
            DisposeEmbeddedDebug();
            EmbeddedFailure(error);
        }
    }

    private async Task CheckEmbeddedGdbAsync(ushort machine, CancellationToken token)
    {
        if (!_toolchains.Runtime.HasCommand("openocd") || !_toolchains.Runtime.HasCommand("gdb-multiarch"))
            throw new NotSupportedException("Install OpenOCD and gdb-multiarch with Python/DAP support from the Embedded Debug toolchain.");
        var capabilities = await CommandRunner.RunAsync(_toolchains.Runtime, new ProcessStartSpec
        {
            Program = "gdb-multiarch", Arguments = ["--quiet", "--nx", "--batch", "-ex", EmbeddedGdbCapabilities.Query],
        }, null, token);
        if (!capabilities.Succeeded) throw new NotSupportedException("GDB could not query its Python/DAP and target support: " + capabilities.Tail());
        EmbeddedGdbCapabilities.RequireSupport(capabilities.Output, machine);
    }

    private string FindOpenOcdScripts(EmbeddedDebugProfile profile)
    {
        foreach (var root in new[] { "/usr/share/openocd/scripts", "/usr/local/share/openocd/scripts", "/opt/homebrew/share/openocd/scripts" })
        {
            var actualInterface = _toolchains.Runtime.ToDevicePath(root + "/" + profile.InterfaceConfig, null);
            var actualTarget = _toolchains.Runtime.ToDevicePath(root + "/" + profile.TargetConfig, null);
            if (actualInterface is not null && actualTarget is not null && File.Exists(actualInterface) && File.Exists(actualTarget)) return root;
        }
        throw new FileNotFoundException("The selected OpenOCD probe/target configuration is not installed. Choose the matching installed interface/*.cfg and target/*.cfg; vendor probes may need their vendor's OpenOCD fork.");
    }

    private static int EmbeddedGdbPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try { listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static async Task ReadOpenOcdAsync(Stream stream, Action<string> line)
    {
        try
        {
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } text) line(text);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException) { /* Process cleanup closes its log pipes. */ }
    }

    private async Task ObserveEmbeddedServerAsync(ToolchainProcess server)
    {
        var exit = await server.Completion.ConfigureAwait(false);
        _ui.Post(async () =>
        {
            if (!ReferenceEquals(server, _embeddedDebugServer)) return;
            Output.WriteLine("OpenOCD " + exit.Description + "; the on-chip debug session is ending.", OutputKind.Failure);
            await StopAdapterAsync();
            DisposeEmbeddedDebug();
        });
    }

    private async Task RestartEmbeddedDebugAsync()
    {
        var root = _adapterRoot ?? _adapter;
        await StopAdapterAsync();
        if (root is not null) EndAdapterSession(root);
        await _embeddedDebugCleanup;
        await DebugEmbeddedChipAsync();
    }

    /// <summary>Stops the server and releases USB only after its process exits. Safe during workspace disposal.</summary>
    private void DisposeEmbeddedDebug()
    {
        var server = _embeddedDebugServer;
        var usb = _embeddedDebugUsb;
        var cancellation = _embeddedDebugCancellation;
        _embeddedDebugServer = null;
        _embeddedDebugUsb = null;
        _embeddedDebugCancellation = null;
        cancellation?.Cancel();
        if (server is null && usb is null && cancellation is null) return;
        server?.Stop();
        _embeddedDebugCleanup = DisposeEmbeddedBackendAsync(server, usb, cancellation);
    }

    private static async Task DisposeEmbeddedBackendAsync(ToolchainProcess? server, IUsbConnection? usb, CancellationTokenSource? cancellation)
    {
        try { if (server is not null) await server.Completion.ConfigureAwait(false); }
        finally { server?.Dispose(); usb?.Dispose(); cancellation?.Dispose(); }
    }
}
