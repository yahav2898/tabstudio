using System.Diagnostics;
using TabStudio.App.ViewModels;
using TabStudio.Database;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    private static async Task<SelfTestResult> CheckDatabaseAsync(WorkspaceViewModel workspace, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        var file = Path.Combine(workspace.Workspace!.RootDirectory, ".tabstudio-selftest.db");
        var explorer = workspace.DatabaseExplorer;
        var id = Guid.Empty;
        try
        {
            var connection = new DatabaseConnection(Guid.NewGuid(), "Database self-test", DatabaseProvider.SQLite, new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ConnectionString);
            await new SqlExecutor().ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS people(id INTEGER PRIMARY KEY, name TEXT); DELETE FROM people; INSERT INTO people VALUES(1,'Tablet');", token);
            await workspace.ShowDatabaseCommand.ExecuteAsync(null);
            explorer.ConnectionName = connection.Name; explorer.Provider = connection.Provider; explorer.ConnectionString = connection.ConnectionString;
            await explorer.SaveConnectionCommand.ExecuteAsync(null); id = explorer.SelectedConnection?.Id ?? Guid.Empty;
            if (id == Guid.Empty) throw new InvalidOperationException("Secure connection could not be saved.");
            await explorer.ReloadConnectionsAsync();
            if (explorer.SelectedConnection?.Id != id) throw new InvalidOperationException("Secure connection could not be decrypted.");
            await explorer.ConnectCommand.ExecuteAsync(null);
            if (explorer.Nodes.Count == 0) throw new InvalidOperationException(explorer.Status);
            explorer.NewQueryCommand.Execute(null);
            var query = workspace.ActiveDocument?.DatabaseQuery ?? throw new InvalidOperationException("SQL tab did not open.");
            await query.ExecuteAsync("SELECT name FROM people; SELECT 2 AS second_result;");
            if (query.Results.Count != 2 || !Equals(query.Results[0].Data.Rows[0][0], "Tablet")) throw new InvalidOperationException("SQL results were incorrect.");
            return new SelfTestResult(true, "Database: Keystore connection, SQLite schema and multiple SQL results passed", clock.Elapsed);
        }
        finally
        {
            if (id != Guid.Empty) { explorer.SelectedConnection = explorer.Connections.FirstOrDefault(c => c.Id == id); await explorer.RemoveConnectionCommand.ExecuteAsync(null); }
            File.Delete(file);
        }
    }
}
