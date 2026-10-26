using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace TabStudio.Database;

public static class DatabaseConnections
{
    public static DbConnection Create(DatabaseConnection connection) => connection.Provider switch
    {
        DatabaseProvider.SQLite => new SqliteConnection(connection.ConnectionString),
        DatabaseProvider.PostgreSql => new NpgsqlConnection(connection.ConnectionString),
        DatabaseProvider.SqlServer => new SqlConnection(connection.ConnectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(connection)),
    };

    public static DatabaseConnection WithDatabase(DatabaseConnection connection, string database)
    {
        string value;
        if (connection.Provider == DatabaseProvider.PostgreSql)
        {
            var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = database };
            value = builder.ConnectionString;
        }
        else if (connection.Provider == DatabaseProvider.SqlServer)
        {
            var builder = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = database };
            value = builder.ConnectionString;
        }
        else
        {
            value = connection.ConnectionString;
        }

        return connection with { ConnectionString = value };
    }

    public static string Quote(DatabaseProvider provider, string identifier) => provider == DatabaseProvider.SqlServer
        ? "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]"
        : "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public static string Qualified(DatabaseProvider provider, DatabaseTable table) =>
        Quote(provider, table.Schema) + "." + Quote(provider, table.Name);

    public static DatabaseProvider? Detect(string value)
    {
        try
        {
            var b = new DbConnectionStringBuilder { ConnectionString = value };
            if (b.ContainsKey("Host")) return DatabaseProvider.PostgreSql;
            if (new[] { "Initial Catalog", "Server", "Database", "User ID", "UserID", "UID", "Integrated Security", "Trusted_Connection" }.Any(b.ContainsKey)) return DatabaseProvider.SqlServer;
            if (b.ContainsKey("Data Source") || b.ContainsKey("DataSource") || b.ContainsKey("Filename")) return DatabaseProvider.SQLite;
        }
        catch (ArgumentException) { }
        return null;
    }

    public static string ResolveSqlitePath(string value, string projectDirectory)
    {
        var b = new SqliteConnectionStringBuilder(value);
        if (b.DataSource.Length > 0 && b.DataSource != ":memory:" && !Path.IsPathRooted(b.DataSource)
            && !b.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            b.DataSource = Path.GetFullPath(b.DataSource, projectDirectory);
        return b.ConnectionString;
    }
}
