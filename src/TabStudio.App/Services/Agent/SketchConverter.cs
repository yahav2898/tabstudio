using TabStudio.Agent;
using TabStudio.Agent.Anthropic;

namespace TabStudio.App.Services.Agent;

/// <summary>Turns sketches into XAML with Claude, using the API key and model of the agent's settings.</summary>
/// <param name="settings">The agent's settings, read at every sketch so a key added meanwhile is used.</param>
/// <param name="http">The HTTP client the agent uses.</param>
public sealed class SketchConverter(AgentSettingsStore settings, HttpClient http)
{
    /// <summary>Whether a Claude API key is set, without which sketches cannot be turned into XAML.</summary>
    public bool IsAvailable => settings.Load().HasApiKey;

    /// <summary>Turns a sketch into Avalonia XAML.</summary>
    /// <param name="png">The sketch.</param>
    /// <param name="hint">What the user said about the screen, if anything.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="AnthropicException">There is no API key, or Claude could not answer.</exception>
    public Task<SketchResult> ConvertAsync(ReadOnlyMemory<byte> png, string? hint, CancellationToken cancellationToken)
    {
        var current = settings.Load();
        if (!current.HasApiKey)
        {
            throw new AnthropicException("Add your Claude API key in the Claude panel's settings to turn sketches into XAML.");
        }

        return new SketchToXaml(new AnthropicClient(http, current.ApiKey!), current.Model).ConvertAsync(png, XamlDialect.Avalonia, hint, cancellationToken);
    }
}
