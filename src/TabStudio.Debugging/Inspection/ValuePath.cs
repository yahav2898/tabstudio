namespace TabStudio.Debugging.Inspection;

/// <summary>
/// Resolves simple value paths such as <c>order.Customer.Name</c>, <c>items[0]</c> or <c>prices["tea"]</c> against
/// the locals of a pause, for DataTips and the Watch window.
/// </summary>
/// <remarks>
/// This is not a C# evaluator: a path is a variable name followed by member names and indexes that match the names
/// <see cref="DebugValue.Expand"/> shows. Method calls and operators are not supported.
/// </remarks>
internal static class ValuePath
{
    /// <summary>Finds the value a path names.</summary>
    /// <param name="locals">The locals of the paused frame.</param>
    /// <param name="path">The path.</param>
    /// <returns>The value, or <see langword="null"/> when a part of the path does not exist.</returns>
    public static DebugValue? Find(IReadOnlyList<DebugValue> locals, string path)
    {
        // Qualified static fields remain distinct from a same-named local in recorded snapshots.
        if (locals.FirstOrDefault(l => l.Name == path) is { } exact) return exact;
        var parts = Split(path);
        if (parts.Count == 0)
        {
            return null;
        }

        // The first part is a local or, as in C#, a member of this.
        var current = locals.FirstOrDefault(l => l.Name == parts[0]) ??
            Child(locals.FirstOrDefault(l => l.Name == "this"), parts[0]);
        for (var i = 1; i < parts.Count && current is not null; i++)
        {
            current = Child(current, parts[i]);
        }

        return current;
    }

    private static DebugValue? Child(DebugValue? parent, string name) =>
        parent is null || !parent.HasChildren ? null : parent.Expand().FirstOrDefault(c => c.Name == name);

    /// <summary>Splits <c>a.b[0]["k"]</c> into <c>a</c>, <c>b</c>, <c>[0]</c> and <c>["k"]</c>.</summary>
    private static List<string> Split(string path)
    {
        var parts = new List<string>();
        var start = 0;
        var i = 0;
        while (i < path.Length)
        {
            var c = path[i];
            if (c == '.')
            {
                Add(parts, path[start..i]);
                start = ++i;
            }
            else if (c == '[')
            {
                Add(parts, path[start..i]);
                var end = IndexEnd(path, i);
                parts.Add(path[i..end]);
                start = i = end;
            }
            else
            {
                i++;
            }
        }

        Add(parts, path[start..]);
        return parts;
    }

    /// <summary>The position just past the <c>]</c> that closes the index starting at <paramref name="open"/>.</summary>
    private static int IndexEnd(string path, int open)
    {
        var quoted = false;
        for (var i = open + 1; i < path.Length; i++)
        {
            switch (path[i])
            {
                case '\\' when quoted:
                    i++;
                    break;
                case '"':
                    quoted = !quoted;
                    break;
                case ']' when !quoted:
                    return i + 1;
            }
        }

        return path.Length;
    }

    private static void Add(List<string> parts, string part)
    {
        var trimmed = part.Trim();
        if (trimmed.Length > 0)
        {
            parts.Add(trimmed);
        }
    }
}
