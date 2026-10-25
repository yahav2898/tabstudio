using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent.Tests;

public sealed class PullRequestReviewerTests
{
    private static readonly ReviewedFile Greeter = new("src/Greeter.cs", "modified",
    [
        new ReviewedLine(ReviewedLineKind.Context, 10, 10, "public string Greet(string name)"),
        new ReviewedLine(ReviewedLineKind.Removed, 11, null, "    return \"Hi\";"),
        new ReviewedLine(ReviewedLineKind.Added, null, 11, "    return $\"Hello, {name.Trim()}\";"),
        new ReviewedLine(ReviewedLineKind.Context, 12, 12, "}"),
        new ReviewedLine(ReviewedLineKind.Context, 40, 40, "// end"),
    ]);

    [Fact]
    public async Task Sends_the_numbered_diff_and_returns_the_findings_on_its_lines()
    {
        var claude = new FakeClaude().ToolCall("toolu_1", PullRequestReviewer.ToolName, new JsonObject
        {
            ["summary"] = "Greets by name; one crash.",
            ["findings"] = new JsonArray(
                Finding("src/Greeter.cs", 11, "RIGHT", "bug", "`name` can be null: `Trim()` throws."),
                Finding("src/Greeter.cs", 11, "LEFT", "suggestion", "The old greeting is also used in the docs."),
                Finding("src/Greeter.cs", 14, "RIGHT", "warning", "Nearby, moved onto line 12."),
                Finding("src/Greeter.cs", 30, "RIGHT", "warning", "Far from the diff: dropped."),
                Finding("src/Other.cs", 1, "RIGHT", "bug", "Not in the pull request: dropped.")),
        });
        var reviewer = new PullRequestReviewer(new AnthropicClient(new HttpClient(claude), "sk-test"), "claude-test");

        var review = await reviewer.ReviewAsync(new PullRequestReviewRequest("Greet by name", "Uses the **name**.", [Greeter]));

        Assert.Equal("Greets by name; one crash.", review.Summary);
        Assert.Equal(0, review.SkippedFiles);
        Assert.Equal(3, review.Findings.Count);
        Assert.Equal(new ReviewFinding("src/Greeter.cs", 11, ReviewSide.Right, ReviewSeverity.Bug, "`name` can be null: `Trim()` throws."), review.Findings[0]);
        Assert.Equal((11, ReviewSide.Left, ReviewSeverity.Suggestion), (review.Findings[1].Line, review.Findings[1].Side, review.Findings[1].Severity));
        Assert.Equal(12, review.Findings[2].Line);

        var request = claude.Requests.Single();
        Assert.Equal("claude-test", request["model"]!.GetValue<string>());
        Assert.Equal(PullRequestReviewer.ToolName, request["tools"]![0]!["name"]!.GetValue<string>());
        var prompt = request["messages"]![0]!["content"]![0]!["text"]!.GetValue<string>();
        Assert.Contains("Pull request: Greet by name", prompt, StringComparison.Ordinal);
        Assert.Contains("=== src/Greeter.cs (modified) ===", prompt, StringComparison.Ordinal);
        Assert.Contains("L11   -|     return \"Hi\";", prompt, StringComparison.Ordinal);
        Assert.Contains("R11   +|     return $\"Hello, {name.Trim()}\";", prompt, StringComparison.Ordinal);
        Assert.Contains("…\nR40", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void An_answer_without_the_tool_becomes_the_summary()
    {
        var review = PullRequestReviewer.Parse(ChatMessage.FromText(MessageRole.Assistant, "Looks good to me."), [Greeter], 0);

        Assert.Equal("Looks good to me.", review.Summary);
        Assert.Empty(review.Findings);
    }

    private static JsonObject Finding(string path, int line, string side, string severity, string body) => new()
    {
        ["path"] = path,
        ["line"] = line,
        ["side"] = side,
        ["severity"] = severity,
        ["body"] = body,
    };
}
