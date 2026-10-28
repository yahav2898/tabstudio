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
/// <para>
/// In Visual Basic code a condition is written as Visual Basic writes it: comparisons with <c>=</c>, <c>&lt;&gt;</c>,
/// <c>Is</c> and <c>IsNot</c>, joined by <c>AndAlso</c>, <c>And</c>, <c>OrElse</c> or <c>Or</c>, each optionally
/// preceded by <c>Not</c>, with the literals <c>Nothing</c>, <c>True</c>, <c>False</c>, <c>"string"</c> and <c>"c"c</c>,
/// and names in any case. Examples: <c>count = 3</c>, <c>name IsNot Nothing AndAlso total &gt; 10.5</c>. The C# operators
/// work there too.
/// </para>
/// <para>This is not a C# or Visual Basic evaluator: method calls and arithmetic are not supported.</para>
/// </remarks>
internal static class BreakpointCondition
{
    private static readonly string[] Operators = ["==", "!=", "<=", ">=", "<", ">"];

    /// <summary>Visual Basic's operators first, longest first, then the C# ones that are not also Visual Basic's.</summary>
    private static readonly string[] VisualBasicOperators = ["<>", "<=", ">=", "==", "!=", "=", "<", ">"];

    private static readonly string[] CSharpOr = ["||"];
    private static readonly string[] CSharpAnd = ["&&"];
    private static readonly string[] VisualBasicOr = ["OrElse", "Or", "||"];
    private static readonly string[] VisualBasicAnd = ["AndAlso", "And", "&&"];

    /// <summary>Whether a C# condition holds.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="locals">The locals of the statement.</param>
    /// <param name="error">Why the condition could not be evaluated, when it could not.</param>
    /// <returns>The result; <see langword="true"/> when it could not be evaluated, so the breakpoint still stops.</returns>
    public static bool IsMet(string condition, IReadOnlyList<DebugValue> locals, out string? error) =>
        IsMet(condition, locals, visualBasic: false, out error);

    /// <summary>Whether a condition holds, written in C# or in Visual Basic.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="locals">The locals of the statement.</param>
    /// <param name="visualBasic">Whether the statement is Visual Basic, so the condition is too.</param>
    /// <param name="error">Why the condition could not be evaluated, when it could not.</param>
    /// <returns>The result; <see langword="true"/> when it could not be evaluated, so the breakpoint still stops.</returns>
    public static bool IsMet(string condition, IReadOnlyList<DebugValue> locals, bool visualBasic, out string? error)
    {
        error = null;
        foreach (var alternative in SplitOutsideQuotes(condition, visualBasic ? VisualBasicOr : CSharpOr))
        {
            var all = true;
            foreach (var comparison in SplitOutsideQuotes(alternative, visualBasic ? VisualBasicAnd : CSharpAnd))
            {
                if (!TryCompare(comparison.Trim(), locals, visualBasic, out var result, out error))
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

    private static bool TryCompare(string comparison, IReadOnlyList<DebugValue> locals, bool visualBasic, out bool result, out string? error)
    {
        // Not binds looser than a comparison in Visual Basic: Not x = 3 is Not (x = 3).
        var negate = visualBasic && StartsWithWord(comparison, "Not");
        if (negate)
        {
            comparison = comparison[3..].Trim();
        }

        if (!TryCompareOperands(comparison, locals, visualBasic, out result, out error))
        {
            return false;
        }

        result ^= negate;
        return true;
    }

    private static bool TryCompareOperands(string comparison, IReadOnlyList<DebugValue> locals, bool visualBasic, out bool result, out string? error)
    {
        result = false;
        error = null;
        var (left, op, right) = SplitComparison(comparison, visualBasic);
        if (!TryResolve(left, locals, visualBasic, out var leftValue, out error))
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

            error = $"'{left}' is not a {(visualBasic ? "Boolean" : "bool")}.";
            return false;
        }

        if (!TryResolve(right!, locals, visualBasic, out var rightValue, out error, leftValue))
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
    private static bool TryResolve(string text, IReadOnlyList<DebugValue> locals, bool visualBasic, out object? value, out string? error, object? other = null)
    {
        error = null;
        value = null;
        text = text.Trim();
        if (visualBasic ? TryResolveVisualBasicLiteral(text, out value) : TryResolveCSharpLiteral(text, out value))
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            value = number;
            return true;
        }

        if (other is Enum && Enum.TryParse(other.GetType(), text.Split('.')[^1], visualBasic, out var member))
        {
            value = member;
            return true;
        }

        if (ValuePath.Find(locals, text, visualBasic) is { } found)
        {
            value = found.Object;
            return true;
        }

        error = $"'{text}' is not a variable in scope.";
        return false;
    }

    private static bool TryResolveCSharpLiteral(string text, out object? value)
    {
        value = null;
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

        return false;
    }

    /// <summary>Visual Basic's <c>Nothing</c>, <c>True</c>, <c>False</c>, <c>"string"</c> and <c>"c"c</c>, in any case.</summary>
    private static bool TryResolveVisualBasicLiteral(string text, out object? value)
    {
        value = null;
        if (string.Equals(text, "Nothing", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(text, "True", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "False", StringComparison.OrdinalIgnoreCase))
        {
            value = string.Equals(text, "True", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        if (text.Length >= 4 && text[0] == '"' && text[^2] == '"' && (text[^1] == 'c' || text[^1] == 'C'))
        {
            var character = text[1..^2].Replace("\"\"", "\"", StringComparison.Ordinal);
            if (character.Length == 1)
            {
                value = character[0];
                return true;
            }
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            value = text[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
            return true;
        }

        return false;
    }

    private static (string Left, string? Operator, string? Right) SplitComparison(string comparison, bool visualBasic)
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

            foreach (var op in visualBasic ? VisualBasicOperators : Operators)
            {
                if (string.CompareOrdinal(comparison, i, op, 0, op.Length) == 0)
                {
                    var canonical = op switch
                    {
                        "<>" => "!=",
                        "=" => "==",
                        _ => op,
                    };
                    return (comparison[..i], canonical, comparison[(i + op.Length)..]);
                }
            }

            if (visualBasic && IsWordAt(comparison, i, "IsNot"))
            {
                return (comparison[..i], "!=", comparison[(i + "IsNot".Length)..]);
            }

            if (visualBasic && IsWordAt(comparison, i, "Is"))
            {
                return (comparison[..i], "==", comparison[(i + "Is".Length)..]);
            }
        }

        return (comparison, null, null);
    }

    private static List<string> SplitOutsideQuotes(string text, string[] separators)
    {
        var parts = new List<string>();
        var quoted = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (quoted)
            {
                continue;
            }

            foreach (var separator in separators)
            {
                var isWord = char.IsLetter(separator[0]);
                if (isWord ? IsWordAt(text, i, separator) : string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0)
                {
                    parts.Add(text[start..i]);
                    start = i + separator.Length;
                    i += separator.Length - 1;
                    break;
                }
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    private static bool StartsWithWord(string text, string word) => IsWordAt(text, 0, word);

    /// <summary>Whether a Visual Basic keyword, in any case, stands on its own at a position of the text.</summary>
    private static bool IsWordAt(string text, int index, string word) =>
        string.Compare(text, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
        index + word.Length <= text.Length &&
        (index == 0 || !IsIdentifierCharacter(text[index - 1])) &&
        (index + word.Length == text.Length || !IsIdentifierCharacter(text[index + word.Length]));

    private static bool IsIdentifierCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';
}
