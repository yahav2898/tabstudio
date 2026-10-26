using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent;

/// <summary>The agent's inference transport: Claude or entirely on-device inference.</summary>
public interface IAgentClient
{
    /// <summary>Generates one assistant message, reporting text while it is produced.</summary>
    Task<MessageResponse> SendAsync(MessageRequest request, Action<string>? onText = null, CancellationToken cancellationToken = default);
}
