using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Database;

namespace TabStudio.App.ViewModels.Database;

public sealed partial class DatabaseExplorerViewModel : ObservableObject, IDisposable
{
    private readonly ConnectionStore _store;
    private readonly Func<string?> _root;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private readonly SemaphoreSlim _workGate = new(1);
    private bool _initialized;
    public DatabaseExplorerViewModel(ConnectionStore store, Func<string?> root, EfCoreViewModel efCore)
    {
        _store = store; _root = root; EfCore = efCore;
        Connections.CollectionChanged += (_, _) => OnContentChanged();
        Nodes.CollectionChanged += (_, _) => OnContentChanged();
    }
    public EfCoreViewModel EfCore { get; }
    public IReadOnlyList<DatabaseProvider> Providers { get; } = Enum.GetValues<DatabaseProvider>();
    public ObservableCollection<DatabaseConnection> Connections { get; } = [];
    public ObservableCollection<DiscoveredConnection> Suggestions { get; } = [];
    public ObservableCollection<DatabaseNode> Nodes { get; } = [];
    [ObservableProperty] public partial DatabaseConnection? SelectedConnection { get; set; }
    [ObservableProperty] public partial DiscoveredConnection? SelectedSuggestion { get; set; }
    [ObservableProperty] public partial DatabaseNode? SelectedNode { get; set; }
    [ObservableProperty] public partial string ConnectionName { get; set; } = "Local database";
    [ObservableProperty] public partial string ConnectionString { get; set; } = string.Empty;
    [ObservableProperty] public partial DatabaseProvider Provider { get; set; }
    [ObservableProperty] public partial bool IsAddingConnection { get; set; }

    /// <summary>Whether the EF Core tab shows, rather than Connections.</summary>
    [ObservableProperty] public partial bool IsEfCoreSelected { get; set; }

    /// <summary>Whether there is a saved connection.</summary>
    public bool HasConnections => Connections.Count > 0;

    /// <summary>No connection yet: the empty state, which offers to add one.</summary>
    public bool ShowsEmptyState => !IsAddingConnection && !HasConnections;

    /// <summary>A connection that has not been opened yet: an offer to connect.</summary>
    public bool ShowsConnectOffer => !IsAddingConnection && HasConnections && Nodes.Count == 0;

    /// <summary>The schemas of the open connections.</summary>
    public bool ShowsTree => !IsAddingConnection && Nodes.Count > 0;

    /// <summary>The connections the solution's settings and user secrets name.</summary>
    public bool HasSuggestions => Suggestions.Count > 0;

    partial void OnIsAddingConnectionChanged(bool value) => OnContentChanged();

    private void OnContentChanged()
    {
        foreach (var name in new[] { nameof(HasConnections), nameof(ShowsEmptyState), nameof(ShowsConnectOffer), nameof(ShowsTree), nameof(HasSuggestions) })
        {
            OnPropertyChanged(name);
        }
    }

    [RelayCommand] private void ShowConnections() => IsEfCoreSelected = false;
    [RelayCommand] private void ShowEfCore() => IsEfCoreSelected = true;
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial string Status { get; internal set; } = "Add a connection or discover one from the solution.";
    public event Action<DatabaseConnection, DatabaseSchema?, DatabaseTable?>? OpenRequested;
    partial void OnSelectedConnectionChanged(DatabaseConnection? value) => SelectedNode = null;

    public Task ReloadConnectionsAsync() => GuardAsync(async token =>
    {
        var id = SelectedConnection?.Id;
        var saved = await _store.LoadAsync(token);
        Connections.Clear(); foreach (var connection in saved) Connections.Add(connection);
        SelectedConnection = Connections.FirstOrDefault(c => c.Id == id) ?? Connections.FirstOrDefault();
    });

    public async Task ActivateAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await GuardAsync(async token =>
        {
            foreach (var c in await _store.LoadAsync(token)) Connections.Add(c);
            SelectedConnection = Connections.FirstOrDefault();
            await DiscoverCoreAsync(token);
        });
    }

    /// <summary>Opens the connection form filled in, such as with a project's connection string from its appsettings.</summary>
    /// <param name="name">The connection's name.</param>
    /// <param name="provider">Its provider.</param>
    /// <param name="connectionString">The connection string to start from.</param>
    /// <param name="status">What the form is for, in the status line.</param>
    public void BeginConnection(string name, DatabaseProvider provider, string connectionString, string status)
    {
        ConnectionName = name; Provider = provider; ConnectionString = connectionString; IsAddingConnection = true; Status = status;
    }

    /// <summary>The saved connections, with their connection strings from the encrypted store.</summary>
    /// <param name="token">Cancels reading.</param>
    public Task<IReadOnlyList<DatabaseConnection>> LoadSavedConnectionsAsync(CancellationToken token = default) => _store.LoadAsync(token);

    /// <summary>Saves a connection that a project's runs use, such as a SQLite file in place of its SQL Server.</summary>
    /// <param name="connection">The connection.</param>
    public async Task SaveRunConnectionAsync(DatabaseConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await _store.SaveAsync(connection, _lifetime.Token);
        Connections.Add(connection);
    }

    [RelayCommand] private void AddConnection() => IsAddingConnection = true;
    [RelayCommand] private void DismissConnection() { ConnectionString = string.Empty; IsAddingConnection = false; }
    [RelayCommand] private void UseSuggestion()
    {
        if (SelectedSuggestion is not { } s) return;
        ConnectionName = s.Name; ConnectionString = s.Value; Provider = s.Provider ?? DatabaseProvider.SqlServer; IsAddingConnection = true;
    }
    [RelayCommand] private Task DiscoverAsync() => GuardAsync(DiscoverCoreAsync);
    private async Task DiscoverCoreAsync(CancellationToken token)
    {
        Suggestions.Clear();
        if (_root() is not { } root) return;
        foreach (var s in await ConnectionDiscovery.FindAsync(root, token: token)) Suggestions.Add(s);
        SelectedSuggestion = Suggestions.FirstOrDefault();
        OnContentChanged();
        Status = Suggestions.Count switch
        {
            0 => "No connections found in the solution's appsettings or user secrets.",
            1 => "1 connection found in the solution's appsettings or user secrets.",
            var count => $"{count} connections found in the solution's appsettings or user secrets.",
        };
    }
    [RelayCommand] private Task SaveConnectionAsync() => GuardAsync(async token =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
        var value = Provider == DatabaseProvider.SQLite && _root() is { } root ? DatabaseConnections.ResolveSqlitePath(ConnectionString, root) : ConnectionString;
        var connection = new DatabaseConnection(Guid.NewGuid(), ConnectionName.Trim(), Provider, value);
        await _store.SaveAsync(connection, token);
        Connections.Add(connection); SelectedConnection = connection; ConnectionString = string.Empty; IsAddingConnection = false;
        Status = "Connection saved. Connect to browse its tables and views.";
    });
    [RelayCommand] private Task RemoveConnectionAsync() => GuardAsync(async token =>
    {
        if (SelectedConnection is not { } c) return;
        await _store.RemoveAsync(c.Id, token); Connections.Remove(c);
        foreach (var node in Nodes.Where(n => n.Connection?.Id == c.Id).ToArray()) Nodes.Remove(node);
        SelectedConnection = Connections.FirstOrDefault(); SelectedNode = null;
        Status = "Connection removed.";
    });
    [RelayCommand] private Task ConnectAsync() => GuardAsync(async token =>
    {
        if (SelectedConnection is not { } c) return;
        var reader = new SchemaReader();
        var schema = await reader.ReadAsync(c, token);
        var names = await reader.DatabasesAsync(c, token);
        foreach (var old in Nodes.Where(n => n.Connection?.Id == c.Id).ToArray()) Nodes.Remove(old);
        var root = new DatabaseNode(c.Name, c.Provider.ToString(), c, schema, icon: "Icon.Database") { IsExpanded = true };
        foreach (var name in names)
        {
            var selected = DatabaseConnections.WithDatabase(c, name);
            DatabaseNode node = null!;
            node = new DatabaseNode(name, "database", selected, icon: "Icon.Database", load: () => GuardAsync(async t =>
            {
                var loaded = await reader.ReadAsync(selected, t); node.Fill(selected, loaded); Status = $"{loaded.Tables.Count} objects loaded.";
            }));
            if (name == schema.Database || c.Provider == DatabaseProvider.SQLite)
            { node.Fill(selected, schema); node.IsExpanded = true; SelectedNode = node; }
            root.Children.Add(node);
        }
        Nodes.Add(root); SelectedNode ??= root;
        Status = $"Connected to {c.Name}: {schema.Tables.Count} tables and views.";
    });
    [RelayCommand] private void NewQuery()
    {
        var c = SelectedNode?.Connection ?? SelectedConnection;
        if (c is not null) OpenRequested?.Invoke(c, SelectedNode?.Schema, null);
    }
    [RelayCommand] private void EditData()
    {
        if (SelectedNode is { Connection: { } c, Table: { } t } node) OpenRequested?.Invoke(c, node.Schema, t);
        else Status = "Select a table or a view to edit its data.";
    }
    [RelayCommand] private void Cancel() => _operation?.Cancel();

    private async Task GuardAsync(Func<CancellationToken, Task> work)
    {
        if (_lifetime.IsCancellationRequested) return;
        try { await _workGate.WaitAsync(_lifetime.Token); }
        catch (OperationCanceledException) { return; }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _operation = operation; IsBusy = true;
        try { await work(operation.Token); }
        catch (OperationCanceledException) { Status = "Cancelled."; }
        // Provider errors can echo credentials or SQL; only the server's reason is shown, not the raw exception.
        catch (Exception ex) when (ex is not OutOfMemoryException) { Status = DatabaseError.Message(ex); }
        finally { _operation = null; IsBusy = false; _workGate.Release(); }
    }
    public void Dispose() { _lifetime.Cancel(); _operation?.Cancel(); _lifetime.Dispose(); EfCore.Dispose(); }
}
