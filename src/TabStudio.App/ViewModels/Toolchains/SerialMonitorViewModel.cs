using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

/// <summary>USB serial independent of an IDE run, with bounded output and explicit connection lifetime.</summary>
public sealed partial class SerialMonitorViewModel(ToolchainServices tools, IUiDispatcher ui) : ObservableObject, IDisposable
{
    private CancellationTokenSource? _session;
    private ToolchainProcess? _process;

    /// <summary>Serial output, kept separate from build output.</summary>
    public OutputViewModel Output { get; } = new(ui, 200_000);

    /// <summary>Attached USB devices.</summary>
    public ObservableCollection<UsbDeviceInfo> Devices { get; } = [];

    /// <summary>Whether a USB device is attached.</summary>
    public bool HasDevices => Devices.Count > 0;

    /// <summary>No device and no session: the monitor says how to attach a board.</summary>
    public bool ShowsEmptyState => !HasDevices && !IsConnected && Output.IsEmpty;

    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ShowsEmptyState));

    /// <summary>Selected USB adapter.</summary>
    [ObservableProperty]
    public partial UsbDeviceInfo? SelectedDevice { get; set; }

    /// <summary>Serial speed, 8-N-1.</summary>
    [ObservableProperty]
    public partial string Baud { get; set; } = "115200";

    /// <summary>Interface number for multi-port adapters; empty selects the first serial interface.</summary>
    [ObservableProperty]
    public partial string Interface { get; set; } = string.Empty;

    /// <summary>Whether a session or permission request owns the device.</summary>
    [ObservableProperty]
    public partial bool IsConnected { get; private set; }

    /// <summary>The active physical attachment, independent of selection changes in the monitor's device list.</summary>
    public string? ConnectedDeviceId { get; private set; }

    /// <summary>Line to send, followed by LF.</summary>
    [ObservableProperty]
    public partial string Input { get; set; } = string.Empty;

    /// <summary>Refreshes the attachment list without requesting permission.</summary>
    [RelayCommand]
    public void Refresh()
    {
        if (IsConnected) return;
        var selected = SelectedDevice?.Id;
        Devices.Clear();
        foreach (var device in tools.Usb.Devices) Devices.Add(device);
        SelectedDevice = Devices.FirstOrDefault(d => d.Id == selected) ?? Devices.FirstOrDefault();
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(ShowsEmptyState));
    }

    /// <summary>Connects after the user selects a USB adapter; Android asks for permission.</summary>
    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnected || SelectedDevice is not { } device) return;
        if (!int.TryParse(Baud, out var baud) || baud is < 1 or > 3_500_000)
        {
            Output.WriteLine("Enter a baud rate from 1 to 3500000.", OutputKind.Failure);
            return;
        }

        ConnectedDeviceId = device.Id;
        IsConnected = true;
        using var cancel = new CancellationTokenSource();
        _session = cancel;
        try
        {
            await tools.PreparePicSupportAsync(false, line => Output.WriteLine(line, OutputKind.Info), cancel.Token);
            using var connection = await tools.Usb.OpenAsync(device, cancel.Token);
            var arguments = new List<string> { "serial", "--baud", baud.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (Interface.Length > 0)
            {
                if (!int.TryParse(Interface, out var number) || number < 0) throw new IOException("Enter a USB interface number, or leave it empty.");
                arguments.AddRange(["--interface", number.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            }
            using var process = tools.Runtime.Start(PicUsbProcess.Create(connection, arguments));
            _process = process;
            var reads = Task.WhenAll(ReadAsync(process.Output, OutputKind.Program), ReadAsync(process.Error!, OutputKind.Error));
            var exit = await process.WaitAsync(cancel.Token);
            await reads;
            Output.WriteLine($"Serial disconnected (exit {exit.ExitCode}).", OutputKind.Info);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException
            or TabStudio.Toolchains.Installation.ToolchainInstallException or TabStudio.Toolchains.Processes.ToolchainRuntimeException)
        {
            Output.WriteLine(error.Message, OutputKind.Failure);
        }
        finally
        {
            _process = null;
            _session = null;
            ConnectedDeviceId = null;
            IsConnected = false;
        }
    }

    /// <summary>Releases the USB connection, including after a detach.</summary>
    [RelayCommand]
    private void Disconnect() => _session?.Cancel();

    /// <summary>Waits until the permissioned descriptor has been released for flashing.</summary>
    public async Task ReleaseAsync()
    {
        _session?.Cancel();
        if (ConnectCommand.ExecutionTask is { } execution) await execution;
    }

    /// <summary>Sends a UTF-8 line; raw output remains visible even without newline characters.</summary>
    [RelayCommand]
    private async Task SendAsync()
    {
        if (_process is not { HasExited: false } process) return;
        try
        {
            await process.Input.WriteAsync(Encoding.UTF8.GetBytes(Input + "\n"));
            await process.Input.FlushAsync();
            Input = string.Empty;
        }
        catch (IOException error) { Output.WriteLine(error.Message, OutputKind.Failure); }
    }

    private async Task ReadAsync(Stream stream, OutputKind kind)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var buffer = new char[2048];
        while (await reader.ReadAsync(buffer) is var count && count > 0) Output.Write(new string(buffer, 0, count), kind);
    }

    /// <inheritdoc />
    public void Dispose() => _session?.Cancel();
}
