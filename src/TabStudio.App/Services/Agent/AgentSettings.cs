using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.Agent.Anthropic;

namespace TabStudio.App.Services.Agent;

/// <summary>A Model Context Protocol server the user connected.</summary>
/// <param name="Name">A short name; its tools appear as <c>name__tool</c>.</param>
/// <param name="Url">The server's Streamable HTTP address.</param>
/// <param name="Token">A bearer token, if the server needs one.</param>
public sealed record McpServerSetting(string Name, string Url, string? Token);

/// <summary>The agent's settings.</summary>
/// <param name="ApiKey">The Claude API key, or <see langword="null"/> before the user adds one.</param>
/// <param name="Model">The model.</param>
/// <param name="Instructions">Instructions added to every conversation, such as house style.</param>
/// <param name="McpServers">Connected MCP servers.</param>
public sealed record AgentSettings(string? ApiKey, string Model, string? Instructions, IReadOnlyList<McpServerSetting> McpServers, bool LocalMode = false)
{
    /// <summary>The models offered in the settings, most capable first.</summary>
    public static IReadOnlyList<string> Models { get; } = [AnthropicClient.DefaultModel, "claude-sonnet-5-5", "claude-haiku-5-5"];

    /// <summary>Settings before the user changed anything.</summary>
    public static AgentSettings Default { get; } = new(null, AnthropicClient.DefaultModel, null, []);

    /// <summary>Whether the agent can run.</summary>
    [JsonIgnore]
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Keeps the agent's settings in the app's state folder. On Android that folder is the app's private storage,
/// which other apps cannot read; the key is never sent anywhere but to Claude's API.
/// </summary>
/// <param name="paths">The app's folders.</param>
public sealed class AgentSettingsStore(AppPaths paths)
{
    private string FilePath => Path.Combine(paths.State, "agent.json");

    /// <summary>Reads the settings; the defaults when there are none or the file is damaged.</summary>
    public AgentSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize(stream, AgentSettingsJson.Default.AgentSettings) ?? AgentSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return AgentSettings.Default;
        }
    }

    /// <summary>Saves the settings.</summary>
    /// <param name="settings">The settings.</param>
    public void Save(AgentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(paths.State);
        var temporary = FilePath + ".tmp";
        using (var stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, settings, AgentSettingsJson.Default.AgentSettings);
        }

        // The file holds the Claude API key and MCP tokens: readable by its owner only, as the GitHub account's is.
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                // The State folder is private to the app on Android.
            }
        }

        File.Move(temporary, FilePath, overwrite: true);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AgentSettings))]
internal sealed partial class AgentSettingsJson : JsonSerializerContext
{
}
