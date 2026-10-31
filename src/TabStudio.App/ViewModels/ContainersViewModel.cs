using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels.LongOperations;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Runtime;
using TabStudio.Containers.Store;
using TabStudio.Core.Progress;
using TabStudio.Services;
using TabStudio.Services.Compose;
using TabStudio.Services.Native;

namespace TabStudio.App.ViewModels;

/// <summary>Images, processes, their endpoints, logs and a real exec terminal in the Containers tool window.</summary>
public sealed partial class ContainersViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ToolchainServices _toolchains;
    private readonly IUiDispatcher _ui;
    private readonly OperationsViewModel _operations;
    private readonly AppPaths _paths;
    private readonly Action<Uri> _preview;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly SessionRegistryCredentialStore _credentials = new();
    private readonly Dictionary<string, ServiceManager> _compose = new(StringComparer.Ordinal);
    private readonly IProgress<PullProgress> _progress;
    private DockerCommandBridge? _bridge;
    private ContainerLog? _selectedLog;
    private CancellationTokenSource? _operation;
    private bool _disposed;

    public ContainersViewModel(ToolchainServices toolchains, AppPaths paths, IUiDispatcher ui,
        OperationsViewModel operations, Action<Uri> preview)
    {
        _toolchains = toolchains; _paths = paths; _ui = ui; _operations = operations; _preview = preview;
        Engine = new ContainerEngine(new()
        {
            StoreRoot = Path.Combine(paths.Root, "Containers"),
            ProotPath = toolchains.Platform.ProotPath ?? Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_PROOT"),
            LoaderPath = toolchains.Platform.LoaderPath ?? Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_LOADER"),
            ProcStandInRoot = toolchains.Environment?.Rootfs,
            DnsServers = toolchains.Platform.DnsServers,
            Emulator = TabStudio.Containers.Images.OciPlatform.Host.Architecture == "arm64" && toolchains.Platform.NativeLibraryDirectory is { } native
                ? new Box64ContainerEmulator(() => new[] { toolchains.Environment?.Rootfs, Path.Combine(paths.Root, "Toolchains/embedded-glibc/rootfs") }
                    .FirstOrDefault(r => r is not null && File.Exists(Path.Combine(r, "usr/lib/aarch64-linux-gnu/ld-linux-aarch64.so.1"))), Path.Combine(native, "libbox64.so")) : null,
        }, new RegistryClient(_http, _credentials));
        _progress = new Progress<PullProgress>(ReportPull);
        ImageBackend = Engine.Options.Problem is null ? new PreparedImages(this) : null;
        Terminal = new TerminalViewModel(toolchains, ui, () => "/", _ => { });
        Engine.Changed += OnEngineChanged;
        _ = RefreshAsync();
    }

    public ContainerEngine Engine { get; }
    public IServiceImages? ImageBackend { get; }
    public TerminalViewModel Terminal { get; }
    public ObservableCollection<StoredImage> Images { get; } = [];
    public ObservableCollection<ContainerInfo> Items { get; } = [];
    [ObservableProperty] public partial string ImageName { get; set; } = "nginx:alpine";
    [ObservableProperty] public partial string PortMappings { get; set; } = "8080:80";
    [ObservableProperty] public partial StoredImage? SelectedImage { get; set; }
    [ObservableProperty] public partial ContainerInfo? SelectedItem { get; set; }
    [ObservableProperty] public partial string Logs { get; private set; } = "Select a container to see its logs.";
    [ObservableProperty] public partial string? Error { get; private set; }
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial bool ShowsTerminal { get; set; }
    [ObservableProperty] public partial string RegistryHost { get; set; } = "docker.io";
    [ObservableProperty] public partial string RegistryUser { get; set; } = "";
    [ObservableProperty] public partial string RegistrySecret { get; set; } = "";
    public string Limits { get; } = string.Join("\n", ContainerEngine.Limits);
    public string? RuntimeProblem => Engine.Options.Problem;
    public bool HasError => Error is not null;
    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void ReportPull(PullProgress progress)
    {
        if (_disposed) return;
        var key = "container-pull:" + progress.Image;
        var operation = _operations.Begin(key, "Pulling " + progress.Image, cancel: _operation is { } pending ? pending.Cancel : null);
        operation.Report(new OperationProgress(progress.Phase + " " + progress.Image, progress.Received,
            progress.Total > 0 ? progress.Total : null, ProgressUnit.Bytes));
        if (progress.Phase is "Done" or "Failed" or "Cancelled")
            operation.Complete(progress.Phase == "Done" ? OperationOutcome.Succeeded : progress.Phase == "Cancelled" ? OperationOutcome.Cancelled : OperationOutcome.Failed);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_disposed) return;
        var images = await Engine.Store.ListAsync();
        if (_disposed) return;
        var imageId = SelectedImage?.Id; var id = SelectedItem?.Id;
        Images.Clear(); foreach (var image in images) Images.Add(image);
        Items.Clear(); foreach (var item in Engine.Containers) Items.Add(item);
        SelectedImage = Images.FirstOrDefault(i => i.Id == imageId) ?? Images.FirstOrDefault();
        SelectedItem = Items.FirstOrDefault(i => i.Id == id) ?? Items.FirstOrDefault();
    }

    [RelayCommand] private Task PullAsync() => PerformAsync(async token => { await Engine.PullAsync(ImageName.Trim(), progress: _progress, cancellationToken: token); });
    [RelayCommand] private Task RunAsync() => PerformAsync(async token =>
    {
        await PrepareRuntimeAsync(token);
        var spec = new ContainerSpec
        {
            Image = ImageName.Trim(),
            Ports = [.. PortMappings.Split(' ', StringSplitOptions.RemoveEmptyEntries).SelectMany(PortBinding.Parse)],
            PublishAll = string.IsNullOrWhiteSpace(PortMappings),
        };
        var container = await Engine.CreateAsync(spec, _progress, token);
        await Engine.StartAsync(container.Id, token);
    });
    [RelayCommand] private Task StartAsync() => PerformAsync(async token => { if (SelectedItem is { } item) await Engine.StartAsync(item.Id, token); });
    [RelayCommand] private Task StopAsync() => PerformAsync(async token => { if (SelectedItem is { } item) await Engine.StopAsync(item.Id, cancellationToken: token); });
    [RelayCommand] private Task RestartAsync() => PerformAsync(async token => { if (SelectedItem is { } item) await Engine.RestartAsync(item.Id, cancellationToken: token); });
    [RelayCommand] private Task RemoveAsync() => PerformAsync(async token => { if (SelectedItem is { } item) await Engine.RemoveAsync(item.Id, cancellationToken: token); });
    [RelayCommand] private Task RemoveImageAsync() => PerformAsync(async token =>
    {
        if (SelectedImage is { } image)
        {
            if (Engine.IsImageInUse(image.Id)) throw new ContainerException("Remove the containers using this image first.");
            await Engine.Store.RemoveAsync(image.Id, Engine.IsImageInUse, token);
        }
    });
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    [RelayCommand] private async Task SignInAsync()
    {
        try
        {
            var credential = new RegistryCredential(RegistryUser, RegistrySecret);
            await Engine.Registry.LoginAsync(RegistryHost.Trim(), credential);
            await _credentials.SetAsync(RegistryHost.Trim(), credential);
            Error = null;
        }
        catch (Exception ex) when (ex is RegistryException or HttpRequestException) { Error = ex.Message; }
        finally { RegistrySecret = ""; }
    }
    [RelayCommand] private void OpenPort(PublishedPort? port)
    {
        if (port is not null) _preview(new Uri($"http://127.0.0.1:{port.HostPort}/"));
    }
    [RelayCommand] private void OpenTerminal()
    {
        if (SelectedItem is not { IsRunning: true } item) { Error = "Start a container before opening its terminal."; return; }
        Terminal.StopShell();
        Terminal.RemoteShellFactory = size => Task.FromResult(Engine.Exec(item.Id, new ExecSpec(["/bin/sh"]) { Terminal = size }));
        ShowsTerminal = true;
        Terminal.EnsureStarted();
        Terminal.RequestFocus();
    }

    partial void OnSelectedImageChanged(StoredImage? value) { if (value is not null) ImageName = value.DisplayName; }
    partial void OnSelectedItemChanged(ContainerInfo? value)
    {
        if (_selectedLog is not null) _selectedLog.Written -= OnLog;
        _selectedLog = value is null ? null : Engine.Log(value.Id);
        if (_selectedLog is not null) _selectedLog.Written += OnLog;
        RefreshLogs();
    }
    private void OnLog(object? sender, ContainerLogEntry e) => _ui.Post(RefreshLogs);
    private void RefreshLogs()
    {
        if (!_disposed)
        {
            Logs = _selectedLog is null ? "Select a container to see its logs."
                : string.Join('\n', _selectedLog.Recent(500).Select(l => $"{l.Time.LocalDateTime:HH:mm:ss} {l.Text}"));
        }
    }
    private void OnEngineChanged(object? sender, EngineEvent e) => _ui.Post(() => { if (!_disposed) _ = RefreshAsync(); });
    private async Task PrepareRuntimeAsync(CancellationToken token)
    {
        if (_toolchains.Environment is not null && !_toolchains.Manager.IsRuntimeReady)
            await _toolchains.Manager.PrepareRuntimeAsync(cancellationToken: token);
    }
    private async Task PerformAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Error = null;
        using var operation = new CancellationTokenSource(); _operation = operation;
        try { await Task.Run(() => action(operation.Token)); }
        catch (OperationCanceledException) { Error = "Container operation cancelled."; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Error = ex.Message; }
        finally { _operation = null; IsBusy = false; await RefreshAsync(); }
    }

    /// <summary>Installs the private docker command just before starting the local Alpine terminal.</summary>
    public void PrepareTerminalCommand()
    {
        if (_toolchains.Environment is not { IsSetUp: true } environment) return;
        _bridge ??= new DockerCommandBridge(new DockerCommand(Engine, directory =>
        {
            lock (_compose)
            {
                if (_compose.TryGetValue(directory, out var existing)) return existing;
                var project = ComposeReader.Discover(directory) ?? throw new ComposeException("No Compose file in this directory.");
                var manager = new ServiceManager(project, _toolchains.Runtime, new ServicePackages(_toolchains), new ServiceStorage(_paths.Root, directory), images: ImageBackend);
                _compose.Add(directory, manager); return manager;
            }
        }, _progress));
        var file = Path.Combine(environment.Rootfs, "usr/local/bin/docker");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, _bridge.ShellScript);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private sealed class PreparedImages(ContainersViewModel owner) : IServiceImages
    {
        private readonly ComposeImages _images = new(owner.Engine, owner._progress);
        public bool CanRun(ComposeService service) => _images.CanRun(service);
        public async Task<IServiceImageProcess> StartAsync(ComposeProject project, ComposeService service, IReadOnlyList<ServicePort> ports,
            Action<string> log, Action<ServiceState, string?> changed, CancellationToken cancellationToken)
        {
            await owner.PrepareRuntimeAsync(cancellationToken);
            return await _images.StartAsync(project, service, ports, log, changed, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true; _operation?.Cancel(); Terminal.Dispose(); Engine.Changed -= OnEngineChanged;
        if (_selectedLog is not null) _selectedLog.Written -= OnLog;
        if (_bridge is not null) await _bridge.DisposeAsync();
        foreach (var manager in _compose.Values) await manager.DisposeAsync();
        await Engine.DisposeAsync(); _http.Dispose();
    }
}
