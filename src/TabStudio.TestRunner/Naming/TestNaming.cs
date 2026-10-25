using System.Collections;
using System.Globalization;
using System.Text;

namespace TabStudio.TestRunner.Naming;

/// <summary>
/// A value that is already formatted, used by source discovery for arguments it cannot evaluate, such as an
/// enum member or a <c>typeof</c> expression.
/// </summary>
/// <param name="Text">The text to show.</param>
internal sealed record FormattedValue(string Text);

/// <summary>
/// Names tests and data rows. Source discovery and reflection discovery both name rows here, so a row found in
/// the editor has the same <see cref="TestCase.Id"/> as the row the runner reports.
/// </summary>
internal static class TestNaming
{
    private const int MaxStringLength = 60;
    private const int MaxElements = 5;

    /// <summary>The fully qualified name of a test method: its type's full name and the method name.</summary>
    /// <param name="typeFullName">The type's full name, with <c>+</c> between nested types.</param>
    /// <param name="methodName">The method name.</param>
    public static string FullyQualifiedName(string typeFullName, string methodName) => typeFullName + "." + methodName;

    /// <summary>The arguments of a row, as in <c>(1, "two")</c>.</summary>
    /// <param name="values">The row's values, before conversion to the parameter types.</param>
    public static string Row(IReadOnlyList<object?> values) => "(" + string.Join(", ", values.Select(Format)) + ")";

    /// <summary>
    /// The display name of a data row in the style of its framework: <c>Adds(a: 1, b: 2)</c> for xUnit,
    /// <c>Adds(1,2)</c> for NUnit and <c>Adds (1,2)</c> for MSTest.
    /// </summary>
    /// <param name="framework">The test framework.</param>
    /// <param name="baseName">The method name, or the method's custom display name.</param>
    /// <param name="parameterNames">The method's parameter names, used by xUnit.</param>
    /// <param name="values">The row's values.</param>
    public static string RowDisplayName(TestFramework framework, string baseName, IReadOnlyList<string> parameterNames, IReadOnlyList<object?> values)
    {
        var formatted = values.Select(Format).ToList();
        return framework switch
        {
            TestFramework.XUnit => baseName + "(" + string.Join(", ", formatted.Select((v, i) => (i < parameterNames.Count ? parameterNames[i] : "???") + ": " + v)) + ")",
            TestFramework.NUnit => baseName + "(" + string.Join(",", formatted) + ")",
            _ => baseName + " (" + string.Join(",", formatted) + ")",
        };
    }

    /// <summary>
    /// Makes a row id unique within its method by appending <c>#2</c>, <c>#3</c>… to repeats, which happen when
    /// two rows have the same values or values whose text is the same.
    /// </summary>
    /// <param name="id">The candidate id.</param>
    /// <param name="seen">The ids already used by the method, updated by this call.</param>
    public static string Unique(string id, Dictionary<string, int> seen)
    {
        if (!seen.TryGetValue(id, out var count))
        {
            seen[id] = 1;
            return id;
        }

        seen[id] = count + 1;
        return $"{id} #{count + 1}";
    }

    /// <summary>Formats one argument value the way it reads in source.</summary>
    /// <param name="value">The value.</param>
    public static string Format(object? value) => value switch
    {
        null => "null",
        FormattedValue formatted => formatted.Text,
        string text => Quote(text),
        char character => "'" + Escape(new string(character, 1), '\'') + "'",
        bool flag => flag ? "true" : "false",
        float number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        Enum member => member.ToString(),
        Type type => "typeof(" + type.Name + ")",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable sequence => FormatSequence(sequence),
        _ => value.ToString() ?? value.GetType().Name,
    };

    private static string FormatSequence(IEnumerable sequence)
    {
        var builder = new StringBuilder("[");
        var count = 0;
        foreach (var element in sequence)
        {
            if (count > 0)
            {
                builder.Append(", ");
            }

            if (count == MaxElements)
            {
                builder.Append("...");
                break;
            }

            builder.Append(Format(element));
            count++;
        }

        return builder.Append(']').ToString();
    }

    private static string Quote(string text)
    {
        var shown = text.Length > MaxStringLength ? text[..MaxStringLength] + "..." : text;
        return "\"" + Escape(shown, '"') + "\"";
    }

    private static string Escape(string text, char quote)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var escaped = c switch
            {
                '\\' => @"\\",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                '\0' => @"\0",
                _ when c == quote => "\\" + quote,
                _ => null,
            };

            if (escaped is null)
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(escaped);
            }
        }

        return builder.ToString();
    }
}
