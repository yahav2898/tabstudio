using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;
using TabStudio.Testing;

namespace TabStudio.Agent.Tests;

public sealed class AgentSessionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task The_agent_reads_edits_checks_and_summarizes_and_nothing_changes_until_approved()
    {
        _temp.Write("Greeter.cs", "class Greeter { string Greet() => \"Hello\"; }\n");
        var workspace = new FolderWorkspace(_temp);
        var claude = new FakeClaude()
            .ToolCall("toolu_1", "read_file", new JsonObject { ["path"] = "Greeter.cs" }, text: "I'll look at the greeter.")
            .ToolCall("toolu_2", "edit_file", new JsonObject { ["path"] = "Greeter.cs", ["old_text"] = "\"Hello\"", ["new_text"] = "\"Hello, tablet\"" })
            .ToolCall("toolu_3", "get_diagnostics", [])
            .Text("I changed the greeting.");
        var session = new AgentSession(new AnthropicClient(new HttpClient(claude), "sk-test"), workspace, new AgentOptions(Model: "claude-test"));
        var events = new List<AgentEvent>();
        session.Event += (_, e) => events.Add(e);

        var result = await session.SendAsync("Make the greeting friendlier");

        Assert.Equal(AgentTurnEnd.Completed, result.End);
        Assert.Equal("I'll look at the greeter.I changed the greeting.", result.Text);
        Assert.Equal(4, claude.Requests.Count);
        Assert.Equal(new[] { "Read Greeter.cs", "Edit Greeter.cs", "Check for errors" }, events.OfType<AgentToolStarted>().Select(e => e.Description));
        Assert.All(events.OfType<AgentToolFinished>(), e => Assert.False(e.Result.IsError, e.Result.Content));

        // The tool results went back to Claude with the right call ids.
        var lastRequest = claude.Requests[^1]["messages"]!.AsArray();
        Assert.Equal("toolu_3", lastRequest[^1]!["content"]![0]!["tool_use_id"]!.GetValue<string>());
        Assert.Equal("No errors or warnings.", lastRequest[^1]!["content"]![0]!["content"]!.GetValue<string>());
        Assert.Contains(_temp.Path, claude.Requests[0]["system"]![0]!["text"]!.GetValue<string>(), StringComparison.Ordinal);

        // Nothing was written yet.
        Assert.Equal("class Greeter { string Greet() => \"Hello\"; }\n", File.ReadAllText(_temp.Resolve("Greeter.cs")));
        Assert.Single(session.Changes.Pending);

        await session.ApproveAllAsync();

        Assert.Equal("class Greeter { string Greet() => \"Hello, tablet\"; }\n", File.ReadAllText(_temp.Resolve("Greeter.cs")));
        Assert.Empty(session.Changes.Pending);
        Assert.Equal(new TokenUsage(480, 168, 400, 80), session.Usage);
    }

    [Fact]
    public async Task Keeping_a_change_never_undoes_edits_made_since_it_was_proposed()
    {
        _temp.Write("a.txt", "one\n");
        var claude = new FakeClaude()
            .ToolCall("toolu_1", "create_file", new JsonObject { ["path"] = "a.txt", ["content"] = "two\n" })
            .Text("Done.");
        var session = new AgentSession(new AnthropicClient(new HttpClient(claude), "sk-test"), new FolderWorkspace(_temp));
        await session.SendAsync("Change a.txt");

        // The user fixed something in the file while reviewing the change.
        _temp.Write("a.txt", "one, fixed\n");
        var error = await Assert.ThrowsAsync<IOException>(() => session.ApproveAsync(_temp.Resolve("a.txt")));

        Assert.Contains("changed since Claude proposed this", error.Message, StringComparison.Ordinal);
        Assert.Equal("one, fixed\n", File.ReadAllText(_temp.Resolve("a.txt")));
        Assert.Single(session.Changes.Pending);
    }

    [Fact]
    public async Task Rejected_changes_are_mentioned_on_the_next_request()
    {
        _temp.Write("a.txt", "one\n");
        var claude = new FakeClaude()
            .ToolCall("toolu_1", "create_file", new JsonObject { ["path"] = "a.txt", ["content"] = "two\n" })
            .Text("Done.")
            .Text("Understood.");
        var session = new AgentSession(new AnthropicClient(new HttpClient(claude), "sk-test"), new FolderWorkspace(_temp));

        await session.SendAsync("Change a.txt");
        session.Reject(Path.Combine(_temp.Path, "a.txt"));
        await session.SendAsync("Never mind");

        var lastUser = claude.Requests[^1]["messages"]!.AsArray()[^1]!["content"]![0]!["text"]!.GetValue<string>();
        Assert.Contains("rejected your changes to: a.txt", lastUser, StringComparison.Ordinal);
        Assert.Equal("one\n", File.ReadAllText(_temp.Resolve("a.txt")));
    }

    [Fact]
    public async Task Unknown_tools_and_round_limits_are_handled()
    {
        var claude = new FakeClaude()
            .ToolCall("toolu_1", "format_disk", [])
            .ToolCall("toolu_2", "list_files", []);
        var session = new AgentSession(new AnthropicClient(new HttpClient(claude), "sk-test"), new FolderWorkspace(_temp), new AgentOptions(MaxToolRounds: 2));

        var result = await session.SendAsync("Go");

        Assert.Equal(AgentTurnEnd.RoundLimit, result.End);
        var firstResult = claude.Requests[1]["messages"]!.AsArray()[^1]!["content"]![0]!;
        Assert.True(firstResult["is_error"]!.GetValue<bool>());
        Assert.Contains("no tool named format_disk", firstResult["content"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
