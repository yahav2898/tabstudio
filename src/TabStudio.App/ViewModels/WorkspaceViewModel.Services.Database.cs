using TabStudio.App.ViewModels.BackingServices;
using TabStudio.Database;
using TabStudio.Services.Native;

namespace TabStudio.App.ViewModels;

// Compiled when agent/database's WorkspaceViewModel.Database.cs is present. Keep the
// service runtime independent of database providers and resolve the Explorer lazily,
// since the two features can initialize in either order in the workspace constructor.
public sealed partial class WorkspaceViewModel
{
    partial void ConfigureServiceDatabaseExplorer(ref IServiceDatabaseExplorer? databases)
    {
        databases ??= new ServiceDatabaseExplorer(this);
    }

    private sealed class ServiceDatabaseExplorer(WorkspaceViewModel workspace) : IServiceDatabaseExplorer
    {
        private readonly Dictionary<string, Guid> _connections = new(StringComparer.Ordinal);

        // agent/database currently provides SQLite, PostgreSQL and SQL Server.
        public bool CanOpen(NativeServiceKind kind) => kind == NativeServiceKind.Postgres;

        public async Task OpenAsync(string serviceName, NativeServiceKind kind, string connectionString)
        {
            if (!CanOpen(kind)) return;
            var explorer = workspace.DatabaseExplorer;
            await explorer.ActivateAsync();
            if (!_connections.TryGetValue(serviceName, out var id))
                _connections[serviceName] = id = Guid.NewGuid();
            var connection = new DatabaseConnection(id, "Services: " + serviceName, DatabaseProvider.PostgreSql, connectionString);
            var previous = explorer.Connections.FirstOrDefault(c => c.Id == id);
            if (previous is null) explorer.Connections.Add(connection);
            else explorer.Connections[explorer.Connections.IndexOf(previous)] = connection;
            explorer.SelectedConnection = connection;
            explorer.IsAddingConnection = false;
            workspace.SelectSideBarPanel(SideBarPanel.Database);
            // These connections are transient: their ports can change on restart and
            // opening a service must not persist its credentials in the connection store.
            await explorer.ConnectCommand.ExecuteAsync(null);
        }
    }
}
