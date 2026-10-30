using TabStudio.Agent.Anthropic;
using TabStudio.Agent.Tools;

namespace TabStudio.Agent;

/// <summary>Settings for an agent session.</summary>
/// <param name="Model">The Claude model.</param>
/// <param name="MaxToolRounds">How many rounds of tool calls one request may take before the agent stops to check in.</param>
/// <param name="MaxTokens">The longest single answer.</param>
/// <param name="Instructions">Extra instructions from the user, such as house style; appended to the system prompt.</param>
public sealed record AgentOptions(
    string Model = AnthropicClient.DefaultModel,
    int MaxToolRounds = 40,
    int MaxTokens = 16000,
    string? Instructions = null);

/// <summary>Something that happened during a turn, for the conversation view.</summary>
public abstract record AgentEvent;

/// <summary>Text Claude wrote.</summary>
/// <param name="Text">The new text.</param>
public sealed record AgentTextDelta(string Text) : AgentEvent;

/// <summary>Claude started a tool call.</summary>
/// <param name="CallId">The call's id.</param>
/// <param name="ToolName">The tool.</param>
/// <param name="Description">What it does, such as "Read src/Program.cs".</param>
public sealed record AgentToolStarted(string CallId, string ToolName, string Description) : AgentEvent;

/// <summary>A tool call finished.</summary>
/// <param name="CallId">The call's id.</param>
/// <param name="Result">What it returned.</param>
public sealed record AgentToolFinished(string CallId, ToolOutcome Result) : AgentEvent;

/// <summary>How a turn ended.</summary>
public enum AgentTurnEnd
{
    /// <summary>Claude finished its answer.</summary>
    Completed,

    /// <summary>The turn used its tool rounds; send another message to let the agent continue.</summary>
    RoundLimit,

    /// <summary>The answer hit the token limit.</summary>
    TokenLimit,

    /// <summary>The user stopped it.</summary>
    Canceled,

    /// <summary>Claude declined.</summary>
    Refused,
}

/// <summary>The outcome of one request to the agent.</summary>
/// <param name="End">How the turn ended.</param>
/// <param name="Text">Everything Claude wrote in the turn.</param>
/// <param name="Usage">Tokens the turn used.</param>
public sealed record AgentTurnResult(AgentTurnEnd End, string Text, TokenUsage Usage);

/// <summary>
/// A conversation with the agent about the open workspace. Each request runs Claude in a loop: it answers,
/// calls tools, reads their results and continues until it is done. File changes collect in <see cref="Changes"/>
/// until the user approves them.
/// </summary>
public sealed class AgentSession
{
    private readonly IAgentClient _client;
    private readonly IAgentWorkspace _workspace;
    private readonly AgentOptions _options;
    private readonly List<ChatMessage> _history = [];
    private readonly List<AgentTool> _tools;
    private readonly List<string> _rejected = [];
    private int _busy;

    /// <summary>Creates a session.</summary>
    /// <param name="client">The Claude client.</param>
    /// <param name="workspace">The workspace the agent works on.</param>
    /// <param name="options">Settings; defaults when <see langword="null"/>.</param>
    /// <param name="extraTools">More tools, such as those of MCP servers.</param>
    /// <param name="allowedTools">Optional capability restriction, used for on-device models.</param>
    public AgentSession(IAgentClient client, IAgentWorkspace workspace, AgentOptions? options = null, IEnumerable<AgentTool>? extraTools = null, IReadOnlySet<string>? allowedTools = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(workspace);
        _client = client;
        _workspace = workspace;
        _options = options ?? new AgentOptions();
        Changes = new ChangeSet();
        _tools = [.. new WorkspaceTools(workspace, Changes).All.Where(t => allowedTools is null || allowedTools.Contains(t.Definition.Name)), .. (extraTools ?? []).Where(t => allowedTools is null || allowedTools.Contains(t.Definition.Name))];
    }

    /// <summary>Raised as the agent works: text, tool calls and their results. Raised on a background thread.</summary>
    public event EventHandler<AgentEvent>? Event;

    /// <summary>The conversation so far.</summary>
    public IReadOnlyList<ChatMessage> History => _history;

    /// <summary>Changes the agent proposed that the user has not approved or rejected yet.</summary>
    public ChangeSet Changes { get; }

    /// <summary>Tokens used by the whole session.</summary>
    public TokenUsage Usage { get; private set; } = new(0, 0);

    /// <summary>Whether a request is in progress.</summary>
    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    /// <summary>Sends a request and lets the agent work until it is done.</summary>
    /// <param name="text">The request, such as "Fix the build errors".</param>
    /// <param name="cancellationToken">Stops the agent after the current step; the conversation stays usable.</param>
    /// <exception cref="InvalidOperationException">A request is already in progress.</exception>
    public Task<AgentTurnResult> SendAsync(string text, CancellationToken cancellationToken = default) =>
        SendAsync(text, [], cancellationToken);

    /// <summary>Sends a request with pictures, such as a screenshot of a layout problem, and lets the agent work until it is done.</summary>
    /// <param name="text">The request.</param>
    /// <param name="images">Pictures Claude sees before the request's text.</param>
    /// <param name="cancellationToken">Stops the agent after the current step; the conversation stays usable.</param>
    /// <exception cref="InvalidOperationException">A request is already in progress.</exception>
    public async Task<AgentTurnResult> SendAsync(string text, IReadOnlyList<ImageBlock> images, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(images);
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            throw new InvalidOperationException("The agent is still working on the previous request.");
        }

        try
        {
            _history.Add(new ChatMessage(MessageRole.User, [.. images, new TextBlock(text)]));
            return await RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    /// <summary>Applies an approved change to the workspace.</summary>
    /// <param name="fullPath">The changed file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="IOException">
    /// The file changed since the agent proposed the change, which keeping it would undo; the change stays pending.
    /// </exception>
    public async Task ApproveAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (Changes.Find(fullPath) is not { } change)
        {
            return;
        }

        // Kept over edits made since, the change silently undid them; kept and failed, it was gone from the list.
        var current = await _workspace.ReadFileAsync(change.FullPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(current, change.Original, StringComparison.Ordinal))
        {
            throw new IOException($"{change.RelativePath} changed since Claude proposed this, and keeping it would undo those edits. Ask Claude to make the change again, or Undo it.");
        }

        await _workspace.ApplyChangeAsync(change.FullPath, change.Proposed, cancellationToken).ConfigureAwait(false);
        Changes.Take(fullPath);
    }

    /// <summary>Applies every pending change.</summary>
    /// <param name="cancellationToken">Cancels the writes.</param>
    public async Task ApproveAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var change in Changes.Pending)
        {
            await ApproveAsync(change.FullPath, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Throws a pending change away. The agent is told on its next request.</summary>
    /// <param name="fullPath">The changed file.</param>
    public void Reject(string fullPath)
    {
        if (Changes.Take(fullPath) is { } change)
        {
            _rejected.Add(change.RelativePath);
        }
    }

    private async Task<AgentTurnResult> RunAsync(CancellationToken cancellationToken)
    {
        if (_rejected.Count > 0)
        {
            // Let the agent know its view of those files is stale.
            var last = _history[^1];
            var note = $"(The user rejected your changes to: {string.Join(", ", _rejected)}. Those files are as they were before your edits.)\n\n";
            _history[^1] = last with { Content = [new TextBlock(note), .. last.Content] };
            _rejected.Clear();
        }

        var usage = new TokenUsage(0, 0);
        var written = new System.Text.StringBuilder();
        var definitions = _tools.Select(t => t.Definition).ToList();
        var system = SystemPrompt.Create(_workspace.RootDirectory, _options.Instructions);

        for (var round = 0; round < _options.MaxToolRounds; round++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Finish(AgentTurnEnd.Canceled);
            }

            MessageResponse response;
            try
            {
                response = await _client.SendAsync(
                    new MessageRequest(_options.Model, system, _history, definitions, _options.MaxTokens),
                    text =>
                    {
                        written.Append(text);
                        Raise(new AgentTextDelta(text));
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Finish(AgentTurnEnd.Canceled);
            }

            usage = usage.Add(response.Usage);
            Usage = Usage.Add(response.Usage);
            _history.Add(response.Message);

            var calls = response.Message.Content.OfType<ToolUseBlock>().ToList();
            if (response.StopReason != StopReason.ToolUse || calls.Count == 0)
            {
                return Finish(response.StopReason switch
                {
                    StopReason.MaxTokens => AgentTurnEnd.TokenLimit,
                    StopReason.Refusal => AgentTurnEnd.Refused,
                    _ => AgentTurnEnd.Completed,
                });
            }

            var results = new List<ContentBlock>();
            foreach (var call in calls)
            {
                results.Add(await InvokeAsync(call, cancellationToken).ConfigureAwait(false));
            }

            _history.Add(new ChatMessage(MessageRole.User, results));
        }

        return Finish(AgentTurnEnd.RoundLimit);

        AgentTurnResult Finish(AgentTurnEnd end)
        {
            // The API needs every tool call answered; a canceled turn can leave the last calls without results.
            if (_history.Count > 0 && _history[^1] is { Role: MessageRole.Assistant } last && last.Content.OfType<ToolUseBlock>().ToList() is { Count: > 0 } open)
            {
                _history.Add(new ChatMessage(MessageRole.User, [.. open.Select(c => new ToolResultBlock(c.Id, "Not run: the user stopped the agent.", IsError: true))]));
            }

            return new AgentTurnResult(end, written.ToString(), usage);
        }
    }

    private async Task<ToolResultBlock> InvokeAsync(ToolUseBlock call, CancellationToken cancellationToken)
    {
        var tool = _tools.FirstOrDefault(t => t.Definition.Name == call.Name);
        if (tool is null)
        {
            var unknown = ToolOutcome.Error($"There is no tool named {call.Name}.");
            Raise(new AgentToolFinished(call.Id, unknown));
            return new ToolResultBlock(call.Id, unknown.Content, IsError: true);
        }

        Raise(new AgentToolStarted(call.Id, call.Name, tool.Describe(call.Input)));
        ToolOutcome outcome;
        try
        {
            outcome = await tool.InvokeAsync(call.Input, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = ToolOutcome.Error("Stopped by the user.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            outcome = ToolOutcome.Error($"The tool failed: {ex.Message}");
        }

        Raise(new AgentToolFinished(call.Id, outcome));
        return new ToolResultBlock(call.Id, outcome.Content, outcome.IsError);
    }

    private void Raise(AgentEvent agentEvent) => Event?.Invoke(this, agentEvent);
}
