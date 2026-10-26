using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Theming;
using TabStudio.Database;

namespace TabStudio.App.ViewModels.Database;

public sealed partial class DatabaseNode : ObservableObject
{
    private readonly Func<Task>? _load;
    private readonly string? _icon;
    private bool _loaded;
    public DatabaseNode(string text, string kind, DatabaseConnection? connection = null, DatabaseSchema? schema = null, DatabaseTable? table = null, Func<Task>? load = null, string? icon = null)
    {
        Text = text; Kind = kind; Connection = connection; Schema = schema; Table = table; _load = load; _icon = icon;
        if (load is not null) Children.Add(new DatabaseNode("Loading…", ""));
    }
    public string Text { get; }
    public string Kind { get; }

    /// <summary>The row's icon from the icon set: a database, a folder of objects, a table, a view or a key.</summary>
    public Geometry? Icon => _icon is null ? null : ThemeBrushes.Find<Geometry>(_icon);

    /// <summary>Whether the row has an icon; rows without one keep its room, so names line up.</summary>
    public bool HasIcon => _icon is not null;

    /// <summary>What the row is, for its tooltip, when the kind says more than the text.</summary>
    public string? Detail => string.IsNullOrEmpty(Kind) ? null : Kind;
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
            var node = new DatabaseNode(name, "schema", connection, schema, icon: "Icon.Folder") { IsExpanded = true };
            foreach (var view in new[] { false, true })
            {
                var group = new DatabaseNode(view ? "Views" : "Tables", "", connection, schema, icon: "Icon.Folder") { IsExpanded = true };
                foreach (var t in schema.Tables.Where(t => t.Schema == name && t.IsView == view))
                {
                    var table = new DatabaseNode(t.Name, view ? "view" : "table", connection, schema, t, icon: view ? "Icon.Eye" : "Icon.List");
                    var cols = new DatabaseNode("Columns", "", connection, schema, t, icon: "Icon.Folder");
                    foreach (var c in t.Columns) cols.Children.Add(new DatabaseNode($"{c.Name} : {c.Type}{(c.Nullable ? " NULL" : " NOT NULL")}", c.KeyOrder > 0 ? "Primary key" : "", connection, schema, t, icon: c.KeyOrder > 0 ? "Icon.Lock" : null));
                    table.Children.Add(cols);
                    var keys = new DatabaseNode("Keys", "", connection, schema, t, icon: "Icon.Folder");
                    foreach (var k in t.Keys) keys.Children.Add(new DatabaseNode($"{k.Name} — {k.Definition}", "key", connection, schema, t, icon: "Icon.Lock"));
                    table.Children.Add(keys);
                    var indexes = new DatabaseNode("Indexes", "", connection, schema, t, icon: "Icon.Folder");
                    foreach (var i in t.Indexes) indexes.Children.Add(new DatabaseNode(i.Name + (i.Unique ? " (unique)" : ""), i.Definition, connection, schema, t));
                    table.Children.Add(indexes); group.Children.Add(table);
                }
                node.Children.Add(group);
            }
            Children.Add(node);
        }
    }
}
