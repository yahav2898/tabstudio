using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Database;
using TabStudio.App.Views;
using TabStudio.App.Views.Database;
using TabStudio.Database;

namespace TabStudio.App.Tests.Database;

public sealed class DatabaseExplorerTests
{
    [AvaloniaFact] public async Task Explorer_query_selection_results_and_table_editing_in_the_real_shell()
    {
        using var fixture = new WorkspaceFixture(); await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var shell = new WorkspaceView { DataContext = workspace };
        var window = new Window { Width = 1280, Height = 900, Content = shell }; window.Show();
        try
        {
            await workspace.ShowDatabaseCommand.ExecuteAsync(null);
            var explorer = workspace.DatabaseExplorer;
            var connection = new DatabaseConnection(Guid.NewGuid(), "Shop · SQLite", DatabaseProvider.SQLite, $"Data Source={Path.Combine(fixture.Paths.Root, "shop.db")};Pooling=False");
            await new SqlExecutor().ExecuteAsync(connection, """
                CREATE TABLE customers(id INTEGER PRIMARY KEY, name TEXT NOT NULL, city TEXT, orders INTEGER);
                CREATE INDEX customers_city ON customers(city);
                CREATE VIEW active_customers AS SELECT * FROM customers WHERE orders > 0;
                INSERT INTO customers VALUES(1,'Dana Cohen','Tel Aviv',12),(2,'Noam Levi','Haifa',8),(3,'Maya Bar','Jerusalem',23),(4,'Amit Shalev','Netanya',5);
                """);
            explorer.ConnectionName = connection.Name; explorer.ConnectionString = connection.ConnectionString; explorer.Provider = connection.Provider;
            await explorer.SaveConnectionCommand.ExecuteAsync(null); await explorer.ConnectCommand.ExecuteAsync(null);
            Assert.True(workspace.IsDatabaseSelected); Assert.Single(explorer.Nodes);
            var database = Assert.Single(explorer.Nodes[0].Children);
            var table = database.Children.SelectMany(s => s.Children).SelectMany(g => g.Children).Single(t => t.Table?.Name == "customers");
            table.IsExpanded = true; table.Children[0].IsExpanded = true;
            explorer.SelectedNode = table; explorer.NewQueryCommand.Execute(null);
            var document = workspace.ActiveDocument!; var query = Assert.IsType<DatabaseQueryViewModel>(document.DatabaseQuery);
            document.Document.Text = "SELECT id, name, city, orders FROM customers ORDER BY orders DESC;\nSELECT count(*) AS total_customers FROM customers;";
            await UiTestHelpers.WaitUntilAsync(() => window.GetVisualDescendants().OfType<DatabaseQueryView>().Any());
            var view = window.GetVisualDescendants().OfType<DatabaseQueryView>().Single();
            var editor = view.GetVisualDescendants().OfType<TextEditor>().Single();
            // Ctrl+Enter runs only the selected statement; a second run exercises multiple result sets.
            editor.Select(0, document.Document.Text.IndexOf(';', StringComparison.Ordinal) + 1); editor.TextArea.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            await UiTestHelpers.WaitUntilAsync(() => query.Results.Count == 1 && !query.IsBusy, because: "selection execution");
            Assert.Equal(4, query.Rows.Count);
            await query.ExecuteAsync(document.Document.Text); Assert.Equal(2, query.Results.Count);
            Assert.Equal("Maya Bar", query.Rows[0].Values[1]);
            var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var header = grid.GetVisualDescendants().OfType<Avalonia.Controls.DataGridColumnHeader>().Single(h => Equals(h.Content, "orders"));
            var point = header.TranslatePoint(new Avalonia.Point(header.Bounds.Width / 2, header.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            await UiTestHelpers.WaitUntilAsync(() => Equals(query.Rows[0].Values[3], 5L), because: "numeric ascending sort");
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            await UiTestHelpers.WaitUntilAsync(() => Equals(query.Rows[0].Values[3], 23L), because: "numeric descending sort");
            Assert.Contains("customers", SqlCompletions.Get(query.Schema, "SELECT * FROM cust", 18).Select(c => c.Text));
            Assert.Equal(5L, Assert.Single(query.Export()!.Rows, r => Equals(r[0], 4L))[3]);
            UiTestHelpers.SaveScreenshot(window, "database-01-explorer-sql-results");
            query.SelectedResult = query.Results[1]; Assert.Single(query.Rows);
            Assert.Equal(4L, query.Rows[0].Values[0]);

            explorer.EditDataCommand.Execute(null);
            var data = workspace.ActiveDocument!.DatabaseQuery!;
            await UiTestHelpers.WaitUntilAsync(() => !data.IsBusy && data.Rows.Count == 4);
            Assert.True(data.CanEdit); data.SelectedRow = data.Rows[0]; data.EditColumn = data.EditableColumns.Single(c => c.Name == "city"); data.EditValue = "Ashdod";
            await data.ApplyEditCommand.ExecuteAsync(null);
            Assert.Equal("Ashdod", data.Rows[0].Values[2]);
            UiTestHelpers.SaveScreenshot(window, "database-02-table-editor");
            Assert.Contains(workspace.Activity.Items, i => i.Title == "Database Explorer");
            workspace.DatabaseExplorer.AddConnectionCommand.Execute(null);
            window.Width = 800; window.Height = 1100;
            UiTestHelpers.SaveScreenshot(window, "database-03-connection-tablet");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact] public async Task Closing_a_query_cancels_work_and_an_error_does_not_crash_the_shell()
    {
        using var fixture = new WorkspaceFixture(); await fixture.OpenSampleAsync();
        var connection = new DatabaseConnection(Guid.NewGuid(), "test", DatabaseProvider.SQLite, $"Data Source={Path.Combine(fixture.Paths.Root, "cancel.db")};Pooling=False");
        using var query = new DatabaseQueryViewModel(connection, null);
        await query.ExecuteAsync("SELECT * FROM missing_table");
        Assert.Contains("SQLite", query.Status, StringComparison.Ordinal); Assert.False(query.IsBusy);
        var running = query.ExecuteAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000000000) SELECT sum(x) FROM n");
        await Task.Delay(150); query.CancelCommand.Execute(null); await running.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Contains("Cancelled", query.Status, StringComparison.Ordinal);
        await query.ExecuteAsync("SELECT 1"); Assert.Single(query.Rows);
    }
}
