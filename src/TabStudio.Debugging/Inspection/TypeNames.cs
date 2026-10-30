using System.Globalization;

namespace TabStudio.Debugging.Inspection;

/// <summary>
/// Writes runtime types the way C# spells them: <c>int</c>, <c>List&lt;string&gt;</c>, <c>int?[]</c>, <c>(int, string)</c>;
/// or the way Visual Basic does: <c>Integer</c>, <c>List(Of String)</c>, <c>Integer?()</c>, <c>(Integer, String)</c>.
/// </summary>
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

    private static readonly Dictionary<Type, string> VisualBasicKeywords = new()
    {
        [typeof(bool)] = "Boolean",
        [typeof(byte)] = "Byte",
        [typeof(sbyte)] = "SByte",
        [typeof(char)] = "Char",
        [typeof(short)] = "Short",
        [typeof(ushort)] = "UShort",
        [typeof(int)] = "Integer",
        [typeof(uint)] = "UInteger",
        [typeof(long)] = "Long",
        [typeof(ulong)] = "ULong",
        [typeof(float)] = "Single",
        [typeof(double)] = "Double",
        [typeof(decimal)] = "Decimal",
        [typeof(string)] = "String",
        [typeof(object)] = "Object",
        [typeof(DateTime)] = "Date",
    };

    /// <summary>The C# name of a type, without its namespace.</summary>
    public static string Format(Type type) => Format(type, visualBasic: false);

    /// <summary>The C# or Visual Basic name of a type, without its namespace.</summary>
    /// <param name="type">The type.</param>
    /// <param name="visualBasic">Whether to write it as Visual Basic does.</param>
    public static string Format(Type type, bool visualBasic)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsArray)
        {
            var commas = new string(',', type.GetArrayRank() - 1);
            return Format(type.GetElementType()!, visualBasic) + (visualBasic ? "(" + commas + ")" : "[" + commas + "]");
        }

        if (type.IsPointer)
        {
            return Format(type.GetElementType()!, visualBasic) + "*";
        }

        if (type.IsByRef)
        {
            return Format(type.GetElementType()!, visualBasic);
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Format(underlying, visualBasic) + "?";
        }

        if ((visualBasic ? VisualBasicKeywords : Keywords).TryGetValue(type, out var keyword))
        {
            return keyword;
        }

        var name = type.Name;
        if (name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal) || name.StartsWith("VB$AnonymousType", StringComparison.Ordinal))
        {
            return "<anonymous type>";
        }

        var prefix = type.IsNested && !type.IsGenericParameter && type.DeclaringType is { } declaring ? Format(declaring, visualBasic) + "." : string.Empty;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick < 0 || !type.IsGenericType)
        {
            return prefix + name;
        }

        var arguments = type.GetGenericArguments();
        if (type.Namespace == "System" && name.StartsWith("ValueTuple`", StringComparison.Ordinal) && arguments.Length < 8)
        {
            return "(" + string.Join(", ", arguments.Select(a => Format(a, visualBasic))) + ")";
        }

        // Nested generic types repeat the arguments of the types around them; show only their own.
        var own = int.TryParse(name.AsSpan(tick + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : arguments.Length;
        var shown = string.Join(", ", arguments.Skip(Math.Max(0, arguments.Length - own)).Select(a => Format(a, visualBasic)));
        return visualBasic
            ? prefix + name[..tick] + "(Of " + shown + ")"
            : prefix + name[..tick] + "<" + shown + ">";
    }
}
