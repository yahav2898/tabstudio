using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent;

/// <summary>How a line of a reviewed diff relates to the two versions of its file.</summary>
public enum ReviewedLineKind
{
    /// <summary>Unchanged, shown around a change.</summary>
    Context,

    /// <summary>Only in the new version.</summary>
    Added,

    /// <summary>Only in the old version.</summary>
    Removed,
}

/// <summary>Which version of a file a finding's line counts in, as GitHub names them.</summary>
public enum ReviewSide
{
    /// <summary>The old version, where removed lines are.</summary>
    Left,

    /// <summary>The new version, where added and unchanged lines are.</summary>
    Right,
}

/// <summary>How much a finding matters.</summary>
public enum ReviewSeverity
{
    /// <summary>A defect: wrong results, a crash, a security hole, data loss.</summary>
    Bug,

    /// <summary>A risk worth fixing: an edge case, a race, a leak, a misleading name.</summary>
    Warning,

    /// <summary>An improvement: clearer, simpler or more idiomatic code.</summary>
    Suggestion,
}

/// <summary>A line of a reviewed diff.</summary>
/// <param name="Kind">Added, removed or unchanged.</param>
/// <param name="OldLine">Its number in the old version, unless it was added.</param>
/// <param name="NewLine">Its number in the new version, unless it was removed.</param>
/// <param name="Text">The line.</param>
public sealed record ReviewedLine(ReviewedLineKind Kind, int? OldLine, int? NewLine, string Text);

/// <summary>A file of a reviewed pull request.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="Status">How it changed, such as <c>modified</c> or <c>added</c>.</param>
/// <param name="Lines">The lines of its diff: the changes and a few lines around them.</param>
public sealed record ReviewedFile(string Path, string Status, IReadOnlyList<ReviewedLine> Lines);

/// <summary>What Claude reviews.</summary>
/// <param name="Title">The pull request's title.</param>
/// <param name="Description">Its description, if it has one.</param>
/// <param name="Files">Its files.</param>
public sealed record PullRequestReviewRequest(string Title, string? Description, IReadOnlyList<ReviewedFile> Files);

/// <summary>Something Claude would comment on, at a line of the diff.</summary>
/// <param name="Path">The file.</param>
/// <param name="Line">The line, in the version <paramref name="Side"/> names; always a line of the diff.</param>
/// <param name="Side">The version the line counts in.</param>
/// <param name="Severity">How much it matters.</param>
/// <param name="Body">The comment, in Markdown.</param>
public sealed record ReviewFinding(string Path, int Line, ReviewSide Side, ReviewSeverity Severity, string Body);

/// <summary>Claude's review.</summary>
/// <param name="Summary">What the pull request does and Claude's overall view, in a few sentences.</param>
/// <param name="Findings">Its comments on lines, in the order of the files.</param>
/// <param name="SkippedFiles">Files left out because the diff was too long to send.</param>
public sealed record ClaudeReview(string Summary, IReadOnlyList<ReviewFinding> Findings, int SkippedFiles);

/// <summary>
/// Asks Claude to review a pull request's diff and answer with structured findings, one per line it would
/// comment on. The answer comes back as a single call of a reporting tool, so it is data rather than prose;
/// findings that do not land on a line of the diff are moved to the nearest line, or dropped.
/// </summary>
/// <param name="client">The Claude client.</param>
/// <param name="model">The model.</param>
public sealed class PullRequestReviewer(AnthropicClient client, string model = AnthropicClient.DefaultModel)
{
    /// <summary>The tool Claude reports its review with.</summary>
    public const string ToolName = "report_review";

    /// <summary>How far a finding may be from a line of the diff to be moved onto it.</summary>
    private const int NearbyLines = 3;

    /// <summary>The most diff text sent, so a huge pull request stays within the model's context.</summary>
    private const int MaxDiffCharacters = 160_000;

    private const string SystemPrompt = """
        You are a meticulous senior engineer reviewing a pull request in an IDE. The author will see each of your
        findings as a draft review comment on a line of the diff, and decides which ones to post.

        - Report real problems first: bugs, crashes, security issues, data loss, races, broken edge cases, wrong
          error handling. Then risks worth fixing. Then a few suggestions that clearly improve the code.
        - Do not praise, do not restate what the code does, and do not comment on formatting a formatter would fix.
        - Each finding goes on the most specific line it is about. Use only lines shown in the diff, with the number
          and side shown there: R for the new version (added and unchanged lines), L for removed lines.
        - Write each finding as a short, direct review comment in Markdown: what is wrong and how to fix it. Add a
          small code block only when a few lines of code make the fix clearer.
        - At most 15 findings. If the change looks right, report none and say so in the summary.

        Report by calling the report_review tool exactly once.
        """;

    private static readonly JsonObject Schema = (JsonObject)JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "summary": { "type": "string", "description": "One to three sentences: what the pull request does and your overall assessment." },
            "findings": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "path": { "type": "string", "description": "The file, exactly as in its === header." },
                  "line": { "type": "integer", "description": "The line number shown in the diff." },
                  "side": { "type": "string", "enum": ["RIGHT", "LEFT"], "description": "RIGHT for an R line (new version), LEFT for an L line (removed)." },
                  "severity": { "type": "string", "enum": ["bug", "warning", "suggestion"] },
                  "body": { "type": "string", "description": "The review comment, in Markdown." }
                },
                "required": ["path", "line", "side", "severity", "body"]
              }
            }
          },
          "required": ["summary", "findings"]
        }
        """)!;

    /// <summary>Reviews a pull request.</summary>
    /// <param name="request">The pull request and its diff.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="AnthropicException">Claude's API refused or failed the request.</exception>
    public async Task<ClaudeReview> ReviewAsync(PullRequestReviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);
        var (diff, included) = Describe(request.Files);
        var prompt = new StringBuilder();
        prompt.Append(CultureInfo.InvariantCulture, $"Pull request: {request.Title}\n");
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            prompt.Append(CultureInfo.InvariantCulture, $"\nDescription:\n{request.Description.Trim()}\n");
        }

        prompt.Append("\nThe diff, file by file. Each line starts with its number: R for the new version, L for a removed line; + marks added lines, - removed ones.\n\n");
        prompt.Append(diff);

        var tool = new ToolDefinition(ToolName, "Reports the review: a summary and the findings on lines of the diff.", Schema);
        var response = await client.SendAsync(
            new MessageRequest(model, SystemPrompt, [ChatMessage.FromText(MessageRole.User, prompt.ToString())], [tool], MaxTokens: 8000),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Parse(response.Message, included, request.Files.Count - included.Count);
    }

    /// <summary>Writes the files' diffs with line numbers, as many as fit.</summary>
    internal static (string Diff, IReadOnlyList<ReviewedFile> Included) Describe(IReadOnlyList<ReviewedFile> files)
    {
        var text = new StringBuilder();
        var included = new List<ReviewedFile>();
        foreach (var file in files.Where(f => f.Lines.Count > 0))
        {
            var block = Describe(file);
            if (text.Length + block.Length > MaxDiffCharacters && included.Count > 0)
            {
                continue;
            }

            text.Append(block);
            included.Add(file);
        }

        return (text.ToString(), included);
    }

    /// <summary>Takes the findings out of Claude's answer, keeping those on lines of the diff.</summary>
    internal static ClaudeReview Parse(ChatMessage message, IReadOnlyList<ReviewedFile> files, int skippedFiles)
    {
        var call = message.Content.OfType<ToolUseBlock>().FirstOrDefault(b => b.Name == ToolName);
        if (call is null)
        {
            var text = message.Text.Trim();
            return new ClaudeReview(text.Length > 0 ? text : "Claude found nothing to comment on.", [], skippedFiles);
        }

        var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var findings = new List<ReviewFinding>();
        foreach (var item in call.Input["findings"] as JsonArray ?? new JsonArray())
        {
            if (Text(item?["path"]) is not { } path
                || !byPath.TryGetValue(path, out var file)
                || Integer(item?["line"]) is not { } line
                || Text(item?["body"]) is not { Length: > 0 } body)
            {
                continue;
            }

            var side = string.Equals(Text(item?["side"]), "LEFT", StringComparison.OrdinalIgnoreCase) ? ReviewSide.Left : ReviewSide.Right;
            if (Nearest(file, side, line) is { } onDiff)
            {
                findings.Add(new ReviewFinding(path, onDiff, side, Severity(Text(item?["severity"])), body.Trim()));
            }
        }

        var order = files.Select((f, i) => (f.Path, i)).ToDictionary(x => x.Path, x => x.i, StringComparer.Ordinal);
        var summary = Text(call.Input["summary"])?.Trim();
        return new ClaudeReview(
            string.IsNullOrEmpty(summary) ? "Claude reviewed the changes." : summary,
            [.. findings.OrderBy(f => order[f.Path]).ThenBy(f => f.Line)],
            skippedFiles);
    }

    private static string Describe(ReviewedFile file)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"=== {file.Path} ({file.Status}) ===\n");
        int? previousOld = null;
        foreach (var line in file.Lines)
        {
            if (previousOld is { } previous && line.OldLine is { } old && old > previous + 1)
            {
                text.Append("…\n");
            }

            var number = line.Kind == ReviewedLineKind.Removed ? $"L{line.OldLine}" : $"R{line.NewLine}";
            var mark = line.Kind switch
            {
                ReviewedLineKind.Added => '+',
                ReviewedLineKind.Removed => '-',
                _ => ' ',
            };
            text.Append(CultureInfo.InvariantCulture, $"{number,-6}{mark}| {line.Text}\n");
            previousOld = line.OldLine ?? previousOld;
        }

        return text.Append('\n').ToString();
    }

    /// <summary>The line itself when the diff shows it on that side, or the nearest one that it does.</summary>
    private static int? Nearest(ReviewedFile file, ReviewSide side, int line)
    {
        var lines = file.Lines
            .Where(l => side == ReviewSide.Left ? l.Kind != ReviewedLineKind.Added : l.Kind != ReviewedLineKind.Removed)
            .Select(l => side == ReviewSide.Left ? l.OldLine : l.NewLine)
            .OfType<int>()
            .ToHashSet();
        for (var distance = 0; distance <= NearbyLines; distance++)
        {
            if (lines.Contains(line + distance))
            {
                return line + distance;
            }

            if (lines.Contains(line - distance))
            {
                return line - distance;
            }
        }

        return null;
    }

    private static ReviewSeverity Severity(string? severity) => severity?.ToUpperInvariant() switch
    {
        "BUG" or "ERROR" => ReviewSeverity.Bug,
        "WARNING" or "RISK" => ReviewSeverity.Warning,
        _ => ReviewSeverity.Suggestion,
    };

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int? Integer(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue<int>(out var number) => number,
        JsonValue value when value.TryGetValue<double>(out var real) => (int)real,
        JsonValue value when value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };
}
