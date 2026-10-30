using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace TabStudio.Database;

public sealed class TableEditor
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Preserve the public instance service API used by Database Explorer and its consumers.")]
    public async Task<TablePage> ReadAsync(DatabaseConnection connection, DatabaseTable table, int page, int pageSize = 100, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var offset = checked(page * pageSize);
        var p = connection.Provider;
        var keys = table.Columns.Where(c => c.KeyOrder > 0).OrderBy(c => c.KeyOrder).ToArray();
        var order = keys.Length > 0 ? string.Join(",", keys.Select(c => DatabaseConnections.Quote(p, c.Name)))
            : p == DatabaseProvider.SqlServer ? "(SELECT NULL)" : "1";
        var sql = $"SELECT * FROM {DatabaseConnections.Qualified(p, table)} ORDER BY {order} " + (p == DatabaseProvider.SqlServer
            ? $"OFFSET {offset} ROWS FETCH NEXT {pageSize + 1} ROWS ONLY" : $"LIMIT {pageSize + 1} OFFSET {offset}");
        var result = (await new SqlExecutor().ExecuteAsync(connection, sql, token, pageSize + 1).ConfigureAwait(false)).Results.Single();
        return new TablePage(table, page, pageSize, result.Rows.Count > pageSize, result with { Rows = result.Rows.Take(pageSize).ToArray() });
    }

    // One changed cell, with the complete original row as an optimistic concurrency guard.
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Preserve the public instance service API used by Database Explorer and its consumers.")]
    public Task UpdateAsync(DatabaseConnection connection, DatabaseTable table, IReadOnlyList<object?> original, int column, object? value, CancellationToken token = default) => Task.Run(async () =>
    {
        if (table.IsView || !table.Columns.Any(c => c.KeyOrder > 0)) throw new InvalidOperationException("Editing requires a table with a primary key.");
        if (column < 0 || column >= table.Columns.Count || original.Count != table.Columns.Count) throw new ArgumentOutOfRangeException(nameof(column));
        if (table.Columns[column].Generated || table.Columns[column].KeyOrder > 0) throw new InvalidOperationException("Generated and primary-key columns are read-only.");
        await using var db = DatabaseConnections.Create(connection);
        await db.OpenAsync(token).ConfigureAwait(false);
        await using var transaction = await db.BeginTransactionAsync(token).ConfigureAwait(false);
        await using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
        string Q(string name) => DatabaseConnections.Quote(connection.Provider, name);
        var pg = connection.Provider == DatabaseProvider.PostgreSql;
        var predicates = new List<string>();
        for (var i = 0; i < original.Count; i++)
        {
            var c = table.Columns[i];
            // SQL Server text/ntext/image/xml are not equality comparable; keys still identify the row.
            if (connection.Provider == DatabaseProvider.SqlServer && c.Type is "text" or "ntext" or "image" or "xml") continue;
            var name = $"@o{i}";
            predicates.Add(original[i] is null ? $"{Q(c.Name)} IS NULL"
                // json, xml and the geometric types have no = operator: their text forms are compared instead.
                : pg && c.SqlType is { } type && !HasEquality(type) ? $"CAST({Q(c.Name)} AS text) = CAST({Typed(name, type)} AS text)"
                : $"{Q(c.Name)} = {Typed(name, c.SqlType)}");
            if (original[i] is not null) Add(cmd, name, original[i]);
        }
        Add(cmd, "@value", value);
        cmd.CommandText = $"UPDATE {DatabaseConnections.Qualified(connection.Provider, table)} SET {Q(table.Columns[column].Name)} = {Typed("@value", table.Columns[column].SqlType)} WHERE " + string.Join(" AND ", predicates);

        // PostgreSQL does not turn a text parameter into a json, xml, enum, point or date value by itself; a cast to
        // the column's own type parses it as PostgreSQL parses a literal, and converts a typed value as assignment does.
        string Typed(string parameter, string? type) => pg && !string.IsNullOrEmpty(type) ? $"CAST({parameter} AS {type})" : parameter;
        using var registration = SqlExecutor.RegisterCancel(cmd, token);
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("The row changed or was deleted. Refresh the page before editing it again.");
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }, token);

    // PostgreSQL's built-in types without an equality operator, by format_type's spelling (arrays of them neither).
    private static bool HasEquality(string type) =>
        type.TrimEnd('[', ']') is not ("json" or "xml" or "point" or "line" or "lseg" or "box" or "path" or "polygon" or "circle");

    private static void Add(DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter(); p.ParameterName = name; p.Value = value ?? DBNull.Value; cmd.Parameters.Add(p);
    }
}
