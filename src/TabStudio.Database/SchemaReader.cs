using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TabStudio.Database;

public sealed class SchemaReader
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Preserve the public instance service API used by Database Explorer and its consumers.")]
    public Task<IReadOnlyList<string>> DatabasesAsync(DatabaseConnection connection, CancellationToken token = default) => Task.Run(async () =>
    {
        await using var db = DatabaseConnections.Create(connection);
        await db.OpenAsync(token).ConfigureAwait(false);
        var sql = connection.Provider switch
        {
            DatabaseProvider.SQLite => "PRAGMA database_list",
            DatabaseProvider.PostgreSql => "SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate AND has_database_privilege(datname, 'CONNECT') ORDER BY datname",
            _ => "SELECT name FROM sys.databases WHERE state = 0 AND HAS_DBACCESS(name) = 1 ORDER BY name",
        };
        var rows = await ReadAsync(db, sql, token).ConfigureAwait(false);
        return (IReadOnlyList<string>)rows.Select(r => Text(r[connection.Provider == DatabaseProvider.SQLite ? 1 : 0])).ToArray();
    }, token);

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Preserve the public instance service API used by Database Explorer and its consumers.")]
    public Task<DatabaseSchema> ReadAsync(DatabaseConnection connection, CancellationToken token = default) => Task.Run(async () =>
    {
        await using var db = DatabaseConnections.Create(connection);
        await db.OpenAsync(token).ConfigureAwait(false);

        // Npgsql learns the database's types once per data source; an enum created since (by a migration, or in a
        // query tab) could not be read until it learned them again, so reading the schema refreshes them.
        if (db is Npgsql.NpgsqlConnection npgsql)
        {
            await npgsql.ReloadTypesAsync().ConfigureAwait(false);
        }

        return connection.Provider == DatabaseProvider.SQLite
            ? await ReadSqliteAsync(db, token).ConfigureAwait(false)
            : await ReadServerAsync(db, connection.Provider, token).ConfigureAwait(false);
    }, token);

    private static async Task<DatabaseSchema> ReadSqliteAsync(DbConnection db, CancellationToken token)
    {
        var tables = new List<DatabaseTable>();
        foreach (var r in await ReadAsync(db, "SELECT name, type FROM main.sqlite_schema WHERE type IN ('table','view') AND name NOT LIKE 'sqlite_%' ORDER BY name", token).ConfigureAwait(false))
        {
            var name = Text(r[0]);
            var quoted = DatabaseConnections.Quote(DatabaseProvider.SQLite, name);
            var columns = (await ReadAsync(db, $"PRAGMA main.table_xinfo({quoted})", token).ConfigureAwait(false))
                .Select(c => new DatabaseColumn(Text(c[1]), Text(c[2]), Number(c[3]) == 0 && Number(c[5]) == 0, Number(c[5]), Number(c[6]) != 0)).ToArray();
            var keys = new List<DatabaseKey>();
            var pk = columns.Where(c => c.KeyOrder > 0).OrderBy(c => c.KeyOrder).ToArray();
            if (pk.Length > 0) keys.Add(new DatabaseKey("PRIMARY KEY", string.Join(", ", pk.Select(c => c.Name))));
            foreach (var group in (await ReadAsync(db, $"PRAGMA main.foreign_key_list({quoted})", token).ConfigureAwait(false)).GroupBy(k => Number(k[0])))
                keys.Add(new DatabaseKey($"FK_{group.Key}", string.Join(", ", group.OrderBy(k => Number(k[1])).Select(k => $"{Text(k[3])} → {Text(k[2])}.{Text(k[4])}"))));
            var indexes = new List<DatabaseIndex>();
            foreach (var idx in await ReadAsync(db, $"PRAGMA main.index_list({quoted})", token).ConfigureAwait(false))
            {
                var indexName = Text(idx[1]);
                var definition = await ReadAsync(db, "SELECT sql FROM sqlite_schema WHERE name = @name", token, ("@name", indexName)).ConfigureAwait(false);
                var indexColumns = await ReadAsync(db, $"PRAGMA main.index_info({DatabaseConnections.Quote(DatabaseProvider.SQLite, indexName)})", token).ConfigureAwait(false);
                indexes.Add(new DatabaseIndex(indexName, Number(idx[2]) != 0,
                    definition.FirstOrDefault()?[0] is string ddl ? ddl : string.Join(", ", indexColumns.Select(c => Text(c[2])))));
            }
            tables.Add(new DatabaseTable("main", name, Text(r[1]) == "view", columns, keys, indexes));
        }
        return new DatabaseSchema(db.Database, ["main"], tables);
    }

    private static async Task<DatabaseSchema> ReadServerAsync(DbConnection db, DatabaseProvider provider, CancellationToken token)
    {
        var pg = provider == DatabaseProvider.PostgreSql;
        var schemas = (await ReadAsync(db, pg
            ? "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('pg_catalog','information_schema') AND schema_name NOT LIKE 'pg_toast%' ORDER BY schema_name"
            : "SELECT name FROM sys.schemas WHERE schema_id < 16384 AND name NOT IN ('sys','INFORMATION_SCHEMA','guest') ORDER BY name", token).ConfigureAwait(false)).Select(r => Text(r[0])).ToArray();
        var objects = await ReadAsync(db, "SELECT table_schema, table_name, table_type FROM information_schema.tables WHERE table_schema NOT IN ('information_schema','pg_catalog','sys') ORDER BY table_schema, table_name", token).ConfigureAwait(false);
        var tables = new List<DatabaseTable>();
        foreach (var r in objects)
        {
            var schema = Text(r[0]); var name = Text(r[1]);
            (string, object)[] args = [("@schema", schema), ("@name", name)];
            var pk = await ReadAsync(db, """
                SELECT k.column_name, k.ordinal_position FROM information_schema.table_constraints t
                JOIN information_schema.key_column_usage k ON t.constraint_catalog=k.constraint_catalog
                  AND t.constraint_schema=k.constraint_schema AND t.constraint_name=k.constraint_name
                  AND t.table_schema=k.table_schema AND t.table_name=k.table_name
                WHERE t.constraint_type='PRIMARY KEY' AND t.table_schema=@schema AND t.table_name=@name
                """, token, args).ConfigureAwait(false);
            var columns = (await ReadAsync(db, pg ? """
                SELECT c.column_name, c.data_type, c.is_nullable, c.is_generated, c.is_identity, format_type(a.atttypid, a.atttypmod)
                FROM information_schema.columns c
                JOIN pg_catalog.pg_namespace n ON n.nspname=c.table_schema
                JOIN pg_catalog.pg_class t ON t.relnamespace=n.oid AND t.relname=c.table_name
                JOIN pg_catalog.pg_attribute a ON a.attrelid=t.oid AND a.attname=c.column_name AND NOT a.attisdropped
                WHERE c.table_schema=@schema AND c.table_name=@name ORDER BY c.ordinal_position
                """ : """
                SELECT c.name, TYPE_NAME(c.user_type_id), c.is_nullable, c.is_computed, c.is_identity
                FROM sys.columns c JOIN sys.objects o ON o.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=o.schema_id
                WHERE s.name=@schema AND o.name=@name ORDER BY c.column_id
                """, token, args).ConfigureAwait(false)).Select(c => new DatabaseColumn(Text(c[0]), Text(c[1]),
                    pg ? Text(c[2]) == "YES" : Convert.ToBoolean(c[2], CultureInfo.InvariantCulture),
                    pk.FirstOrDefault(k => Text(k[0]) == Text(c[0])) is { } key ? Number(key[1]) : 0,
                    pg ? Text(c[3]) == "ALWAYS" || Text(c[4]) == "YES" : Convert.ToBoolean(c[3], CultureInfo.InvariantCulture) || Convert.ToBoolean(c[4], CultureInfo.InvariantCulture) || Text(c[1]) is "timestamp" or "rowversion",
                    pg ? Text(c[5]) : null)).ToArray();
            var keys = (await ReadAsync(db, pg ? """
                SELECT c.conname, pg_get_constraintdef(c.oid) FROM pg_constraint c
                JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
                WHERE n.nspname=@schema AND t.relname=@name AND c.contype IN ('p','f','u') ORDER BY c.conname
                """ : """
                SELECT k.name, k.type_desc FROM sys.key_constraints k
                JOIN sys.tables t ON t.object_id=k.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
                WHERE s.name=@schema AND t.name=@name
                UNION ALL
                SELECT f.name, CONCAT(COL_NAME(f.parent_object_id,fc.parent_column_id),' → ',
                  OBJECT_SCHEMA_NAME(f.referenced_object_id),'.',OBJECT_NAME(f.referenced_object_id),'.',
                  COL_NAME(f.referenced_object_id,fc.referenced_column_id))
                FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
                JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
                WHERE s.name=@schema AND t.name=@name
                """, token, args).ConfigureAwait(false)).Select(k => new DatabaseKey(Text(k[0]), Text(k[1]))).ToArray();
            var indexes = (await ReadAsync(db, pg ? """
                SELECT i.relname, x.indisunique, pg_get_indexdef(x.indexrelid) FROM pg_index x
                JOIN pg_class t ON t.oid=x.indrelid JOIN pg_namespace n ON n.oid=t.relnamespace JOIN pg_class i ON i.oid=x.indexrelid
                WHERE n.nspname=@schema AND t.relname=@name ORDER BY i.relname
                """ : """
                SELECT i.name, i.is_unique, STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), ', ')
                  WITHIN GROUP (ORDER BY ic.key_ordinal, ic.index_column_id)
                FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
                JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE s.name=@schema AND t.name=@name AND i.name IS NOT NULL GROUP BY i.name, i.is_unique
                """, token, args).ConfigureAwait(false)).Select(i => new DatabaseIndex(Text(i[0]), Convert.ToBoolean(i[1], CultureInfo.InvariantCulture), Text(i[2]))).ToArray();
            tables.Add(new DatabaseTable(schema, name, Text(r[2]) == "VIEW", columns, keys, indexes));
        }
        return new DatabaseSchema(db.Database, schemas, tables);
    }

    internal static async Task<List<object?[]>> ReadAsync(DbConnection db, string sql, CancellationToken token, params (string Name, object Value)[] args)
    {
        await using var cmd = db.CreateCommand(); cmd.CommandText = sql;
        foreach (var (name, value) in args) { var p = cmd.CreateParameter(); p.ParameterName = name; p.Value = value; cmd.Parameters.Add(p); }
        using var registration = SqlExecutor.RegisterCancel(cmd, token);
        await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < values.Length; i++) values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(values);
        }
        return rows;
    }

    private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    private static int Number(object? value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
}
