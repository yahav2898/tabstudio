namespace TabStudio.Debugging.Inspection;

/// <summary>
/// Resolves simple value paths such as <c>order.Customer.Name</c>, <c>items[0]</c> or <c>prices["tea"]</c> against
/// the locals of a pause, for DataTips and the Watch window. In Visual Basic code they read <c>items(0)</c> or
/// <c>prices("tea")</c>, and names are not case-sensitive.
/// </summary>
/// <remarks>
/// This is not a C# or Visual Basic evaluator: a path is a variable name followed by member names and indexes that
/// match the names <see cref="DebugValue.Expand"/> shows. Method calls and operators are not supported.
/// </remarks>
internal static class ValuePath
{
    /// <summary>Finds the value a C# path names.</summary>
    /// <param name="locals">The locals of the paused frame.</param>
    /// <param name="path">The path.</param>
    /// <returns>The value, or <see langword="null"/> when a part of the path does not exist.</returns>
    public static DebugValue? Find(IReadOnlyList<DebugValue> locals, string path) => Find(locals, path, visualBasic: false);

    /// <summary>Finds the value a path names, in C# or in Visual Basic.</summary>
    /// <param name="locals">The locals of the paused frame.</param>
    /// <param name="path">The path.</param>
    /// <param name="visualBasic">Whether the paused code is Visual Basic: indexes in parentheses and names in any case.</param>
    /// <returns>The value, or <see langword="null"/> when a part of the path does not exist.</returns>
    public static DebugValue? Find(IReadOnlyList<DebugValue> locals, string path, bool visualBasic)
    {
        // Qualified static fields remain distinct from a same-named local in recorded snapshots.
        if (locals.FirstOrDefault(l => NameEquals(l.Name, path, visualBasic)) is { } exact) return exact;
        var parts = Split(path, visualBasic);
        if (parts.Count == 0)
        {
            return null;
        }

        // The first part is a local or, as in C# and Visual Basic, a member of this (Me).
        var instance = visualBasic ? "Me" : "this";
        var current = locals.FirstOrDefault(l => NameEquals(l.Name, parts[0], visualBasic)) ??
            Child(locals.FirstOrDefault(l => l.Name == instance), parts[0], visualBasic);
        for (var i = 1; i < parts.Count && current is not null; i++)
        {
            current = Child(current, parts[i], visualBasic);
        }

        return current;
    }

    private static DebugValue? Child(DebugValue? parent, string name, bool visualBasic) =>
        parent is null || !parent.HasChildren ? null : parent.Expand().FirstOrDefault(c => NameEquals(c.Name, name, visualBasic));

    /// <summary>
    /// Whether a name matches a part of a path. Visual Basic names ignore case, and its indexes may be written with
    /// brackets too; the keys inside an index always match exactly.
    /// </summary>
    private static bool NameEquals(string name, string part, bool visualBasic)
    {
        if (!visualBasic)
        {
            return name == part;
        }

        if (part.Length >= 2 && part[0] == '[' && part[^1] == ']')
        {
            part = "(" + part[1..^1] + ")";
        }

        return part.StartsWith('(')
            ? string.Equals(name, part, StringComparison.Ordinal)
            : string.Equals(name, part, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Splits <c>a.b[0]["k"]</c> into <c>a</c>, <c>b</c>, <c>[0]</c> and <c>["k"]</c>; in Visual Basic <c>(0)</c> too.</summary>
    private static List<string> Split(string path, bool visualBasic)
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
            else if (c == '[' || (visualBasic && c == '('))
            {
                Add(parts, path[start..i]);
                var end = IndexEnd(path, i, visualBasic);
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

    /// <summary>
    /// The position just past the <c>]</c> or <c>)</c> that closes the index starting at <paramref name="open"/>. C#
    /// strings escape with backslashes; Visual Basic strings double their quotes, which toggling on each quote handles.
    /// </summary>
    private static int IndexEnd(string path, int open, bool visualBasic)
    {
        var close = path[open] == '(' ? ')' : ']';
        var quoted = false;
        for (var i = open + 1; i < path.Length; i++)
        {
            var c = path[i];
            if (c == '\\' && quoted && !visualBasic)
            {
                i++;
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == close && !quoted)
            {
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
