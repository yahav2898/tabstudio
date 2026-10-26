namespace TabStudio.Agent.Local;

/// <summary>A text completion from a local inference engine.</summary>
public sealed record LocalCompletion(string Text, int Tokens, double TokensPerSecond);

/// <summary>A raw completion transport. Implementations must never forward prompts to a remote host.</summary>
public interface ILocalTextGenerator
{
    /// <summary>Generates at most the requested number of tokens, with optional streaming text.</summary>
    Task<LocalCompletion> GenerateAsync(string prompt, int maxTokens, Action<string>? onText = null, CancellationToken cancellationToken = default);
}
