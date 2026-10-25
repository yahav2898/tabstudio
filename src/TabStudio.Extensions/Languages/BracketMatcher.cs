namespace TabStudio.Extensions.Languages;

/// <summary>A bracket next to the caret and the bracket that matches it.</summary>
/// <param name="OpenStart">Where the opening bracket starts.</param>
/// <param name="OpenLength">The opening bracket's length.</param>
/// <param name="CloseStart">Where the closing bracket starts.</param>
/// <param name="CloseLength">The closing bracket's length.</param>
public sealed record BracketMatch(int OpenStart, int OpenLength, int CloseStart, int CloseLength);

/// <summary>Finds the bracket that matches the one next to the caret, counting nested pairs of the same kind.</summary>
public static class BracketMatcher
{
    /// <summary>How far from the caret the matching bracket is looked for.</summary>
    public const int MaximumDistance = 100_000;

    /// <summary>Finds the bracket pair at the caret: the bracket just before it, or else the one just after it.</summary>
    /// <param name="text">The document's text.</param>
    /// <param name="caret">The caret offset.</param>
    /// <param name="brackets">The language's brackets.</param>
    /// <param name="isCode">Whether an offset is in code rather than a string or comment, where brackets do not count; <see langword="null"/> counts everywhere.</param>
    /// <returns>The pair, or <see langword="null"/> when there is no bracket at the caret or it has no match.</returns>
    public static BracketMatch? Find(string text, int caret, IReadOnlyList<BracketPair> brackets, Func<int, bool>? isCode = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(brackets);
        var pairs = brackets.Where(b => b.Open.Length > 0 && b.Close.Length > 0 && b.Open != b.Close).ToList();
        if (pairs.Count == 0 || caret < 0 || caret > text.Length)
        {
            return null;
        }

        // The bracket ending at the caret first, as when the caret was just placed after a closing bracket.
        foreach (var pair in pairs)
        {
            if (EndsAt(text, caret, pair.Close) && IsCode(isCode, caret - pair.Close.Length))
            {
                return FindOpen(text, caret - pair.Close.Length, pair, isCode);
            }

            if (EndsAt(text, caret, pair.Open) && IsCode(isCode, caret - pair.Open.Length))
            {
                return FindClose(text, caret - pair.Open.Length, pair, isCode);
            }
        }

        foreach (var pair in pairs)
        {
            if (StartsAt(text, caret, pair.Open) && IsCode(isCode, caret))
            {
                return FindClose(text, caret, pair, isCode);
            }

            if (StartsAt(text, caret, pair.Close) && IsCode(isCode, caret))
            {
                return FindOpen(text, caret, pair, isCode);
            }
        }

        return null;
    }

    private static BracketMatch? FindClose(string text, int openStart, BracketPair pair, Func<int, bool>? isCode)
    {
        var depth = 0;
        var limit = Math.Min(text.Length, openStart + MaximumDistance);
        for (var i = openStart; i < limit; i++)
        {
            if (StartsAt(text, i, pair.Open) && IsCode(isCode, i))
            {
                depth++;
                i += pair.Open.Length - 1;
            }
            else if (StartsAt(text, i, pair.Close) && IsCode(isCode, i))
            {
                depth--;
                if (depth == 0)
                {
                    return new BracketMatch(openStart, pair.Open.Length, i, pair.Close.Length);
                }

                i += pair.Close.Length - 1;
            }
        }

        return null;
    }

    private static BracketMatch? FindOpen(string text, int closeStart, BracketPair pair, Func<int, bool>? isCode)
    {
        var depth = 0;
        var limit = Math.Max(0, closeStart - MaximumDistance);
        for (var end = closeStart + pair.Close.Length; end > limit; end--)
        {
            if (EndsAt(text, end, pair.Close) && IsCode(isCode, end - pair.Close.Length))
            {
                depth++;
                end -= pair.Close.Length - 1;
            }
            else if (EndsAt(text, end, pair.Open) && IsCode(isCode, end - pair.Open.Length))
            {
                depth--;
                if (depth == 0)
                {
                    return new BracketMatch(end - pair.Open.Length, pair.Open.Length, closeStart, pair.Close.Length);
                }

                end -= pair.Open.Length - 1;
            }
        }

        return null;
    }

    private static bool StartsAt(string text, int index, string value) =>
        index >= 0 && index + value.Length <= text.Length && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

    private static bool EndsAt(string text, int end, string value) => StartsAt(text, end - value.Length, value);

    private static bool IsCode(Func<int, bool>? isCode, int offset) => isCode?.Invoke(offset) ?? true;
}
