using System.Globalization;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Evaluates a breakpoint condition against the values a debug adapter shows for the paused frame's locals, for an
/// adapter that cannot evaluate it itself: Java's debugger compiles conditions as Java against a JDT project, so in
/// a Kotlin file it ignores them and stops on every hit.
/// </summary>
/// <remarks>
/// Conditions compare locals with literals or with each other (<c>i == 2</c>, <c>name != null</c>,
/// <c>count &gt;= 10 &amp;&amp; total &lt; 3.5</c>), joined by <c>&amp;&amp;</c> and <c>||</c>; a bare local is true when its
/// value is <c>true</c>. Numbers compare as numbers, anything else as text, with a string's quotes removed. A
/// condition that is not of this form cannot be evaluated, and the breakpoint stops, as Visual Studio stops at a
/// condition it cannot evaluate.
/// </remarks>
public static class AdapterCondition
{
    private static readonly string[] Operators = ["==", "!=", ">=", "<=", ">", "<"];

    /// <summary>Evaluates a condition.</summary>
    /// <param name="condition">The condition, such as <c>i == 2</c>.</param>
    /// <param name="locals">The frame's locals: each name and the value the adapter shows.</param>
    /// <param name="met">Whether it holds.</param>
    /// <returns><see langword="false"/> when the condition cannot be evaluated.</returns>
    public static bool TryEvaluate(string condition, IReadOnlyDictionary<string, string> locals, out bool met)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(locals);
        met = false;
        foreach (var alternative in condition.Split("||"))
        {
            var all = true;
            foreach (var part in alternative.Split("&&"))
            {
                if (!TryCompare(part.Trim(), locals, out var holds))
                {
                    return false;
                }

                all &= holds;
            }

            met |= all;
        }

        return true;
    }

    private static bool TryCompare(string comparison, IReadOnlyDictionary<string, string> locals, out bool holds)
    {
        holds = false;
        if (comparison.Length == 0)
        {
            return false;
        }

        foreach (var op in Operators)
        {
            var at = comparison.IndexOf(op, StringComparison.Ordinal);
            if (at <= 0)
            {
                continue;
            }

            if (!TryOperand(comparison[..at].Trim(), locals, out var left) || !TryOperand(comparison[(at + op.Length)..].Trim(), locals, out var right))
            {
                return false;
            }

            var numbers = double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                & double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var b);
            if (!numbers && op is not ("==" or "!="))
            {
                return false;
            }

            var order = numbers ? a.CompareTo(b) : string.CompareOrdinal(left, right);
            holds = op switch
            {
                "==" => order == 0,
                "!=" => order != 0,
                ">=" => order >= 0,
                "<=" => order <= 0,
                ">" => order > 0,
                _ => order < 0,
            };
            return true;
        }

        // A bare local, or true or false.
        if (TryOperand(comparison, locals, out var value) && value is "true" or "false")
        {
            holds = value == "true";
            return true;
        }

        return false;
    }

    /// <summary>A literal's value, or a local's, without a string's quotes.</summary>
    private static bool TryOperand(string operand, IReadOnlyDictionary<string, string> locals, out string value)
    {
        value = operand;
        if (operand.Length >= 2 && (operand[0] is '"' or '\'') && operand[^1] == operand[0])
        {
            value = operand[1..^1];
            return true;
        }

        if (operand is "null" or "true" or "false" || double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        // PHP's locals are named with their $, which a condition may leave out, or the other way round.
        if (!locals.TryGetValue(operand, out var shown) && !locals.TryGetValue(operand.TrimStart('$'), out shown) && !locals.TryGetValue("$" + operand, out shown))
        {
            return false;
        }

        shown = shown.Trim();
        value = shown.Length >= 2 && shown[0] == '"' && shown[^1] == '"' ? shown[1..^1] : shown;
        return true;
    }
}
