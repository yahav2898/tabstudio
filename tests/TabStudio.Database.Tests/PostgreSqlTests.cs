namespace TabStudio.Database.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TABSTUDIO_POSTGRES"))) Skip = "Set TABSTUDIO_POSTGRES to run against PostgreSQL (required in the CI libraries job)."; }
}

public sealed class PostgreSqlTests
{
    [PostgreSqlFact] public async Task Live_postgres_schema_queries_paging_editing_and_cancel()
    {
        var connection = new DatabaseConnection(Guid.NewGuid(), "CI PostgreSQL", DatabaseProvider.PostgreSql, Environment.GetEnvironmentVariable("TABSTUDIO_POSTGRES")!);
        var name = "db_" + Guid.NewGuid().ToString("N"); var sql = new SqlExecutor();
        try
        {
            await sql.ExecuteAsync(connection, $"""
                CREATE SCHEMA {name};
                CREATE TABLE {name}.parents(a integer, b integer, PRIMARY KEY(a,b));
                CREATE TABLE {name}.customers(id integer PRIMARY KEY, email text, a integer, b integer,
                    FOREIGN KEY(a,b) REFERENCES {name}.parents(a,b));
                CREATE UNIQUE INDEX email_idx ON {name}.customers(email);
                CREATE VIEW {name}.names AS SELECT email FROM {name}.customers;
                INSERT INTO {name}.customers(id,email) VALUES (1,'one'),(2,'two'),(3,NULL);
                """);
            Assert.Contains("tabstudio", await new SchemaReader().DatabasesAsync(connection));
            var schema = await new SchemaReader().ReadAsync(connection);
            var table = Assert.Single(schema.Tables, t => t.Schema == name && t.Name == "customers");
            Assert.Contains(table.Keys, k => k.Definition.Contains("FOREIGN KEY", StringComparison.Ordinal));
            Assert.Contains(table.Indexes, i => i.Name == "email_idx" && i.Unique);
            Assert.Contains(schema.Tables, t => t.Schema == name && t.Name == "names" && t.IsView);
            Assert.Equal(new[] { 1, 2 }, schema.Tables.Single(t => t.Schema == name && t.Name == "parents").Columns.Select(c => c.KeyOrder));
            var editor = new TableEditor(); var page = await editor.ReadAsync(connection, table, 0, 2);
            Assert.True(page.HasNext); await editor.UpdateAsync(connection, table, page.Data.Rows[0], 1, "updated");
            await Assert.ThrowsAsync<InvalidOperationException>(() => editor.UpdateAsync(connection, table, page.Data.Rows[0], 1, "stale"));
            Assert.Equal("updated", (await editor.ReadAsync(connection, table, 0)).Data.Rows[0][1]);
            var sets = await sql.ExecuteAsync(connection, "SELECT 1; SELECT 2;"); Assert.Equal(2, sets.Results.Count);
            using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            var exception = await Record.ExceptionAsync(() => sql.ExecuteAsync(connection, "SELECT pg_sleep(30)", source.Token).WaitAsync(TimeSpan.FromSeconds(8)));
            Assert.NotNull(exception); Assert.IsNotType<TimeoutException>(exception);
            Assert.Equal(7, (await sql.ExecuteAsync(connection, "SELECT 7")).Results[0].Rows[0][0]);
        }
        finally { await sql.ExecuteAsync(connection, $"DROP SCHEMA IF EXISTS {name} CASCADE;"); }
    }
    [PostgreSqlFact] public async Task Cells_of_json_xml_point_and_enum_columns_can_be_edited_and_empty_dates_filled()
    {
        var connection = new DatabaseConnection(Guid.NewGuid(), "CI PostgreSQL", DatabaseProvider.PostgreSql, Environment.GetEnvironmentVariable("TABSTUDIO_POSTGRES")!);
        var name = "db_" + Guid.NewGuid().ToString("N"); var sql = new SqlExecutor();
        try
        {
            await sql.ExecuteAsync(connection, $"""
                CREATE SCHEMA {name};
                CREATE TYPE {name}.mood AS ENUM ('sad', 'happy');
                CREATE TABLE {name}.things(id integer PRIMARY KEY, doc json, bin jsonb, x xml, p point, m {name}.mood,
                    d date, ts timestamp, n numeric(10,2), v varchar(20));
                INSERT INTO {name}.things VALUES (1, '{"a": 1}', '{"b": 2}', '<r>1</r>', '(1,2)', 'sad', NULL, NULL, 1.50, 'hi');
                """);
            var table = Assert.Single((await new SchemaReader().ReadAsync(connection)).Tables, t => t.Schema == name && t.Name == "things");
            Assert.Equal($"{name}.mood", table.Columns.Single(c => c.Name == "m").SqlType);
            Assert.Equal("numeric(10,2)", table.Columns.Single(c => c.Name == "n").SqlType);
            var editor = new TableEditor();

            // Each edit is guarded by the whole row as it was read, so every column's value is compared too.
            async Task<object?[]> EditAsync(string column, object? value)
            {
                var row = (await editor.ReadAsync(connection, table, 0)).Data.Rows[0];
                await editor.UpdateAsync(connection, table, row, table.Columns.ToList().FindIndex(c => c.Name == column), value);
                return (await editor.ReadAsync(connection, table, 0)).Data.Rows[0];
            }

            Assert.Equal(new DateOnly(2026, 10, 30), (await EditAsync("d", "2026-10-30"))[6] switch
            {
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                var other => Assert.IsType<DateOnly>(other),
            });
            Assert.Equal("""{"z": [1, 2]}""", (await EditAsync("doc", """{"z": [1, 2]}"""))[1]);
            Assert.Equal("""{"c": 3}""", (await EditAsync("bin", """{"c": 3}"""))[2]);
            Assert.Equal("<r>2</r>", (await EditAsync("x", "<r>2</r>"))[3]);
            Assert.Equal("(3,4)", SqlExecutor.Format((await EditAsync("p", "(3,4)"))[4]));
            Assert.Equal("happy", (await EditAsync("m", "happy"))[5]);
            Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), (await EditAsync("ts", "2026-01-02 03:04:05"))[7]);
            Assert.Equal("there", (await EditAsync("v", "there"))[9]);

            // A stale row is still refused when the column that changed has no = operator.
            var stale = (await editor.ReadAsync(connection, table, 0)).Data.Rows[0];
            await EditAsync("doc", """{"other": true}""");
            await Assert.ThrowsAsync<InvalidOperationException>(() => editor.UpdateAsync(connection, table, stale, 9, "stale"));
        }
        finally { await sql.ExecuteAsync(connection, $"DROP SCHEMA IF EXISTS {name} CASCADE;"); }
    }
}
