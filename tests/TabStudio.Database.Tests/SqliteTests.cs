using TabStudio.Testing;

namespace TabStudio.Database.Tests;

public sealed class SqliteTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private DatabaseConnection Connection => new(Guid.NewGuid(), "Local", DatabaseProvider.SQLite, $"Data Source={_temp.Resolve("data.db")};Pooling=False");
    private readonly SqlExecutor _sql = new();

    private async Task<DatabaseSchema> CreateAsync()
    {
        await _sql.ExecuteAsync(Connection, """
            CREATE TABLE parent(a INTEGER, b INTEGER, PRIMARY KEY(a,b));
            CREATE TABLE "order details"(id INTEGER PRIMARY KEY, a INTEGER, b INTEGER, "display name" TEXT,
                computed TEXT GENERATED ALWAYS AS ("display name" || '!') VIRTUAL,
                FOREIGN KEY(a,b) REFERENCES parent(a,b));
            CREATE UNIQUE INDEX "index with spaces" ON "order details"("display name");
            CREATE VIEW names AS SELECT "display name" FROM "order details";
            INSERT INTO "order details"(id,"display name") VALUES (1,'First'),(2,'Second'),(3,NULL);
            """);
        return await new SchemaReader().ReadAsync(Connection);
    }

    [Fact] public async Task Schema_reads_views_composite_keys_generated_columns_and_indexes()
    {
        var schema = await CreateAsync();
        Assert.Contains("main", await new SchemaReader().DatabasesAsync(Connection));
        Assert.Contains("main", schema.Schemas);
        var parent = Assert.Single(schema.Tables, t => t.Name == "parent");
        Assert.Equal(new[] { 1, 2 }, parent.Columns.Select(c => c.KeyOrder));
        var table = Assert.Single(schema.Tables, t => t.Name == "order details");
        Assert.True(table.Columns.Single(c => c.Name == "computed").Generated);
        Assert.Equal(2, table.Keys.Count);
        Assert.Contains("parent.a", table.Keys.Single(k => k.Name.StartsWith("FK_", StringComparison.Ordinal)).Definition);
        Assert.True(Assert.Single(table.Indexes).Unique);
        Assert.True(Assert.Single(schema.Tables, t => t.Name == "names").IsView);
    }
    [Fact] public async Task Query_preserves_multiple_sets_null_duplicate_names_and_display_limits()
    {
        await CreateAsync();
        var result = await _sql.ExecuteAsync(Connection, "SELECT id,id FROM \"order details\" ORDER BY id; SELECT NULL AS missing;", rowLimit: 2);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal(2, result.Results[0].Rows.Count);
        Assert.True(result.Results[0].Truncated);
        Assert.Null(Assert.Single(result.Results[1].Rows)[0]);
        Assert.Contains("\"rows\"", SqlExecutor.Json(result.Results[0]));
        Assert.StartsWith("\"id\",\"id\"", SqlExecutor.Csv(result.Results[0]), StringComparison.Ordinal);
    }
    [Fact] public async Task Table_paging_updates_parameters_and_rejects_stale_rows()
    {
        var table = (await CreateAsync()).Tables.Single(t => t.Name == "order details");
        var editor = new TableEditor();
        var first = await editor.ReadAsync(Connection, table, 0, 2);
        Assert.True(first.HasNext);
        Assert.Equal(1L, first.Data.Rows[0][0]);
        var second = await editor.ReadAsync(Connection, table, 1, 2);
        Assert.Single(second.Data.Rows); Assert.False(second.HasNext);
        var original = first.Data.Rows[0];
        await editor.UpdateAsync(Connection, table, original, 3, "x'; DROP TABLE parent; --");
        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() => editor.UpdateAsync(Connection, table, original, 3, "stale"));
        Assert.Contains("changed", conflict.Message, StringComparison.Ordinal);
        var rows = await editor.ReadAsync(Connection, table, 0, 2);
        Assert.Equal("x'; DROP TABLE parent; --", rows.Data.Rows[0][3]);
        Assert.Contains((await new SchemaReader().ReadAsync(Connection)).Tables, t => t.Name == "parent");
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.UpdateAsync(Connection, table, rows.Data.Rows[0], 0, 8));
    }
    [Fact] public async Task Cancellation_interrupts_a_running_sqlite_statement()
    {
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var query = _sql.ExecuteAsync(Connection, "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000000000) SELECT sum(x) FROM n", source.Token);
        var error = await Record.ExceptionAsync(() => query.WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.NotNull(error); Assert.IsNotType<TimeoutException>(error); Assert.True(source.IsCancellationRequested);
    }
    [Fact] public async Task Csv_and_json_preserve_quotes_newlines_and_nulls()
    {
        var result = await _sql.ExecuteAsync(Connection, "SELECT 'a,\"b' AS value, char(10) AS newline, NULL AS missing;");
        Assert.Contains("\"a,\"\"b\"", SqlExecutor.Csv(result.Results.Single()), StringComparison.Ordinal);
        using var json = System.Text.Json.JsonDocument.Parse(SqlExecutor.Json(result.Results.Single()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("rows")[0][2].ValueKind);
    }
    [Fact] public void Json_writes_infinity_and_provider_types_as_text()
    {
        var set = new QueryResultSet([new("inf", typeof(double)), new("ip", typeof(System.Net.IPAddress)), new("n", typeof(int))],
            [new object?[] { double.PositiveInfinity, System.Net.IPAddress.Parse("10.0.0.1"), 7 }], false);
        using var json = System.Text.Json.JsonDocument.Parse(SqlExecutor.Json(set));
        var row = json.RootElement.GetProperty("rows")[0];
        Assert.Equal("Infinity", row[0].GetString());
        Assert.Equal("10.0.0.1", row[1].GetString());
        Assert.Equal(7, row[2].GetInt32());
    }
    public void Dispose() => _temp.Dispose();
}
