using System.Globalization;

namespace TabStudio.Debugging;

/// <summary>How a hit count compares the times a breakpoint was reached with its number.</summary>
public enum BreakpointHitKind
{
    /// <summary>Stops on that hit only: <c>5</c> or <c>= 5</c>.</summary>
    Equal,

    /// <summary>Stops on that hit and every one after it: <c>&gt;= 5</c>.</summary>
    AtLeast,

    /// <summary>Stops on every hit that is a multiple of the number: <c>% 5</c>.</summary>
    MultipleOf,
}

/// <summary>
/// A breakpoint's hit count, as Visual Studio's breakpoint settings write it: stop when the line was reached exactly
/// that many times (<c>5</c>, <c>= 5</c> or <c>== 5</c>), that many times or more (<c>&gt;= 5</c>), or a multiple of
/// that many times (<c>% 5</c> or <c>multiple of 5</c>). A breakpoint with a condition counts only the hits where its
/// condition holds.
/// </summary>
/// <remarks>
/// The C# and Visual Basic debugger counts hits itself, and so does the IDE for debug adapters, whose own hit
/// conditions mean different things from one adapter to the next (a bare number stops once in one and on every later
/// hit in another).
/// </remarks>
/// <param name="Kind">How the count compares.</param>
/// <param name="Count">The number of hits, 1 or more.</param>
public sealed record BreakpointHitCondition(BreakpointHitKind Kind, int Count)
{
    /// <summary>What the forms of a hit count are, for a prompt or an error message.</summary>
    public const string Forms = "5 (the fifth time only), >= 5 (the fifth time and after) or % 5 (every fifth time)";

    /// <summary>Reads a hit count as it is typed, such as <c>5</c>, <c>&gt;= 10</c> or <c>% 2</c>.</summary>
    /// <param name="text">The text; blank for none.</param>
    /// <param name="condition">The hit count, or <see langword="null"/> when the text is blank or not a hit count.</param>
    /// <returns><see langword="true"/> when the text is blank or a hit count.</returns>
    public static bool TryParse(string? text, out BreakpointHitCondition? condition)
    {
        condition = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var value = text.Trim();
        var kind = BreakpointHitKind.Equal;
        if (value.StartsWith(">=", StringComparison.Ordinal))
        {
            kind = BreakpointHitKind.AtLeast;
            value = value[2..];
        }
        else if (value.StartsWith("==", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        else if (value.StartsWith('=') || value.StartsWith('%'))
        {
            kind = value[0] == '%' ? BreakpointHitKind.MultipleOf : BreakpointHitKind.Equal;
            value = value[1..];
        }
        else if (value.StartsWith("multiple of", StringComparison.OrdinalIgnoreCase))
        {
            kind = BreakpointHitKind.MultipleOf;
            value = value["multiple of".Length..];
        }

        if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
        {
            return false;
        }

        condition = new BreakpointHitCondition(kind, count);
        return true;
    }

    /// <summary>Reads a hit count, or returns <see langword="null"/> when the text is blank or not one.</summary>
    /// <param name="text">The text.</param>
    public static BreakpointHitCondition? Parse(string? text) => TryParse(text, out var condition) ? condition : null;

    /// <summary>Whether the breakpoint stops on a hit.</summary>
    /// <param name="hits">How many times it was hit, this hit included.</param>
    public bool IsMet(int hits) => Kind switch
    {
        BreakpointHitKind.AtLeast => hits >= Count,
        BreakpointHitKind.MultipleOf => hits % Count == 0,
        _ => hits == Count,
    };

    /// <summary>The hit count as it is written: <c>= 5</c>, <c>&gt;= 5</c> or <c>% 5</c>.</summary>
    public override string ToString() => Kind switch
    {
        BreakpointHitKind.AtLeast => ">= ",
        BreakpointHitKind.MultipleOf => "% ",
        _ => "= ",
    } + Count.ToString(CultureInfo.InvariantCulture);
}
