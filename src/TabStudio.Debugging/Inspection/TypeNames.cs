using System.Globalization;

namespace TabStudio.Debugging.Inspection;

/// <summary>Writes runtime types the way C# spells them: <c>int</c>, <c>List&lt;string&gt;</c>, <c>int?[]</c>, <c>(int, string)</c>.</summary>
internal static class TypeNames
{
    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(nint)] = "nint",
        [typeof(nuint)] = "nuint",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(string)] = "string",
        [typeof(object)] = "object",
        [typeof(void)] = "void",
    };

    /// <summary>The C# name of a type, without its namespace.</summary>
    public static string Format(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsArray)
        {
            return Format(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (type.IsPointer)
        {
            return Format(type.GetElementType()!) + "*";
        }

        if (type.IsByRef)
        {
            return Format(type.GetElementType()!);
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Format(underlying) + "?";
        }

        if (Keywords.TryGetValue(type, out var keyword))
        {
            return keyword;
        }

        var name = type.Name;
        if (name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal))
        {
            return "<anonymous type>";
        }

        var prefix = type.IsNested && !type.IsGenericParameter && type.DeclaringType is { } declaring ? Format(declaring) + "." : string.Empty;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick < 0 || !type.IsGenericType)
        {
            return prefix + name;
        }

        var arguments = type.GetGenericArguments();
        if (type.Namespace == "System" && name.StartsWith("ValueTuple`", StringComparison.Ordinal) && arguments.Length < 8)
        {
            return "(" + string.Join(", ", arguments.Select(Format)) + ")";
        }

        // Nested generic types repeat the arguments of the types around them; show only their own.
        var own = int.TryParse(name.AsSpan(tick + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : arguments.Length;
        var shown = arguments.Skip(Math.Max(0, arguments.Length - own)).Select(Format);
        return prefix + name[..tick] + "<" + string.Join(", ", shown) + ">";
    }
}
