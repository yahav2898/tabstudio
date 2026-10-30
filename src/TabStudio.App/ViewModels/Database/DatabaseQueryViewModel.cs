using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Database;

namespace TabStudio.App.ViewModels.Database;

public sealed class DatabaseRow(object?[] values)
{
    public object?[] Values { get; } = values;
    public string this[int column] => Values[column] is null ? "NULL" : SqlExecutor.Format(Values[column]);
}
public sealed record ResultOption(int Index, QueryResultSet Data)
{
    public override string ToString() => $"Result {Index + 1} · {Data.Rows.Count} rows{(Data.Truncated ? " (limited)" : "")}";
}

public sealed partial class DatabaseQueryViewModel : ObservableObject, IDisposable
{
    private CancellationTokenSource? _operation;
    private readonly CancellationTokenSource _lifetime = new();
    public DatabaseQueryViewModel(DatabaseConnection connection, DatabaseSchema? schema, DatabaseTable? table = null)
    {
        Connection = connection; Schema = schema; Table = table;
        Results.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(ShowsNoResults)); };
    }
    public DatabaseConnection Connection { get; }
    public DatabaseSchema? Schema { get; private set; }
    public DatabaseTable? Table { get; }
    public bool IsTable => Table is not null;
    public bool CanEdit => Table is { IsView: false } table && table.Columns.Any(c => c.KeyOrder > 0);
    public string Heading => string.Join(" / ", new[] { Connection.Name, Schema?.Database, Table?.ToString() }.Where(s => !string.IsNullOrEmpty(s)));
    public string EditHint => CanEdit ? "Select a row and column to edit. Primary keys and generated values are read-only." : "Views and tables without a primary key are read-only.";
    public IReadOnlyList<DatabaseColumn> EditableColumns => Table?.Columns.Where(c => !c.Generated && c.KeyOrder == 0).ToArray() ?? [];
    public ObservableCollection<ResultOption> Results { get; } = [];
    public ObservableCollection<DatabaseRow> Rows { get; } = [];
    [ObservableProperty] public partial ResultOption? SelectedResult { get; set; }
    [ObservableProperty] public partial DatabaseRow? SelectedRow { get; set; }
    [ObservableProperty] public partial DatabaseColumn? EditColumn { get; set; }
    [ObservableProperty] public partial string EditValue { get; set; } = string.Empty;
    [ObservableProperty] public partial bool SetNull { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial string Status { get; private set; } = "Ctrl+Enter runs the selection, or the whole query. Ctrl+Space completes from the schema.";
    [ObservableProperty] public partial int Page { get; private set; }
    [ObservableProperty] public partial bool HasNext { get; private set; }
    public bool HasPrevious => Page > 0;

    /// <summary>Whether a run has returned results to show.</summary>
    public bool HasResults => Results.Count > 0;

    /// <summary>A query that has not run yet: the empty state in place of the grid.</summary>
    public bool ShowsNoResults => !IsTable && Results.Count == 0;

    /// <summary>The page of a table's data, such as "Page 2".</summary>
    public string PageText => $"Page {Page + 1}";

    partial void OnPageChanged(int value) => OnPropertyChanged(nameof(PageText));
    public event EventHandler? ResultChanged;
    public event EventHandler? RunRequested;
    [RelayCommand] private void Run() => RunRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    [RelayCommand] private Task RefreshSchemaAsync() => GuardAsync(async token =>
    { Schema = await new SchemaReader().ReadAsync(Connection, token); Status = "Schema refreshed."; });

    public Task ExecuteAsync(string sql) => GuardAsync(async token =>
    {
        var result = await new SqlExecutor().ExecuteAsync(Connection, sql, token);
        SetResults(result.Results);
        var count = result.Results.Sum(s => s.Rows.Count);
        Status = $"{count:N0} {(count == 1 ? "row" : "rows")} · {result.Results.Count} {(result.Results.Count == 1 ? "result set" : "result sets")} · {result.Duration.TotalMilliseconds:N0} ms" +
            (result.RecordsAffected >= 0 ? $" · {result.RecordsAffected:N0} affected" : "") +
            (result.Results.Any(s => s.Truncated) ? $" · display limited to {SqlExecutor.DefaultRowLimit:N0} rows per result" : "");
    });

    public Task LoadPageAsync(int page = 0) => GuardAsync(async token =>
    {
        if (Table is not { } table) return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = await new TableEditor().ReadAsync(Connection, table, page, token: token);
        Page = page; OnPropertyChanged(nameof(HasPrevious)); HasNext = result.HasNext;
        SetResults([result.Data]); Status = $"Page {Page + 1} · {result.Data.Rows.Count} rows · {clock.Elapsed.TotalMilliseconds:N0} ms";
    });
    [RelayCommand] private Task NextPageAsync() => HasNext ? LoadPageAsync(Page + 1) : Task.CompletedTask;
    [RelayCommand] private Task PreviousPageAsync() => HasPrevious ? LoadPageAsync(Page - 1) : Task.CompletedTask;
    [RelayCommand] private Task RefreshPageAsync() => LoadPageAsync(Page);
    [RelayCommand] private async Task ApplyEditAsync()
    {
        if (IsBusy || Table is not { } table || SelectedRow is not { } row || EditColumn is not { } column) return;
        var updated = false;
        await GuardAsync(async token =>
        {
            var index = table.Columns.ToList().IndexOf(column);
            var original = row.Values[index];
            object? value = SetNull ? null : original is null ? ParseForColumn(EditValue, column.Type) : original switch
            {
                byte[] => Convert.FromHexString(EditValue),
                Guid => Guid.Parse(EditValue),
                DateTimeOffset => DateTimeOffset.Parse(EditValue, CultureInfo.InvariantCulture),
                TimeSpan => TimeSpan.Parse(EditValue, CultureInfo.InvariantCulture),
                DateOnly => DateOnly.Parse(EditValue, CultureInfo.InvariantCulture),
                TimeOnly => TimeOnly.Parse(EditValue, CultureInfo.InvariantCulture),
                _ => Convert.ChangeType(EditValue, original.GetType(), CultureInfo.InvariantCulture),
            };
            await new TableEditor().UpdateAsync(Connection, table, row.Values, index, value, token); updated = true;
        });
        if (updated) await LoadPageAsync(Page);
    }
    private static object ParseForColumn(string text, string type) => type.ToLowerInvariant() switch
    {
        "integer" or "int" or "bigint" or "smallint" => long.Parse(text, CultureInfo.InvariantCulture),
        "numeric" or "decimal" or "real" or "double precision" or "float" => decimal.Parse(text, CultureInfo.InvariantCulture),
        "boolean" or "bit" => bool.Parse(text),
        "uuid" or "uniqueidentifier" => Guid.Parse(text),
        _ => text,
    };
    partial void OnSelectedResultChanged(ResultOption? value)
    {
        Rows.Clear(); SelectedRow = null;
        if (value is not null) foreach (var r in value.Data.Rows) Rows.Add(new DatabaseRow(r));
        ResultChanged?.Invoke(this, EventArgs.Empty);
    }
    partial void OnSelectedRowChanged(DatabaseRow? value) => UpdateEditValue();
    partial void OnEditColumnChanged(DatabaseColumn? value) => UpdateEditValue();
    private void UpdateEditValue()
    {
        if (SelectedRow is { } row && EditColumn is { } col && Table is { } table)
        { var value = row.Values[table.Columns.ToList().IndexOf(col)]; SetNull = value is null; EditValue = SqlExecutor.Format(value); }
    }
    private void SetResults(IReadOnlyList<QueryResultSet> results)
    {
        Results.Clear(); SelectedResult = null; Rows.Clear();
        for (var i = 0; i < results.Count; i++) Results.Add(new ResultOption(i, results[i]));
        SelectedResult = Results.FirstOrDefault();
    }
    public QueryResultSet? Export() => SelectedResult is { } result ? result.Data with { Rows = Rows.Select(r => r.Values).ToArray() } : null;
    private async Task GuardAsync(Func<CancellationToken, Task> work)
    {
        if (IsBusy || _lifetime.IsCancellationRequested) return;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _operation = source; IsBusy = true;
        try { await work(source.Token); }
        catch (Exception) when (source.IsCancellationRequested) { Status = "Cancelled. A command already committed by the server is not rolled back."; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Status = DatabaseError.Message(ex); }
        finally { _operation = null; IsBusy = false; }
    }
    public void Dispose() { _lifetime.Cancel(); _operation?.Cancel(); _lifetime.Dispose(); }
}

internal static class DatabaseError
{
    // Retain useful server diagnostics without echoing query text, literals or connection strings. The server's own
    // reason comes first: "SQL Server error 208, line 1." left out "Invalid object name 'Users'", and every
    // connection failure in Database Explorer read "connection or schema operation failed".
    public static string Message(Exception ex) => ex switch
    {
        Npgsql.PostgresException pg => $"PostgreSQL {pg.SqlState}: {pg.MessageText}",
        Npgsql.NpgsqlException { InnerException: System.Net.Sockets.SocketException socket } => $"Cannot reach the PostgreSQL server: {socket.Message.TrimEnd('.')}. Check the host and port, and that the server is running.",
        Npgsql.NpgsqlException { InnerException: TimeoutException } => "The PostgreSQL server did not answer in time. Check the host and port, and that the server is running.",
        Microsoft.Data.SqlClient.SqlException sql => $"SQL Server error {sql.Number}, line {sql.LineNumber}: {(sql.Errors.Count > 0 ? sql.Errors[0].Message : sql.Message)}",
        Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 14 } => "SQLite cannot open the database file. Check that it exists and that its folder can be written to.",
        Microsoft.Data.Sqlite.SqliteException sqlite => $"SQLite {sqlite.SqliteErrorCode}: {sqlite.Message}",
        System.Net.Sockets.SocketException socket => $"Cannot reach the server: {socket.Message.TrimEnd('.')}. Check the host and port, and that the server is running.",
        TimeoutException => "The server did not answer in time. Check the host and port, and that the server is running.",
        InvalidOperationException invalid => invalid.Message,
        FormatException or OverflowException => "The value does not match the column's type.",
        _ => $"{ex.GetType().Name}: database operation failed. Check the connection and permissions.",
    };
}
