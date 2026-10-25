using System.Globalization;
using System.Text;

namespace TabStudio.Toolchains.Terminals;

/// <summary>
/// How many terminal columns a character takes, as <c>wcwidth</c> says: 2 for East Asian wide characters and
/// emoji, 0 for combining marks and other zero-width characters, 1 for the rest.
/// </summary>
public static class CharacterWidth
{
    // Wide and fullwidth ranges (Unicode's East Asian Width W and F), and the emoji blocks terminals draw wide.
    private static readonly (int First, int Last)[] Wide =
    [
        (0x1100, 0x115F), (0x231A, 0x231B), (0x2329, 0x232A), (0x23E9, 0x23EC), (0x23F0, 0x23F0), (0x23F3, 0x23F3),
        (0x25FD, 0x25FE), (0x2614, 0x2615), (0x2648, 0x2653), (0x267F, 0x267F), (0x2693, 0x2693), (0x26A1, 0x26A1),
        (0x26AA, 0x26AB), (0x26BD, 0x26BE), (0x26C4, 0x26C5), (0x26CE, 0x26CE), (0x26D4, 0x26D4), (0x26EA, 0x26EA),
        (0x26F2, 0x26F3), (0x26F5, 0x26F5), (0x26FA, 0x26FA), (0x26FD, 0x26FD), (0x2705, 0x2705), (0x270A, 0x270B),
        (0x2728, 0x2728), (0x274C, 0x274C), (0x274E, 0x274E), (0x2753, 0x2755), (0x2757, 0x2757), (0x2795, 0x2797),
        (0x27B0, 0x27B0), (0x27BF, 0x27BF), (0x2B1B, 0x2B1C), (0x2B50, 0x2B50), (0x2B55, 0x2B55), (0x2E80, 0x303E),
        (0x3041, 0x33FF), (0x3400, 0x4DBF), (0x4E00, 0x9FFF), (0xA000, 0xA4CF), (0xA960, 0xA97F), (0xAC00, 0xD7A3),
        (0xF900, 0xFAFF), (0xFE10, 0xFE19), (0xFE30, 0xFE6F), (0xFF00, 0xFF60), (0xFFE0, 0xFFE6), (0x16FE0, 0x16FE4),
        (0x17000, 0x18CFF), (0x1B000, 0x1B2FF), (0x1F004, 0x1F004), (0x1F0CF, 0x1F0CF), (0x1F18E, 0x1F18E),
        (0x1F191, 0x1F19A), (0x1F200, 0x1F251), (0x1F300, 0x1F64F), (0x1F680, 0x1F6FF), (0x1F7E0, 0x1F7EB),
        (0x1F90C, 0x1F9FF), (0x1FA70, 0x1FAFF), (0x20000, 0x2FFFD), (0x30000, 0x3FFFD),
    ];

    /// <summary>The columns a character takes: 0, 1 or 2.</summary>
    /// <param name="rune">The character.</param>
    public static int Of(Rune rune)
    {
        var value = rune.Value;
        if (value < 0x300)
        {
            return value is < 0x20 or (>= 0x7F and < 0xA0) ? 0 : 1;
        }

        if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        if (value is >= 0x1160 and <= 0x11FF or 0x200B)
        {
            // Hangul medial vowels and final consonants join the syllable before them; a zero-width space takes none.
            return 0;
        }

        return IsWide(value) ? 2 : 1;
    }

    private static bool IsWide(int value)
    {
        var low = 0;
        var high = Wide.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var (first, last) = Wide[middle];
            if (value < first)
            {
                high = middle - 1;
            }
            else if (value > last)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
