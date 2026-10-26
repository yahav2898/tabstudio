using System.Text.RegularExpressions;

namespace TabStudio.Database;

public sealed record SqlCompletion(string Text, string Kind, string? InsertionText = null);

public static partial class SqlCompletions
{
    private static readonly string[] Keywords = "SELECT FROM WHERE JOIN LEFT RIGHT INNER OUTER FULL CROSS ON AS AND OR NOT NULL IS IN EXISTS BETWEEN LIKE ORDER BY GROUP HAVING DISTINCT TOP LIMIT OFFSET FETCH INSERT INTO VALUES UPDATE SET DELETE CREATE ALTER DROP TABLE VIEW INDEX PRIMARY FOREIGN KEY REFERENCES WITH UNION ALL CASE WHEN THEN ELSE END ASC DESC COUNT SUM AVG MIN MAX BEGIN COMMIT ROLLBACK RETURNING".Split(' ');

    public static IReadOnlyList<SqlCompletion> Get(DatabaseSchema? schema, string sql, int offset, DatabaseProvider provider = DatabaseProvider.SQLite)
    {
        offset = Math.Clamp(offset, 0, sql.Length);
        if (InsideLiteralOrComment(sql, offset)) return [];
        var before = sql[..offset];
        var prefix = Prefix().Match(before).Value;
        var items = new List<SqlCompletion>();
        var dot = before.Length - prefix.Length - 1;
        if (schema is not null && dot >= 0 && before[dot] == '.')
        {
            var owner = Prefix().Match(before[..dot]).Value;
            var aliases = Aliases().Matches(sql).GroupBy(m => m.Groups[2].Value, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last().Groups[1].Value, StringComparer.OrdinalIgnoreCase);
            if (aliases.TryGetValue(owner, out var tableName)) owner = tableName;
            foreach (var table in schema.Tables.Where(t => t.Name.Equals(owner, StringComparison.OrdinalIgnoreCase)))
                items.AddRange(table.Columns.Select(c => Identifier(c.Name, c.Type)));
            foreach (var table in schema.Tables.Where(t => t.Schema.Equals(owner, StringComparison.OrdinalIgnoreCase)))
                items.Add(Identifier(table.Name, table.IsView ? "view" : "table"));
        }
        else
        {
            items.AddRange(Keywords.Select(k => new SqlCompletion(k, "keyword")));
            if (schema is not null)
                foreach (var table in schema.Tables)
                {
                    items.Add(Identifier(table.Name, table.IsView ? "view" : "table"));
                    items.AddRange(table.Columns.Select(c => Identifier(c.Name, $"{table.Name}: {c.Type}")));
                }
        }
        return items.Where(i => i.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).DistinctBy(i => i.Text, StringComparer.OrdinalIgnoreCase).OrderBy(i => i.Text, StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
        SqlCompletion Identifier(string name, string kind) => new(name, kind, DatabaseConnections.Quote(provider, name));
    }

    private static bool InsideLiteralOrComment(string sql, int offset)
    {
        char quote = '\0'; var line = false; var block = false;
        for (var i = 0; i < offset; i++)
        {
            var c = sql[i]; var next = i + 1 < offset ? sql[i + 1] : '\0';
            if (line) { if (c == '\n') line = false; continue; }
            if (block) { if (c == '*' && next == '/') { block = false; i++; } continue; }
            if (quote != '\0')
            {
                if (c == quote && next == quote) { i++; continue; }
                if (c == quote) quote = '\0';
                continue;
            }
            if (c == '-' && next == '-') { line = true; i++; }
            else if (c == '/' && next == '*') { block = true; i++; }
            else if (c is '\'' or '"' or '`') quote = c;
            else if (c == '[') quote = ']';
        }
        return quote != '\0' || line || block;
    }

    public static int PrefixLength(string sql, int offset) => Prefix().Match(sql[..Math.Clamp(offset, 0, sql.Length)]).Length;
    [GeneratedRegex(@"[\p{L}\p{N}_]+$")]
    private static partial Regex Prefix();
    [GeneratedRegex(@"\b(?:FROM|JOIN)\s+(?:\w+\.)?(\w+)\s+(?:AS\s+)?(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex Aliases();
}
