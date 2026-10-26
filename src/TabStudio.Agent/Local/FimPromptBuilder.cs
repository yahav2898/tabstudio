using System.Text;

namespace TabStudio.Agent.Local;

/// <summary>The fill-in-the-middle vocabulary of a code model.</summary>
public sealed record FimTokens(string Prefix, string Suffix, string Middle, IReadOnlyList<string> Stops)
{
    /// <summary>Qwen2.5-Coder's trained FIM tokens.</summary>
    public static FimTokens Qwen { get; } = new("<|fim_prefix|>", "<|fim_suffix|>", "<|fim_middle|>", ["<|fim_suffix|>", "<|fim_middle|>", "<|fim_prefix|>", "<|endoftext|>", "<|im_end|>", "<|file_sep|>"]);
}

/// <summary>An unsaved open file, supplied from the editor rather than read from disk.</summary>
public sealed record OpenFileContext(string Path, string Text);

/// <summary>A bounded FIM request. No source text is sent over the network.</summary>
public sealed record FimPrompt(string Text, IReadOnlyList<string> Stops);

/// <summary>Builds language-independent FIM context, reserving room for the insertion and model tokens.</summary>
public static class FimPromptBuilder
{
    /// <summary>Builds prefix/suffix context and short excerpts from up to three open neighbors.</summary>
    public static FimPrompt Build(string path, string text, int caret, IEnumerable<OpenFileContext>? neighbors = null, FimTokens? tokens = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(caret);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(caret, text.Length);
        tokens ??= FimTokens.Qwen;
        var nearby = new StringBuilder();
        foreach (var file in (neighbors ?? []).Where(f => !string.Equals(f.Path, path, StringComparison.Ordinal)).Take(3))
        {
            nearby.Append("\n<|file_sep|>").Append(Path.GetFileName(file.Path)).Append('\n').Append(Head(file.Text, 180));
        }

        // A UTF-8 byte budget is conservative even for tokenizers without English/code merges.
        var prefix = Tail(text[..caret], 1600);
        var suffix = Head(text[caret..], 600);
        return new FimPrompt($"{Head(nearby.ToString(), 600)}\n<|file_sep|>{Path.GetFileName(path)}\n{tokens.Prefix}{prefix}{tokens.Suffix}{suffix}{tokens.Middle}", tokens.Stops);
    }

    /// <summary>Removes generated stop tokens and an echoed suffix.</summary>
    public static string Clean(string insertion, IReadOnlyList<string> stops, string suffix)
    {
        var end = insertion.Length;
        foreach (var stop in stops)
        {
            var index = insertion.IndexOf(stop, StringComparison.Ordinal);
            if (index >= 0) end = Math.Min(end, index);
        }

        insertion = insertion[..end];
        // Strip only a complete suffix echo, preserving short intentional overlaps such as braces.
        if (suffix.Length >= 8 && insertion.EndsWith(suffix, StringComparison.Ordinal)) insertion = insertion[..^suffix.Length];
        return insertion;
    }

    /// <summary>The leading complete Unicode characters fitting a UTF-8 byte budget.</summary>
    public static string Head(string text, int bytes)
    {
        var count = 0;
        var used = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (used + rune.Utf8SequenceLength > bytes) break;
            used += rune.Utf8SequenceLength;
            count += rune.Utf16SequenceLength;
        }

        return text[..count];
    }

    private static string Tail(string text, int bytes)
    {
        var start = Math.Max(0, text.Length - bytes);
        if (start > 0 && char.IsLowSurrogate(text[start])) start++;
        while (Encoding.UTF8.GetByteCount(text.AsSpan(start)) > bytes)
        {
            start++;
            if (start < text.Length && char.IsLowSurrogate(text[start])) start++;
        }

        return text[start..];
    }
}
