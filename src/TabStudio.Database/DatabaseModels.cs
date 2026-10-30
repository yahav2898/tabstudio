namespace TabStudio.Database;

public enum DatabaseProvider { SQLite, PostgreSql, SqlServer }

// Connection strings are deliberately excluded from ToString (ComboBox, logs and diagnostics).
public sealed record DatabaseConnection(Guid Id, string Name, DatabaseProvider Provider, string ConnectionString)
{
    public override string ToString() => Name;
}

// SqlType is the column's type as SQL spells it for a cast, PostgreSQL's format_type (numeric(10,2), mood, point);
// edits are cast to it, since a text parameter is not a json, enum or date value to PostgreSQL.
public sealed record DatabaseColumn(string Name, string Type, bool Nullable, int KeyOrder = 0, bool Generated = false, string? SqlType = null);
public sealed record DatabaseKey(string Name, string Definition);
public sealed record DatabaseIndex(string Name, bool Unique, string Definition);
public sealed record DatabaseTable(string Schema, string Name, bool IsView,
    IReadOnlyList<DatabaseColumn> Columns, IReadOnlyList<DatabaseKey> Keys, IReadOnlyList<DatabaseIndex> Indexes)
{
    public override string ToString() => $"{Schema}.{Name}";
}

public sealed record DatabaseSchema(string Database, IReadOnlyList<string> Schemas, IReadOnlyList<DatabaseTable> Tables);
public sealed record QueryColumn(string Name, Type Type);
public sealed record QueryResultSet(IReadOnlyList<QueryColumn> Columns, IReadOnlyList<object?[]> Rows, bool Truncated);
public sealed record QueryResult(IReadOnlyList<QueryResultSet> Results, int RecordsAffected, TimeSpan Duration);
public sealed record TablePage(DatabaseTable Table, int Page, int PageSize, bool HasNext, QueryResultSet Data);
