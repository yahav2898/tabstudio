using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.Database;

namespace TabStudio.App.ViewModels.Database;

public sealed partial class DatabaseNode : ObservableObject
{
    private readonly Func<Task>? _load;
    private bool _loaded;
    public DatabaseNode(string text, string kind, DatabaseConnection? connection = null, DatabaseSchema? schema = null, DatabaseTable? table = null, Func<Task>? load = null)
    {
        Text = text; Kind = kind; Connection = connection; Schema = schema; Table = table; _load = load;
        if (load is not null) Children.Add(new DatabaseNode("Expand to load", ""));
    }
    public string Text { get; }
    public string Kind { get; }
    public DatabaseConnection? Connection { get; }
    public DatabaseSchema? Schema { get; set; }
    public DatabaseTable? Table { get; }
    public ObservableCollection<DatabaseNode> Children { get; } = [];
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_loaded && _load is not null) { _loaded = true; _ = _load(); }
    }

    public void Fill(DatabaseConnection connection, DatabaseSchema schema)
    {
        _loaded = true; Schema = schema; Children.Clear();
        foreach (var name in schema.Schemas)
        {
            var node = new DatabaseNode(name, "schema", connection, schema) { IsExpanded = true };
            foreach (var view in new[] { false, true })
            {
                var group = new DatabaseNode(view ? "Views" : "Tables", "", connection, schema) { IsExpanded = true };
                foreach (var t in schema.Tables.Where(t => t.Schema == name && t.IsView == view))
                {
                    var table = new DatabaseNode(t.Name, view ? "view" : "table", connection, schema, t);
                    var cols = new DatabaseNode("Columns", "", connection, schema, t);
                    foreach (var c in t.Columns) cols.Children.Add(new DatabaseNode($"{c.Name} : {c.Type}{(c.Nullable ? " NULL" : " NOT NULL")}", c.KeyOrder > 0 ? "PK" : "", connection, schema, t));
                    table.Children.Add(cols);
                    var keys = new DatabaseNode("Keys", "", connection, schema, t);
                    foreach (var k in t.Keys) keys.Children.Add(new DatabaseNode($"{k.Name} — {k.Definition}", "key", connection, schema, t));
                    table.Children.Add(keys);
                    var indexes = new DatabaseNode("Indexes", "", connection, schema, t);
                    foreach (var i in t.Indexes) indexes.Children.Add(new DatabaseNode(i.Name + (i.Unique ? " (unique)" : ""), i.Definition, connection, schema, t));
                    table.Children.Add(indexes); group.Children.Add(table);
                }
                node.Children.Add(group);
            }
            Children.Add(node);
        }
    }
}
