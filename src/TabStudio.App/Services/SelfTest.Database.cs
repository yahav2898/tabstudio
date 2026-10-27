using System.Diagnostics;
using TabStudio.App.ViewModels;
using TabStudio.Database;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    internal static async Task<SelfTestResult> CheckDatabaseAsync(WorkspaceViewModel workspace, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        var previousDocument = workspace.ActiveDocument;
        var previousPanel = workspace.SelectedSideBarPanel;
        var previousVisibility = workspace.IsSideBarVisible;
        var file = Path.Combine(workspace.Workspace!.RootDirectory, ".tabstudio-selftest.db");
        var explorer = workspace.DatabaseExplorer;
        var id = Guid.Empty;
        DocumentViewModel? queryDocument = null;
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
            // A refused loopback port exercises the packaged managed drivers without a remote service.
            foreach (var probe in new[]
            {
                new DatabaseConnection(Guid.NewGuid(), "PostgreSQL driver", DatabaseProvider.PostgreSql, "Host=127.0.0.1;Port=1;Database=test;Username=test;Password=test;Timeout=1;Pooling=False"),
                new DatabaseConnection(Guid.NewGuid(), "SQL Server driver", DatabaseProvider.SqlServer, "Server=127.0.0.1,1;Database=test;User ID=test;Password=test;Connect Timeout=1;Encrypt=False;Pooling=False"),
            })
            {
                await using var driver = DatabaseConnections.Create(probe);
                try { await driver.OpenAsync(token); }
                catch (System.Data.Common.DbException) { /* A connection refusal proves the provider initialized. */ }
            }
            explorer.NewQueryCommand.Execute(null);
            queryDocument = workspace.ActiveDocument;
            var query = workspace.ActiveDocument?.DatabaseQuery ?? throw new InvalidOperationException("SQL tab did not open.");
            await query.ExecuteAsync("SELECT name FROM people; SELECT 2 AS second_result;");
            if (query.Results.Count != 2 || !Equals(query.Results[0].Data.Rows[0][0], "Tablet")) throw new InvalidOperationException("SQL results were incorrect.");

            // A program's package e_sqlite3 (a glibc build for linux-arm64) resolves to the app's own Bionic copy.
            var packageSqlite = Path.Combine(workspace.Workspace.RootDirectory, "packages", "sqlitepclraw.lib.e_sqlite3", "2.1.11", "runtimes", "linux-arm64", "native", "libe_sqlite3.so");
            if ((OperatingSystem.IsAndroid() || OperatingSystem.IsLinux())
                && new TabStudio.Compilation.Running.PackageNativeLibraries([packageSqlite]).Load("e_sqlite3") == 0) throw new InvalidOperationException("A program's e_sqlite3 did not resolve to the app's SQLite.");
            return new SelfTestResult(true, "Database: Keystore connection, SQLite schema, multiple SQL results and package native SQLite passed", clock.Elapsed);
        }
        finally
        {
            if (id != Guid.Empty) { explorer.SelectedConnection = explorer.Connections.FirstOrDefault(c => c.Id == id); await explorer.RemoveConnectionCommand.ExecuteAsync(null); }
            if (queryDocument is not null) await workspace.CloseDocumentCommand.ExecuteAsync(queryDocument);
            if (previousDocument is not null && workspace.Documents.Contains(previousDocument)) workspace.ActiveDocument = previousDocument;
            workspace.SelectedSideBarPanel = previousPanel;
            workspace.IsSideBarVisible = previousVisibility;
            File.Delete(file);
        }
    }
}
