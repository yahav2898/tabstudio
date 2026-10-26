using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Opens the board's real MicroPython/CircuitPython REPL through mpremote in the existing terminal.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task OpenMicroPythonReplAsync() => RunMicroPythonAsync(syncFile: false);

    /// <summary>Copies the active Python file to the board and compares a separate readback before reporting success.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task SyncMicroPythonFileAsync() => RunMicroPythonAsync(syncFile: true);

    private async Task RunMicroPythonAsync(bool syncFile)
    {
        try
        {
            if (IsRemoteWorkspace || Workspace is not { } workspace) return;
            if (syncFile && (ActiveDocument?.FilePath is not { } active || !active.EndsWith(".py", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Open the Python file to synchronize first.");
            if (!await SaveAllCoreAsync()) return;
            if (_toolchains.Catalog.Find("micropython") is not { } toolchain || !await EnsureToolchainReadyAsync(toolchain, "mpremote supplies the board REPL and file synchronization.")) return;
            if (_toolchains.Environment is null) throw new NotSupportedException("Select your desktop serial port with mpremote in the Terminal. USB-OTG board selection is available on Android.");
            await SerialMonitor.ReleaseAsync();
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            State = IdeState.Running;
            IUsbConnection? connection = null;
            string? readback = null;
            try
            {
                await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
                var devices = _toolchains.Usb.Devices;
                if (devices.Count == 0) throw new IOException("Connect the MicroPython board with USB-OTG.");
                var index = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("MicroPython board", "Choose the board's USB serial interface.", "Connect", devices.Select(d => d.ToString()).ToArray());
                if (index is null) return;
                connection = await _toolchains.Usb.OpenAsync(devices[index.Value], cancellation.Token);
                var alias = Path.Combine(workspace.RootDirectory, ".tabstudio", "dev", "micropython");
                ProcessStartSpec Command(params string[] args) => UsbBridgeProcess.CreateSerial(connection,
                    new ProcessStartSpec { Program = "mpremote", Arguments = ["connect", alias, .. args], WorkingDirectory = workspace.RootDirectory }, alias);
                if (!syncFile)
                {
                    SelectedPanel = BottomPanel.Terminal;
                    IsBottomPanelVisible = true;
                    var exit = await Terminal.RunProgramAsync(Command("repl"), "MicroPython REPL", cancellation.Token);
                    StatusText = exit.ExitCode == 0 ? "MicroPython REPL disconnected" : "MicroPython REPL ended: " + exit.Description;
                    return;
                }
                var source = ActiveDocument!.FilePath;
                var name = Path.GetFileName(source);
                if (name.Any(c => c is ':' or '/' or '\\' || char.IsControl(c))) throw new InvalidDataException("Unsupported board file name.");
                readback = Path.Combine(workspace.RootDirectory, ".tabstudio", "embedded", Guid.NewGuid().ToString("N") + ".py");
                Directory.CreateDirectory(Path.GetDirectoryName(readback)!);
                SelectedPanel = BottomPanel.Output;
                IsBottomPanelVisible = true;
                foreach (var step in new[] { Command("fs", "cp", source, ":" + name), Command("fs", "cp", ":" + name, readback) })
                {
                    var result = await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, cancellation.Token);
                    if (!result.Succeeded) throw new IOException("MicroPython file transfer failed; see Output.");
                }
                var comparison = await FirmwareBackup.CompareAsync(source, readback, cancellationToken: cancellation.Token);
                if (!comparison.Identical) throw new InvalidDataException("MicroPython file readback differs; synchronization was not verified.");
                StatusText = "Board file verified: " + name;
                Output.WriteLine("Copied and verified :" + name + ". Existing files with other names were preserved.", OutputKind.Info);
            }
            finally
            {
                connection?.Dispose();
                if (readback is not null) File.Delete(readback);
                _run = null;
                if (State == IdeState.Running) State = IdeState.Ready;
            }
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Board operation stopped"; }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }
}
