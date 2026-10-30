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

    private string? _files;
    public ServicesViewModel(ToolchainServices toolchains, AppPaths paths, IUiDispatcher ui, Action<Uri> preview, Action remoteHost, IServiceDatabaseExplorer? databases = null)
    {
        _toolchains = toolchains; _paths = paths; _ui = ui; _preview = preview; _remoteHost = remoteHost; _databases = databases;
    }
    public IServiceImages? Images { get; init; }
    public ObservableCollection<ServiceItemViewModel> Items { get; } = [];
    public ServiceManager? Manager => _manager;
    public bool HasServices => Items.Count > 0;
    public bool IsEmpty => !HasServices;

    /// <summary>The Compose files the services come from, for the panel's header.</summary>
    public string? Subtitle => _files;

    /// <summary>Whether reading the Compose files failed.</summary>
    public bool HasError => Error is not null;

    /// <summary>Whether the last start or stop failed for some services, whose rows say why.</summary>
    public bool HasOperationError => OperationError is not null;

    /// <summary>The services the last F5 needed and started.</summary>
    public IReadOnlyList<string> LastNeeded { get; private set; } = [];

    /// <summary>
    /// While no service runs, how they start: nothing is downloaded or started when a repository opens; F5 starts
    /// those the run points at, and Start the others.
    /// </summary>
    public string Offer => _manager?.Project is { Origin: { } origin, RunEnvironment: var variables } && Items.Count == 1
        ? $"{Items[0].Name} is the database {origin} names. F5 starts it and gives the run {string.Join(", ", variables.Keys)}. Nothing is downloaded until then."
        : Items.Count == 1
        ? $"{Items[0].Name} starts when the app's configuration points at it, or with Start. Nothing is downloaded until then."
        : "Each service starts when the app's configuration points at it, or with Start. Nothing is downloaded until then.";

    /// <summary>Whether to show <see cref="Offer"/>: there are services, and none runs.</summary>
    public bool HasOffer => Items.Count > 0 && Items.All(i => !i.Status.IsRunning && i.Status.State is not (ServiceState.Installing or ServiceState.Completed));

    /// <summary>No services and nothing wrong: the empty state, which says where services come from.</summary>
    public bool ShowsEmptyState => IsEmpty && !HasError;

    partial void OnErrorChanged(string? value) { OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(ShowsEmptyState)); }
    partial void OnOperationErrorChanged(string? value) => OnPropertyChanged(nameof(HasOperationError));
    public bool CanUseDatabaseExplorer => _databases is not null;
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial string Summary { get; private set; } = "Open a repository with compose.yaml or docker-compose.yml.";
    /// <summary>Why the Compose files could not be read; the services are not listed then.</summary>
    [ObservableProperty] public partial string? Error { get; private set; }

    /// <summary>
    /// Why the last start, stop or restart failed. Each failed service shows its own error on its row, and the
    /// others keep running: this is the summary over the list, never a reason to hide it.
    /// </summary>
    [ObservableProperty] public partial string? OperationError { get; private set; }
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
        OperationError = null;
        if (_manager is { } previous)
        {
            previous.Changed -= OnChanged; previous.Logged -= OnLogged;
            await previous.DisposeAsync(); _manager = null;
        }
        Items.Clear(); SelectedItem = null; _files = null;
        try
        {
            var project = await Task.Run(() => Discover(_root));
            _configuration = JsonSerializer.Serialize(project);
            if (project is null) { Summary = "Add compose.yaml to the repository, and its databases, caches and queues run here, without Docker."; return; }
            _manager = new ServiceManager(project, _toolchains.Runtime, new ServicePackages(_toolchains), new ServiceStorage(_paths.Root, _root), images: Images);
            _manager.Changed += OnChanged; _manager.Logged += OnLogged;
            foreach (var status in _manager.Statuses) Items.Add(new ServiceItemViewModel(this, status));
            SelectedItem = Items.FirstOrDefault();
            _files = project.Origin is { } origin ? $"for {origin}" : string.Join(", ", project.Files.Select(Path.GetFileName));
            Summary = Items.Count == 1 ? "1 service" : $"{Items.Count} services";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Error = ex.Message; Summary = "Could not read Services configuration."; }
        finally
        {
            foreach (var name in new[] { nameof(HasServices), nameof(IsEmpty), nameof(ShowsEmptyState), nameof(Subtitle), nameof(Offer), nameof(HasOffer) })
            {
                OnPropertyChanged(name);
            }
        }
    }

    [RelayCommand] private async Task StartAllAsync() => await PerformAsync(token => _manager?.StartAllAsync(token) ?? Task.CompletedTask);
    [RelayCommand] private async Task StopAllAsync()
    {
        _operation?.Cancel();
        if (_manager is not { } manager) return;
        try { await manager.StopAsync(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OperationError = ex.Message; }
    }
    [RelayCommand] private async Task RestartAllAsync() => await PerformAsync(token => _manager?.RestartAsync(cancellationToken: token) ?? Task.CompletedTask);
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    [RelayCommand] private void UseRemoteHost() => _remoteHost();
    internal Task StartAsync(string name) => PerformAsync(token => _manager?.StartAsync([name], token) ?? Task.CompletedTask);
    internal Task StopAsync(string name) => PerformAsync(token => _manager?.StopAsync(name, token) ?? Task.CompletedTask);
    internal Task RestartAsync(string name) => PerformAsync(token => _manager?.RestartAsync(name, token) ?? Task.CompletedTask);
    internal void OpenPort(ServicePort port) => _preview(new Uri($"http://127.0.0.1:{port.Published}/"));

    /// <summary>
    /// Before F5: rereads the Compose files, and starts only the services the run needs, those its Compose service
    /// depends on or its configuration points at (<see cref="ServiceManager.NeededBy"/>). Nothing is downloaded or
    /// started for the others; the Services window offers them.
    /// </summary>
    /// <returns>The run's connection variables, or <see langword="null"/> when a needed service did not start.</returns>
    public async Task<IReadOnlyDictionary<string, string?>?> PrepareAsync(string projectDirectory, string environmentName, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string?>? runEnvironment = null;
        LastNeeded = [];
        // Reparse before F5 so saved Compose and .env edits take effect, while unchanged data/processes persist.
        if (_root is not null)
        {
            try
            {
                var latest = await Task.Run(() => Discover(_root), cancellationToken);
                if (_configuration != JsonSerializer.Serialize(latest)) await ReloadAsync();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Error = ex.Message; return null; }
        }
        if (_manager is null && _root is not null && _configuration is null) await ReloadAsync();
        if (_manager is null) return Error is null ? new Dictionary<string, string?>() : null;
        var manager = _manager;
        LastNeeded = manager.NeededBy(projectDirectory, runEnvironment, environmentName);
        if (LastNeeded.Count == 0) return ConnectionEnvironment.ForProject(manager.Project, projectDirectory, manager.Endpoints, environmentName);
        IReadOnlyDictionary<string, string?>? environment = null;
        var success = await PerformAsync(async token => environment = await manager.StartForProjectAsync(projectDirectory, environmentName, runEnvironment, token), cancellationToken);
        return success ? environment : null;
    }

    /// <summary>
    /// A repository's services: its Compose files (a Laravel Sail app's run pointed at its database), or without them
    /// the database its Prisma schema (<see cref="PrismaServices"/>) or its Laravel <c>.env</c>
    /// (<see cref="LaravelServices"/>) names.
    /// </summary>
    internal static ComposeProject? Discover(string directory) =>
        LaravelServices.WithCompose(ComposeReader.Discover(directory), directory) ?? PrismaServices.Discover(directory) ?? LaravelServices.Discover(directory);

    private async Task<bool> PerformAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return false;
        IsBusy = true; OperationError = null;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _operation = operation;
        try { await action(operation.Token); return true; }
        catch (OperationCanceledException) { OperationError = "Service operation cancelled."; return false; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OperationError = ex.Message; return false; }
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
        OnPropertyChanged(nameof(HasOffer));
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

    /// <summary>The state in words, such as "Running" or "Not supported here".</summary>
    public string StateText => Status.State switch
    {
        ServiceState.Unsupported => "Not supported here",
        ServiceState.Unhealthy => "Not healthy",
        var state => state.ToString(),
    };

    /// <summary>Whether the service runs as it should.</summary>
    public bool IsGood => Status.State is ServiceState.Healthy or ServiceState.Running or ServiceState.Completed;

    /// <summary>Whether the service failed, or runs but is not healthy.</summary>
    public bool IsBad => Status.State is ServiceState.Failed or ServiceState.Unhealthy;

    /// <summary>Whether the service is on its way to a state: installing, starting or stopping.</summary>
    public bool IsChanging => Status.State is ServiceState.Installing or ServiceState.Starting or ServiceState.Stopping;

    /// <summary>Whether the service cannot run here.</summary>
    public bool IsWarning => Status.State is ServiceState.Unsupported;

    /// <summary>Whether there is a detail line: the error, or the packages it runs from.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Detail);
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
        foreach (var property in new[] { nameof(State), nameof(StateText), nameof(IsGood), nameof(IsBad), nameof(IsChanging), nameof(IsWarning), nameof(Detail), nameof(HasDetail), nameof(IsUnsupported), nameof(CanStart), nameof(CanStop), nameof(CanOpenDatabase), nameof(Ports) }) OnPropertyChanged(property);
    }
}

