using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace TabStudio.Debugging.Inspection;

/// <summary>
/// Describes values of the paused program for the Locals panel: how a value reads, what type it has and which
/// children it expands into.
/// </summary>
/// <remarks>
/// <para>
/// Base library values (numbers, strings, dates) are formatted directly, always with the invariant culture.
/// Anything that runs the program's own code, such as a property getter, a <c>ToString</c> override or a
/// collection's enumerator, goes through the <see cref="Evaluator"/> and its time limit.
/// </para>
/// <para>
/// An inspector for Visual Basic code writes values and types as Visual Basic does: <c>Nothing</c>, <c>True</c>,
/// <c>"x"c</c>, <c>Integer</c>, <c>List(Of String)</c>, and items as <c>(0)</c>.
/// </para>
/// </remarks>
internal sealed class ValueInspector
{
    /// <summary>How many items of a collection are shown.</summary>
    public const int MaxItems = 100;

    private const int MaxStringLength = 1000;

    private readonly Evaluator _evaluator;

    /// <summary>Creates an inspector.</summary>
    /// <param name="evaluator">Runs the program's code with a time limit.</param>
    /// <param name="visualBasic">Whether to show values and types as Visual Basic writes them.</param>
    public ValueInspector(Evaluator evaluator, bool visualBasic = false)
    {
        _evaluator = evaluator;
        IsVisualBasic = visualBasic;
    }

    /// <summary>Whether values and types read as Visual Basic.</summary>
    public bool IsVisualBasic { get; }

    /// <summary>An inspector with the same time limit that shows values as C# or as Visual Basic writes them.</summary>
    /// <param name="visualBasic">Whether to show values and types as Visual Basic writes them.</param>
    public ValueInspector ForLanguage(bool visualBasic) => visualBasic == IsVisualBasic ? this : new ValueInspector(_evaluator, visualBasic);

    /// <summary>The value as the Locals panel shows it.</summary>
    public string FormatValue(object? value)
    {
        if (value is null)
        {
            return IsVisualBasic ? "Nothing" : "null";
        }

        switch (value)
        {
            case string text:
                return IsVisualBasic ? QuoteVisualBasic(text) : Quote(text);
            case char c:
                return IsVisualBasic ? VisualBasicCharacter(c) : "'" + Escape(c, '\'') + "'";
            case bool flag:
                return IsVisualBasic ? (flag ? "True" : "False") : (flag ? "true" : "false");
            case Enum member:
                return FormatEnum(member, IsVisualBasic ? " Or " : " | ");
            case DateTime date:
                return date.ToString("O", CultureInfo.InvariantCulture);
            case DateTimeOffset date:
                return date.ToString("O", CultureInfo.InvariantCulture);
            case TimeSpan span:
                return span.ToString("c", CultureInfo.InvariantCulture);
            case Guid guid:
                return guid.ToString("D", CultureInfo.InvariantCulture);
            case Array array:
                return "Length = " + array.Length.ToString(CultureInfo.InvariantCulture);
            case Exception exception:
                return "{" + TypeNames.Format(exception.GetType(), IsVisualBasic) + ": " + exception.Message + "}";
            case IFormattable number when IsNumber(number.GetType()):
                return number.ToString(null, CultureInfo.InvariantCulture);
            case ITuple tuple when tuple.GetType().Namespace == "System":
                return FormatTuple(tuple);
        }

        var type = value.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            return "[" + FormatValue(type.GetProperty("Key")?.GetValue(value)) + ", " + FormatValue(type.GetProperty("Value")?.GetValue(value)) + "]";
        }

        if (TryGetCount(value, out var count))
        {
            return "Count = " + count.ToString(CultureInfo.InvariantCulture);
        }

        if (OverridesToString(type))
        {
            return _evaluator.TryRun(() => value.ToString(), out var text, out var failure)
                ? "{" + (text ?? FormatValue(null)) + "}"
                : "{" + TypeNames.Format(type, IsVisualBasic) + "} (" + failure + ")";
        }

        return "{" + TypeNames.Format(type, IsVisualBasic) + "}";
    }

    /// <summary>
    /// The type column: the declared type, followed by the runtime type in braces when it is more specific, as in
    /// <c>object {string}</c>.
    /// </summary>
    public static string FormatType(object? value, string? declaredType) => FormatType(value, declaredType, visualBasic: false);

    /// <summary>The type column, in C# or Visual Basic, as in <c>Object {String}</c>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="declaredType">The declared type, in the same language, or <see langword="null"/>.</param>
    /// <param name="visualBasic">Whether to write the runtime type as Visual Basic does.</param>
    public static string FormatType(object? value, string? declaredType, bool visualBasic)
    {
        if (value is null)
        {
            return declaredType ?? (visualBasic ? "Object" : "object");
        }

        var runtime = TypeNames.Format(value.GetType(), visualBasic);
        if (declaredType is null)
        {
            return runtime;
        }

        // A nullable declaration boxes to its underlying type, and tuple names do not exist at run time. Visual Basic
        // writes generic types with parentheses too, so only a leading one marks a tuple there.
        var isTuple = visualBasic ? declaredType.StartsWith('(') : declaredType.Contains('(', StringComparison.Ordinal);
        return declaredType.TrimEnd('?') == runtime || isTuple
            ? declaredType
            : declaredType + " {" + runtime + "}";
    }

    /// <summary>Whether a value has children to expand. Never runs the program's code.</summary>
    public static bool HasChildren(object? value)
    {
        if (value is null || IsSimple(value.GetType()))
        {
            return false;
        }

        if (value is Array array)
        {
            return array.Length > 0;
        }

        if (IsCollection(value))
        {
            return true;
        }

        return Members(value.GetType()).Any();
    }

    /// <summary>
    /// The children of a value: the items of an array or collection (the first <see cref="MaxItems"/>), the entries
    /// of a dictionary, or otherwise its public fields and readable public properties. Indexers and properties
    /// that throw are left out.
    /// </summary>
    public IReadOnlyList<DebugValue> GetChildren(object? value)
    {
        if (value is null || IsSimple(value.GetType()))
        {
            return [];
        }

        var gathered = new List<DebugValue>();
        Func<bool> gather = value switch
        {
            Array array => () => AddItems(array, ElementType(array.GetType()), gathered),
            IDictionary dictionary => () => AddEntries(dictionary, gathered),
            IEnumerable items when IsCollection(value) => () => AddItems(items, ElementType(value.GetType()), gathered),
            _ => () => AddMembers(value, gathered),
        };

        // Children are gathered on the evaluation thread; on a time-out, the ones read so far are shown.
        _evaluator.TryRun(gather, out _, out _);
        lock (gathered)
        {
            return [.. gathered];
        }
    }

    private bool AddItems(IEnumerable items, string? elementType, List<DebugValue> gathered)
    {
        var index = 0;
        foreach (var item in items)
        {
            if (index >= MaxItems)
            {
                break;
            }

            var child = new DebugValue(IndexName(index.ToString(CultureInfo.InvariantCulture)), elementType, item, this);
            lock (gathered)
            {
                gathered.Add(child);
            }

            index++;
        }

        return true;
    }

    private bool AddEntries(IDictionary dictionary, List<DebugValue> gathered)
    {
        var valueType = DictionaryValueType(dictionary.GetType());
        var index = 0;
        foreach (DictionaryEntry entry in dictionary)
        {
            if (index++ >= MaxItems)
            {
                break;
            }

            var child = new DebugValue(IndexName(FormatValue(entry.Key)), valueType, entry.Value, this);
            lock (gathered)
            {
                gathered.Add(child);
            }
        }

        return true;
    }

#pragma warning disable CA1031 // A getter of the program can throw anything; such properties are left out.
    private bool AddMembers(object value, List<DebugValue> gathered)
    {
        foreach (var member in Members(value.GetType()).OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            object? memberValue;
            Type memberType;
            try
            {
                if (member is FieldInfo field)
                {
                    memberValue = field.GetValue(value);
                    memberType = field.FieldType;
                }
                else
                {
                    var property = (PropertyInfo)member;
                    memberValue = property.GetValue(value);
                    memberType = property.PropertyType;
                }
            }
            catch (Exception)
            {
                continue;
            }

            var child = new DebugValue(member.Name, TypeNames.Format(memberType, IsVisualBasic), memberValue, this);
            lock (gathered)
            {
                gathered.Add(child);
            }
        }

        return true;
    }
#pragma warning restore CA1031

    /// <summary>Public instance fields and readable, non-indexed public properties whose values can be boxed.</summary>
    private static IEnumerable<MemberInfo> Members(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var field in type.GetFields(flags))
        {
            if (CanBox(field.FieldType))
            {
                yield return field;
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (property.GetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0 && CanBox(property.PropertyType))
            {
                yield return property;
            }
        }
    }

    private static bool CanBox(Type type) => !type.IsByRef && !type.IsByRefLike && !type.IsPointer && !type.IsFunctionPointer;

    private bool TryGetCount(object value, out int count)
    {
        count = 0;
        Func<int>? read = null;
        if (value is ICollection collection)
        {
            read = () => collection.Count;
        }
        else if (CountProperty(value.GetType()) is { } property)
        {
            read = () => (int)property.GetValue(value)!;
        }

        if (read is null || !_evaluator.TryRun(read, out var result, out _))
        {
            return false;
        }

        count = result;
        return true;
    }

    private static bool IsCollection(object value) => value is ICollection || CountProperty(value.GetType()) is not null;

    /// <summary>The <c>Count</c> of <see cref="ICollection{T}"/> or <see cref="IReadOnlyCollection{T}"/>, if implemented.</summary>
    private static PropertyInfo? CountProperty(Type type)
    {
        foreach (var candidate in type.GetInterfaces())
        {
            if (candidate.IsGenericType &&
                (candidate.GetGenericTypeDefinition() == typeof(ICollection<>) || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)))
            {
                return candidate.GetProperty("Count");
            }
        }

        return null;
    }

    /// <summary>The name of an item or entry: <c>[0]</c> in C#, <c>(0)</c> in Visual Basic.</summary>
    private string IndexName(string index) => IsVisualBasic ? "(" + index + ")" : "[" + index + "]";

    private string? ElementType(Type type)
    {
        if (type.IsArray)
        {
            return TypeNames.Format(type.GetElementType()!, IsVisualBasic);
        }

        var enumerable = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable is null ? null : TypeNames.Format(enumerable.GetGenericArguments()[0], IsVisualBasic);
    }

    private string? DictionaryValueType(Type type)
    {
        var dictionary = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        return dictionary is null ? null : TypeNames.Format(dictionary.GetGenericArguments()[1], IsVisualBasic);
    }

    private static bool OverridesToString(Type type) =>
        type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is { } method &&
        method.DeclaringType != typeof(object) &&
        method.DeclaringType != typeof(ValueType);

    private static bool IsSimple(Type type) =>
        type.IsPrimitive || type.IsEnum || IsNumber(type) ||
        type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
        type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(DateOnly) || type == typeof(TimeOnly);

    private static bool IsNumber(Type type) =>
        (type.IsPrimitive && type != typeof(bool) && type != typeof(char)) ||
        type == typeof(decimal) || type == typeof(Half) || type == typeof(Int128) || type == typeof(UInt128) || type == typeof(BigInteger);

    private static string FormatEnum(Enum member, string separator)
    {
        var text = Enum.Format(member.GetType(), member, "G");
        return member.GetType().IsDefined(typeof(FlagsAttribute), inherit: false) ? text.Replace(", ", separator, StringComparison.Ordinal) : text;
    }

    private string FormatTuple(ITuple tuple)
    {
        var parts = new string[tuple.Length];
        for (var i = 0; i < tuple.Length; i++)
        {
            parts[i] = FormatValue(tuple[i]);
        }

        return "(" + string.Join(", ", parts) + ")";
    }

    /// <summary>A C# string literal for a string, cut off after <c>1000</c> characters.</summary>
    public static string Quote(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, MaxStringLength) + 2).Append('"');
        foreach (var c in text.Length > MaxStringLength ? text[..MaxStringLength] : text)
        {
            builder.Append(Escape(c, '"'));
        }

        builder.Append('"');
        if (text.Length > MaxStringLength)
        {
            builder.Append("...");
        }

        return builder.ToString();
    }

    /// <summary>
    /// A Visual Basic string literal for a string, cut off after <c>1000</c> characters. Visual Basic strings have no
    /// escapes: quotes are doubled, and control characters are joined in as constants, as in <c>"a" &amp; vbLf &amp; "b"</c>.
    /// </summary>
    public static string QuoteVisualBasic(string text)
    {
        var shown = text.Length > MaxStringLength ? text[..MaxStringLength] : text;
        var builder = new StringBuilder(shown.Length + 2);
        var open = false;
        for (var i = 0; i < shown.Length; i++)
        {
            var c = shown[i];
            var isLineBreak = c == '\r' && i + 1 < shown.Length && shown[i + 1] == '\n';
            var constant = isLineBreak ? "vbCrLf" : VisualBasicConstant(c);
            if (constant is null)
            {
                if (!open)
                {
                    builder.Append(builder.Length > 0 ? " & \"" : "\"");
                    open = true;
                }

                if (c == '"')
                {
                    builder.Append("\"\"");
                }
                else
                {
                    builder.Append(c);
                }

                continue;
            }

            if (open)
            {
                builder.Append('"');
                open = false;
            }

            builder.Append(builder.Length > 0 ? " & " : string.Empty).Append(constant);
            if (isLineBreak)
            {
                i++;
            }
        }

        if (open)
        {
            builder.Append('"');
        }

        if (builder.Length == 0)
        {
            builder.Append("\"\"");
        }

        if (text.Length > MaxStringLength)
        {
            builder.Append("...");
        }

        return builder.ToString();
    }

    /// <summary>A Visual Basic character literal, such as <c>"x"c</c>, or the constant of a control character.</summary>
    private static string VisualBasicCharacter(char c) =>
        VisualBasicConstant(c) ?? (c == '"' ? "\"\"\"\"c" : "\"" + new string(c, 1) + "\"c");

    /// <summary>The Visual Basic constant for a control character, such as <c>vbLf</c>, or <see langword="null"/> for others.</summary>
    private static string? VisualBasicConstant(char c) => c switch
    {
        '\r' => "vbCr",
        '\n' => "vbLf",
        '\t' => "vbTab",
        '\0' => "vbNullChar",
        '\b' => "vbBack",
        '\f' => "vbFormFeed",
        '\v' => "vbVerticalTab",
        _ when char.IsControl(c) => "ChrW(" + ((int)c).ToString(CultureInfo.InvariantCulture) + ")",
        _ => null,
    };

    private static string Escape(char c, char quote) => c switch
    {
        '\\' => "\\\\",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\0' => "\\0",
        '\a' => "\\a",
        '\b' => "\\b",
        '\f' => "\\f",
        '\v' => "\\v",
        _ when c == quote => "\\" + c,
        _ when char.IsControl(c) => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
        _ => new string(c, 1),
    };
}
