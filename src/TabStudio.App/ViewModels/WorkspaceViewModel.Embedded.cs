using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private string? _lastEmbeddedSerialDevice;
    private static readonly JsonSerializerOptions EmbeddedJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private async Task<EmbeddedProject?> SelectEmbeddedProjectAsync()
    {
        if (IsRemoteWorkspace || Workspace is not { } workspace) return null;
        var projects = await Task.Run(() => EmbeddedProjectDetector.Detect(workspace.RootDirectory));
        if (projects.Count == 0)
        {
            Output.WriteLine("No embedded project found. Open platformio.ini, an Arduino sketch, a vendor project, or a supported SDK project.", OutputKind.Info);
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            return null;
        }
        var active = CurrentToolchainProject()?.Project.Directory;
        if (projects.FirstOrDefault(p => string.Equals(p.Directory, active, StringComparison.Ordinal)) is { } selected) return selected;
        if (projects.Count == 1) return projects[0];
        var index = await Prompt.ChooseAsync("Embedded project", "Choose the project to work with.", "Select",
            projects.Select(p => p.Name + " · " + p.Kind).ToArray());
        return index is { } choice ? projects[choice] : null;
    }

    /// <summary>Edits explicit native board, environment and upload choices without changing the repository's project files.</summary>
    [RelayCommand]
    private async Task ConfigureEmbeddedAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is not { } project) return;
            var path = Path.Combine(project.Directory, EmbeddedBuildPlanner.OptionsFile);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new EmbeddedBuildOptions
                {
                    Environment = project.DefaultEnvironment,
                    Target = project.Target,
                }, EmbeddedJson) + "\n");
            }
            await OpenDocumentAsync(path);
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Builds firmware with the project's own build system.</summary>
    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildEmbeddedAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is { } project) await BuildEmbeddedCoreAsync(project);
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    private async Task<bool> BuildEmbeddedCoreAsync(EmbeddedProject project)
    {
        if (!await SaveAllCoreAsync()) return false;
        var plan = EmbeddedBuildPlanner.Plan(project);
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        foreach (var note in plan.Notes) Output.WriteLine(note, OutputKind.Info);
        if (plan.Build.Count == 0)
        {
            StatusText = "Choose an embedded build configuration";
            await ConfigureEmbeddedAsync();
            return false;
        }
        if (_toolchains.Catalog.Find(plan.ToolchainId) is not { } toolchain)
            throw new InvalidOperationException("Install the project's native SDK in the Terminal; no compatible Toolchain Store package is configured.");
        if (!await EnsureToolchainReadyAsync(toolchain, project.Name + " builds with " + toolchain.Name + ".")) return false;
        using var cancellation = new CancellationTokenSource();
        _run = cancellation;
        State = IdeState.Running;
        StatusText = "Building " + project.Name;
        try
        {
            foreach (var step in plan.Preparation.Concat(plan.Build))
            {
                Output.WriteLine("> " + step.CommandLine, OutputKind.Info);
                var result = await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, cancellation.Token);
                if (!result.Succeeded)
                {
                    State = IdeState.Failed;
                    StatusText = "Firmware build failed";
                    var nativeProjects = ToolchainProjectDetector.Detect(project.Directory, _toolchains.Catalog);
                    if (nativeProjects.Count > 0) ReportToolchainRun(nativeProjects[0], project.Marker, result);
                    return false;
                }
            }
            State = IdeState.Ready;
            StatusText = "Firmware built";
            await ReportEmbeddedMemoryAsync(project.Directory, cancellation.Token);
            return true;
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Firmware build stopped"; return false; }
        finally { _run = null; }
    }

    /// <summary>Programs only an explicitly mapped application image through one selected permissioned USB device.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task UploadEmbeddedAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is not { } project || !await BuildEmbeddedCoreAsync(project)) return;
            var profilePath = Path.Combine(project.Directory, ".tabstudio", "embedded-flash.json");
            if (!File.Exists(profilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(new EmbeddedFlashRequest
                {
                    Protocol = EmbeddedFlashProtocol.OpenOcd, Target = "SELECT_ACTUAL_CHIP",
                    FirmwarePath = "firmware.elf", WorkingDirectory = project.Directory,
                    SerialPort = "/dev/ttyUSB0",
                }, EmbeddedJson) + "\n");
                await OpenDocumentAsync(profilePath);
                Output.WriteLine("Set the actual firmware, probe/bootloader, target and application-flash ranges in this profile. Empty ranges refuse every write before erase.", OutputKind.Info);
                return;
            }
            if (new FileInfo(profilePath).Length > 65536) throw new InvalidDataException("Flash profile exceeds 64 KiB.");
            var request = JsonSerializer.Deserialize<EmbeddedFlashRequest>(await File.ReadAllTextAsync(profilePath), EmbeddedJson)
                ?? throw new InvalidDataException("Flash profile is empty.");
            request = request with
            {
                WorkingDirectory = project.Directory,
                FirmwarePath = Path.GetFullPath(request.FirmwarePath, project.Directory),
                SerialPort = _toolchains.Environment is null ? request.SerialPort
                    : Path.Combine(project.Directory, ".tabstudio", "dev", "ttyUSB0"),
            };
            if (request.Protocol == EmbeddedFlashProtocol.ProbeRs && _toolchains.Catalog.Find("probe-rs") is { } probeTools
                && !await EnsureToolchainReadyAsync(probeTools, "probe-rs uses the embedded glibc environment and the selected device's permissioned USBFS inventory."))
            {
                return;
            }
            var plan = await Task.Run(() => EmbeddedFlashPlanner.Plan(request));
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "Uploading firmware";
            IUsbConnection? connection = null;
            UsbfsInventory? inventory = null;
            try
            {
                Func<ProcessStartSpec, ProcessStartSpec>? bridge = null;
                Func<CancellationToken, Task>? prepareBootloader = null;
                if (_toolchains.Environment is not null)
                {
                    await SerialMonitor.ReleaseAsync();
                    await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
                    await PrepareEmbeddedProtocolAsync(request, cancellation.Token);
                    var devices = _toolchains.Usb.Devices;
                    if (devices.Count == 0) throw new IOException("Connect the board or debug probe using USB-OTG.");
                    var choice = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Upload device", "Choose the board or probe to program.", "Select", devices.Select(d => d.ToString()).ToArray());
                    if (choice is null) return;
                    connection = await _toolchains.Usb.OpenAsync(devices[choice.Value], cancellation.Token);
                    if (request.Protocol == EmbeddedFlashProtocol.ProbeRs)
                    {
                        var granted = connection;
                        inventory = await Task.Run(() => UsbfsDeviceInventory.Create(granted, Path.Combine(_paths.Root, "embedded-usb")), cancellation.Token);
                    }
                    bridge = step => EmbeddedFlashPlanner.RequiresSerial(request)
                        ? UsbBridgeProcess.CreateSerial(connection ?? throw new IOException("USB device disconnected."), step, request.SerialPort!, request.BaudRate)
                        : inventory is not null
                            ? UsbBridgeProcess.CreateUsbfs(connection ?? throw new IOException("USB device disconnected."), inventory, step)
                            : UsbBridgeProcess.CreateLibUsb(connection ?? throw new IOException("USB device disconnected."), step);
                    prepareBootloader = async token =>
                    {
                        var before = _toolchains.Usb.Devices.Select(device => device.Id).ToHashSet(StringComparer.Ordinal);
                        var touched = await OutputProcess.RunAsync(_toolchains.Runtime, UsbBridgeProcess.CreateTouch1200(connection), Output, null, token);
                        if (!touched.Succeeded) throw new IOException("The 1200-baud bootloader touch failed.");
                        connection.Dispose();
                        connection = null;
                        var deadline = DateTime.UtcNow.AddSeconds(15);
                        while (DateTime.UtcNow < deadline)
                        {
                            var bootloaders = _toolchains.Usb.Devices.Where(device => !before.Contains(device.Id)).ToArray();
                            if (bootloaders.Length > 0)
                            {
                                var index = bootloaders.Length == 1 ? 0 : await Prompt.ChooseAsync("Bootloader device", "Choose the board that reconnected after the 1200-baud touch.", "Select", bootloaders.Select(d => d.ToString()).ToArray());
                                if (index is null) throw new OperationCanceledException(token);
                                connection = await _toolchains.Usb.OpenAsync(bootloaders[index.Value], token);
                                return;
                            }
                            await Task.Delay(100, token);
                        }
                        throw new IOException("No new USB bootloader appeared after the 1200-baud touch. Reconnect in bootloader mode and select its actual USB identity.");
                    };
                }
                var result = await EmbeddedFlashExecutor.ExecuteAsync(_toolchains.Runtime, plan, bridge,
                    new Progress<string>(line => Output.WriteLine(line.TrimEnd(), OutputKind.Info)), prepareBootloader, cancellation.Token);
                Output.WriteLine((result.Verified ? "Verified firmware SHA-256: " : "Transferred firmware SHA-256: ") + result.FirmwareSha256, OutputKind.Info);
                Output.WriteLine(plan.Verification, OutputKind.Info);
                if (connection is not null) await RememberEmbeddedDeviceAsync(connection.Device, project.Name, request);
                _lastEmbeddedSerialDevice = EmbeddedFlashPlanner.RequiresSerial(request)
                    ? connection?.Device.Id : null;
                State = IdeState.Ready;
                StatusText = result.Verified ? "Firmware upload verified" : "Firmware transferred; readback unavailable";
            }
            finally { inventory?.Dispose(); connection?.Dispose(); _run = null; if (State == IdeState.Running) State = IdeState.Ready; }
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Firmware upload stopped"; }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Lists attached USB identities and conservative board/probe suggestions without requesting permission.</summary>
    [RelayCommand]
    private async Task ShowEmbeddedDevicesAsync()
    {
        try
        {
            var database = await EmbeddedDeviceDatabase.LoadAsync(Path.Combine(_paths.Root, "embedded-devices.json"));
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            foreach (var device in _toolchains.Usb.Devices)
            {
                Output.WriteLine(device.ToString(), OutputKind.Info);
                foreach (var candidate in database.Identify(device)) Output.WriteLine("  " + candidate.Name + " · " + candidate.Route, OutputKind.Info);
            }
            if (_toolchains.Usb.Devices.Count == 0) Output.WriteLine("No USB board or debug probe attached.", OutputKind.Info);
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Converts an active vendor project to an independent GCC/CMake project with structured port notes.</summary>
    [RelayCommand]
    private async Task ImportEmbeddedProjectAsync()
    {
        try
        {
            if (ActiveDocument?.FilePath is not { } path || Path.GetExtension(path).ToLowerInvariant() is not (".uvprojx" or ".ewp" or ".cproj"))
                throw new InvalidOperationException("Open a Keil .uvprojx, IAR .ewp or Atmel/Microchip .cproj file first.");
            var destination = await Prompt.AskAsync("Import embedded project", "Create a separate GCC/CMake project. The original is preserved.", "Import", Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-gcc"));
            if (destination is null) return;
            await Task.Run(() => VendorProjectImporter.Import(path, destination));
            await OpenDocumentAsync(Path.Combine(destination, "CMakeLists.txt"));
            StatusText = "GCC/CMake import created; review port notes before building";
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    private static bool IsEmbeddedError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
        or ArgumentException or InvalidOperationException or NotSupportedException or System.Xml.XmlException or OverflowException or FormatException
        or ToolchainRuntimeException or TabStudio.Toolchains.Installation.ToolchainInstallException;

    private async Task RememberEmbeddedDeviceAsync(UsbDeviceInfo device, string board, EmbeddedFlashRequest request)
    {
        try
        {
            var path = Path.Combine(_paths.Root, "embedded-devices.json");
            var database = await EmbeddedDeviceDatabase.LoadAsync(path);
            database.Remember(new(device.VendorId, device.ProductId, null, board, request.Protocol.ToString(), request.Target));
            await database.SaveAsync(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Output.WriteLine("Firmware verified; could not save the device association: " + error.Message, OutputKind.Info);
        }
    }

    private void EmbeddedFailure(Exception error)
    {
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        Output.WriteLine(error.Message, OutputKind.Failure);
        State = IdeState.Failed;
        StatusText = "Embedded operation failed; see Output";
    }
}
