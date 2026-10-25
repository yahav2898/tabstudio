using System.Globalization;

namespace TabStudio.Debugging.Inspection;

/// <summary>
/// Evaluates the condition of a conditional breakpoint against the locals of the statement it is bound to.
/// </summary>
/// <remarks>
/// <para>
/// A condition is one or more comparisons joined by <c>&amp;&amp;</c> or <c>||</c> (no parentheses; <c>&amp;&amp;</c>
/// binds tighter). A comparison is a value path as in <see cref="ValuePath"/>, optionally followed by
/// <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> or <c>&gt;=</c> and a literal (number, <c>"string"</c>,
/// <c>'c'</c>, <c>true</c>, <c>false</c>, <c>null</c>, an enum member name) or another path. A path alone must be
/// a <see cref="bool"/>. Examples: <c>count == 3</c>, <c>name != null &amp;&amp; total &gt; 10.5</c>.
/// </para>
/// <para>This is not a C# evaluator: method calls and arithmetic are not supported.</para>
/// </remarks>
internal static class BreakpointCondition
{
    private static readonly string[] Operators = ["==", "!=", "<=", ">=", "<", ">"];

    /// <summary>Whether the condition holds.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="locals">The locals of the statement.</param>
    /// <param name="error">Why the condition could not be evaluated, when it could not.</param>
    /// <returns>The result; <see langword="true"/> when it could not be evaluated, so the breakpoint still stops.</returns>
    public static bool IsMet(string condition, IReadOnlyList<DebugValue> locals, out string? error)
    {
        error = null;
        foreach (var alternative in SplitOutsideQuotes(condition, "||"))
        {
            var all = true;
            foreach (var comparison in SplitOutsideQuotes(alternative, "&&"))
            {
                if (!TryCompare(comparison.Trim(), locals, out var result, out error))
                {
                    return true;
                }

                all &= result;
            }

            if (all)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCompare(string comparison, IReadOnlyList<DebugValue> locals, out bool result, out string? error)
    {
        result = false;
        error = null;
        var (left, op, right) = SplitComparison(comparison);
        if (!TryResolve(left, locals, out var leftValue, out error))
        {
            return false;
        }

        if (op is null)
        {
            if (leftValue is bool flag)
            {
                result = flag;
                return true;
            }

            error = $"'{left}' is not a bool.";
            return false;
        }

        if (!TryResolve(right!, locals, out var rightValue, out error, leftValue))
        {
            return false;
        }

        if (!TryOrder(leftValue, rightValue, out var order))
        {
            if (op is "==" or "!=")
            {
                result = Equals(leftValue, rightValue) == (op == "==");
                return true;
            }

            error = $"'{left}' and '{right}' cannot be ordered.";
            return false;
        }

        result = op switch
        {
            "==" => order == 0,
            "!=" => order != 0,
            "<" => order < 0,
            "<=" => order <= 0,
            ">" => order > 0,
            _ => order >= 0,
        };
        return true;
    }

    /// <summary>Compares numbers, strings, characters, booleans and nulls.</summary>
    private static bool TryOrder(object? left, object? right, out int order)
    {
        order = 0;
        switch (left, right)
        {
            case (null, null):
                return true;
            case (null, _) or (_, null):
                order = left is null ? -1 : 1;
                return true;
            case (string a, string b):
                order = string.CompareOrdinal(a, b);
                return true;
            case (char a, char b):
                order = a.CompareTo(b);
                return true;
            case (bool a, bool b):
                order = a.CompareTo(b);
                return true;
        }

        if (IsNumber(left) && IsNumber(right))
        {
            order = Convert.ToDouble(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(right, CultureInfo.InvariantCulture));
            return true;
        }

        return false;
    }

    private static bool IsNumber(object? value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    /// <summary>Resolves a literal or a path. An enum member name resolves against the type of the other side.</summary>
    private static bool TryResolve(string text, IReadOnlyList<DebugValue> locals, out object? value, out string? error, object? other = null)
    {
        error = null;
        value = null;
        text = text.Trim();
        switch (text)
        {
            case "null":
                return true;
            case "true":
                value = true;
                return true;
            case "false":
                value = false;
                return true;
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            value = text[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
            return true;
        }

        if (text.Length == 3 && text[0] == '\'' && text[^1] == '\'')
        {
            value = text[1];
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            value = number;
            return true;
        }

        if (other is Enum && Enum.TryParse(other.GetType(), text.Split('.')[^1], out var member))
        {
            value = member;
            return true;
        }

        if (ValuePath.Find(locals, text) is { } found)
        {
            value = found.Object;
            return true;
        }

        error = $"'{text}' is not a variable in scope.";
        return false;
    }

    private static (string Left, string? Operator, string? Right) SplitComparison(string comparison)
    {
        var quoted = false;
        for (var i = 0; i < comparison.Length; i++)
        {
            if (comparison[i] == '"')
            {
                quoted = !quoted;
            }

            if (quoted)
            {
                continue;
            }

            foreach (var op in Operators)
            {
                if (string.CompareOrdinal(comparison, i, op, 0, op.Length) == 0)
                {
                    return (comparison[..i], op, comparison[(i + op.Length)..]);
                }
            }
        }

        return (comparison, null, null);
    }

    private static List<string> SplitOutsideQuotes(string text, string separator)
    {
        var parts = new List<string>();
        var quoted = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0)
            {
                parts.Add(text[start..i]);
                start = i + separator.Length;
                i += separator.Length - 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }
}
