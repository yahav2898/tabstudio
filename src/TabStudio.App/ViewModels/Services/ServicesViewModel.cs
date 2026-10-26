using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.Services;
using TabStudio.Services.Compose;
using TabStudio.Services.Native;

namespace TabStudio.App.ViewModels.BackingServices;

/// <summary>Optional Database Explorer integration. A host registers this when the explorer is installed.</summary>
public interface IServiceDatabaseExplorer
{
    bool CanOpen(NativeServiceKind kind);
    Task OpenAsync(string serviceName, NativeServiceKind kind, string connectionString);
}

public sealed partial class ServicesViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ToolchainServices _toolchains;
    private readonly AppPaths _paths;
    private readonly IUiDispatcher _ui;
    private readonly Action<Uri> _preview;
    private readonly Action _remoteHost;
    private readonly IServiceDatabaseExplorer? _databases;
    private CancellationTokenSource? _operation;
    private ServiceManager? _manager;
    private string? _root;
    private string? _configuration;
    private bool _disposed;

    public ServicesViewModel(ToolchainServices toolchains, AppPaths paths, IUiDispatcher ui, Action<Uri> preview, Action remoteHost, IServiceDatabaseExplorer? databases = null)
    {
        _toolchains = toolchains; _paths = paths; _ui = ui; _preview = preview; _remoteHost = remoteHost; _databases = databases;
    }
    public ObservableCollection<ServiceItemViewModel> Items { get; } = [];
    public ServiceManager? Manager => _manager;
    public bool HasServices => Items.Count > 0;
    public bool IsEmpty => !HasServices;
    public bool CanUseDatabaseExplorer => _databases is not null;
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial string Summary { get; private set; } = "Open a repository with compose.yaml or docker-compose.yml.";
    [ObservableProperty] public partial string? Error { get; private set; }
    [ObservableProperty] public partial ServiceItemViewModel? SelectedItem { get; set; }
    [ObservableProperty] public partial string Logs { get; private set; } = "Select a service to see its logs.";

    public Task LoadAsync(string directory)
    {
        _root = directory;
        return ReloadAsync();
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (_root is null || IsBusy) return;
        Error = null;
        if (_manager is { } previous)
        {
            previous.Changed -= OnChanged; previous.Logged -= OnLogged;
            await previous.DisposeAsync(); _manager = null;
        }
        Items.Clear(); SelectedItem = null;
        try
        {
            var project = await Task.Run(() => ComposeReader.Discover(_root));
            _configuration = JsonSerializer.Serialize(project);
            if (project is null) { Summary = "No Compose file found. Add compose.yaml to run backing services on this device."; return; }
            _manager = new ServiceManager(project, _toolchains.Runtime, new ServicePackages(_toolchains), new ServiceStorage(_paths.Root, _root));
            _manager.Changed += OnChanged; _manager.Logged += OnLogged;
            foreach (var status in _manager.Statuses) Items.Add(new ServiceItemViewModel(this, status));
            SelectedItem = Items.FirstOrDefault();
            Summary = $"{Items.Count} services · {string.Join(", ", project.Files.Select(Path.GetFileName))}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Error = ex.Message; Summary = "Could not read Services configuration."; }
        finally { OnPropertyChanged(nameof(HasServices)); OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand] private Task StartAllAsync() => PerformAsync(token => _manager?.StartAllAsync(token) ?? Task.CompletedTask);
    [RelayCommand] private async Task StopAllAsync()
    {
        _operation?.Cancel();
        if (_manager is not { } manager) return;
        try { await manager.StopAsync(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Error = ex.Message; }
    }
    [RelayCommand] private Task RestartAllAsync() => PerformAsync(token => _manager?.RestartAsync(cancellationToken: token) ?? Task.CompletedTask);
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    [RelayCommand] private void UseRemoteHost() => _remoteHost();
    internal Task StartAsync(string name) => PerformAsync(token => _manager?.StartAsync([name], token) ?? Task.CompletedTask);
    internal Task StopAsync(string name) => PerformAsync(token => _manager?.StopAsync(name, token) ?? Task.CompletedTask);
    internal Task RestartAsync(string name) => PerformAsync(token => _manager?.RestartAsync(name, token) ?? Task.CompletedTask);
    internal void OpenPort(ServicePort port) => _preview(new Uri($"http://127.0.0.1:{port.Published}/"));

    public async Task<IReadOnlyDictionary<string, string?>?> PrepareAsync(string projectDirectory, string environmentName, CancellationToken cancellationToken)
    {
        // Reparse before F5 so saved Compose and .env edits take effect, while unchanged data/processes persist.
        if (_root is not null)
        {
            try
            {
                var latest = await Task.Run(() => ComposeReader.Discover(_root), cancellationToken);
                if (_configuration != JsonSerializer.Serialize(latest)) await ReloadAsync();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Error = ex.Message; return null; }
        }
        if (_manager is null && _root is not null && _configuration is null) await ReloadAsync();
        if (_manager is null) return Error is null ? new Dictionary<string, string?>() : null;
        IReadOnlyDictionary<string, string?>? environment = null;
        var success = await PerformAsync(async token => environment = await _manager.StartForProjectAsync(projectDirectory, environmentName, token), cancellationToken);
        return success ? environment : null;
    }

    private async Task<bool> PerformAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return false;
        IsBusy = true; Error = null;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _operation = operation;
        try { await action(operation.Token); return true; }
        catch (OperationCanceledException) { Error = "Service operation cancelled."; return false; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Error = ex.Message; return false; }
        finally { _operation = null; IsBusy = false; }
    }
    internal async Task OpenDatabaseAsync(ServiceItemViewModel item)
    {
        if (_manager is null || item.Status.Mapping is not { } mapping || _databases?.CanOpen(mapping.Kind) != true) return;
        var env = ConnectionEnvironment.ForProject(_manager.Project, _root!, _manager.Endpoints);
        if (env.TryGetValue("ConnectionStrings__" + item.Name, out var connection) && connection is not null) await _databases.OpenAsync(item.Name, mapping.Kind, connection);
    }
    internal bool CanOpenDatabase(ServiceItemViewModel item) => item.Status.Mapping is { } mapping && item.Status.IsRunning && _databases?.CanOpen(mapping.Kind) == true;
    partial void OnSelectedItemChanged(ServiceItemViewModel? value) => RefreshLogs();
    private void RefreshLogs() => Logs = SelectedItem is { } selected && _manager is { } manager ? string.Join('\n', manager.Logs(selected.Name).Select(l => $"{l.Time.LocalDateTime:HH:mm:ss} {l.Text}")) : "Select a service to see its logs.";
    private void OnChanged(object? sender, ServiceStatus status) => _ui.Post(() =>
    {
        if (_disposed || !ReferenceEquals(sender, _manager)) return;
        Items.FirstOrDefault(i => i.Name == status.Name)?.Update(status);
    });
    private void OnLogged(object? sender, ServiceLogLine line) => _ui.Post(() => { if (!_disposed && ReferenceEquals(sender, _manager) && SelectedItem?.Name == line.Service) RefreshLogs(); });
    public async ValueTask DisposeAsync()
    {
        _disposed = true; _operation?.Cancel();
        if (_manager is { } manager) { manager.Changed -= OnChanged; manager.Logged -= OnLogged; await manager.DisposeAsync(); }
    }
}

public sealed partial class ServiceItemViewModel : ObservableObject
{
    private readonly ServicesViewModel _owner;
    public ServiceItemViewModel(ServicesViewModel owner, ServiceStatus status)
    {
        _owner = owner; Status = status;
        StartCommand = new AsyncRelayCommand(() => owner.StartAsync(Name));
        StopCommand = new AsyncRelayCommand(() => owner.StopAsync(Name));
        RestartCommand = new AsyncRelayCommand(() => owner.RestartAsync(Name));
        DatabaseCommand = new AsyncRelayCommand(() => owner.OpenDatabaseAsync(this));
        RemoteCommand = owner.UseRemoteHostCommand;
        OpenPortCommand = new RelayCommand<ServicePort>(p => { if (p is not null) owner.OpenPort(p); });
    }
    public ServiceStatus Status { get; private set; }
    public string Name => Status.Name;
    public string Image => Status.Definition.Image ?? "Build from repository";
    public string State => Status.State.ToString();
    public string Detail => Status.Detail ?? (Status.Mapping is { } mapping ? string.Join(", ", mapping.Packages) : "");
    public bool IsUnsupported => Status.State == ServiceState.Unsupported;
    public bool CanStart => !Status.IsRunning && !IsUnsupported;
    public bool CanStop => Status.IsRunning;
    public bool CanOpenDatabase => _owner.CanOpenDatabase(this);
    public IReadOnlyList<ServicePort> Ports => Status.Ports;
    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand RestartCommand { get; }
    public IAsyncRelayCommand DatabaseCommand { get; }
    public IRelayCommand RemoteCommand { get; }
    public IRelayCommand<ServicePort> OpenPortCommand { get; }
    public void Update(ServiceStatus status)
    {
        Status = status;
        foreach (var property in new[] { nameof(State), nameof(Detail), nameof(IsUnsupported), nameof(CanStart), nameof(CanStop), nameof(CanOpenDatabase), nameof(Ports) }) OnPropertyChanged(property);
    }
}
