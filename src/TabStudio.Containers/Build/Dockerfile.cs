using System.Text;
using System.Text.Json;

namespace TabStudio.Containers.Build;

/// <summary>A here-document of an instruction: <c>RUN &lt;&lt;EOF … EOF</c> or <c>COPY &lt;&lt;EOF /file</c>.</summary>
/// <param name="Name">Its delimiter.</param>
/// <param name="Content">Its text, with a final line break.</param>
/// <param name="Expand">Whether variables in it are expanded (an unquoted delimiter).</param>
/// <param name="StripTabs">Whether leading tabs were removed (<c>&lt;&lt;-</c>).</param>
public sealed record Heredoc(string Name, string Content, bool Expand, bool StripTabs);

/// <summary>One Dockerfile instruction.</summary>
/// <param name="Keyword">The instruction, upper case: <c>RUN</c>, <c>COPY</c>, …</param>
/// <param name="Flags">Its <c>--name=value</c> flags in order; <c>--mount</c> may repeat.</param>
/// <param name="Arguments">The rest of the line, with continuations joined.</param>
/// <param name="Line">The line it starts on, 1-based.</param>
public sealed record DockerfileInstruction(string Keyword, IReadOnlyList<KeyValuePair<string, string>> Flags, string Arguments, int Line)
{
    /// <summary>Its here-documents, in order.</summary>
    public IReadOnlyList<Heredoc> Heredocs { get; init; } = [];

    /// <summary>The instruction as written, for history and the build cache.</summary>
    public string Original { get; init; } = "";

    /// <summary>A flag's last value.</summary>
    public string? Flag(string name) => Flags.LastOrDefault(f => f.Key == name).Value;

    /// <summary>Every value of a flag that may repeat.</summary>
    public IEnumerable<string> FlagValues(string name) => Flags.Where(f => f.Key == name).Select(f => f.Value);

    /// <summary>The JSON array form (<c>["a", "b"]</c>), or <see langword="null"/> for the shell form.</summary>
    public IReadOnlyList<string>? JsonArguments
    {
        get
        {
            var text = Arguments.Trim();
            if (!text.StartsWith('[') || !text.EndsWith(']')) return null;
            try
            {
                return JsonSerializer.Deserialize<List<string>>(text);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}

/// <summary>One stage: from <c>FROM</c> to the next.</summary>
/// <param name="Index">Its position, from 0.</param>
/// <param name="BaseImage">What it starts from: an image, an earlier stage's name, or <c>scratch</c>.</param>
/// <param name="Name">Its <c>AS</c> name, if any.</param>
/// <param name="Platform">Its <c>--platform</c>, if any.</param>
/// <param name="Instructions">Its instructions after <c>FROM</c>.</param>
/// <param name="Line">The line of its <c>FROM</c>.</param>
public sealed record DockerfileStage(int Index, string BaseImage, string? Name, string? Platform, IReadOnlyList<DockerfileInstruction> Instructions, int Line);

/// <summary>A parsed Dockerfile.</summary>
/// <param name="GlobalArgs">The <c>ARG</c>s before the first <c>FROM</c>.</param>
/// <param name="Stages">The stages.</param>
/// <param name="Escape">The escape character, <c>\</c> or <c>`</c>.</param>
public sealed record Dockerfile(IReadOnlyList<DockerfileInstruction> GlobalArgs, IReadOnlyList<DockerfileStage> Stages, char Escape)
{
    /// <summary>The instructions this builder knows.</summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        "FROM", "RUN", "CMD", "LABEL", "MAINTAINER", "EXPOSE", "ENV", "ADD", "COPY", "ENTRYPOINT", "VOLUME", "USER",
        "WORKDIR", "ARG", "ONBUILD", "STOPSIGNAL", "HEALTHCHECK", "SHELL",
    };

    /// <summary>Finds a stage by name or number.</summary>
    public DockerfileStage? Stage(string nameOrIndex) =>
        Stages.FirstOrDefault(s => string.Equals(s.Name, nameOrIndex, StringComparison.OrdinalIgnoreCase))
        ?? (int.TryParse(nameOrIndex, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var i) && i < Stages.Count ? Stages[i] : null);

    /// <summary>Parses a Dockerfile.</summary>
    /// <exception cref="FormatException">It is not valid; the message names the line.</exception>
    public static Dockerfile Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var escape = '\\';
        var index = 0;

        // Parser directives come first, before any other comment or instruction.
        for (; index < lines.Length; index++)
        {
            var directive = lines[index].Trim();
            if (directive.Length == 0) break;
            if (!directive.StartsWith('#')) break;
            var body = directive[1..].Trim();
            var equals = body.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0) break;
            var name = body[..equals].Trim().ToLowerInvariant();
            if (name == "escape")
            {
                var value = body[(equals + 1)..].Trim();
                escape = value switch { "`" => '`', "\\" => '\\', _ => throw new FormatException($"Line {index + 1}: the escape character must be \\ or `.") };
            }
            else if (name is not ("syntax" or "check"))
            {
                break;
            }
        }

        var instructions = new List<DockerfileInstruction>();
        while (index < lines.Length)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                index++;
                continue;
            }

            var start = index + 1;
            var logical = new StringBuilder();
            var original = new StringBuilder();
            while (true)
            {
                var current = lines[index];
                original.Append(current).Append('\n');
                index++;
                var content = current.TrimEnd();
                if (content.Length > 0 && content[^1] == escape && !IsEscaped(content, content.Length - 1, escape))
                {
                    logical.Append(content[..^1]);
                    // Comment and blank lines inside a continuation are skipped, as Docker does.
                    while (index < lines.Length && (lines[index].TrimStart().StartsWith('#') || lines[index].Trim().Length == 0)) index++;
                    if (index >= lines.Length) break;
                    continue;
                }

                logical.Append(current);
                break;
            }

            var (keyword, flags, arguments) = Split(logical.ToString().Trim(), start);
            var heredocs = new List<Heredoc>();
            if (keyword is "RUN" or "COPY" or "ADD")
            {
                foreach (var (name, quoted, strip) in HeredocMarkers(arguments))
                {
                    var body = new StringBuilder();
                    var closed = false;
                    while (index < lines.Length)
                    {
                        var bodyLine = lines[index++];
                        original.Append(bodyLine).Append('\n');
                        var candidate = strip ? bodyLine.TrimStart('\t') : bodyLine;
                        if (candidate == name)
                        {
                            closed = true;
                            break;
                        }

                        body.Append(candidate).Append('\n');
                    }

                    if (!closed) throw new FormatException($"Line {start}: the here-document <<{name} is never closed.");
                    heredocs.Add(new Heredoc(name, body.ToString(), !quoted, strip));
                }
            }

            if (!Known.Contains(keyword)) throw new FormatException($"Line {start}: unknown instruction {keyword}.");
            instructions.Add(new DockerfileInstruction(keyword, flags, arguments, start) { Heredocs = heredocs, Original = original.ToString().TrimEnd() });
        }

        var global = new List<DockerfileInstruction>();
        var stages = new List<DockerfileStage>();
        DockerfileInstruction? from = null;
        var body2 = new List<DockerfileInstruction>();
        void Close()
        {
            if (from is null) return;
            var words = from.Arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) throw new FormatException($"Line {from.Line}: FROM needs an image.");
            string? name = null;
            if (words.Length >= 3 && words[1].Equals("AS", StringComparison.OrdinalIgnoreCase)) name = words[2];
            else if (words.Length != 1) throw new FormatException($"Line {from.Line}: FROM takes an image and an optional AS name.");
            stages.Add(new DockerfileStage(stages.Count, words[0], name, from.Flag("platform"), [.. body2], from.Line));
            body2.Clear();
        }

        foreach (var instruction in instructions)
        {
            if (instruction.Keyword == "FROM")
            {
                Close();
                from = instruction;
            }
            else if (from is null)
            {
                if (instruction.Keyword != "ARG") throw new FormatException($"Line {instruction.Line}: only ARG may come before the first FROM.");
                global.Add(instruction);
            }
            else
            {
                body2.Add(instruction);
            }
        }

        Close();
        if (stages.Count == 0) throw new FormatException("The Dockerfile has no FROM instruction.");
        return new Dockerfile(global, stages, escape);
    }

    private static bool IsEscaped(string text, int position, char escape)
    {
        var count = 0;
        for (var i = position - 1; i >= 0 && text[i] == escape; i--) count++;
        return count % 2 == 1;
    }

    private static (string Keyword, List<KeyValuePair<string, string>> Flags, string Arguments) Split(string text, int line)
    {
        var space = text.IndexOfAny([' ', '\t']);
        var keyword = (space < 0 ? text : text[..space]).ToUpperInvariant();
        var rest = space < 0 ? "" : text[(space + 1)..].TrimStart();
        var flags = new List<KeyValuePair<string, string>>();
        while (rest.StartsWith("--", StringComparison.Ordinal))
        {
            var end = rest.IndexOfAny([' ', '\t']);
            var flag = end < 0 ? rest : rest[..end];
            rest = end < 0 ? "" : rest[(end + 1)..].TrimStart();
            var equals = flag.IndexOf('=', StringComparison.Ordinal);
            flags.Add(equals < 0 ? new(flag[2..], "true") : new(flag[2..equals], flag[(equals + 1)..]));
        }

        if (keyword.Length == 0) throw new FormatException($"Line {line}: an instruction is empty.");
        return (keyword, flags, rest);
    }

    /// <summary>The here-document markers in an instruction's arguments: <c>&lt;&lt;EOF</c>, <c>&lt;&lt;-"EOF"</c>.</summary>
    internal static IEnumerable<(string Name, bool Quoted, bool Strip)> HeredocMarkers(string arguments)
    {
        var position = 0;
        while ((position = arguments.IndexOf("<<", position, StringComparison.Ordinal)) >= 0)
        {
            var i = position + 2;
            var strip = i < arguments.Length && arguments[i] == '-';
            if (strip) i++;
            var quote = i < arguments.Length && arguments[i] is '"' or '\'' ? arguments[i] : '\0';
            if (quote != '\0') i++;
            var nameStart = i;
            if (i >= arguments.Length || !(char.IsAsciiLetter(arguments[i]) || arguments[i] == '_'))
            {
                position = i;
                continue;
            }

            while (i < arguments.Length && (char.IsAsciiLetterOrDigit(arguments[i]) || arguments[i] == '_')) i++;
            var name = arguments[nameStart..i];
            if (quote != '\0' && (i >= arguments.Length || arguments[i] != quote)) name = "";
            position = i;
            if (name.Length > 0) yield return (name, quote != '\0', strip);
        }
    }
}
