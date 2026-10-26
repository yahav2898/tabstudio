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
}
