namespace TabStudio.App.Editing;

/// <summary>The expression a DataTip shows, and where the identifier under the pointer is in its line.</summary>
/// <param name="Expression">The expression, such as <c>order.Customer.Name</c>.</param>
/// <param name="Start">Zero-based column where the identifier under the pointer starts.</param>
/// <param name="Length">Length of that identifier.</param>
public readonly record struct DataTipTarget(string Expression, int Start, int Length);

/// <summary>
/// Finds what a DataTip evaluates for the identifier under the pointer, as Visual Studio does: the identifier and the
/// member accesses and indexes before it. Pointing at <c>Name</c> in <c>order.Customer.Name</c> evaluates
/// <c>order.Customer.Name</c>; pointing at <c>order</c> evaluates <c>order</c>.
/// </summary>
/// <remarks>
/// The debugger evaluates paths, not C#, so an expression that goes through a call, such as <c>GetOrder().Total</c>,
/// has no DataTip.
/// </remarks>
public static class DataTipExpression
{
    /// <summary>Finds the DataTip expression at a column of a line.</summary>
    /// <param name="line">The text of the line.</param>
    /// <param name="column">Zero-based column under the pointer.</param>
    /// <returns>The target, or <see langword="null"/> when the column is not on an identifier that can be evaluated.</returns>
    public static DataTipTarget? Find(string line, int column)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (column < 0 || column > line.Length)
        {
            return null;
        }

        var position = column < line.Length && IsIdentifierPart(line[column]) ? column
            : column > 0 && IsIdentifierPart(line[column - 1]) ? column - 1
            : -1;
        if (position < 0)
        {
            return null;
        }

        var start = position;
        while (start > 0 && IsIdentifierPart(line[start - 1]))
        {
            start--;
        }

        var end = position + 1;
        while (end < line.Length && IsIdentifierPart(line[end]))
        {
            end++;
        }

        if (!IsIdentifierStart(line[start]))
        {
            return null;
        }

        var expressionStart = start;
        while (expressionStart > 0 && line[expressionStart - 1] == '.')
        {
            expressionStart = SegmentStart(line, expressionStart - 1);
            if (expressionStart < 0)
            {
                return null;
            }
        }

        return new DataTipTarget(line[expressionStart..end], start, end - start);
    }

    /// <summary>
    /// The start of the identifier, with the indexes that follow it, which ends just before <paramref name="end"/>:
    /// <c>items[0]</c> in <c>items[0].Name</c>.
    /// </summary>
    /// <returns>The start, or -1 when there is no such identifier, as after a call.</returns>
    private static int SegmentStart(string line, int end)
    {
        var i = end;
        while (i > 0 && line[i - 1] == ']')
        {
            i = MatchingOpenBracket(line, i - 1);
            if (i < 0)
            {
                return -1;
            }
        }

        var identifierEnd = i;
        while (i > 0 && IsIdentifierPart(line[i - 1]))
        {
            i--;
        }

        return i < identifierEnd && IsIdentifierStart(line[i]) ? i : -1;
    }

    /// <summary>The <c>[</c> that the <c>]</c> at <paramref name="close"/> closes, skipping string literals.</summary>
    private static int MatchingOpenBracket(string line, int close)
    {
        var depth = 0;
        var inString = false;
        for (var i = close; i >= 0; i--)
        {
            var c = line[i];
            if (c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString && c == ']')
            {
                depth++;
            }
            else if (!inString && c == '[' && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
