using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Link;
using System.Text.Json.Nodes;
using TabStudio.Core.Workspaces;
using TabStudio.LanguageServers.Configuration;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private ToolchainStartupItem? CurrentPicProject => !IsRemoteWorkspace && CurrentToolchainProject() is { } item && MicrochipInstallation.IsCompiler(item.Toolchain.Id) ? item : null;

    /// <summary>Whether the selected startup project is MPLAB X.</summary>
    public bool IsPicProject => CurrentPicProject is not null;

    /// <summary>Flash is offered only for an explicitly configured compatible local protocol.</summary>
    public string PicFlashMenuText
    {
        get
        {
            try { return CurrentPicProject?.Project.MarkerPath is { } path && PicFlashProfile.Load(MplabProject.Load(path)).IsLocal ? "_Flash" : "Use the _Windows Bridge"; }
            catch (Exception error) when (error is IOException or InvalidDataException or JsonException or System.Xml.XmlException or KeyNotFoundException or InvalidOperationException) { return "Use the _Windows Bridge"; }
        }
    }

    /// <summary>Whether the bottom dock shows the Serial Monitor tab: for a PIC project, while connected, or while it is the panel shown.</summary>
    public bool ShowsSerialMonitorTab => IsSerialMonitorSelected || IsPicProject || SerialMonitor.IsConnected;

    private void RefreshPicCommands()
    {
        OnPropertyChanged(nameof(IsPicProject));
        OnPropertyChanged(nameof(PicFlashMenuText));
        OnPropertyChanged(nameof(ShowsSerialMonitorTab));
        OnPropertyChanged(nameof(ShowsLogcatTab));
    }

    /// <summary>Opens the board's explicit protocol, application bounds and programmer settings.</summary>
    [RelayCommand]
    private async Task ConfigurePicTargetAsync()
    {
        if (CurrentPicProject?.Project.MarkerPath is not { } marker) return;
        try
        {
            var project = MplabProject.Load(marker);
            var conf = project.SelectedConfiguration();
            var path = Path.Combine(project.Directory, PicFlashProfile.FileName);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var text = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["_help"] = "protocol: an1388-hid, an1388-uart, mcc-uart, pymcuprog (PKOB nano/nedbg), pk2cmd, pic32prog, or windows-bridge. See docs/features/pic.md. Bootloader images need their own linker offset/script; bounds below are HEX BYTE addresses, end exclusive.",
                    ["protocol"] = "windows-bridge", ["device"] = conf.Device,
                    ["firmware"] = $"dist/{conf.Name}/production/firmware.hex", ["tool"] = "nedbg",
                    ["applicationStart"] = "0x0", ["applicationEnd"] = "0x0", ["pageSizeBytes"] = 0,
                    ["addressUnitBytes"] = conf.Device.StartsWith("PIC16", StringComparison.OrdinalIgnoreCase) ? 2 : 1,
                    ["baud"] = 115200, ["interface"] = null, ["bridgeTool"] = "PK4", ["bridgeSerial"] = null,
                    ["autoBaud"] = true,
                    ["deviceFile"] = "", ["deviceFileSha256"] = "",
                }, PicJsonOptions);
                await File.WriteAllTextAsync(path, text + "\n");
            }
            await OpenDocumentAsync(path);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or System.Xml.XmlException) { Output.WriteLine(error.Message, OutputKind.Failure); }
    }

    private static readonly JsonSerializerOptions PicJsonOptions = new() { WriteIndented = true };

    /// <summary>Selects an existing MPLAB configuration, retaining all of its options and per-file settings.</summary>
    [RelayCommand]
    private async Task SelectPicConfigurationAsync()
    {
        if (CurrentPicProject?.Project.MarkerPath is not { } marker) return;
        try
        {
            var project = MplabProject.Load(marker);
            var configurations = project.Configurations.ToList();
            var current = configurations.FindIndex(c => c.Name == project.SelectedConfiguration().Name);
            var index = await Prompt.ChooseAsync("MPLAB configuration", "Choose the configuration TabStudio builds, flashes and debugs: its device and compiler.", "Select",
                [.. configurations.Select(c => $"{c.Name} · {c.Device} · {c.Compiler} {c.CompilerVersion}")], Math.Max(current, 0));
            if (index is not { } chosen) return;
            var selected = configurations[chosen].Name;
            var path = Path.Combine(project.Directory, ".tabstudio/pic-build.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new Dictionary<string, string> { ["configuration"] = selected }));
            StatusText = "MPLAB configuration: " + selected;
            RefreshPicCommands();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or System.Xml.XmlException) { Output.WriteLine(error.Message, OutputKind.Failure); }
    }

    /// <summary>Builds, requests Android USB permission, programs and verifies the configured target.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task FlashPicAsync()
    {
        if (CurrentPicProject is not { } item || item.Project.MarkerPath is not { } marker) return;
        try
        {
            var project = MplabProject.Load(marker);
            var profile = File.Exists(Path.Combine(project.Directory, PicFlashProfile.FileName)) ? PicFlashProfile.Load(project) : null;
            if (profile is not { IsLocal: true }) { await ProgramPicOnBridgeAsync(item, project, profile); return; }
            if (_toolchains.Environment is null) { OpenWindowsBridge(); return; }
            await RunToolchainProjectAsync(item.Info);
            if (!_lastPicBuildSucceeded) return;
            if (profile.Protocol is "pymcuprog" or "pk2cmd" or "pic32prog" && !await PreparePicToolAsync(profile.Protocol)) return;
            using var deviceReservation = SerialMonitor.ReserveDeviceOperation();
            await SerialMonitor.ReleaseAsync();
            var devices = _toolchains.Usb.Devices;
            if (devices.Count == 0) throw new IOException("Connect the bootloader/programmer with a USB-OTG cable, then try Flash again.");
            var device = devices[0];
            if (devices.Count > 1)
            {
                var index = await Prompt.ChooseAsync("Choose a USB device", "More than one USB device is connected. Choose the bootloader or programmer to flash with.", "Flash",
                    [.. devices.Select(d => d.ToString())]);
                if (index is not { } chosen) return;
                device = devices[chosen];
            }
            State = IdeState.Running;
            using var cancel = new CancellationTokenSource();
            _run = cancel;
            using var connection = await _toolchains.Usb.OpenAsync(device, cancel.Token);
            var spec = PicUsbProcess.Create(connection, ["flash", "--profile", Path.Combine(project.Directory, PicFlashProfile.FileName)], project.Directory);
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            var result = await OutputProcess.RunAsync(_toolchains.Runtime, spec, Output, null, cancel.Token);
            State = result.Succeeded ? IdeState.Ready : IdeState.Failed;
            StatusText = result.Succeeded ? "Flash verified" : "Flash failed; see Output";
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Flash stopped"; }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or System.Xml.XmlException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or HttpRequestException or TabStudio.Toolchains.Installation.ToolchainInstallException or ToolchainRuntimeException or NotSupportedException or LinkRemoteException or LinkConnectionLostException)
        {
            Output.WriteLine(error.Message, OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Flash failed";
        }
        finally { _run = null; }
    }

    private async Task<bool> PreparePicToolAsync(string tool)
    {
        var program = MicrochipInstallation.ToolsDirectory + "/native/" + tool + "/bin/" + tool;
        if (tool != "pymcuprog" && _toolchains.Runtime.HasCommand(program)) return true;
        var acceptedPk2 = false;
        if (tool == "pk2cmd")
        {
            var terms = await _toolchains.ReadPicKitLicenseAsync(CancellationToken.None);
            acceptedPk2 = await Prompt.ConfirmAsync("pk2cmd Microchip PICkit license", terms, "I accept — install");
            if (!acceptedPk2) return false;
        }
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        await _toolchains.PreparePicSupportAsync(tool != "pymcuprog", text => Output.WriteLine(text, OutputKind.Info), CancellationToken.None);
        var spec = new ProcessStartSpec { Program = "python3", Arguments = [MicrochipInstallation.ToolsDirectory + "/programmers.py", tool, .. (acceptedPk2 ? new[] { "--accept-pk2cmd-license" } : Array.Empty<string>())] };
        return (await OutputProcess.RunAsync(_toolchains.Runtime, spec, Output, null, CancellationToken.None)).Succeeded;
    }

    private async Task DebugPicProjectAsync(ProjectInfo info, DebugStart start)
    {
        if (FindToolchainProject(info)?.Project.MarkerPath is not { } marker) return;
        try
        {
            var project = MplabProject.Load(marker);
            var conf = project.SelectedConfiguration();
            using var supported = JsonDocument.Parse(MicrochipInstallation.Resource("gpsim.devices.json"));
            if (!supported.RootElement.GetProperty("devices").EnumerateArray().Any(d => string.Equals(d.GetString(), conf.Device, StringComparison.OrdinalIgnoreCase)))
            {
                Output.WriteLine($"gpsim 0.32.1 has no {conf.Device} model. On-chip debugging with PICkit 4/5 uses MPLAB X on the Windows Bridge.", OutputKind.Info);
                await DebugPicOnBridgeAsync();
                return;
            }
            await RunToolchainProjectAsync(info);
            if (!_lastPicBuildSucceeded || !await PreparePicToolAsync("gpsim")) return;
            var receipt = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(project.Directory, ".tabstudio/pic/build.json")))!;
            var elf = receipt["elf"]?.GetValue<string>() ?? throw new IOException("Simulation needs an unambiguous XC8 ELF image with DWARF line information.");
            var hex = receipt["hex"]?.GetValue<string>() ?? throw new IOException("Simulation needs a production HEX image.");
            var properties = new JsonObject { ["name"] = project.SelectedConfiguration().Device + " (gpsim)", ["type"] = "gpsim", ["request"] = "launch",
                ["program"] = elf, ["hex"] = hex, ["device"] = conf.Device, ["cwd"] = project.Directory, ["stopOnEntry"] = true };
            var configuration = new LaunchConfiguration { Name = conf.Device + " (gpsim)", Type = "gpsim", Request = "launch", Properties = properties };
            await LaunchAdapterAsync(new LaunchTarget(configuration.Name, configuration, IsCurrentFile: false), start, noDebug: false);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or System.Xml.XmlException or TabStudio.Toolchains.Installation.ToolchainInstallException)
        {
            Output.WriteLine(error.Message, OutputKind.Failure);
            State = IdeState.Failed;
        }
    }

    /// <summary>Uses MPLAB X's UI and vendor tools on the paired PC for on-chip debugging.</summary>
    [RelayCommand]
    private async Task DebugPicOnBridgeAsync()
    {
        if (CurrentPicProject is not { } item || Workspace is not { } workspace) return;
        if (_windowsBridge is not { } bridge || await bridge.EnsureConnectedAsync() is not { } client) { OpenWindowsBridge(); return; }
        try
        {
            await SaveAllCoreAsync();
            await client.SyncWorkspaceAsync(workspace, TabStudio.Compilation.FileSystemTextProvider.Instance);
            var started = await client.OpenMplabAsync(item.Info.FilePath);
            _bridgeRuns[started.AppId] = new BridgeRun(item.Info, started.AppId);
            WindowsBridgePane?.Show(started);
            SelectedSideBarPanel = SideBarPanel.WindowsBridge;
            IsSideBarVisible = true;
            Output.WriteLine("Use MPLAB X's Debug Project with PICkit 4/5 on the PC. Its UI is in the Bridge pane; this is vendor on-chip debugging, separate from gpsim's local DAP session.", OutputKind.Info);
        }
        catch (Exception error) when (error is LinkRemoteException or LinkConnectionLostException or TimeoutException or IOException)
        {
            Output.WriteLine(error.Message, OutputKind.Failure);
        }
    }

    private async Task ProgramPicOnBridgeAsync(ToolchainStartupItem item, MplabProject project, PicFlashProfile? profile)
    {
        if (_windowsBridge is not { } bridge) { OpenWindowsBridge(); return; }
        var device = project.SelectedConfiguration().Device;
        var tool = profile?.BridgeTool ?? await Prompt.AskAsync("MPLAB IPE tool", "Connect the programmer to the paired PC. Enter PK3, PK4, PK5, SNAP, ICD4 or ICD5. Target power stays external.", "Build and program", "PK4");
        if (tool is null) return;
        if (await BuildOnWindowsBridgeAsync(item.Info, true, false) is not { Success: true, Program: { } firmware }) return;
        State = IdeState.Running;
        using var cancel = new CancellationTokenSource();
        _run = cancel;
        var outcome = await bridge.Client.ProgramPicAsync(item.Info.FilePath, firmware.Path, device, tool.ToUpperInvariant(), profile?.BridgeSerial,
            line => Output.WriteLine(line, OutputKindOf(line)), cancel.Token);
        ErrorList.SetWindowsBridgeDiagnostics(item.Info.FilePath, bridge.PcName ?? "the PC", outcome.Diagnostics);
        State = outcome.Success ? IdeState.Ready : IdeState.Failed;
        StatusText = outcome.Success ? "MPLAB IPE: programmed and verified" : "MPLAB IPE programming failed";
    }
}
