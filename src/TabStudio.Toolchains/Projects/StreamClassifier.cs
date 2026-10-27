namespace TabStudio.Toolchains.Projects;

/// <summary>A piece of a stream's text and what it says, for the Output panel's colors.</summary>
/// <param name="Text">The text, which ends with a line break unless the line has not ended yet.</param>
/// <param name="Kind">What its line says.</param>
public readonly record struct ClassifiedText(string Text, OutputLineKind Kind);

/// <summary>
/// Splits one stream's output, as it arrives in arbitrary pieces, into lines that <see cref="OutputClassifier"/>
/// classifies. A line that has not ended yet, such as a prompt, is passed on at once, classified by what it says
/// so far, and the rest of it by the whole line once it ends.
/// </summary>
public sealed class StreamClassifier
{
    private const int MaxLine = 4096;
    private readonly OutputClassifier _classifier;
    private string _partial = string.Empty;

    /// <summary>Creates the splitter for one stream.</summary>
    /// <param name="toolchainId">The toolchain that runs the program, such as <c>python</c>, or <see langword="null"/>.</param>
    public StreamClassifier(string? toolchainId = null) => _classifier = new OutputClassifier(toolchainId);

    /// <summary>Classifies the next piece of the stream.</summary>
    /// <param name="text">The text, with <c>\n</c> line breaks.</param>
    /// <returns>The text, split where its lines' kinds change, in order.</returns>
    public IReadOnlyList<ClassifiedText> Feed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<ClassifiedText>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                var rest = text[start..];
                result.Add(new(rest, _classifier.Peek(_partial + rest)));
                _partial = _partial.Length + rest.Length > MaxLine ? (_partial + rest)[^MaxLine..] : _partial + rest;
                break;
            }

            var segment = text[start..end];
            result.Add(new(segment + "\n", _classifier.Classify(_partial + segment)));
            _partial = string.Empty;
            start = end + 1;
        }

        // Neighbours of the same kind become one piece, so the panel adds one colored range for them.
        for (var i = result.Count - 1; i > 0; i--)
        {
            if (result[i].Kind == result[i - 1].Kind)
            {
                result[i - 1] = result[i - 1] with { Text = result[i - 1].Text + result[i].Text };
                result.RemoveAt(i);
            }
        }

        return result;
    }
}
