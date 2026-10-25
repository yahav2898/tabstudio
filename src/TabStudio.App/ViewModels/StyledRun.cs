using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A piece of text in a tooltip, signature or completion item, colored like code and optionally bold. View models
/// produce runs; <see cref="Controls.StyledTextBlock"/> draws them.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Classification">The Roslyn classification that sets its color, or <see langword="null"/> for plain text.</param>
/// <param name="IsBold">Whether it is bold, such as the matched letters of a completion item or the active parameter.</param>
public sealed record StyledRun(string Text, string? Classification = null, bool IsBold = false)
{
    /// <summary>Converts the tagged text of the language service.</summary>
    /// <param name="runs">The tagged runs.</param>
    /// <param name="bold">Whether every run is bold.</param>
    public static IReadOnlyList<StyledRun> From(IEnumerable<TaggedRun> runs, bool bold = false)
    {
        ArgumentNullException.ThrowIfNull(runs);
        return runs.Select(r => new StyledRun(r.Text, r.Classification, bold)).ToList();
    }

    /// <summary>Splits text into plain and bold runs at the given character positions.</summary>
    /// <param name="text">The text.</param>
    /// <param name="boldIndices">The positions of the bold characters, in ascending order.</param>
    /// <param name="classification">The classification of the whole text, or <see langword="null"/>.</param>
    public static IReadOnlyList<StyledRun> WithBold(string text, IReadOnlyList<int> boldIndices, string? classification = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(boldIndices);
        var runs = new List<StyledRun>();
        var start = 0;
        var bold = false;
        var next = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            while (next < boldIndices.Count && boldIndices[next] < i)
            {
                next++;
            }

            var isBold = i < text.Length && next < boldIndices.Count && boldIndices[next] == i;
            if (i == text.Length || isBold != bold)
            {
                if (i > start)
                {
                    runs.Add(new StyledRun(text[start..i], classification, bold));
                }

                start = i;
                bold = isBold;
            }
        }

        return runs;
    }
}
