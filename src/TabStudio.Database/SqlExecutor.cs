using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TabStudio.Database;

public sealed class SqlExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public const int DefaultRowLimit = 5000;
    public const int MaximumResultSets = 32;

    // SQLite's async API executes synchronously. Always keep both reading and Cancel off the UI thread.
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Preserve the public instance service API used by Database Explorer and its consumers.")]
    [SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Preserve the existing public parameter order, including callers passing a token before the optional row limit.")]
    public Task<QueryResult> ExecuteAsync(DatabaseConnection connection, string sql, CancellationToken cancellationToken = default, int rowLimit = DefaultRowLimit) =>
        Task.Run(async () =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sql);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowLimit);
            var watch = Stopwatch.StartNew();
            await using var db = DatabaseConnections.Create(connection);
            await db.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;
            using var registration = RegisterCancel(cmd, cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var sets = new List<QueryResultSet>();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.FieldCount == 0) continue;
                if (sets.Count == MaximumResultSets)
                    throw new InvalidOperationException($"A query can display up to {MaximumResultSets} result sets. Run a smaller selection.");
                var columns = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => new QueryColumn(reader.GetName(i), reader.GetFieldType(i))).ToArray();
                var rows = new List<object?[]>();
                var truncated = false;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (rows.Count == rowLimit) { truncated = true; continue; }
                    var row = new object?[reader.FieldCount];
                    for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
                sets.Add(new QueryResultSet(columns, rows, truncated));
            }
            while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
            var result = new QueryResult(sets, reader.RecordsAffected, watch.Elapsed);

            // A statement that made or changed a type: Npgsql learns types once per data source, and a later query
            // could not read a column of the new type.
            await reader.DisposeAsync().ConfigureAwait(false);
            if (db is Npgsql.NpgsqlConnection npgsql && ChangesTypes(sql))
            {
                await npgsql.ReloadTypesAsync().ConfigureAwait(false);
            }

            return result;
        }, cancellationToken);

    private static bool ChangesTypes(string sql) =>
        sql.Contains("TYPE", StringComparison.OrdinalIgnoreCase) || sql.Contains("EXTENSION", StringComparison.OrdinalIgnoreCase);

    internal static CancellationTokenRegistration RegisterCancel(DbCommand cmd, CancellationToken token) => token.Register(() =>
    {
        try
        {
            if (cmd is Microsoft.Data.Sqlite.SqliteCommand { Connection.Handle: { } handle })
                SQLitePCL.raw.sqlite3_interrupt(handle);
            else cmd.Cancel();
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or ObjectDisposedException) { }
    });

    public static string Csv(QueryResultSet result)
    {
        static string Escape(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var text = new StringBuilder();
        text.AppendLine(string.Join(",", result.Columns.Select(c => Escape(c.Name))));
        foreach (var row in result.Rows) text.AppendLine(string.Join(",", row.Select(v => Escape(Format(v)))));
        return text.ToString();
    }

    // An array of columns/rows preserves duplicate column names and SQL NULL without loss.
    public static string Json(QueryResultSet result) => JsonSerializer.Serialize(new
    {
        columns = result.Columns.Select(c => c.Name), rows = result.Rows.Select(r => r.Select(JsonValue)), truncated = result.Truncated,
    }, JsonOptions);

    // JSON has no NaN or Infinity, and provider types such as IPAddress (PostgreSQL inet) or NpgsqlRange throw or
    // serialize their internals; such values are written as the text the grid shows.
    private static object? JsonValue(object? value) => value switch
    {
        null or DBNull => null,
        double d when !double.IsFinite(d) => Format(d),
        float f when !float.IsFinite(f) => Format(f),
        string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid or byte[] => value,
        _ => Format(value),
    };

    public static string Format(object? value) => value switch
    {
        null or DBNull => string.Empty,
        byte[] bytes => Convert.ToHexString(bytes),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
