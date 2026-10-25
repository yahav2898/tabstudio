using System.Text;
using System.Text.RegularExpressions;
using TabStudio.Compilation.Features;
using TabStudio.LanguageServers.Features;

namespace TabStudio.App.Services.LanguageServers;

/// <summary>
/// Turns a language server's Markdown into the colored runs Quick Info, Parameter Info and the completion list show:
/// code blocks become code, colored the way Roslyn colors C#; the rest becomes text without its Markdown marks.
/// </summary>
internal static partial class MarkupRuns
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        // Python
        "def", "class", "return", "if", "elif", "else", "for", "while", "in", "is", "not", "and", "or", "import",
        "from", "as", "with", "lambda", "None", "True", "False", "pass", "yield", "async", "await", "raise", "try",
        "except", "finally", "global", "nonlocal", "del", "assert", "break", "continue", "self", "cls",
        // TypeScript and JavaScript
        "function", "const", "let", "var", "interface", "type", "enum", "new", "this", "extends", "implements",
        "export", "typeof", "keyof", "readonly", "public", "private", "protected", "static", "void", "any", "number",
        "string", "boolean", "unknown", "never", "undefined", "null", "true", "false", "declare", "namespace",
        "module", "default", "abstract", "get", "set", "of", "constructor",
        // C, C++, Rust, Go and Java
        "int", "long", "short", "char", "float", "double", "bool", "unsigned", "signed", "struct", "union", "auto",
        "fn", "pub", "impl", "trait", "mut", "use", "mod", "crate", "Self", "func", "package", "chan", "map", "go",
        "defer", "final", "throws", "byte", "template", "typename", "virtual", "override", "inline", "constexpr",
    };

    /// <summary>The sections of a hover: the first code block as the signature, then the documentation.</summary>
    /// <param name="markup">The hover's contents.</param>
    public static List<QuickInfoSection> ToSections(MarkupText markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        var sections = new List<QuickInfoSection>();
        var documentation = new List<TaggedRun>();
        var hasSignature = false;
        foreach (var block in Blocks(markup))
        {
            if (block.IsCode && !hasSignature)
            {
                hasSignature = true;
                sections.Add(new QuickInfoSection(QuickInfoSectionKind.Description, Code(block.Text)));
                continue;
            }

            if (block.IsRule)
            {
                Flush(sections, documentation);
                continue;
            }

            if (documentation.Count > 0)
            {
                documentation.Add(new TaggedRun("\n", TaggedTextKind.LineBreak));
            }

            documentation.AddRange(block.IsCode ? Code(block.Text) : Text(block.Text, markup.IsMarkdown));
        }

        Flush(sections, documentation);
        return sections;
    }

    /// <summary>All of a text as runs, code blocks colored as code.</summary>
    /// <param name="markup">The text.</param>
    public static List<TaggedRun> ToRuns(MarkupText markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        var runs = new List<TaggedRun>();
        foreach (var block in Blocks(markup).Where(b => !b.IsRule))
        {
            if (runs.Count > 0)
            {
                runs.Add(new TaggedRun("\n", TaggedTextKind.LineBreak));
            }

            runs.AddRange(block.IsCode ? Code(block.Text) : Text(block.Text, markup.IsMarkdown));
        }

        return runs;
    }

    /// <summary>The text of Markdown without its marks, for plain text such as a parameter's documentation.</summary>
    /// <param name="markup">The text.</param>
    public static string ToPlainText(MarkupText? markup) =>
        markup is null ? string.Empty : string.Concat(ToRuns(markup).Select(r => r.Text)).Trim();

    /// <summary>
    /// Code as runs: keywords, strings, numbers, punctuation, the names of functions being declared or called, their
    /// parameters, and type names after <c>:</c> or <c>-&gt;</c>.
    /// </summary>
    /// <param name="code">A line or block of code, such as a signature.</param>
    public static List<TaggedRun> Code(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var runs = new List<TaggedRun>();
        var depth = 0;
        var expectType = false;
        var i = 0;
        while (i < code.Length)
        {
            var c = code[i];
            if (c == '\n')
            {
                runs.Add(new TaggedRun("\n", TaggedTextKind.LineBreak));
                expectType = false;
                i++;
            }
            else if (char.IsWhiteSpace(c))
            {
                var start = i;
                while (i < code.Length && char.IsWhiteSpace(code[i]) && code[i] != '\n')
                {
                    i++;
                }

                runs.Add(new TaggedRun(code[start..i], TaggedTextKind.Space));
            }
            else if (c is '"' or '\'' or '`')
            {
                var start = i++;
                while (i < code.Length && code[i] != c && code[i] != '\n')
                {
                    i += code[i] == '\\' ? 2 : 1;
                }

                i = Math.Min(code.Length, i + 1);
                runs.Add(new TaggedRun(code[start..i], TaggedTextKind.StringLiteral));
                expectType = false;
            }
            else if (char.IsAsciiDigit(c))
            {
                var start = i;
                while (i < code.Length && (char.IsAsciiLetterOrDigit(code[i]) || code[i] is '.' or '_'))
                {
                    i++;
                }

                runs.Add(new TaggedRun(code[start..i], TaggedTextKind.NumericLiteral));
                expectType = false;
            }
            else if (char.IsLetter(c) || c is '_' or '$')
            {
                var start = i;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '$'))
                {
                    i++;
                }

                var word = code[start..i];
                runs.Add(new TaggedRun(word, KindOf(word, code, start, i, depth, expectType)));
                expectType = expectType && Next(code, i) is '.' or '[' or '|';
            }
            else
            {
                var start = i;
                if ((c == '-' && Next(code, i + 1) == '>') || (c == '=' && Next(code, i + 1) == '>'))
                {
                    i += 2;
                    expectType = true;
                }
                else
                {
                    depth += c is '(' or '[' or '{' or '<' ? 1 : c is ')' or ']' or '}' or '>' ? -1 : 0;
                    expectType = c is ':' or '|' || (expectType && c is '[' or '<' or '.');
                    i++;
                }

                runs.Add(new TaggedRun(code[start..i], c is '=' or '+' or '-' or '*' or '/' or '%' or '!' or '&' or '|' ? TaggedTextKind.Operator : TaggedTextKind.Punctuation));
            }
        }

        return runs;
    }

    /// <summary>Text as runs with its line breaks; Markdown without its marks.</summary>
    private static List<TaggedRun> Text(string text, bool isMarkdown)
    {
        var runs = new List<TaggedRun>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                runs.Add(new TaggedRun("\n", TaggedTextKind.LineBreak));
            }

            var line = isMarkdown ? StripMarks(lines[i].TrimEnd()) : lines[i].TrimEnd();
            if (line.Length > 0)
            {
                runs.Add(new TaggedRun(line, TaggedTextKind.Text));
            }
        }

        return runs;
    }

    private static string StripMarks(string line)
    {
        var text = line.TrimStart();
        var indent = line.Length - text.Length;
        if (text.StartsWith('#'))
        {
            text = text.TrimStart('#').TrimStart();
        }
        else if (text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal))
        {
            text = "• " + text[2..];
        }
        else if (text.StartsWith("> ", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        text = LinkPattern().Replace(text, "$1");
        text = EmphasisPattern().Replace(text, "$2");
        text = text.Replace("`", string.Empty, StringComparison.Ordinal);
        text = TagPattern().Replace(text, string.Empty);
        text = EscapePattern().Replace(text, "$1");
        text = text.Replace("&nbsp;", " ", StringComparison.Ordinal).Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
        return new string(' ', Math.Min(indent, 8)) + text;
    }

    private static TaggedTextKind KindOf(string word, string code, int start, int end, int depth, bool expectType)
    {
        // Pyright starts a hover with what the name is, such as "(function)".
        if (start > 0 && code[start - 1] == '(' && Next(code, end) == ')' && depth == 1 && start == 1)
        {
            return TaggedTextKind.Text;
        }

        if (Keywords.Contains(word))
        {
            return TaggedTextKind.Keyword;
        }

        if (expectType)
        {
            return TaggedTextKind.Class;
        }

        if (Next(code, end) == '(' || (Next(code, end) == '<' && depth == 0))
        {
            return TaggedTextKind.Method;
        }

        // A parameter: after "(", "," or the "..." of a rest parameter, before its type, default or the next one.
        var previous = Previous(code, start);
        if (depth > 0 && (previous is '(' or ',' or '.') && (Next(code, end) is ':' or ',' or ')' or '=' or '?'))
        {
            return TaggedTextKind.Parameter;
        }

        return char.IsUpper(word[0]) ? TaggedTextKind.Class : TaggedTextKind.Local;
    }

    private static char Next(string code, int index)
    {
        while (index < code.Length && code[index] == ' ')
        {
            index++;
        }

        return index < code.Length ? code[index] : '\0';
    }

    private static char Previous(string code, int index)
    {
        index--;
        while (index >= 0 && code[index] == ' ')
        {
            index--;
        }

        return index >= 0 ? code[index] : '\0';
    }

    private static void Flush(List<QuickInfoSection> sections, List<TaggedRun> documentation)
    {
        while (documentation.Count > 0 && string.IsNullOrWhiteSpace(documentation[^1].Text))
        {
            documentation.RemoveAt(documentation.Count - 1);
        }

        if (documentation.Count > 0)
        {
            sections.Add(new QuickInfoSection(QuickInfoSectionKind.Documentation, [.. documentation]));
        }

        documentation.Clear();
    }

    /// <summary>The blocks of Markdown: fenced code, rules (<c>---</c>), and text between them.</summary>
    private static List<(bool IsCode, bool IsRule, string Text)> Blocks(MarkupText markup)
    {
        var blocks = new List<(bool IsCode, bool IsRule, string Text)>();
        var value = markup.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n');
        if (!markup.IsMarkdown)
        {
            blocks.Add((false, false, value));
            return blocks;
        }

        var text = new StringBuilder();
        var code = new StringBuilder();
        var inCode = false;
        foreach (var line in value.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    blocks.Add((true, false, code.ToString().TrimEnd('\n')));
                    code.Clear();
                }
                else
                {
                    AddText(blocks, text);
                }

                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                code.Append(line).Append('\n');
            }
            else if (trimmed is "---" or "***" or "___")
            {
                AddText(blocks, text);
                blocks.Add((false, true, string.Empty));
            }
            else
            {
                text.Append(line).Append('\n');
            }
        }

        if (inCode)
        {
            blocks.Add((true, false, code.ToString().TrimEnd('\n')));
        }

        AddText(blocks, text);
        return blocks;
    }

    private static void AddText(List<(bool IsCode, bool IsRule, string Text)> blocks, StringBuilder text)
    {
        var value = text.ToString().Trim('\n');
        text.Clear();
        if (value.Trim().Length > 0)
        {
            blocks.Add((false, false, value));
        }
    }

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"(?<!\w)(\*\*|__|\*|_)(?=\S)(.+?)(?<=\S)\1(?!\w)")]
    private static partial Regex EmphasisPattern();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\\([\\`*_{}\[\]()#+\-.!])")]
    private static partial Regex EscapePattern();
}
