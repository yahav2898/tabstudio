using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Configuration;

/// <summary>
/// A workspace's own configuration in its <c>.tabstudio</c> folder: <c>settings.json</c>, which changes the
/// language servers and debug adapters and holds settings for the servers, and <c>launch.json</c>, the launch
/// configurations, in VS Code's format.
/// </summary>
/// <remarks>
/// <para><c>.tabstudio/settings.json</c> looks like this; every part is optional:</para>
/// <code>
/// {
///   // Settings for the servers, as in VS Code: dotted or nested names.
///   "python.analysis.typeCheckingMode": "strict",
///   "typescript": { "inlayHints": { "includeInlayVariableTypeHints": true } },
///
///   // Change a built-in server by its id, or add one.
///   "languageServers": {
///     "pyright": { "command": "basedpyright-langserver", "args": [ "--stdio" ] },
///     "lua-language-server": { "name": "Lua", "languages": [ "lua" ], "command": "lua-language-server" }
///   },
///   // Teach TabStudio a language: its id as the protocols name it, and its file extensions.
///   "languages": { "lua": { "name": "Lua", "extensions": [ ".lua" ] } },
///   // Change or add a debug adapter, by the type launch configurations name.
///   "debugAdapters": { "debugpy": { "command": "/opt/python/bin/python3" } }
/// }
/// </code>
/// <para>
/// A server or adapter takes <c>name</c>, <c>languages</c>, <c>command</c>, <c>alternativeCommands</c>,
/// <c>args</c>, <c>env</c>, <c>enabled</c> and <c>package</c> (the Toolchain Store package that installs it). A
/// server also takes <c>rootMarkers</c>, <c>initializationOptions</c> and <c>settings</c>; an adapter takes
/// <c>aliases</c>, <c>transport</c> (<c>stdio</c> or <c>tcp</c>), <c>defaults</c> for its launch configurations, and
/// <c>terminalDefaults</c>, the values that run the program in TabStudio's console so it can read input.
/// A string in <c>initializationOptions</c> may be <c>${packageFile:typescript/lib/tsserver.js}</c>: a file of an npm
/// package, from the workspace's <c>node_modules</c> or else installed beside the server's own package.
/// </para>
/// </remarks>
public sealed class WorkspaceConfiguration
{
    /// <summary>The folder of a workspace that holds TabStudio's files.</summary>
    public const string FolderName = ".tabstudio";

    /// <summary>The settings file's name.</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>The launch configurations file's name.</summary>
    public const string LaunchFileName = "launch.json";

    private WorkspaceConfiguration(string root, LanguageServerRegistry registry, IReadOnlyList<LaunchConfiguration> launch, IReadOnlyList<string> problems)
    {
        RootDirectory = root;
        Registry = registry;
        LaunchConfigurations = launch;
        Problems = problems;
    }

    /// <summary>The workspace's root folder.</summary>
    public string RootDirectory { get; }

    /// <summary>The built-in registry changed by the workspace's settings.</summary>
    public LanguageServerRegistry Registry { get; }

    /// <summary>The launch configurations of <c>.tabstudio/launch.json</c>, in order, with variables not yet substituted.</summary>
    public IReadOnlyList<LaunchConfiguration> LaunchConfigurations { get; }

    /// <summary>What could not be read, such as a settings file that is not JSON, for the log.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>The settings file of a workspace.</summary>
    /// <param name="rootDirectory">The workspace's root folder.</param>
    public static string SettingsPath(string rootDirectory) => Path.Combine(rootDirectory, FolderName, SettingsFileName);

    /// <summary>The launch configurations file of a workspace.</summary>
    /// <param name="rootDirectory">The workspace's root folder.</param>
    public static string LaunchPath(string rootDirectory) => Path.Combine(rootDirectory, FolderName, LaunchFileName);

    /// <summary>Reads a workspace's configuration. Files that are missing or cannot be read are left out, and said so in <see cref="Problems"/>.</summary>
    /// <param name="rootDirectory">The workspace's root folder.</param>
    /// <param name="registry">The registry to start from; the built-in one when <see langword="null"/>.</param>
    public static WorkspaceConfiguration Load(string rootDirectory, LanguageServerRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        var problems = new List<string>();
        var effective = registry ?? LanguageServerRegistry.BuiltIn;
        if (ReadObject(SettingsPath(rootDirectory), problems) is { } settings)
        {
            effective = effective.WithWorkspaceSettings(settings);
        }

        problems.AddRange(effective.Problems);
        IReadOnlyList<LaunchConfiguration> launch = ReadObject(LaunchPath(rootDirectory), problems) is { } file
            ? LaunchConfiguration.ParseFile(file, problems)
            : [];
        return new WorkspaceConfiguration(rootDirectory, effective, launch, problems);
    }

    private static JsonObject? ReadObject(string path, List<string> problems)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSettings.ParseObject(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            problems.Add($"{Path.GetFileName(path)} is not valid JSON: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add($"{Path.GetFileName(path)} could not be read: {ex.Message}");
        }

        return null;
    }
}
