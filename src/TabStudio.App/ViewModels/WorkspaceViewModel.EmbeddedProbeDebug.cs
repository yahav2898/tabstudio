using System.Globalization;
using System.Text;
using TabStudio.Debugging;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private async Task DebugEmbeddedProbeRsAsync(EmbeddedProject project, EmbeddedDebugProfile profile, string firmware)
    {
        if (_toolchains.Catalog.Find("probe-rs") is not { } toolchain
            || !await EnsureToolchainReadyAsync(toolchain, "On-chip debugging uses the pinned official probe-rs DAP server."))
        {
            return;
        }
        var cancellation = new CancellationTokenSource();
        _embeddedDebugCancellation = cancellation;
        var token = cancellation.Token;
        _run = cancellation;
        State = IdeState.Running;
        StatusText = "Preparing probe-rs debugger";
        try
        {
            if (!_toolchains.Runtime.HasCommand("probe-rs")) throw new NotSupportedException("Install the pinned probe-rs toolchain before attaching.");
            var version = await CommandRunner.RunAsync(_toolchains.Runtime, new ProcessStartSpec { Program = "probe-rs", Arguments = ["--version"] }, null, token);
            if (!version.Succeeded || !version.Output.Contains("probe-rs 0.32.0", StringComparison.Ordinal))
                throw new NotSupportedException("This attach configuration targets the pinned probe-rs 0.32.0 DAP schema. Install that Toolchain Store version. " + version.Tail());
            var port = EmbeddedGdbPort();
            var spec = ProbeRsDebugPlan.Server(project.Directory, port);
            string? selectedProbe = null;
            if (_toolchains.Environment is not null)
            {
                await SerialMonitor.ReleaseAsync();
                await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), token);
                var devices = _toolchains.Usb.Devices;
                if (devices.Count == 0) throw new IOException("Connect a supported debug probe using USB-OTG.");
                var index = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Debug probe", "Choose the probe connected to the selected chip.", "Select", devices.Select(device => device.ToString()).ToArray());
                if (index is null) return;
                _embeddedDebugUsb = await _toolchains.Usb.OpenAsync(devices[index.Value], token);
                var connection = _embeddedDebugUsb;
                _embeddedDebugInventory = await Task.Run(() => UsbfsDeviceInventory.Create(connection, Path.Combine(_paths.Root, "EmbeddedUsb")), token);
                spec = UsbBridgeProcess.CreateUsbfs(connection, _embeddedDebugInventory, spec);
                selectedProbe = string.Create(CultureInfo.InvariantCulture, $"{connection.Device.VendorId:x4}:{connection.Device.ProductId:x4}");
            }
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
                if (text.Contains("Listening for requests on port " + port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) ready.TrySetResult();
                _ui.Post(() => { if (!_disposed) DebugAdapterOutput.WriteLine("[probe-rs] " + text, OutputKind.Info); });
            }
            token.ThrowIfCancellationRequested();
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
                throw new IOException("The probe-rs DAP server did not start: " + details);
            }
            var properties = ProbeRsDebugPlan.Attach(profile, project.Directory, firmware, port, selectedProbe);
            var configuration = new LaunchConfiguration
            {
                Name = project.Name + " · probe-rs", Type = "embedded-probe-rs", Request = "attach", Properties = properties,
            };
            await LaunchAdapterAsync(new LaunchTarget(configuration.Name, configuration, false), DebugStart.Run, noDebug: false);
            if (_adapter is not { Adapter.Id: "embedded-probe-rs", HasEnded: false })
                throw new IOException("probe-rs DAP did not attach. See Debug Adapters output for the exact probe, chip or USB transport error.");
            _ = ObserveEmbeddedServerAsync(server, "probe-rs");
            Output.WriteLine("Attached with probe-rs using " + Path.GetFileName(firmware) + ". The programmed firmware must match these symbols.", OutputKind.Info);
        }
        finally
        {
            _run = null;
            if (_adapter?.Adapter.Id != "embedded-probe-rs") DisposeEmbeddedDebug();
            if (State == IdeState.Running) State = IdeState.Ready;
        }
    }
}
