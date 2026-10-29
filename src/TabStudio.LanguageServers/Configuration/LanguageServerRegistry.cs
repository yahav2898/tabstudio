using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Configuration;

/// <summary>
/// The languages, language servers and debug adapters TabStudio knows, as data: the built-in list, changed and
/// extended by a workspace's <c>.tabstudio/settings.json</c>, and the settings servers read.
/// </summary>
/// <remarks>
/// <para>
/// The registry's JSON has three sections keyed by identifier: <c>languages</c> (a name and file extensions),
/// <c>languageServers</c> and <c>debugAdapters</c>. Every other top-level key of a workspace's settings is a setting
/// for the servers, written as in VS Code's <c>settings.json</c>, with dotted or nested names.
/// </para>
/// <para>Instances are immutable; <see cref="WithWorkspaceSettings"/> returns a new one.</para>
/// </remarks>
public sealed class LanguageServerRegistry
{
    private const string LanguagesSection = "languages";
    private const string ServersSection = "languageServers";
    private const string AdaptersSection = "debugAdapters";

    private static readonly Lazy<LanguageServerRegistry> BuiltInRegistry = new(LoadBuiltIn);

    private readonly JsonObject _source;

    private LanguageServerRegistry(JsonObject source, JsonObject settings, IReadOnlyList<string> problems)
    {
        _source = source;
        Settings = settings;
        var parsed = new List<string>(problems);
        Languages = ParseLanguages(source, parsed);
        Servers = ParseServers(source, parsed);
        Adapters = ParseAdapters(source, parsed);
        Problems = parsed;
    }

    /// <summary>TabStudio's built-in languages, servers and adapters.</summary>
    public static LanguageServerRegistry BuiltIn => BuiltInRegistry.Value;

    /// <summary>The languages, in the order they were defined.</summary>
    public IReadOnlyList<LanguageDefinition> Languages { get; }

    /// <summary>The language servers, in order of preference.</summary>
    public IReadOnlyList<LanguageServerDefinition> Servers { get; }

    /// <summary>The debug adapters.</summary>
    public IReadOnlyList<DebugAdapterDefinition> Adapters { get; }

    /// <summary>The workspace's settings for every server, with nested names.</summary>
    public JsonObject Settings { get; }

    /// <summary>What was wrong with the definitions, such as a server without a command, for the log.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Reads a registry from JSON in the shape of the built-in one.</summary>
    /// <param name="json">The JSON; comments and trailing commas are allowed.</param>
    /// <exception cref="JsonException">The text is not a JSON object.</exception>
    public static LanguageServerRegistry Parse(string json) => new(JsonSettings.ParseObject(json), new JsonObject(), []);

    /// <summary>
    /// The registry changed by a workspace's settings: definitions merge key by key into the ones with the same
    /// identifier or are added, and every other key becomes a setting for the servers.
    /// </summary>
    /// <param name="workspaceSettings">The contents of <c>.tabstudio/settings.json</c>.</param>
    public LanguageServerRegistry WithWorkspaceSettings(JsonObject workspaceSettings)
    {
        ArgumentNullException.ThrowIfNull(workspaceSettings);
        var source = JsonSettings.Merge(_source, Sections(workspaceSettings));
        var settings = new JsonObject();
        foreach (var (name, value) in workspaceSettings)
        {
            if (!IsSection(name))
            {
                settings[name] = value?.DeepClone();
            }
        }

        return new LanguageServerRegistry(source, JsonSettings.Merge(Settings, JsonSettings.ExpandDottedNames(settings)), []);
    }

    /// <summary>The language of a file, by its extension, or <see langword="null"/> when none matches.</summary>
    /// <param name="filePath">A file name or path.</param>
    public LanguageDefinition? FindLanguage(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        foreach (var language in Languages)
        {
            if (language.Matches(filePath))
            {
                return language;
            }
        }

        return null;
    }

    /// <summary>The enabled servers of a language, in order of preference.</summary>
    /// <param name="languageId">The language identifier, such as <c>python</c>.</param>
    public IEnumerable<LanguageServerDefinition> FindServers(string languageId) =>
        Servers.Where(s => s.IsEnabled && s.Languages.Contains(languageId, StringComparer.Ordinal));

    /// <summary>The enabled adapter for a launch configuration type, or <see langword="null"/>.</summary>
    /// <param name="type">The type, such as <c>debugpy</c> or <c>python</c>.</param>
    public DebugAdapterDefinition? FindAdapter(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        foreach (var adapter in Adapters)
        {
            if (adapter.IsEnabled && adapter.Handles(type))
            {
                return adapter;
            }
        }

        return null;
    }

    /// <summary>The enabled adapters that debug a language, in order of preference.</summary>
    /// <param name="languageId">The language identifier.</param>
    public IEnumerable<DebugAdapterDefinition> FindAdaptersForLanguage(string languageId) =>
        Adapters.Where(a => a.IsEnabled && a.Languages.Contains(languageId, StringComparer.Ordinal));

    /// <summary>
    /// The settings a server reads: its own defaults, under the workspace's settings, which win.
    /// </summary>
    /// <param name="server">The server.</param>
    public JsonObject SettingsFor(LanguageServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return JsonSettings.Merge(server.Settings, Settings);
    }

    private static bool IsSection(string name) =>
        name is LanguagesSection or ServersSection or AdaptersSection;

    private static JsonObject Sections(JsonObject settings)
    {
        var sections = new JsonObject();
        foreach (var name in new[] { LanguagesSection, ServersSection, AdaptersSection })
        {
            if (settings[name] is JsonObject section)
            {
                sections[name] = section.DeepClone();
            }
        }

        return sections;
    }

    private static List<LanguageDefinition> ParseLanguages(JsonObject source, List<string> problems)
    {
        var languages = new List<LanguageDefinition>();
        foreach (var (id, node) in Entries(source, LanguagesSection))
        {
            var extensions = JsonSettings.GetStrings(node, "extensions")
                .Select(e => e.StartsWith('.') ? e : "." + e)
                .ToList();
            if (extensions.Count == 0)
            {
                problems.Add($"The language '{id}' has no file extensions.");
                continue;
            }

            languages.Add(new LanguageDefinition(id, JsonSettings.GetString(node, "name") ?? id, extensions));
        }

        return languages;
    }

    private static List<LanguageServerDefinition> ParseServers(JsonObject source, List<string> problems)
    {
        var servers = new List<LanguageServerDefinition>();
        foreach (var (id, node) in Entries(source, ServersSection))
        {
            var command = JsonSettings.GetString(node, "command");
            var languages = JsonSettings.GetStrings(node, "languages");
            if (string.IsNullOrWhiteSpace(command) || languages.Count == 0)
            {
                problems.Add($"The language server '{id}' needs a \"command\" and \"languages\".");
                continue;
            }

            servers.Add(new LanguageServerDefinition
            {
                Id = id,
                Name = JsonSettings.GetString(node, "name") ?? id,
                Languages = languages,
                Command = command,
                AlternativeCommands = JsonSettings.GetStrings(node, "alternativeCommands"),
                Arguments = JsonSettings.GetStrings(node, "args"),
                Environment = JsonSettings.GetStringMap(node, "env"),
                RootMarkers = JsonSettings.GetStrings(node, "rootMarkers"),
                InitializationOptions = node["initializationOptions"]?.DeepClone() as JsonObject,
                Settings = node["settings"] is JsonObject settings ? JsonSettings.ExpandDottedNames(settings) : null,
                Package = JsonSettings.GetString(node, "package"),
                IsEnabled = JsonSettings.GetBoolean(node, "enabled") ?? true,
                SendsClientProcessId = JsonSettings.GetBoolean(node, "clientProcessId") ?? true,
            });
        }

        return servers;
    }

    private static List<DebugAdapterDefinition> ParseAdapters(JsonObject source, List<string> problems)
    {
        var adapters = new List<DebugAdapterDefinition>();
        foreach (var (id, node) in Entries(source, AdaptersSection))
        {
            var command = JsonSettings.GetString(node, "command");
            if (string.IsNullOrWhiteSpace(command))
            {
                problems.Add($"The debug adapter '{id}' needs a \"command\".");
                continue;
            }

            var transport = JsonSettings.GetString(node, "transport");
            adapters.Add(new DebugAdapterDefinition
            {
                Id = id,
                Name = JsonSettings.GetString(node, "name") ?? id,
                Aliases = JsonSettings.GetStrings(node, "aliases"),
                Languages = JsonSettings.GetStrings(node, "languages"),
                Command = command,
                AlternativeCommands = JsonSettings.GetStrings(node, "alternativeCommands"),
                Arguments = JsonSettings.GetStrings(node, "args"),
                Environment = JsonSettings.GetStringMap(node, "env"),
                Transport = string.Equals(transport, "unix", StringComparison.OrdinalIgnoreCase) ? DebugAdapterTransport.UnixSocket
                    : string.Equals(transport, "tcp", StringComparison.OrdinalIgnoreCase) ? DebugAdapterTransport.Tcp : DebugAdapterTransport.Stdio,
                UsesProcessInput = JsonSettings.GetBoolean(node, "usesProcessInput") ?? false,
                Defaults = node["defaults"]?.DeepClone() as JsonObject,
                TerminalDefaults = node["terminalDefaults"]?.DeepClone() as JsonObject,
                Package = JsonSettings.GetString(node, "package"),
                IsEnabled = JsonSettings.GetBoolean(node, "enabled") ?? true,
            });
        }

        return adapters;
    }

    private static IEnumerable<(string Id, JsonObject Node)> Entries(JsonObject source, string section)
    {
        if (source[section] is not JsonObject entries)
        {
            yield break;
        }

        foreach (var (id, node) in entries)
        {
            if (node is JsonObject definition)
            {
                yield return (id, definition);
            }
        }
    }

    private static LanguageServerRegistry LoadBuiltIn()
    {
        using var stream = typeof(LanguageServerRegistry).Assembly.GetManifestResourceStream("TabStudio.LanguageServers.BuiltInLanguages.json")
            ?? throw new InvalidOperationException("The built-in language list is missing from this build.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
