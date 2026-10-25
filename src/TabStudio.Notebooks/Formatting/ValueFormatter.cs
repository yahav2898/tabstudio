using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Formatting;

/// <summary>
/// Turns a value into what a notebook shows for it, the way Polyglot Notebooks does: plain text always, and a
/// table for collections and objects.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see langword="null"/>, strings, numbers, dates and other simple values become plain text.</description></item>
/// <item><description>Dictionaries become a table of keys and values.</description></item>
/// <item><description>
/// Other collections become a table with one row per item and one column per public property or field,
/// showing the first <see cref="ValueFormatterOptions.MaxRows"/> items. Collections of simple values have a
/// single <c>value</c> column.
/// </description></item>
/// <item><description>Other objects become a table with one row, and keep their own <c>ToString()</c> as plain text.</description></item>
/// </list>
/// <para>
/// Tables come as <c>text/html</c>, as aligned <c>text/plain</c> and as a <see cref="TableOutput"/> that a
/// native view can show without HTML. Numbers and dates are formatted with the invariant culture.
/// </para>
/// <para>
/// Formatting reads properties and enumerates collections, which runs the value's own code: a lazy LINQ query
/// runs at this point. A property that throws shows the error in its cell instead of failing the whole value.
/// </para>
/// </remarks>
public sealed class ValueFormatter
{
    /// <summary>The text shown for <see langword="null"/>.</summary>
    public const string NullText = "<null>";

    /// <summary>The column that holds simple values in a table.</summary>
    public const string ValueColumn = "value";

    private const int MaxInlineDepth = 2;

    private static readonly HashSet<Type> ScalarTypes =
    [
        typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid), typeof(DateOnly),
        typeof(TimeOnly), typeof(Half), typeof(Int128), typeof(UInt128), typeof(BigInteger), typeof(Complex),
        typeof(Uri), typeof(Version),
    ];

    private static readonly Type[] OpaqueTypes =
    [
        typeof(Task), typeof(Delegate), typeof(MemberInfo), typeof(Assembly), typeof(Module), typeof(Stream),
        typeof(TextReader), typeof(TextWriter), typeof(Thread), typeof(WaitHandle), typeof(CancellationTokenSource),
        typeof(Exception),
    ];

    /// <summary>Creates a formatter.</summary>
    /// <param name="options">Limits on how much of a value is formatted, or <see langword="null"/> for the defaults.</param>
    public ValueFormatter(ValueFormatterOptions? options = null)
    {
        Options = options ?? new ValueFormatterOptions();
    }

    /// <summary>A formatter with the default limits.</summary>
    public static ValueFormatter Default { get; } = new();

    /// <summary>The limits this formatter applies.</summary>
    public ValueFormatterOptions Options { get; }

    /// <summary>Formats a value for display below a cell.</summary>
    /// <param name="value">The value.</param>
    public FormattedValue Format(object? value)
    {
        if (value is null)
        {
            return Text(NullText);
        }

        if (value is string text)
        {
            return Text(text);
        }

        if (value is Exception exception)
        {
            return Text(exception.ToString());
        }

        var type = value.GetType();
        if (IsScalar(type))
        {
            return Text(FormatScalar(value));
        }

        if (IsOpaque(type))
        {
            return Text(SafeToString(value));
        }

        var members = new MemberCache(Options.MaxColumns);
        if (TryFormatDictionary(value, out var dictionary))
        {
            return Table(dictionary, plainText: null);
        }

        return value is IEnumerable sequence
            ? FormatSequence(sequence, members)
            : FormatObject(value, type, members);
    }

    /// <summary>
    /// Formats a value as the short, single-line text used inside table cells: simple values as text, nested
    /// collections as a bracketed list, and other objects with their <c>ToString()</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    public string FormatInline(object? value) => FormatInline(value, depth: 0);

    private string FormatInline(object? value, int depth)
    {
        var text = value switch
        {
            null => NullText,
            string s => s,
            Exception e => $"{e.GetType().Name}: {e.Message}",
            object scalar when IsScalar(scalar.GetType()) => FormatScalar(scalar),
            IEnumerable sequence when depth < MaxInlineDepth && !IsOpaque(sequence.GetType()) => FormatInlineList(sequence, depth),
            object other => SafeToString(other),
        };

        return text.Length <= Options.MaxCellLength ? text : string.Concat(text.AsSpan(0, Options.MaxCellLength), "…");
    }

    private string FormatInlineList(IEnumerable sequence, int depth)
    {
        var (items, truncated) = Take(sequence, Options.MaxInlineItems);
        var parts = items.Select(item => FormatInline(item, depth + 1));
        return "[" + string.Join(", ", truncated ? parts.Append("…") : parts) + "]";
    }

    private FormattedValue FormatSequence(IEnumerable sequence, MemberCache members)
    {
        var (items, truncated) = Take(sequence, Options.MaxRows);
        var totalRows = truncated ? CountOf(sequence) : items.Count;

        if (items.TrueForAll(IsInlineValue))
        {
            var cells = items.Select(item => FormatInline(item)).ToList();
            var valueRows = new List<IReadOnlyList<string>>(cells.Count);
            foreach (var cell in cells)
            {
                valueRows.Add([cell]);
            }

            var single = new TableOutput([ValueColumn], valueRows) { IsTruncated = truncated, TotalRowCount = totalRows };
            var list = "[" + string.Join(", ", truncated ? cells.Append("…") : cells) + "]";
            return Table(single, list);
        }

        var columns = new List<string>();
        foreach (var item in items)
        {
            if (item is not null && !IsInlineValue(item))
            {
                foreach (var member in members.Get(item.GetType()))
                {
                    if (!columns.Contains(member.Name) && columns.Count < Options.MaxColumns)
                    {
                        columns.Add(member.Name);
                    }
                }
            }
        }

        if (items.Exists(item => item is not null && IsInlineValue(item)) && !columns.Contains(ValueColumn))
        {
            columns.Add(ValueColumn);
        }

        var rows = new List<IReadOnlyList<string>>(items.Count);
        foreach (var item in items)
        {
            var row = new string[columns.Count];
            Array.Fill(row, string.Empty);
            if (item is not null && IsInlineValue(item))
            {
                row[columns.IndexOf(ValueColumn)] = FormatInline(item);
            }
            else if (item is not null)
            {
                foreach (var member in members.Get(item.GetType()))
                {
                    var index = columns.IndexOf(member.Name);
                    if (index >= 0)
                    {
                        row[index] = ReadInline(member, item);
                    }
                }
            }

            rows.Add(row);
        }

        return Table(new TableOutput(columns, rows) { IsTruncated = truncated, TotalRowCount = totalRows }, plainText: null);
    }

    private FormattedValue FormatObject(object value, Type type, MemberCache members)
    {
        var fields = members.Get(type);
        var cells = fields.Select(member => ReadInline(member, value)).ToArray();
        var plainText = HasOwnToString(type) ? SafeToString(value) : Describe(type, fields, cells);
        if (fields.Length == 0)
        {
            return Text(plainText);
        }

        var table = new TableOutput(fields.Select(f => f.Name).ToList(), [cells]) { TotalRowCount = 1 };
        return Table(table, plainText);
    }

    private bool TryFormatDictionary(object value, [NotNullWhen(true)] out TableOutput? table)
    {
        IEnumerable<(object? Key, object? Value)>? entries = null;
        if (value is IDictionary dictionary)
        {
            entries = DictionaryEntries(dictionary);
        }
        else if (value is IEnumerable pairs && FindKeyValuePairType(value.GetType()) is { } pairType)
        {
            entries = PairEntries(pairs, pairType);
        }

        if (entries is null)
        {
            table = null;
            return false;
        }

        var rows = new List<IReadOnlyList<string>>();
        var truncated = false;
        foreach (var (key, entryValue) in entries)
        {
            if (rows.Count == Options.MaxRows)
            {
                truncated = true;
                break;
            }

            rows.Add([FormatInline(key), FormatInline(entryValue)]);
        }

        table = new TableOutput(["Key", "Value"], rows)
        {
            IsTruncated = truncated,
            TotalRowCount = truncated ? CountOf((IEnumerable)value) : rows.Count,
        };
        return true;
    }

    private string ReadInline(MemberInfo member, object target)
    {
        try
        {
            var value = member is PropertyInfo property ? property.GetValue(target) : ((FieldInfo)member).GetValue(target);
            return FormatInline(value, depth: 1);
        }
        catch (Exception ex)
        {
            var error = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            return $"<{error.GetType().Name}: {error.Message}>";
        }
    }

    private static string Describe(Type type, MemberInfo[] members, string[] cells)
    {
        var properties = members.Select((member, i) => $"{member.Name} = {cells[i]}");
        return members.Length == 0 ? type.Name : $"{type.Name} {{ {string.Join(", ", properties)} }}";
    }

    private static FormattedValue Text(string text) => new(MimeBundle.FromText(text));

    private static FormattedValue Table(TableOutput table, string? plainText) => new(
        MimeBundle.Create(
        [
            new(MimeTypes.Html, TableRenderer.ToHtml(table)),
            new(MimeTypes.PlainText, plainText ?? TableRenderer.ToText(table)),
        ]),
        table);

    private static bool IsScalar(Type type) => type.IsPrimitive || type.IsEnum || ScalarTypes.Contains(type);

    private static bool IsOpaque(Type type) =>
        type == typeof(CancellationToken) ||
        type == typeof(ValueTask) ||
        (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>)) ||
        Array.Exists(OpaqueTypes, opaque => opaque.IsAssignableFrom(type));

    private static bool IsInlineValue(object? value) =>
        value is null || value is string || IsScalar(value.GetType()) || IsOpaque(value.GetType());

    private static string FormatScalar(object value) => value switch
    {
        bool flag => flag ? "True" : "False",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => SafeToString(value),
    };

    private static string SafeToString(object value)
    {
        try
        {
            return value.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            return $"<{ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static bool HasOwnToString(Type type)
    {
        var method = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        return method is not null && method.DeclaringType != typeof(object) && method.DeclaringType != typeof(ValueType);
    }

    private static (List<object?> Items, bool Truncated) Take(IEnumerable sequence, int max)
    {
        var items = new List<object?>();
        var enumerator = sequence.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                if (items.Count == max)
                {
                    return (items, true);
                }

                items.Add(enumerator.Current);
            }

            return (items, false);
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>The number of items of a collection that knows it, without enumerating it.</summary>
    private static int? CountOf(IEnumerable sequence)
    {
        if (sequence is ICollection collection)
        {
            return collection.Count;
        }

        var counted = sequence.GetType().GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>) || i.GetGenericTypeDefinition() == typeof(ICollection<>)));

        return counted?.GetProperty("Count")?.GetValue(sequence) as int?;
    }

    private static Type? FindKeyValuePairType(Type type) => type.GetInterfaces()
        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        .Select(i => i.GetGenericArguments()[0])
        .FirstOrDefault(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));

    private static IEnumerable<(object? Key, object? Value)> DictionaryEntries(IDictionary dictionary)
    {
        var enumerator = dictionary.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                yield return (enumerator.Key, enumerator.Value);
            }
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private static IEnumerable<(object? Key, object? Value)> PairEntries(IEnumerable pairs, Type pairType)
    {
        var key = pairType.GetProperty(nameof(KeyValuePair<int, int>.Key));
        var value = pairType.GetProperty(nameof(KeyValuePair<int, int>.Value));
        foreach (var pair in pairs)
        {
            yield return pair is null ? (null, null) : (key?.GetValue(pair), value?.GetValue(pair));
        }
    }

    /// <summary>The public properties and fields of each type seen while formatting one value.</summary>
    private sealed class MemberCache(int maxColumns)
    {
        private readonly Dictionary<Type, MemberInfo[]> _members = [];

        public MemberInfo[] Get(Type type)
        {
            if (!_members.TryGetValue(type, out var members))
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is { IsPublic: true });
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                members = [.. properties.Cast<MemberInfo>().Concat(fields).Take(maxColumns)];
                _members[type] = members;
            }

            return members;
        }
    }
}
