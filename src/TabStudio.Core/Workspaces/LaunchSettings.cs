using System.Text.Json;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// How a project wants to be started, from the first <c>Project</c> profile of its
/// <c>Properties/launchSettings.json</c>, as Visual Studio reads it for F5.
/// </summary>
/// <param name="ProfileName">The profile, such as <c>https</c> or the project's name.</param>
/// <param name="LaunchBrowser">Whether to show the app in a browser once it starts.</param>
/// <param name="LaunchUrl">The page to show first, relative to the app's address, such as <c>swagger</c>.</param>
/// <param name="Environment">The <c>ASPNETCORE_ENVIRONMENT</c> (or <c>DOTNET_ENVIRONMENT</c>) the profile sets.</param>
/// <param name="CommandLineArgs">The profile's <c>commandLineArgs</c>, split as a shell would.</param>
public sealed record LaunchProfile(
    string ProfileName,
    bool LaunchBrowser,
    string? LaunchUrl,
    string? Environment,
    IReadOnlyList<string> CommandLineArgs)
{
    /// <summary>The profile a project without launch settings gets: the browser opens, in Development.</summary>
    public static LaunchProfile Default { get; } = new("Default", LaunchBrowser: true, LaunchUrl: null, Environment: null, CommandLineArgs: []);
}

/// <summary>Reads <c>Properties/launchSettings.json</c>.</summary>
public static class LaunchSettings
{
    /// <summary>The launch settings file of a project, relative to its folder.</summary>
    public const string RelativePath = "Properties/launchSettings.json";

    /// <summary>Reads the first profile that starts the project itself.</summary>
    /// <param name="projectDirectory">The folder of the project file.</param>
    /// <returns>The profile, or <see cref="LaunchProfile.Default"/> when there is none or the file cannot be read.</returns>
    public static LaunchProfile Read(string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        var path = Path.Combine(projectDirectory, "Properties", "launchSettings.json");
        if (!File.Exists(path))
        {
            return LaunchProfile.Default;
        }

        try
        {
            return Parse(File.ReadAllText(path)) ?? LaunchProfile.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return LaunchProfile.Default;
        }
    }

    /// <summary>Parses launch settings.</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The first <c>Project</c> profile, or <see langword="null"/> when there is none or the text is not valid.</returns>
    public static LaunchProfile? Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (!document.RootElement.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var profile in profiles.EnumerateObject())
            {
                var value = profile.Value;
                if (value.ValueKind != JsonValueKind.Object
                    || !string.Equals(ReadString(value, "commandName"), "Project", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? environment = null;
                if (value.TryGetProperty("environmentVariables", out var variables) && variables.ValueKind == JsonValueKind.Object)
                {
                    environment = ReadString(variables, "ASPNETCORE_ENVIRONMENT") ?? ReadString(variables, "DOTNET_ENVIRONMENT");
                }

                return new LaunchProfile(
                    profile.Name,
                    value.TryGetProperty("launchBrowser", out var launch) && launch.ValueKind == JsonValueKind.True,
                    ReadString(value, "launchUrl") is { Length: > 0 } url ? url : null,
                    environment,
                    SplitArguments(ReadString(value, "commandLineArgs")));
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Splits a command line on spaces, keeping quoted parts together.</summary>
    private static List<string> SplitArguments(string? commandLine)
    {
        var arguments = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return arguments;
        }

        var current = new System.Text.StringBuilder();
        var quoted = false;
        var inArgument = false;
        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                quoted = !quoted;
                inArgument = true;
            }
            else if (char.IsWhiteSpace(character) && !quoted)
            {
                if (inArgument)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    inArgument = false;
                }
            }
            else
            {
                current.Append(character);
                inArgument = true;
            }
        }

        if (inArgument)
        {
            arguments.Add(current.ToString());
        }

        return arguments;
    }
}
