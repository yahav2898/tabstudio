using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>What a line of a job's log is, for its color.</summary>
public enum CiLogLineKind
{
    /// <summary>Ordinary output.</summary>
    Normal,

    /// <summary>A step's heading, which folds the step's lines.</summary>
    Step,

    /// <summary>The heading of a <c>##[group]</c>, which folds the group's lines.</summary>
    Group,

    /// <summary>A command the runner ran (<c>##[command]</c>).</summary>
    Command,

    /// <summary>An error: <c>##[error]</c>, or a compiler error.</summary>
    Error,

    /// <summary>A warning: <c>##[warning]</c>, or a compiler warning.</summary>
    Warning,

    /// <summary>A notice (<c>##[notice]</c>).</summary>
    Notice,

    /// <summary>Debug output (<c>##[debug]</c>).</summary>
    Debug,

    /// <summary>The line that stands for the part of a large log that was not downloaded.</summary>
    Omitted,
}

/// <summary>The terminal colors a log can use, mapped to the theme's colors.</summary>
public enum CiLogColor
{
    /// <summary>The default color.</summary>
    Default,

    /// <summary>Black or dark gray, shown muted.</summary>
    Gray,

    /// <summary>Red.</summary>
    Red,

    /// <summary>Green.</summary>
    Green,

    /// <summary>Yellow.</summary>
    Yellow,

    /// <summary>Blue.</summary>
    Blue,

    /// <summary>Magenta.</summary>
    Magenta,

    /// <summary>Cyan.</summary>
    Cyan,

    /// <summary>White, shown bright.</summary>
    White,
}

/// <summary>A run of colored text the program wrote with ANSI escape codes.</summary>
/// <param name="Start">The 0-based column where it starts, after any timestamp.</param>
/// <param name="Length">How many characters it covers.</param>
/// <param name="Color">The color.</param>
/// <param name="IsBold">Whether it is bold.</param>
public sealed record CiLogSpan(int Start, int Length, CiLogColor Color, bool IsBold);

/// <summary>A section of the log that folds: a step or a group.</summary>
/// <param name="StartLine">The 1-based heading line, which stays visible when folded.</param>
/// <param name="EndLine">The 1-based last line it folds.</param>
/// <param name="Title">What the folded section says.</param>
/// <param name="IsStep">Whether it is a step rather than a group.</param>
/// <param name="StartsFolded">Whether it is folded when the log opens.</param>
public sealed record CiLogFold(int StartLine, int EndLine, string Title, bool IsStep, bool StartsFolded);

/// <summary>Where a step starts in the log.</summary>
/// <param name="Number">The step's number in the job.</param>
/// <param name="Name">The step's name.</param>
/// <param name="Line">The 1-based heading line.</param>
public sealed record CiLogStep(int Number, string Name, int Line);

/// <summary>
/// A job's log made readable: GitHub's timestamps hidden or shortened, steps and <c>##[group]</c>s as folds,
/// workflow commands such as <c>##[error]</c> turned into colored lines, and terminal colors kept as spans.
/// </summary>
/// <param name="Text">The text the editor shows.</param>
/// <param name="Kinds">The kind of each line, by 0-based line index.</param>
/// <param name="Colors">The terminal colors of lines that have any, by 1-based line.</param>
/// <param name="Folds">The steps and groups, by start line.</param>
/// <param name="Steps">Where each step that has output starts.</param>
/// <param name="ErrorLines">The 1-based lines of errors, in order.</param>
/// <param name="PrefixLength">How many characters the timestamp column takes at the start of each line; 0 when hidden.</param>
public sealed record CiLog(
    string Text,
    IReadOnlyList<CiLogLineKind> Kinds,
    IReadOnlyDictionary<int, IReadOnlyList<CiLogSpan>> Colors,
    IReadOnlyList<CiLogFold> Folds,
    IReadOnlyList<CiLogStep> Steps,
    IReadOnlyList<int> ErrorLines,
    int PrefixLength)
{
    /// <summary>The kind of a 1-based line.</summary>
    /// <param name="line">The line.</param>
    public CiLogLineKind KindOf(int line) => line >= 1 && line <= Kinds.Count ? Kinds[line - 1] : CiLogLineKind.Normal;

    /// <summary>The first error's line, if there is an error.</summary>
    public int? FirstErrorLine => ErrorLines.Count > 0 ? ErrorLines[0] : null;
}

/// <summary>Reads GitHub Actions job logs into <see cref="CiLog"/>.</summary>
public static partial class CiLogParser
{
    private const string TimestampFormat = "HH:mm:ss.fff";

    /// <summary>How the runner begins the output of a step that runs a command or an action.</summary>
    private const string StepGroupStart = "##[group]Run ";

    /// <summary>Makes a downloaded log readable.</summary>
    /// <param name="log">The log, or its start and end.</param>
    /// <param name="steps">The job's steps, for step headings; none when empty.</param>
    /// <param name="showTimestamps">Whether to keep each line's time, shortened, in a column at the start.</param>
    /// <param name="jobFailed">Whether the job failed: the failed steps then open unfolded, as on GitHub.</param>
    public static CiLog Parse(GitHubJobLog log, IReadOnlyList<GitHubJobStep> steps, bool showTimestamps, bool jobFailed)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(steps);
        var lines = SplitLines(log.Head).Select(ReadRawLine).ToList();
        if (!log.IsComplete)
        {
            lines.Add(new RawLine(null, OmittedText(log.OmittedBytes), IsOmitted: true));
            lines.AddRange(SplitLines(log.Tail).Select(ReadRawLine));
        }

        return new Builder(showTimestamps, jobFailed).Build(lines, steps);
    }

    /// <summary>"··· 33.2 MB of the log are not shown here ···".</summary>
    private static string OmittedText(long bytes) =>
        $"··· {Size(bytes)} of the log are not shown here. Show the full log to see everything. ···";

    /// <summary>A size for people: "512 KB", "33.2 MB".</summary>
    /// <param name="bytes">The size in bytes.</param>
    public static string Size(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{Math.Max(1, bytes / 1024).ToString(CultureInfo.CurrentCulture)} KB",
        _ => $"{(bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.CurrentCulture)} MB",
    };

    private static IEnumerable<string> SplitLines(string text)
    {
        if (text.Length == 0)
        {
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                yield return text[start..].TrimEnd('\r');
                yield break;
            }

            yield return text[start..end].TrimEnd('\r');
            start = end + 1;
        }
    }

    private static RawLine ReadRawLine(string line)
    {
        var match = TimestampPattern().Match(line);
        return match.Success && DateTimeOffset.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? new RawLine(time, line[match.Length..], IsOmitted: false)
            : new RawLine(null, line, IsOmitted: false);
    }

    [GeneratedRegex(@"^(?<time>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z) ?", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampPattern();

    // A compiler's error or warning line, such as MSBuild's "file(1,2): error CS0103: ..." or "error CS0103: ...".
    [GeneratedRegex(@"(?:^|[\s:)])(?<severity>error|warning)\s+[A-Z]{1,10}\d{1,6}\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex CompilerLinePattern();

    [GeneratedRegex(@"\u001b\[(?<codes>[0-9;]*)m|\u001b(?:\[[0-?]*[ -/]*[@-~]|\][^\u0007\u001b]*(?:\u0007|\u001b\\)|[@-Z\\-_])", RegexOptions.CultureInvariant)]
    private static partial Regex EscapePattern();

    /// <summary>A line of the downloaded log: its time, if it has one, and its content.</summary>
    private sealed record RawLine(DateTimeOffset? Time, string Content, bool IsOmitted);

    /// <summary>Builds the readable log line by line.</summary>
    private sealed class Builder(bool showTimestamps, bool jobFailed)
    {
        /// <summary>The workflow commands that start a line, what they make of it, and the label they leave.</summary>
        private static readonly (string Command, CiLogLineKind Kind, string Label)[] Commands =
        [
            ("##[error]", CiLogLineKind.Error, "Error: "),
            ("##[warning]", CiLogLineKind.Warning, "Warning: "),
            ("##[notice]", CiLogLineKind.Notice, "Notice: "),
            ("##[debug]", CiLogLineKind.Debug, string.Empty),
            ("##[command]", CiLogLineKind.Command, string.Empty),
            ("[command]", CiLogLineKind.Command, string.Empty),
        ];

        private readonly StringBuilder _text = new();
        private readonly List<CiLogLineKind> _kinds = [];
        private readonly Dictionary<int, IReadOnlyList<CiLogSpan>> _colors = [];
        private readonly List<CiLogFold> _folds = [];
        private readonly List<CiLogStep> _steps = [];
        private readonly List<int> _errors = [];
        private readonly Stack<(int Line, string Title)> _groups = new();
        private (int Line, string Title, bool Failed)? _step;

        private int PrefixLength => showTimestamps ? TimestampFormat.Length + 2 : 0;

        public CiLog Build(List<RawLine> lines, IReadOnlyList<GitHubJobStep> steps)
        {
            var starts = StepStarts(lines, steps);
            for (var i = 0; i < lines.Count; i++)
            {
                if (starts.TryGetValue(i, out var step))
                {
                    StartStep(step);
                }

                AddRawLine(lines[i]);
            }

            CloseGroups(0);
            CloseStep();
            if (_text.Length > 0 && _text[^1] == '\n')
            {
                _text.Length--;
            }

            _folds.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));
            return new CiLog(_text.ToString(), _kinds, _colors, _folds, _steps, _errors, PrefixLength);
        }

        /// <summary>
        /// Finds the line each step starts at from its start time: the first line at or after it, or the
        /// <c>##[group]Run ...</c> line in that second, which is how the runner begins a step's output. The first
        /// step starts at the top, since the times of its first lines may be a moment before its own.
        /// </summary>
        private static Dictionary<int, GitHubJobStep> StepStarts(List<RawLine> lines, IReadOnlyList<GitHubJobStep> steps)
        {
            var starts = new Dictionary<int, GitHubJobStep>();
            var from = 0;
            foreach (var step in steps.Where(s => s.StartedAt is not null).OrderBy(s => s.Number))
            {
                var at = step.StartedAt!.Value;
                var second = at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond));
                var index = -1;
                for (var i = from; i < lines.Count; i++)
                {
                    if (lines[i].Time is { } time && time >= second)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    continue;
                }

                if (starts.Count == 0)
                {
                    index = 0;
                }

                for (var i = index; i < lines.Count && lines[i].Time is { } time && time < second.AddSeconds(1); i++)
                {
                    if (lines[i].Content.StartsWith(StepGroupStart, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }

                // A step that wrote nothing has no lines of its own; the next one may start at the same line.
                if (!starts.ContainsKey(index))
                {
                    starts[index] = step;
                }

                from = index + 1;
            }

            return starts;
        }

        private int CurrentLine => _kinds.Count;

        private void StartStep(GitHubJobStep step)
        {
            CloseGroups(0);
            CloseStep();
            var duration = GitHubRunStates.Duration(step.StartedAt, step.CompletedAt, DateTimeOffset.UtcNow);
            var title = new StringBuilder(step.Name);
            if (duration is { } time && step.Status == GitHubRunStatus.Completed)
            {
                title.Append("  ·  ").Append(ActionsFormat.Duration(time));
            }

            if (step.IsFailed)
            {
                title.Append("  ·  failed");
            }

            AppendLine(title.ToString(), null, CiLogLineKind.Step, []);
            _steps.Add(new CiLogStep(step.Number, step.Name, CurrentLine));
            _step = (CurrentLine, title.ToString(), step.IsFailed);
        }

        private void CloseStep()
        {
            if (_step is { } step && CurrentLine > step.Line)
            {
                _folds.Add(new CiLogFold(step.Line, CurrentLine, step.Title, IsStep: true, StartsFolded: !(jobFailed && step.Failed)));
            }

            _step = null;
        }

        private void AddRawLine(RawLine line)
        {
            if (line.IsOmitted)
            {
                CloseGroups(0);
                AppendLine(line.Content, null, CiLogLineKind.Omitted, []);
                return;
            }

            var (content, spans) = ReadColors(line.Content);
            if (content.StartsWith("##[endgroup]", StringComparison.Ordinal))
            {
                CloseGroups(_groups.Count - 1);
                return;
            }

            if (content.StartsWith("##[group]", StringComparison.Ordinal))
            {
                var title = content["##[group]".Length..];
                AppendLine(title, line.Time, CiLogLineKind.Group, Shift(spans, "##[group]".Length));
                _groups.Push((CurrentLine, title));
                return;
            }

            var (kind, text, removed) = Classify(content);
            AppendLine(text, line.Time, kind, Shift(spans, removed));
            if (kind == CiLogLineKind.Error)
            {
                _errors.Add(CurrentLine);
            }
        }

        /// <summary>Turns a workflow command into its line; recognizes compiler errors and warnings in ordinary output.</summary>
        private static (CiLogLineKind Kind, string Text, int Removed) Classify(string content)
        {
            foreach (var (command, kind, label) in Commands)
            {
                if (content.StartsWith(command, StringComparison.Ordinal))
                {
                    return (kind, label + content[command.Length..], command.Length - label.Length);
                }
            }

            var compiler = CompilerLinePattern().Match(content);
            return compiler.Success
                ? (compiler.Groups["severity"].Value == "error" ? CiLogLineKind.Error : CiLogLineKind.Warning, content, 0)
                : (CiLogLineKind.Normal, content, 0);
        }

        /// <summary>Folds the open groups down to <paramref name="keep"/> of them.</summary>
        private void CloseGroups(int keep)
        {
            while (_groups.Count > Math.Max(0, keep))
            {
                var (line, title) = _groups.Pop();
                if (CurrentLine > line)
                {
                    _folds.Add(new CiLogFold(line, CurrentLine, title, IsStep: false, StartsFolded: true));
                }
            }
        }

        private void AppendLine(string content, DateTimeOffset? time, CiLogLineKind kind, List<CiLogSpan> spans)
        {
            if (showTimestamps)
            {
                var stamp = time is { } value ? value.ToLocalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture) : new string(' ', TimestampFormat.Length);
                _text.Append(stamp).Append("  ");
            }

            _text.Append(content).Append('\n');
            _kinds.Add(kind);
            if (spans.Count > 0)
            {
                _colors[CurrentLine] = spans;
            }
        }

        private static List<CiLogSpan> Shift(List<CiLogSpan> spans, int removed) =>
            removed == 0 ? spans : [.. spans.Select(s => s with { Start = Math.Max(0, s.Start - removed) }).Where(s => s.Length > 0)];

        /// <summary>Removes escape sequences, keeping the colors they set as spans of the remaining text.</summary>
        private static (string Text, List<CiLogSpan> Spans) ReadColors(string content)
        {
            var spans = new List<CiLogSpan>();
            if (!content.Contains('\u001b', StringComparison.Ordinal))
            {
                return (content, spans);
            }

            var text = new StringBuilder(content.Length);
            var color = CiLogColor.Default;
            var bold = false;
            var spanStart = 0;
            var position = 0;
            foreach (Match match in EscapePattern().Matches(content))
            {
                text.Append(content, position, match.Index - position);
                position = match.Index + match.Length;
                if (!match.Groups["codes"].Success)
                {
                    continue;
                }

                AddSpan(spans, spanStart, text.Length - spanStart, color, bold);
                spanStart = text.Length;
                (color, bold) = ApplyCodes(match.Groups["codes"].Value, color, bold);
            }

            text.Append(content, position, content.Length - position);
            AddSpan(spans, spanStart, text.Length - spanStart, color, bold);
            return (text.ToString(), spans);
        }

        private static void AddSpan(List<CiLogSpan> spans, int start, int length, CiLogColor color, bool bold)
        {
            if (length > 0 && (color != CiLogColor.Default || bold))
            {
                spans.Add(new CiLogSpan(start, length, color, bold));
            }
        }

        /// <summary>Applies SGR codes: reset, bold, and the 16 foreground colors (also as 256-color indexes below 16).</summary>
        private static (CiLogColor Color, bool Bold) ApplyCodes(string codes, CiLogColor color, bool bold)
        {
            string[] parts = codes.Length == 0 ? ["0"] : codes.Split(';');
            for (var i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var code))
                {
                    continue;
                }

                switch (code)
                {
                    case 0:
                        (color, bold) = (CiLogColor.Default, false);
                        break;
                    case 1:
                        bold = true;
                        break;
                    case 22:
                        bold = false;
                        break;
                    case 39:
                        color = CiLogColor.Default;
                        break;
                    case >= 30 and <= 37:
                        color = Basic(code - 30);
                        break;
                    case >= 90 and <= 97:
                        color = Basic(code - 90);
                        break;
                    case 38 when i + 2 < parts.Length && parts[i + 1] == "5":
                        color = int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < 16 ? Basic(index % 8) : CiLogColor.Default;
                        i += 2;
                        break;
                    case 38 when i + 4 < parts.Length && parts[i + 1] == "2":
                        i += 4;
                        break;
                    case 48 when i + 1 < parts.Length:
                        i += parts[i + 1] == "5" ? 2 : 4;
                        break;
                }
            }

            return (color, bold);
        }

        private static CiLogColor Basic(int index) => index switch
        {
            0 => CiLogColor.Gray,
            1 => CiLogColor.Red,
            2 => CiLogColor.Green,
            3 => CiLogColor.Yellow,
            4 => CiLogColor.Blue,
            5 => CiLogColor.Magenta,
            6 => CiLogColor.Cyan,
            _ => CiLogColor.White,
        };
    }
}
